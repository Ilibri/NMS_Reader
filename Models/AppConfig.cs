using System;
using System.IO;
using System.Text.Json;
using NMS_Reader.Services;

namespace NMS_Reader.Models
{
    public class AppConfig
    {
        public string VoiceModel { get; set; } = "irina"; // "dmitri", "ruslan", "irina", "denis", "windows"
        public string VoiceName { get; set; } = "Ирина";
        public string ActiveProfileId { get; set; } = "factory_default";
        public string ActivePresetKey { get; set; } = "suit_female";
        public double SpeakingRate { get; set; } = 1.0;
        public string CharacterMode { get; set; } = "suit_female";

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
        public bool VykeenRoar { get; set; } = false;
        public bool WarmDrive { get; set; } = false;
        public bool StudioCompressor { get; set; } = true;

        // Scanning & Automation
        public bool SkipTitle { get; set; } = true;
        public bool SkipChoices { get; set; } = true;
        public bool MinimizeToTray { get; set; } = true;
        public bool AutoDetectEnabled { get; set; } = true;
        public bool OnlyInNms { get; set; } = true;

        public int XMinPercent { get; set; } = 10;
        public int XMaxPercent { get; set; } = 90;
        public int YMinPercent { get; set; } = 45;
        public int YMaxPercent { get; set; } = 96;

        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

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
            VykeenRoar = VykeenRoar,
            WarmDrive = WarmDrive,
            StudioCompressor = StudioCompressor,
            CharacterMode = CharacterMode
        };

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                    if (cfg != null)
                    {
                        cfg.XMinPercent = 10;
                        cfg.XMaxPercent = 90;
                        cfg.YMinPercent = 45;
                        cfg.YMaxPercent = 96;
                        return cfg;
                    }
                }
            }
            catch { }

            var def = new AppConfig();
            def.Save();
            return def;
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }
}
