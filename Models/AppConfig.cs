using System;
using System.IO;
using System.Text.Json;
using NMS_Reader.Services;

namespace NMS_Reader.Models
{
    public class AppConfig
    {
        public string VoiceModel { get; set; } = "ruslan"; // "ruslan", "irina", "dmitri"
        public string VoiceName { get; set; } = "Руслан";
        public string ActiveProfileId { get; set; } = "factory_default";
        public string ActivePresetKey { get; set; } = "suit_male";
        public string ProtagonistPresetKey { get; set; } = "suit_male"; // Chosen voice for protagonist's thoughts
        public double SpeakingRate { get; set; } = 1.0;
        public string CharacterMode { get; set; } = "suit_male";

        // Neural Synthesis Nuances
        public double NoiseScale { get; set; } = 0.67;     // Expressiveness / emotional variability (0.2 - 1.0)
        public double SentenceSilence { get; set; } = 0.25;// Pause between sentences in seconds (0.1 - 0.8)

        // Audio DSP Effects
        public double PitchShift { get; set; } = 1.0;
        public double BassGainDb { get; set; } = 0.0;
        public double TrebleGainDb { get; set; } = 0.0;
        public double ReverbWet { get; set; } = 0.0;
        public double EchoWet { get; set; } = 0.0;
        public double ExosuitMix { get; set; } = 0.0; // 0.0 to 1.0 (smooth blend of helmet intercom)
        public bool CyberEffect { get; set; } = false;
        public bool ExosuitRadio { get; set; } = false;
        public bool GekThroat { get; set; } = false;
        public bool MonolithChorus { get; set; } = false;
        public bool WarmDrive { get; set; } = false;
        public bool StudioCompressor { get; set; } = true;

        // Scanning & Automation
        public bool SkipTitle { get; set; } = true;
        public bool SkipChoices { get; set; } = true;
        public bool MinimizeToTray { get; set; } = true;
        public bool AutoDetectEnabled { get; set; } = true;
        public bool OnlyInNms { get; set; } = true;

        public int XMinPercent { get; set; } = 10;
        public int XMaxPercent { get; set; } = 62;
        public int YMinPercent { get; set; } = 45;
        public int YMaxPercent { get; set; } = 96;

        public static string GetConfigPath()
        {
            string p1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            if (File.Exists(p1)) return p1;
            string p2 = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
            if (File.Exists(p2)) return p2;
            string p3 = Path.Combine(AppContext.BaseDirectory, "config.json");
            if (File.Exists(p3)) return p3;
            return p1;
        }

        public AudioEffectsConfig GetAudioEffectsConfig() => new()
        {
            PitchShift = PitchShift,
            BassGainDb = BassGainDb,
            TrebleGainDb = TrebleGainDb,
            ReverbWet = ReverbWet,
            EchoWet = EchoWet,
            CyberEffect = CyberEffect,
            ExosuitRadio = ExosuitRadio,
            ExosuitMix = ExosuitMix,
            GekThroat = GekThroat,
            MonolithChorus = MonolithChorus,
            WarmDrive = WarmDrive,
            StudioCompressor = StudioCompressor,
            CharacterMode = CharacterMode
        };

        public static AppConfig Load()
        {
            try
            {
                string path = GetConfigPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    };
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json, options);
                    if (cfg != null)
                    {
                        cfg.XMinPercent = 5;
                        cfg.XMaxPercent = 95;
                        cfg.YMinPercent = 45;
                        cfg.YMaxPercent = 96;
                        return cfg;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppConfig] Load error: {ex}");
            }

            var def = new AppConfig();
            def.Save();
            return def;
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(GetConfigPath(), json, new System.Text.UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppConfig] Save error: {ex}");
            }
        }
    }
}
