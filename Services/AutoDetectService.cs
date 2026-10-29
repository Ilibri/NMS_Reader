using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NMS_Reader.Models;

namespace NMS_Reader.Services
{
    public class AutoDetectService : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_E = 0x45;

        private readonly OcrService _ocrService;
        private readonly SpeechService _speechService;
        private readonly AppConfig _config;

        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private string _lastSpokenHash = string.Empty;
        private string _lastSpokenText = string.Empty;
        private bool _dialogueWasOpen = false;
        private int _emptyCount = 0;
        private bool _wasNmsActive = false;

        private int _cachedNmsPid = 0;
        private DateTime _lastNmsPidCheck = DateTime.MinValue;

        private DateTime _eHoldStart = DateTime.MinValue;
        private DateTime _armedUntil = DateTime.MinValue;

        public event Action<string>? OnDialogueDetected;
        public event Action<string, string>? OnStatusChanged;

        public bool IsAutoDetectEnabled { get; set; } = true;

        public AutoDetectService(OcrService ocrService, SpeechService speechService, AppConfig config)
        {
            _ocrService = ocrService;
            _speechService = speechService;
            _config = config;
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();
            Task.Run(() => DetectionLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            _isRunning = false;
            try
            {
                _cts?.Cancel();
            }
            catch { }
        }

        public void Arm(int milliseconds = 6000)
        {
            _armedUntil = DateTime.UtcNow.AddMilliseconds(milliseconds);
        }

        public void SuppressCurrentDialogue()
        {
            _speechService.Stop();
            _dialogueWasOpen = true;
            _armedUntil = DateTime.MinValue;
        }

        public void ResetSuppression()
        {
            _dialogueWasOpen = false;
            _lastSpokenHash = string.Empty;
            _lastSpokenText = string.Empty;
            _armedUntil = DateTime.UtcNow.AddMilliseconds(6000);
        }

        public bool IsNmsForeground()
        {
            if (!_config.OnlyInNms) return true;

            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return false;

                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0) return false;

                // Refresh NMS process PID cache every 3 seconds
                if ((DateTime.Now - _lastNmsPidCheck).TotalSeconds > 3 || _cachedNmsPid == 0)
                {
                    _lastNmsPidCheck = DateTime.Now;
                    var procs = Process.GetProcessesByName("NMS");
                    if (procs.Length > 0)
                    {
                        _cachedNmsPid = procs[0].Id;
                    }
                    else
                    {
                        _cachedNmsPid = 0;
                    }
                }

                // Strictly match the PID of the NMS game process
                if (_cachedNmsPid != 0 && pid == (uint)_cachedNmsPid)
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        private async Task DetectionLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!IsAutoDetectEnabled)
                    {
                        await Task.Delay(500, token);
                        continue;
                    }

                    bool nmsActive = IsNmsForeground();
                    if (!nmsActive)
                    {
                        if (_wasNmsActive)
                        {
                            _wasNmsActive = false;
                            OnStatusChanged?.Invoke("Ожидание игры No Man's Sky", "ready");
                        }
                        await Task.Delay(400, token);
                        continue;
                    }

                    if (!_wasNmsActive)
                    {
                        _wasNmsActive = true;
                        OnStatusChanged?.Invoke("NMS активна: зажмите 'E' для диалога", "ready");
                    }

                    // 1. Detect holding 'E' key in No Man's Sky
                    bool isEHeld = (GetAsyncKeyState(VK_E) & 0x8000) != 0;
                    if (isEHeld)
                    {
                        if (_eHoldStart == DateTime.MinValue)
                        {
                            _eHoldStart = DateTime.UtcNow;
                        }
                        else if ((DateTime.UtcNow - _eHoldStart).TotalMilliseconds >= 500)
                        {
                            // Player held 'E' for >= 500ms -> Arm detector for 7 seconds
                            Arm(7000);
                        }
                    }
                    else
                    {
                        _eHoldStart = DateTime.MinValue;
                    }

