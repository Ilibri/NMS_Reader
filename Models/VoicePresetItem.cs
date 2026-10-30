using System;

namespace NMS_Reader.Models
{
    public class VoicePresetItem
    {
        public string CharacterKey { get; set; } = "wanderer";
        public string DisplayName { get; set; } = "Странник";
        public string VoiceModel { get; set; } = "dmitri";
        public double SpeakingRate { get; set; } = 1.15;
        public double PitchShift { get; set; } = 1.00;
        public double BassGainDb { get; set; } = 0.0;
        public double TrebleGainDb { get; set; } = 0.0;
        public double ReverbWet { get; set; } = 0.0;
        public double ExosuitMix { get; set; } = 0.0;
        public bool CyberEffect { get; set; } = false;
        public bool GekThroat { get; set; } = false;
        public bool WarmDrive { get; set; } = false;
        public bool MonolithChorus { get; set; } = false;
        public double NoiseScale { get; set; } = 0.67;
        public double SentenceSilence { get; set; } = 0.25;

        public VoicePresetItem Clone()
        {
            return new VoicePresetItem
            {
                CharacterKey = CharacterKey,
                DisplayName = DisplayName,
                VoiceModel = VoiceModel,
                SpeakingRate = SpeakingRate,
                PitchShift = PitchShift,
                BassGainDb = BassGainDb,
                TrebleGainDb = TrebleGainDb,
                ReverbWet = ReverbWet,
                ExosuitMix = ExosuitMix,
                CyberEffect = CyberEffect,
                GekThroat = GekThroat,
                WarmDrive = WarmDrive,
                MonolithChorus = MonolithChorus,
                NoiseScale = NoiseScale,
                SentenceSilence = SentenceSilence
            };
        }
    }
}
