using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NMS_Reader.Models;

namespace NMS_Reader.Services
{
    public static class VoiceProfileManager
    {
        public const string FactoryProfileId = "factory_default";

        public static string GetProfilesPath()
        {
            string p1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles.json");
            if (File.Exists(p1)) return p1;
            string p2 = Path.Combine(Directory.GetCurrentDirectory(), "profiles.json");
            if (File.Exists(p2)) return p2;
            string p3 = Path.Combine(AppContext.BaseDirectory, "profiles.json");
            if (File.Exists(p3)) return p3;
            return p1;
        }

        public static VoiceProfile GetFactoryProfile()
        {
            var factory = new VoiceProfile
            {
                Id = FactoryProfileId,
                Name = "Заводской (По умолчанию)",
                IsFactory = true,
                Presets = new Dictionary<string, VoicePresetItem>(StringComparer.OrdinalIgnoreCase)
                {
                    ["suit_female"] = new VoicePresetItem
                    {
                        CharacterKey = "suit_female",
                        DisplayName = "Женский голос",
                        VoiceModel = "irina",
                        SpeakingRate = 1.00,
                        PitchShift = 1.00,
                        BassGainDb = 2.0,
                        TrebleGainDb = 2.0,
                        ReverbWet = 0.0,
                        ExosuitMix = 0.40,
                        WarmDrive = true,
                        NoiseScale = 0.68,
                        SentenceSilence = 0.40
                    },
                    ["suit_male"] = new VoicePresetItem
                    {
                        CharacterKey = "suit_male",
                        DisplayName = "Мужской голос",
                        VoiceModel = "ruslan",
                        SpeakingRate = 1.00,
                        PitchShift = 1.00,
                        BassGainDb = 3.0,
                        TrebleGainDb = 2.0,
                        ReverbWet = 0.05,
                        ExosuitMix = 0.0,
                        NoiseScale = 0.65,
                        SentenceSilence = 0.40
                    },
                    ["gek"] = new VoicePresetItem
                    {
                        CharacterKey = "gek",
                        DisplayName = "Торговец Гек",
                        VoiceModel = "dmitri",
                        SpeakingRate = 1.18,
                        PitchShift = 1.15,
                        BassGainDb = -1.0,
                        TrebleGainDb = -1.0,
                        GekThroat = true,
                        ReverbWet = 0.02,
                        NoiseScale = 0.75,
                        SentenceSilence = 0.20
                    },
                    ["korvax"] = new VoicePresetItem
                    {
                        CharacterKey = "korvax",
                        DisplayName = "Корвакс",
                        VoiceModel = "dmitri",
                        SpeakingRate = 1.05,
                        PitchShift = 1.05,
                        BassGainDb = -2.0,
                        TrebleGainDb = 4.0,
                        CyberEffect = true,
                        ReverbWet = 0.10,
                        NoiseScale = 0.30,
                        SentenceSilence = 0.20
                    },
                    ["vykeen"] = new VoicePresetItem
                    {
                        CharacterKey = "vykeen",
                        DisplayName = "Воин Вай'кин",
                        VoiceModel = "pavel",
                        SpeakingRate = 1.00,
                        PitchShift = 0.78,
                        BassGainDb = 10.0,
                        TrebleGainDb = 7.0,
                        ReverbWet = 0.05,
                        ExosuitMix = 0.05,
                        NoiseScale = 0.90,
                        SentenceSilence = 0.30
                    },
                    ["wanderer"] = new VoicePresetItem
                    {
                        CharacterKey = "wanderer",
                        DisplayName = "Раса Странников",
                        VoiceModel = "dmitri",
                        SpeakingRate = 1.15,
                        PitchShift = 1.00,
                        BassGainDb = 0.0,
                        TrebleGainDb = 0.0,
                        ReverbWet = 0.0,
                        NoiseScale = 0.67,
                        SentenceSilence = 0.25
                    },
                    ["artemis"] = new VoicePresetItem
                    {
                        CharacterKey = "artemis",
                        DisplayName = "Артемида",
                        VoiceModel = "irina",
                        SpeakingRate = 1.10,
                        PitchShift = 1.06,
                        BassGainDb = 5.0,
                        TrebleGainDb = 6.0,
                        ReverbWet = 0.30,
                        ExosuitMix = 0.30,
                        CyberEffect = true,
                        WarmDrive = true,
                        NoiseScale = 0.90,
                        SentenceSilence = 0.25
                    },
                    ["monolith"] = new VoicePresetItem
                    {
                        CharacterKey = "monolith",
                        DisplayName = "Монолит Атласа",
                        VoiceModel = "irina",
                        SpeakingRate = 0.90,
                        PitchShift = 0.70,
                        BassGainDb = -6.0,
                        TrebleGainDb = 10.0,
                        ReverbWet = 0.20,
                        ExosuitMix = 0.05,
                        CyberEffect = false,
                        GekThroat = false,
                        WarmDrive = false,
                        NoiseScale = 0.50,
                        SentenceSilence = 0.20
                    }
                }
            };
            return factory;
        }

        public static List<VoiceProfile> LoadProfiles()
        {
            var list = new List<VoiceProfile> { GetFactoryProfile() };

            try
            {
                string path = GetProfilesPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            ReadCommentHandling = JsonCommentHandling.Skip,
                            AllowTrailingCommas = true
                        };

                        List<VoiceProfile>? customList = null;

                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            customList = JsonSerializer.Deserialize<List<VoiceProfile>>(json, options);
                        }
                        else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            var single = JsonSerializer.Deserialize<VoiceProfile>(json, options);
                            if (single != null)
                            {
                                customList = new List<VoiceProfile> { single };
                            }
                        }

                        if (customList != null)
                        {
                            foreach (var p in customList)
                            {
                                if (!string.IsNullOrWhiteSpace(p.Id) && p.Id != FactoryProfileId)
                                {
                                    p.IsFactory = false;
                                    // Make sure all default characters exist in the profile
                                    var factory = GetFactoryProfile();
                                    foreach (var kvp in factory.Presets)
                                    {
                                        if (!p.Presets.ContainsKey(kvp.Key))
                                        {
                                            p.Presets[kvp.Key] = kvp.Value.Clone();
                                        }
                                    }
                                    list.Add(p);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceProfileManager] Load error: {ex}");
            }

            return list;
        }

        public static void SaveProfiles(IEnumerable<VoiceProfile> profiles)
        {
            try
            {
                var customProfiles = profiles.Where(p => !p.IsFactory).ToList();
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string json = JsonSerializer.Serialize(customProfiles, options);
                File.WriteAllText(GetProfilesPath(), json, new System.Text.UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VoiceProfileManager] Save error: {ex}");
            }
        }
    }
}