                    // 2. Gating: Only scan when armed by 'E' hold OR when a dialogue is already active
                    bool isArmed = DateTime.UtcNow <= _armedUntil || _dialogueWasOpen;
                    if (!isArmed)
                    {
                        // Sleeping peacefully while player runs, aims, looks around
                        await Task.Delay(120, token);
                        continue;
                    }

                    using var bmp = _ocrService.CaptureScreen(fullscreen: false, _config);
                    var rawLines = await _ocrService.RecognizeFromBitmapAsync(bmp, _config.SkipChoices);

                    string cleaned = NmsTextCleaner.Clean(rawLines, _config.SkipTitle, _config.SkipChoices);

                    if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length >= 12)
                    {
                        _emptyCount = 0;
                        string textHash = ComputeHash(cleaned);

                        // Trigger on any newly appeared dialogue page (Page 1 thoughts, Page 2 alien speech, etc.)
                        if (textHash != _lastSpokenHash)
                        {
                            OnStatusChanged?.Invoke("Диалог обнаружен, стабилизация текста...", "busy");

                            // Wait for NMS typewriter animation to finish
                            string stableText = cleaned;
                            int stableCount = 0;

                            for (int i = 0; i < 8; i++) // up to ~2.0s
                            {
                                await Task.Delay(250, token);

                                using var checkBmp = _ocrService.CaptureScreen(fullscreen: false, _config);
                                var checkLines = await _ocrService.RecognizeFromBitmapAsync(checkBmp, _config.SkipChoices);
                                string checkCleaned = NmsTextCleaner.Clean(checkLines, _config.SkipTitle, _config.SkipChoices);

                                if (string.IsNullOrWhiteSpace(checkCleaned))
                                    continue;

                                if (checkCleaned == stableText)
                                {
                                    stableCount++;
                                    if (stableCount >= 2) // 500ms stable
                                    {
                                        break;
                                    }
                                }
                                else
                                {
                                    stableText = checkCleaned;
                                    stableCount = 0;
                                }
                            }

                            string finalCleaned = stableText;
                            if (string.IsNullOrWhiteSpace(finalCleaned))
                                finalCleaned = cleaned;

                            _lastSpokenText = finalCleaned;
                            _lastSpokenHash = ComputeHash(finalCleaned);
                            _dialogueWasOpen = true;
                            Arm(9000); // Generous 9-second arming window to transition to subsequent pages!

                            OnDialogueDetected?.Invoke(finalCleaned);
                            OnStatusChanged?.Invoke("Чтение сюжета...", "busy");

                            string spokenText = CharacterStylizer.Stylize(finalCleaned, _config.CharacterMode);
                            await _speechService.SpeakAsync(spokenText, _config.SpeakingRate, _config.GetAudioEffectsConfig(), () =>
                            {
                                Arm(9000); // Re-arm for 9 seconds after speech finishes so next page is never missed
                                OnStatusChanged?.Invoke("Диалог активен (ожидание)", "busy");
                            }, _config.NoiseScale, _config.SentenceSilence);
                        }
                    }
                    else
                    {
                        _emptyCount++;
                        // Wait for at least 8 consecutive empty scans (~2.0 seconds of no dialogue) before disarming
                        if (_emptyCount >= 8 && _dialogueWasOpen)
                        {
                            _dialogueWasOpen = false;
                            _lastSpokenHash = string.Empty;
                            _lastSpokenText = string.Empty;
                            _armedUntil = DateTime.MinValue;
                            _emptyCount = 0;
                            OnStatusChanged?.Invoke("NMS активна: зажмите 'E' для диалога", "ready");
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AutoDetect Loop Exception]: {ex.Message}");
                }

                // Poll every 250ms when armed
                try
                {
                    await Task.Delay(250, token);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        private static string ComputeHash(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
