using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
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

        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;
        private const int VK_SPACE = 0x20;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_E = 0x45;

        private readonly OcrService _ocrService;
        private readonly SpeechService _speechService;
        private readonly AppConfig _config;

        private CancellationTokenSource? _cts;
        private bool _isRunning = false;
        private string _lastSpokenText = string.Empty;
        private string _lastSpokenNormalized = string.Empty;
        private DateTime _lastSpokenTime = DateTime.MinValue;
        private bool _dialogueWasOpen = false;
        private int _emptyCount = 0;
        private bool _wasNmsActive = false;

        private int _cachedNmsPid = 0;
        private DateTime _lastNmsPidCheck = DateTime.MinValue;

        private DateTime _eHoldStart = DateTime.MinValue;
        private DateTime _armedUntil = DateTime.MinValue;

        public event Action<string>? OnDialogueDetected;
        public event Action<List<DialogueSegment>>? OnDialogueSegmentsDetected;
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
            _lastSpokenText = string.Empty;
            _lastSpokenNormalized = string.Empty;
            _armedUntil = DateTime.UtcNow.AddMilliseconds(6000);
        }

        public bool HasDialogueClosedOrSkipped(string expectedText)
        {
            if (!_dialogueWasOpen) return true;
            if (!IsNmsForeground()) return true;

            // Only Escape indicates player explicitly cancelled/exited out
            if ((GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0)
                return true;

            return false;
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

                if ((DateTime.Now - _lastNmsPidCheck).TotalSeconds > 3 || _cachedNmsPid == 0)
                {
                    _lastNmsPidCheck = DateTime.Now;
                    var procs = Process.GetProcessesByName("NMS");
                    _cachedNmsPid = procs.Length > 0 ? procs[0].Id : 0;
                }

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
                        await Task.Delay(300, token);
                        continue;
                    }

                    if (!_wasNmsActive)
                    {
                        _wasNmsActive = true;
                        OnStatusChanged?.Invoke("NMS активна: зажмите 'E' для диалога", "ready");
                    }

                    // --- MOUSE & KEY INTERCEPTION ---
                    bool isEscape = (GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0;
                    if (isEscape && (_speechService.IsSpeaking || _dialogueWasOpen))
                    {
                        _speechService.Stop();
                        _dialogueWasOpen = false;
                        _armedUntil = DateTime.MinValue;
                        _emptyCount = 0;
                        _lastSpokenNormalized = string.Empty;
                        _lastSpokenText = string.Empty;
                        OnStatusChanged?.Invoke("Диалог закрыт (Esc)", "ready");
                        await Task.Delay(200, token);
                        continue;
                    }

                    // 1. Detect 'E' key in No Man's Sky (tapping or holding)
                    bool isEHeld = (GetAsyncKeyState(VK_E) & 0x8000) != 0;
                    if (isEHeld)
                    {
                        Arm(12000);
                        if (_speechService.IsSpeaking)
                        {
                            _lastSpokenNormalized = string.Empty;
                            _lastSpokenText = string.Empty;
                        }
                    }

                    // 1.1 Fast dialogue advance / click check (LMB, RMB, Space)
                    bool isLmb = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
                    bool isRmb = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
                    bool isSpace = (GetAsyncKeyState(VK_SPACE) & 0x8000) != 0;
                    if (_dialogueWasOpen && (isLmb || isRmb || isSpace))
                    {
                        Arm(12000);
                    }

                    // 2. Gating: Only scan when armed or dialogue was already active
                    bool isArmed = DateTime.UtcNow <= _armedUntil || _dialogueWasOpen;
                    if (!isArmed)
                    {
                        await Task.Delay(150, token);
                        continue;
                    }

                    using var bmp = _ocrService.CaptureScreen(fullscreen: false, _config);
                    var rawLines = await _ocrService.RecognizeFromBitmapAsync(bmp, _config.SkipChoices);
                    string cleaned = NmsTextCleaner.Clean(rawLines, _config.SkipTitle, _config.SkipChoices);

                    if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length >= 8)
                    {
                        _emptyCount = 0;

                        // If speech is playing:
                        // If current on-screen text matches what's playing, let it finish speaking.
                        // But if screen text CHANGED to a new dialogue phrase, interrupt old speech to read the new one!
                        if (_speechService.IsSpeaking)
                        {
                            if (IsSameDialogue(cleaned, _lastSpokenNormalized))
                            {
                                await Task.Delay(150, token);
                                continue;
                            }
                            else
                            {
                                // Screen text changed to a new phrase! Stop previous speech to read the new one.
                                _speechService.Stop();
                            }
                        }

                        // Check if this dialogue text was ALREADY read (prevents reading same dialogue repeatedly!)
                        if (IsSameDialogue(cleaned, _lastSpokenNormalized))
                        {
                            _dialogueWasOpen = true;
                            Arm(9000);
                            await Task.Delay(200, token);
                            continue;
                        }

                                                OnStatusChanged?.Invoke("Печать диалога, ждём значок готовности...", "busy");

                                                                        // Wait for NMS typewriter animation to finish:
                        // Trigger 1 (Primary): Page ready icon (mouse + downward chevron \/) appears
                        // Trigger 2 (Choices): Choices buttons appear on right side (narrative typing complete)
                        // Trigger 3 (Manual): Player clicked LMB/RMB/Space to skip typewriter
                        string stableText = cleaned;
                        Bitmap? readyBmp = null;
                        bool isConfirmedDialogue = false;

                        // Check if prompt icon or choices are already visible on initial bmp
                        if (_ocrService.DetectDialogueReadyIcon(bmp) || rawLines.Any(NmsTextCleaner.IsChoiceLine))
                        {
                            readyBmp = bmp;
                            isConfirmedDialogue = true;
                        }
                        else
                        {
                            for (int i = 0; i < 24; i++) // up to ~2.8s total wait
                            {
                                await Task.Delay(120, token);

                                // If Esc pressed, abort
                                if ((GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0 && IsNmsForeground())
                                {
                                    _speechService.Stop();
                                    _dialogueWasOpen = false;
                                    _armedUntil = DateTime.MinValue;
                                    break;
                                }

                                var checkBmp = _ocrService.CaptureScreen(fullscreen: false, _config);

                                // 1. Instant trigger: Prompt icon appeared!
                                if (_ocrService.DetectDialogueReadyIcon(checkBmp))
                                {
                                    readyBmp = checkBmp;
                                    isConfirmedDialogue = true;
                                    break;
                                }

                                // 2. Instant trigger: Player clicked LMB, RMB, or Space to finish typewriter
                                bool clicked = ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0) ||
                                               ((GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0) ||
                                               ((GetAsyncKeyState(VK_SPACE) & 0x8000) != 0);
                                if (clicked)
                                {
                                    await Task.Delay(100, token);
                                    var clickedBmp = _ocrService.CaptureScreen(fullscreen: false, _config);
                                    checkBmp.Dispose();

                                    var clickedLines = await _ocrService.RecognizeFromBitmapAsync(clickedBmp, skipRightSideChoices: false);
                                    if (_ocrService.DetectDialogueReadyIcon(clickedBmp) || clickedLines.Any(NmsTextCleaner.IsChoiceLine))
                                    {
                                        readyBmp = clickedBmp;
                                        isConfirmedDialogue = true;
                                    }
                                    else
                                    {
                                        clickedBmp.Dispose();
                                    }
                                    break;
                                }

                                // 3. Instant trigger: Choices menu appeared on the right! (Narrative typing complete)
                                var checkAllLines = await _ocrService.RecognizeFromBitmapAsync(checkBmp, skipRightSideChoices: false);
                                if (checkAllLines.Any(NmsTextCleaner.IsChoiceLine))
                                {
                                    readyBmp = checkBmp;
                                    isConfirmedDialogue = true;
                                    break;
                                }

                                checkBmp.Dispose();
                            }
                        }

                        // Strict anti-phantom check: In No Man's Sky, an active dialogue ALWAYS has either:
                        // A) The prompt icon (mouse + downward chevron \/) for page advance
                        // B) Interactive choice buttons ([1], [2], etc.) for decision
                        // If NEITHER is present, this is NOT a dialogue (spaceflight HUD, quest tracker, planet scan, cockpit, etc.)!
                        if (!isConfirmedDialogue || readyBmp == null)
                        {
                            continue;
                        }

                        // Use captured ready frame
                        using var colorBmp = (readyBmp == bmp) ? _ocrService.CaptureScreen(fullscreen: false, _config) : readyBmp;
                        
                        var segments = await _ocrService.RecognizeDialogueSegmentsAsync(colorBmp, _config.SkipTitle, _config.SkipChoices);
                        
                        string finalCleaned;
                        if (segments.Count > 0)
                        {
                            finalCleaned = string.Join(" ", segments.Select(s => s.Text)).Trim();
                        }
                        else
                        {
                            var finalLines = await _ocrService.RecognizeFromBitmapAsync(colorBmp, _config.SkipChoices);
                            finalCleaned = NmsTextCleaner.Clean(finalLines, _config.SkipTitle, _config.SkipChoices);
                            if (string.IsNullOrWhiteSpace(finalCleaned))
                                finalCleaned = stableText;

                            string textForHint = (rawLines.Count > 0 ? rawLines[0] + " " : "") + finalCleaned;
                            string detectedRace = _ocrService.DetectRaceFromBitmap(colorBmp, textForHint);
                            segments.Add(new DialogueSegment(detectedRace, finalCleaned));
                        }

                        if (string.IsNullOrWhiteSpace(finalCleaned) || finalCleaned.Length < 6)
                        {
                            continue;
                        }

                        // Duplicate check after stabilization
                        if (IsSameDialogue(finalCleaned, _lastSpokenNormalized))
                        {
                            _dialogueWasOpen = true;
                            Arm(9000);
                            await Task.Delay(200, token);
                            continue;
                        }

                        _lastSpokenText = finalCleaned;
                        _lastSpokenNormalized = NormalizeText(finalCleaned);
                        _lastSpokenTime = DateTime.UtcNow;
                        _dialogueWasOpen = true;
                        Arm(9000);

                        string spokenJoined = string.Join(" ", segments.Select(s => s.Text));
                        OnDialogueDetected?.Invoke(spokenJoined);
                        OnDialogueSegmentsDetected?.Invoke(segments);
                    }
                    else
                    {
                        _emptyCount++;
                        // If empty 3 consecutive scans (~0.6s), dialogue was closed or skipped
                        if (_emptyCount >= 3 && _dialogueWasOpen)
                        {
                            _speechService.Stop();
                            _dialogueWasOpen = false;
                            _armedUntil = DateTime.MinValue;
                            _emptyCount = 0;
                            OnStatusChanged?.Invoke("NMS активна: зажмите 'E' для диалога", "ready");
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AutoDetect Loop Exception]: {ex.Message}");
                }

                try
                {
                    await Task.Delay(200, token);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        public static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string norm = Regex.Replace(text.ToLowerInvariant(), @"[^\p{L}\p{Nd}\s]", " ");
            return Regex.Replace(norm, @"\s+", " ").Trim();
        }

        public static bool IsSameDialogue(string text1, string text2)
        {
            string n1 = NormalizeText(text1);
            string n2 = NormalizeText(text2);

            if (string.IsNullOrEmpty(n1) || string.IsNullOrEmpty(n2))
                return false;

            if (n1 == n2)
                return true;

            // Handle typewriter partial text matches
            if (n1.Length >= 15 && n2.Length >= 15)
            {
                if (n1.Contains(n2) || n2.Contains(n1))
                {
                    double ratio = (double)Math.Min(n1.Length, n2.Length) / Math.Max(n1.Length, n2.Length);
                    if (ratio > 0.70) return true;
                }
            }

            int maxLen = Math.Max(n1.Length, n2.Length);
            if (maxLen == 0) return true;

            int dist = ComputeLevenshteinDistance(n1, n2);
            double similarity = 1.0 - ((double)dist / maxLen);

            return similarity >= 0.75; // 75%+ identical -> same dialogue, do not repeat!
        }

        private static int ComputeLevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
            if (string.IsNullOrEmpty(t)) return s.Length;

            int[] d0 = new int[t.Length + 1];
            int[] d1 = new int[t.Length + 1];

            for (int j = 0; j <= t.Length; j++) d0[j] = j;

            for (int i = 0; i < s.Length; i++)
            {
                d1[0] = i + 1;
                for (int j = 0; j < t.Length; j++)
                {
                    int cost = s[i] == t[j] ? 0 : 1;
                    d1[j + 1] = Math.Min(Math.Min(d1[j] + 1, d0[j + 1] + 1), d0[j] + cost);
                }
                Array.Copy(d1, d0, d0.Length);
            }

            return d0[t.Length];
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
