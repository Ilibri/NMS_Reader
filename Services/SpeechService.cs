using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.SpeechSynthesis;
using NMS_Reader.Models;

namespace NMS_Reader.Services
{
    public class VoiceItem
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsNeural { get; set; } = false;
        public string? ModelPath { get; set; }
        public string? ConfigPath { get; set; }
        public VoiceInformation? WinRtVoice { get; set; }

        public override string ToString() => DisplayName;
    }

    public class SpeechService : IDisposable
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint GetShortPathName(string lpszLongPath, StringBuilder lpszShortPath, uint cchBuffer);

        private readonly SpeechSynthesizer _winRtSynth = new();
        private SoundPlayer? _player;
        private Process? _currentProc;
        private CancellationTokenSource? _currentSpeechCts;
        private readonly object _lock = new();

        public List<VoiceItem> Voices { get; } = new();
        public VoiceItem? CurrentVoice { get; private set; }
        public bool IsSpeaking { get; private set; } = false;

        public SpeechService()
        {
            LoadVoices();
        }

        private static string ToSafePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            try
            {
                var sb = new StringBuilder(1024);
                if (File.Exists(path) || Directory.Exists(path))
                {
                    uint res = GetShortPathName(path, sb, (uint)sb.Capacity);
                    if (res > 0) return sb.ToString();
                }
                else
                {
                    string? dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        uint res = GetShortPathName(dir, sb, (uint)sb.Capacity);
                        if (res > 0) return Path.Combine(sb.ToString(), Path.GetFileName(path));
                    }
                }
            }
            catch { }
            return path;
        }

        private void LoadVoices()
        {
            Voices.Clear();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string piperExe = Path.Combine(baseDir, "piper", "piper.exe");
            string voicesDir = Path.Combine(baseDir, "Voices");

            bool hasPiper = File.Exists(piperExe);

            // 1. Add Offline Neural Piper Voices
            if (hasPiper && Directory.Exists(voicesDir))
            {
                var neuralVoices = new[]
                {
                    ("irina",  "Ирина",   "Чистый выразительный женский голос", "ru_RU-irina-medium.onnx"),
                    ("ruslan", "Руслан",  "Студийный мужской баритон", "ru_RU-ruslan-medium.onnx"),
                    ("dmitri", "Дмитрий", "Спокойный дикторский голос", "ru_RU-dmitri-medium.onnx")
                };

                foreach (var (id, name, desc, file) in neuralVoices)
                {
                    string modelPath = Path.Combine(voicesDir, file);
                    string cfgPath = modelPath + ".json";
                    if (File.Exists(modelPath))
                    {
                        Voices.Add(new VoiceItem
                        {
                            Id = id,
                            DisplayName = name,
                            Description = desc,
                            IsNeural = true,
                            ModelPath = modelPath,
                            ConfigPath = File.Exists(cfgPath) ? cfgPath : null
                        });
                    }
                }
            }

            // 2. Add System Windows Voices (Fallback)
            foreach (var v in SpeechSynthesizer.AllVoices)
            {
                if (v.Language.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
                {
                    Voices.Add(new VoiceItem
                    {
                        Id = $"winrt:{v.Id}",
                        DisplayName = v.DisplayName,
                        Description = "Стандартный системный синтез Windows",
                        IsNeural = false,
                        WinRtVoice = v
                    });
                }
            }

            // Select default voice (prefer Dmitri)
            CurrentVoice = Voices.FirstOrDefault(v => v.Id == "dmitri")
                           ?? Voices.FirstOrDefault(v => v.IsNeural)
                           ?? Voices.FirstOrDefault();

            if (CurrentVoice?.WinRtVoice != null)
            {
                _winRtSynth.Voice = CurrentVoice.WinRtVoice;
            }
        }

        public void SetVoice(VoiceItem? voice)
        {
            if (voice == null) return;
            CurrentVoice = voice;
            if (voice.WinRtVoice != null)
            {
                _winRtSynth.Voice = voice.WinRtVoice;
            }
        }

        public void SetVoiceById(string id)
        {
            var match = Voices.FirstOrDefault(v => v.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                        ?? Voices.FirstOrDefault(v => v.Id.Contains(id, StringComparison.OrdinalIgnoreCase))
                        ?? Voices.FirstOrDefault(v => v.DisplayName.Contains(id, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                SetVoice(match);
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                IsSpeaking = false;

                try
                {
                    _currentSpeechCts?.Cancel();
                    _currentSpeechCts?.Dispose();
                }
                catch { }
                _currentSpeechCts = null;

                try
                {
                    if (_currentProc != null && !_currentProc.HasExited)
                    {
                        _currentProc.Kill();
                        _currentProc.Dispose();
                    }
                }
                catch { }
                _currentProc = null;

                try
                {
                    _player?.Stop();
                    _player?.Dispose();
                }
                catch { }
                _player = null;
            }
        }

        public async Task SpeakAsync(string text, double rate, AudioEffectsConfig effects, Action? onComplete = null, double noiseScale = 0.67, double sentenceSilence = 0.25)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            CancellationToken token;
            lock (_lock)
            {
                Stop();
                IsSpeaking = true;
                _currentSpeechCts = new CancellationTokenSource();
                token = _currentSpeechCts.Token;
            }

            try
            {
                if (CurrentVoice != null && CurrentVoice.IsNeural && !string.IsNullOrEmpty(CurrentVoice.ModelPath))
                {
                    await SpeakNeuralPiperAsync(text, rate, effects, onComplete, noiseScale, sentenceSilence, token);
                }
                else
                {
                    await SpeakWinRtAsync(text, rate, effects, onComplete, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Instant clean abort on player skip
                lock (_lock) { IsSpeaking = false; }
            }
            catch (Exception ex)
            {
                lock (_lock) { IsSpeaking = false; }
                Debug.WriteLine($"[Speech Error]: {ex.Message}");
                onComplete?.Invoke();
            }
        }

        // Backward compatibility overload
        public Task SpeakAsync(string text, double rate, Action? onComplete = null)
        {
            return SpeakAsync(text, rate, new AudioEffectsConfig(), onComplete);
        }

        private async Task SpeakNeuralPiperAsync(string text, double rate, AudioEffectsConfig effects, Action? onComplete, double noiseScale, double sentenceSilence, CancellationToken token)
        {
            if (token.IsCancellationRequested || !IsSpeaking) return;

            var sentences = SplitIntoSentences(text);
            if (sentences.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            if (sentences.Count == 1)
            {
                var sw = Stopwatch.StartNew();
                byte[]? wavBytes = await Task.Run(() => SynthesizeWithPiper(sentences[0], rate, noiseScale, sentenceSilence, token), token);
                if (token.IsCancellationRequested || !IsSpeaking || wavBytes == null || wavBytes.Length <= 44)
                {
                    if (wavBytes == null || wavBytes.Length <= 44)
                    {
                        await SpeakWinRtAsync(text, rate, effects, onComplete, token);
                    }
                    return;
                }

                byte[] processedWav = await Task.Run(() => AudioDspService.ProcessWav(wavBytes, effects), token);
                if (token.IsCancellationRequested || !IsSpeaking) return;

                Debug.WriteLine($"[TTS Single] Synthesis & DSP completed in {sw.ElapsedMilliseconds} ms");
                await PlayWavAsync(processedWav, token);

                lock (_lock) { IsSpeaking = false; }
                if (!token.IsCancellationRequested)
                {
                    onComplete?.Invoke();
                }
                return;
            }

            // MULTI-SENTENCE STREAMING PIPELINE:
            // First sentence plays immediately upon synthesis while subsequent sentences are computed in background.
            var totalSw = Stopwatch.StartNew();
            Debug.WriteLine($"[TTS Streaming] Pipeline started: {sentences.Count} sentences");

            Task<byte[]?> nextTask = Task.Run(() =>
            {
                byte[]? raw = SynthesizeWithPiper(sentences[0], rate, noiseScale, sentenceSilence, token);
                if (raw != null && raw.Length > 44)
                {
                    return AudioDspService.ProcessWav(raw, effects);
                }
                return null;
            }, token);

            for (int i = 0; i < sentences.Count; i++)
            {
                if (token.IsCancellationRequested || !IsSpeaking) break;

                byte[]? currentWav = await nextTask;
                if (token.IsCancellationRequested || !IsSpeaking) break;

                if (i == 0)
                {
                    Debug.WriteLine($"[TTS Streaming] First sentence ready in {totalSw.ElapsedMilliseconds} ms -> Start Playback!");
                }

                // Concurrently synthesize sentence i + 1 while sentence i is playing!
                int nextIndex = i + 1;
                if (nextIndex < sentences.Count)
                {
                    nextTask = Task.Run(() =>
                    {
                        byte[]? raw = SynthesizeWithPiper(sentences[nextIndex], rate, noiseScale, sentenceSilence, token);
                        if (raw != null && raw.Length > 44)
                        {
                            return AudioDspService.ProcessWav(raw, effects);
                        }
                        return null;
                    }, token);
                }

                if (currentWav != null && currentWav.Length > 44)
                {
                    await PlayWavAsync(currentWav, token);
                }
            }

            lock (_lock)
            {
                IsSpeaking = false;
            }

            if (!token.IsCancellationRequested)
            {
                onComplete?.Invoke();
            }
        }

        private async Task PlayWavAsync(byte[] wavBytes, CancellationToken token)
        {
            if (token.IsCancellationRequested || !IsSpeaking) return;

            lock (_lock)
            {
                if (token.IsCancellationRequested || !IsSpeaking) return;
                var ms = new MemoryStream(wavBytes);
                _player?.Dispose();
                _player = new SoundPlayer(ms);
            }

            await Task.Run(() =>
            {
                try
                {
                    if (token.IsCancellationRequested || !IsSpeaking) return;
                    _player?.PlaySync();
                }
                catch { }
            }, token);
        }

        private static List<string> SplitIntoSentences(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            var rawSentences = System.Text.RegularExpressions.Regex.Split(text.Trim(), @"(?<=[.!?…\n])\s+");
            var buffer = new StringBuilder();

            foreach (var raw in rawSentences)
            {
                string s = raw.Trim();
                if (string.IsNullOrEmpty(s)) continue;

                if (buffer.Length > 0)
                {
                    if (buffer.Length < 15)
                    {
                        buffer.Append(' ').Append(s);
                    }
                    else
                    {
                        result.Add(buffer.ToString());
                        buffer.Clear();
                        buffer.Append(s);
                    }
                }
                else
                {
                    buffer.Append(s);
                }
            }

            if (buffer.Length > 0)
            {
                result.Add(buffer.ToString());
            }

            return result;
        }

        private byte[]? SynthesizeWithPiper(string text, double rate, double noiseScale = 0.67, double sentenceSilence = 0.25, CancellationToken token = default)
        {
            if (token.IsCancellationRequested || !IsSpeaking || CurrentVoice?.ModelPath == null) return null;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string piperExe = Path.Combine(baseDir, "piper", "piper.exe");
            if (!File.Exists(piperExe)) return null;

            string safePiper = ToSafePath(piperExe);
            string safeModel = ToSafePath(CurrentVoice.ModelPath);
            string safeConfig = !string.IsNullOrEmpty(CurrentVoice.ConfigPath)
                ? ToSafePath(CurrentVoice.ConfigPath)
                : $"{safeModel}.json";

            double effectiveRate = Math.Clamp(rate, 0.6, 2.0);
            double lengthScale = 1.0 / effectiveRate;
            string ns = Math.Clamp(noiseScale, 0.1, 1.2).ToString("F3", CultureInfo.InvariantCulture);
            string ss = Math.Clamp(sentenceSilence, 0.05, 1.5).ToString("F2", CultureInfo.InvariantCulture);

            // Use a temp file — avoids Windows text-mode stdout corruption of binary WAV bytes
            string tempWav = Path.Combine(Path.GetTempPath(), $"nms_reader_{Guid.NewGuid():N}.wav");

            var psi = new ProcessStartInfo
            {
                FileName = safePiper,
                Arguments = $"-m \"{safeModel}\" -c \"{safeConfig}\" --length_scale {lengthScale.ToString(CultureInfo.InvariantCulture)} --noise_scale {ns} --sentence_silence {ss} -f \"{tempWav}\"",
                WorkingDirectory = Path.GetDirectoryName(safePiper),
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            lock (_lock)
            {
                if (token.IsCancellationRequested || !IsSpeaking) return null;
                _currentProc = Process.Start(psi);
            }

            if (_currentProc == null) return null;

            try
            {
                using (var writer = new StreamWriter(_currentProc.StandardInput.BaseStream, new UTF8Encoding(false)))
                {
                    writer.WriteLine(text);
                }

                while (!_currentProc.WaitForExit(100))
                {
                    if (token.IsCancellationRequested || !IsSpeaking)
                    {
                        try { _currentProc.Kill(); } catch { }
                        return null;
                    }
                }

                int exitCode = _currentProc.ExitCode;

                lock (_lock)
                {
                    _currentProc?.Dispose();
                    _currentProc = null;
                }

                if (token.IsCancellationRequested || !IsSpeaking) return null;

                if (exitCode == 0 && File.Exists(tempWav))
                {
                    byte[] wavBytes = File.ReadAllBytes(tempWav);
                    return wavBytes.Length > 44 ? wavBytes : null;
                }
            }
            finally
            {
                try { if (File.Exists(tempWav)) File.Delete(tempWav); } catch { }
            }

            return null;
        }

        private async Task SpeakWinRtAsync(string text, double rate, AudioEffectsConfig effects, Action? onComplete, CancellationToken token = default)
        {
            if (token.IsCancellationRequested || !IsSpeaking) return;

            _winRtSynth.Options.SpeakingRate = Math.Clamp(rate, 0.5, 3.0);
            var stream = await _winRtSynth.SynthesizeTextToStreamAsync(text);
            if (token.IsCancellationRequested || !IsSpeaking) return;

            byte[] wavBytes;
            using (var netStream = stream.AsStreamForRead())
            using (var ms = new MemoryStream())
            {
                await netStream.CopyToAsync(ms, token);
                wavBytes = ms.ToArray();
            }
            if (token.IsCancellationRequested || !IsSpeaking) return;

            byte[] processedWav = await Task.Run(() => AudioDspService.ProcessWav(wavBytes, effects), token);
            if (token.IsCancellationRequested || !IsSpeaking) return;

            lock (_lock)
            {
                if (token.IsCancellationRequested || !IsSpeaking) return;
                var ms = new MemoryStream(processedWav);
                _player = new SoundPlayer(ms);
            }

            await Task.Run(() =>
            {
                try
                {
                    if (token.IsCancellationRequested || !IsSpeaking) return;
                    _player?.PlaySync();
                }
                catch { }
                finally
                {
                    lock (_lock)
                    {
                        IsSpeaking = false;
                    }
                    if (!token.IsCancellationRequested)
                    {
                        onComplete?.Invoke();
                    }
                }
            }, token);
        }

        public void Dispose()
        {
            Stop();
            _winRtSynth.Dispose();
        }
    }
}
