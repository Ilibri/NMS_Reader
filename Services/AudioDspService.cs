using System;
using System.IO;

namespace NMS_Reader.Services
{
    public class AudioEffectsConfig
    {
        public double PitchShift { get; set; } = 1.0;   // 0.70 (low) to 1.40 (high)
        public double BassGainDb { get; set; } = 0.0;   // -6 dB to +10 dB
        public double TrebleGainDb { get; set; } = 0.0; // -6 dB to +10 dB
        public double ReverbWet { get; set; } = 0.0;    // 0.0 (dry) to 0.60 (wet space)
        public double EchoWet { get; set; } = 0.0;      // 0.0 (dry) to 0.50 (discrete echo repeats)
        public double ExosuitMix { get; set; } = 0.0;   // 0.0 (dry) to 1.0 (helmet radio intercom mix)
        public bool CyberEffect { get; set; } = false;  // metallic ring modulation (Korvax)
        public bool ExosuitRadio { get; set; } = false; // helmet radio bandpass filter
        public bool GekThroat { get; set; } = false;    // frog/toad throat resonance & flutter (Gek)
        public bool MonolithChorus { get; set; } = false;// telepathic detuned entity chorus (S.T.A.L.K.E.R. Monolith)
        public bool WarmDrive { get; set; } = false;    // warm harmonic saturation (Vy'keen/Suit)
        public bool StudioCompressor { get; set; } = true; // speech clarity & density
        public string CharacterMode { get; set; } = "wanderer";

        public void ApplyPreset(string presetName)
        {
            CyberEffect = false;
            ExosuitRadio = false;
            ExosuitMix = 0.0;
            GekThroat = false;
            MonolithChorus = false;
            WarmDrive = false;
            EchoWet = 0.0;

            switch (presetName?.ToLowerInvariant())
            {
                case "suit_female":
                case "женский голос":
                case "женский":
                case "женский шлем":
                case "женский голос шлема":
                    CharacterMode = "wanderer";
                    PitchShift = 1.00;
                    BassGainDb = 2.0;
                    TrebleGainDb = 2.0;
                    WarmDrive = true;
                    ExosuitRadio = true;
                    ExosuitMix = 0.40;
                    ReverbWet = 0.0;
                    EchoWet = 0.0;
                    break;

                case "suit_male":
                case "мужской голос":
                case "мужской":
                case "мужской шлем":
                case "мужской голос шлема":
                    CharacterMode = "wanderer";
                    PitchShift = 1.00;
                    BassGainDb = 3.0;
                    TrebleGainDb = 2.0;
                    ExosuitRadio = false;
                    ExosuitMix = 0.0;
                    ReverbWet = 0.05;
                    EchoWet = 0.0;
                    break;

                case "suit_miku":
                case "мику":
                case "хацунэ мику":
                    CharacterMode = "suit_miku";
                    PitchShift = 1.00;
                    BassGainDb = 1.0;
                    TrebleGainDb = 1.5;
                    WarmDrive = false;
                    ExosuitRadio = false;
                    ExosuitMix = 0.15;
                    ReverbWet = 0.03;
                    EchoWet = 0.0;
                    break;

                case "suit_teto":
                case "тето":
                case "касане тето":
                    CharacterMode = "suit_teto";
                    PitchShift = 1.00;
                    BassGainDb = 1.0;
                    TrebleGainDb = 2.0;
                    WarmDrive = false;
                    ExosuitRadio = false;
                    ExosuitMix = 0.15;
                    ReverbWet = 0.03;
                    EchoWet = 0.0;
                    break;

                case "gek":
                case "гек":
                case "торговец гек":
                    CharacterMode = "wanderer";
                    PitchShift = 1.15;
                    BassGainDb = -1.0;
                    TrebleGainDb = -1.0;
                    GekThroat = true;
                    ReverbWet = 0.02;
                    EchoWet = 0.0;
                    break;

                case "vykeen":
                case "вайкин":
                case "вай'кин":
                case "воин вай'кин":
                    CharacterMode = "vykeen";
                    PitchShift = 0.80;
                    BassGainDb = 10.0;
                    TrebleGainDb = 7.0; // Sharp bite & attack on consonants!
                    ExosuitMix = 0.05;
                    ExosuitRadio = true;
                    WarmDrive = false;
                    ReverbWet = 0.05;
                    EchoWet = 0.0;
                    break;

                case "korvax":
                case "корвакс":
                    CharacterMode = "wanderer";
                    PitchShift = 1.05;
                    BassGainDb = -2.0;
                    TrebleGainDb = 4.0;
                    CyberEffect = true;
                    ReverbWet = 0.10;
                    EchoWet = 0.0;
                    break;

                case "monolith":
                case "монолит":
                case "атлас":
                case "монолит атласа":
                    CharacterMode = "wanderer";
                    PitchShift = 0.74;
                    BassGainDb = 2.0;
                    TrebleGainDb = 10.0;
                    CyberEffect = true;
                    ReverbWet = 0.40;
                    EchoWet = 0.0;
                    break;

                case "artemis":
                case "артемида":
                case "странница артемида":
                    CharacterMode = "artemis";
                    PitchShift = 1.06;
                    BassGainDb = 5.0;
                    TrebleGainDb = 6.0;
                    ExosuitMix = 0.30;
                    ExosuitRadio = true;
                    CyberEffect = true;
                    WarmDrive = true;
                    ReverbWet = 0.30;
                    EchoWet = 0.0;
                    break;

                case "wanderer":
                case "странник":
                case "раса странников":
                default:
                    CharacterMode = "wanderer";
                    PitchShift = 1.0;
                    BassGainDb = 0.0;
                    TrebleGainDb = 0.0;
                    ReverbWet = 0.0;
                    EchoWet = 0.0;
                    break;
            }
        }
    }

    public static class AudioDspService
    {
        public static byte[] ProcessWav(byte[] wavBytes, AudioEffectsConfig config)
        {
            if (wavBytes == null || wavBytes.Length <= 44) return wavBytes ?? Array.Empty<byte>();

            // Verify RIFF header
            if (wavBytes[0] != 'R' || wavBytes[1] != 'I' || wavBytes[2] != 'F' || wavBytes[3] != 'F')
                return wavBytes;

            int sampleRate = BitConverter.ToInt32(wavBytes, 24);
            int bitsPerSample = BitConverter.ToInt16(wavBytes, 34);

            if (bitsPerSample != 16) return wavBytes; // Only 16-bit PCM supported

            // Find "data" chunk dynamically
            int headerOffset = -1;
            for (int i = 12; i < wavBytes.Length - 8; i++)
            {
                if (wavBytes[i] == 'd' && wavBytes[i + 1] == 'a' && wavBytes[i + 2] == 't' && wavBytes[i + 3] == 'a')
                {
                    headerOffset = i + 8;
                    break;
                }
            }
            if (headerOffset < 0 || headerOffset >= wavBytes.Length) return wavBytes;

            int numSamples = (wavBytes.Length - headerOffset) / 2;
            if (numSamples <= 0) return wavBytes;

            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                short val = BitConverter.ToInt16(wavBytes, headerOffset + i * 2);
                samples[i] = val / 32768.0f;
            }

            // 1. Cyber / Metallic Ring Modulation (Korvax)
            if (config.CyberEffect)
            {
                double modFreq = 65.0; // 65 Hz drone
                for (int i = 0; i < samples.Length; i++)
                {
                    double t = (double)i / sampleRate;
                    float carrier = (float)Math.Sin(2.0 * Math.PI * modFreq * t);
                    samples[i] = samples[i] * 0.65f + (samples[i] * carrier) * 0.35f;
                }
            }

            // 2. Gek Amphibian Throat Formant & Flutter (Gek Trader)
            if (config.GekThroat)
            {
                // Throat Formant Peak at 880 Hz (croaking vowel resonance)
                ApplyPeaking(samples, sampleRate, 880.0, 7.5, 2.2);

                // Amphibian vocal sac flutter / subtle throat bubbling (22 Hz, ~15% depth)
                double flutterFreq = 22.0;
                for (int i = 0; i < samples.Length; i++)
                {
                    double t = (double)i / sampleRate;
                    float flutter = 0.85f + 0.15f * (float)Math.Sin(2.0 * Math.PI * flutterFreq * t);
                    samples[i] *= flutter;
                }
            }

            // 3. Warm Harmonic Saturation / Drive (Teto & Vy'keen)
            if (config.WarmDrive)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    float x = samples[i];
                    samples[i] = Math.Clamp(1.3f * x - 0.3f * (x * x * x), -1.0f, 1.0f);
                }
            }

            // 4. Bass Low-Shelf Filter (160 Hz)
            if (Math.Abs(config.BassGainDb) > 0.4)
            {
                ApplyLowShelf(samples, sampleRate, 160.0, config.BassGainDb);
            }

            // 5. Treble High-Shelf Filter (3800 Hz)
            if (Math.Abs(config.TrebleGainDb) > 0.4)
            {
                ApplyHighShelf(samples, sampleRate, 3800.0, config.TrebleGainDb);
            }

            // 6. Exosuit Radio Intercom Bandpass (smooth blend 0.0 - 1.0)
            double suitMix = config.ExosuitMix;

            if (suitMix > 0.03)
            {
                float[] radio = (float[])samples.Clone();
                ApplyBandpass(radio, sampleRate, 350.0, 4200.0);
                for (int i = 0; i < radio.Length; i++)
                {
                    float r = radio[i] * 1.35f;
                    radio[i] = Math.Clamp(1.2f * r - 0.2f * (r * r * r), -1.0f, 1.0f);
                }
                float m = (float)Math.Clamp(suitMix, 0.0, 1.0);
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = samples[i] * (1.0f - m) + radio[i] * m;
                }
            }

            // 7. Pitch Shift (SmbPhaseVocoder)
            if (Math.Abs(config.PitchShift - 1.0) > 0.03)
            {
                samples = SmbPitchShift.Process((float)config.PitchShift, samples, sampleRate);
            }

            // 8. Monolith Telepathic Detuned Chorus (S.T.A.L.K.E.R. Monolith Entity)
            if (config.MonolithChorus)
            {
                ApplyMonolithChorus(samples, sampleRate);
            }

            // 9. Studio Dynamic Compressor (Voice Presence & Clarity)
            if (config.StudioCompressor)
            {
                ApplyCompressor(samples, 0.40f, 2.5f);
            }

            // 10. Discrete Echo / Delay (Repetitions)
            if (config.EchoWet > 0.03)
            {
                ApplyEcho(samples, sampleRate, (float)Math.Clamp(config.EchoWet, 0.0, 0.50), 0.22f, 0.35f);
            }

            // 11. Reverb (Room & Space Volume)
            if (config.ReverbWet > 0.03)
            {
                ApplyReverb(samples, sampleRate, (float)Math.Clamp(config.ReverbWet, 0.0, 0.75));
            }

            // 11. Normalize & Soft-clip
            float maxPeak = 0.0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float abs = Math.Abs(samples[i]);
                if (abs > maxPeak) maxPeak = abs;
            }

            float gain = maxPeak > 0.95f ? 0.95f / maxPeak : 1.0f;

            // Convert back to 16-bit PCM bytes
            byte[] outBytes = new byte[headerOffset + samples.Length * 2];
            Array.Copy(wavBytes, outBytes, headerOffset);

            for (int i = 0; i < samples.Length; i++)
            {
                float s = samples[i] * gain;
                s = Math.Clamp(s, -1.0f, 1.0f);
                short pcm = (short)(s * 32767.0f);
                outBytes[headerOffset + i * 2] = (byte)(pcm & 0xFF);
                outBytes[headerOffset + i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);
            }

            // Patch "data" chunk size and RIFF size
            int dataLen = samples.Length * 2;
            Array.Copy(BitConverter.GetBytes(dataLen), 0, outBytes, headerOffset - 4, 4);
            Array.Copy(BitConverter.GetBytes(outBytes.Length - 8), 0, outBytes, 4, 4);

            return outBytes;
        }

        private static void ApplyEcho(float[] samples, int sampleRate, float echoWet, float delaySec = 0.22f, float feedback = 0.35f)
        {
            int delaySamples = (int)(sampleRate * delaySec);
            if (delaySamples <= 0 || delaySamples >= samples.Length) return;

            float[] delayBuffer = new float[delaySamples];
            int bufIdx = 0;

            for (int i = 0; i < samples.Length; i++)
            {
                float inVal = samples[i];
                float delayedVal = delayBuffer[bufIdx];

                delayBuffer[bufIdx] = inVal + delayedVal * feedback;
                bufIdx = (bufIdx + 1) % delaySamples;

                samples[i] = inVal + delayedVal * echoWet;
            }
        }

        private static void ApplyMonolithChorus(float[] samples, int sampleRate)
        {
            // Max delay ~40ms
            int maxDelay = (int)(sampleRate * 0.040);
            if (maxDelay <= 0 || maxDelay >= samples.Length) return;

            float[] delayBuf = new float[maxDelay * 2];
            int bufIdx = 0;
            int bufLen = delayBuf.Length;

            double lfoFreq1 = 0.32; // slow ominous phase drift
            double lfoFreq2 = 0.44; // secondary voice detune
            double baseDelay1 = sampleRate * 0.016; // 16ms
            double baseDelay2 = sampleRate * 0.025; // 25ms
            double modDepth = sampleRate * 0.006;   // 6ms modulation swing

            for (int i = 0; i < samples.Length; i++)
            {
                float inVal = samples[i];
                delayBuf[bufIdx] = inVal;

                double t = (double)i / sampleRate;

                // Voice 1 delay
                double d1 = baseDelay1 + modDepth * Math.Sin(2.0 * Math.PI * lfoFreq1 * t);
                int delayInt1 = (int)d1;
                float frac1 = (float)(d1 - delayInt1);
                int rIdx1 = (bufIdx - delayInt1 + bufLen) % bufLen;
                int rIdx1Next = (rIdx1 - 1 + bufLen) % bufLen;
                float v1 = delayBuf[rIdx1] * (1.0f - frac1) + delayBuf[rIdx1Next] * frac1;

                // Voice 2 delay
                double d2 = baseDelay2 + modDepth * Math.Cos(2.0 * Math.PI * lfoFreq2 * t);
                int delayInt2 = (int)d2;
                float frac2 = (float)(d2 - delayInt2);
                int rIdx2 = (bufIdx - delayInt2 + bufLen) % bufLen;
                int rIdx2Next = (rIdx2 - 1 + bufLen) % bufLen;
                float v2 = delayBuf[rIdx2] * (1.0f - frac2) + delayBuf[rIdx2Next] * frac2;

                bufIdx = (bufIdx + 1) % bufLen;

                // Mix: 55% direct speech + 25% voice 1 + 20% voice 2 (multi-entity unison)
                samples[i] = inVal * 0.55f + v1 * 0.25f + v2 * 0.20f;
            }
        }

        private static void ApplyCompressor(float[] samples, float threshold, float ratio)
        {
            float slope = 1.0f / ratio;
            for (int i = 0; i < samples.Length; i++)
            {
                float s = samples[i];
                float abs = Math.Abs(s);
                if (abs > threshold)
                {
                    float excess = abs - threshold;
                    float compressed = threshold + excess * slope;
                    samples[i] = Math.Sign(s) * compressed;
                }
            }
        }

        private static void ApplyLowShelf(float[] samples, int sRate, double freq, double gainDb)
        {
            double A = Math.Pow(10.0, gainDb / 40.0);
            double omega = 2.0 * Math.PI * freq / sRate;
            double sin = Math.Sin(omega);
            double cos = Math.Cos(omega);
            double beta = Math.Sqrt(A + A);

            double b0 = A * ((A + 1.0) - (A - 1.0) * cos + beta * sin);
            double b1 = 2.0 * A * ((A - 1.0) - (A + 1.0) * cos);
            double b2 = A * ((A + 1.0) - (A - 1.0) * cos - beta * sin);
            double a0 = (A + 1.0) + (A - 1.0) * cos + beta * sin;
            double a1 = -2.0 * ((A - 1.0) + (A + 1.0) * cos);
            double a2 = (A + 1.0) + (A - 1.0) * cos - beta * sin;

            ApplyBiquad(samples, b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
        }

        private static void ApplyHighShelf(float[] samples, int sRate, double freq, double gainDb)
        {
            double A = Math.Pow(10.0, gainDb / 40.0);
            double omega = 2.0 * Math.PI * freq / sRate;
            double sin = Math.Sin(omega);
            double cos = Math.Cos(omega);
            double beta = Math.Sqrt(A + A);

            double b0 = A * ((A + 1.0) + (A - 1.0) * cos + beta * sin);
            double b1 = -2.0 * A * ((A - 1.0) + (A + 1.0) * cos);
            double b2 = A * ((A + 1.0) + (A - 1.0) * cos - beta * sin);
            double a0 = (A + 1.0) - (A - 1.0) * cos + beta * sin;
            double a1 = 2.0 * ((A - 1.0) - (A + 1.0) * cos);
            double a2 = (A + 1.0) - (A - 1.0) * cos - beta * sin;

            ApplyBiquad(samples, b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
        }

        private static void ApplyPeaking(float[] samples, int sRate, double freq, double gainDb, double q = 1.0)
        {
            double A = Math.Pow(10.0, gainDb / 40.0);
            double omega = 2.0 * Math.PI * freq / sRate;
            double alpha = Math.Sin(omega) / (2.0 * q);
            double cos = Math.Cos(omega);

            double b0 = 1.0 + alpha * A;
            double b1 = -2.0 * cos;
            double b2 = 1.0 - alpha * A;
            double a0 = 1.0 + alpha / A;
            double a1 = -2.0 * cos;
            double a2 = 1.0 - alpha / A;

            ApplyBiquad(samples, b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
        }

        private static void ApplyBandpass(float[] samples, int sRate, double lowFreq, double highFreq)
        {
            float hpCoeff = (float)Math.Exp(-2.0 * Math.PI * lowFreq / sRate);
            float lpCoeff = (float)Math.Exp(-2.0 * Math.PI * highFreq / sRate);

            float hpPrevIn = 0f, hpPrevOut = 0f;
            float lpPrevOut = 0f;

            for (int i = 0; i < samples.Length; i++)
            {
                float inVal = samples[i];
                float hpOut = inVal - hpPrevIn + hpCoeff * hpPrevOut;
                hpPrevIn = inVal;
                hpPrevOut = hpOut;

                float lpOut = hpOut * (1f - lpCoeff) + lpPrevOut * lpCoeff;
                lpPrevOut = lpOut;

                samples[i] = lpOut;
            }
        }

        private static void ApplyBiquad(float[] samples, double b0, double b1, double b2, double a1, double a2)
        {
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                double x0 = samples[i];
                double y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x0;
                y2 = y1; y1 = y0;
                samples[i] = (float)y0;
            }
        }

        private static void ApplyReverb(float[] samples, int sRate, float wet)
        {
            int[] combDelays = { (int)(sRate * 0.0297), (int)(sRate * 0.0371), (int)(sRate * 0.0411), (int)(sRate * 0.0437) };
            float feedback = 0.72f;

            float[][] combBuffers = new float[4][];
            int[] combIndices = new int[4];
            for (int c = 0; c < 4; c++) combBuffers[c] = new float[combDelays[c]];

            int[] allpassDelays = { (int)(sRate * 0.005), (int)(sRate * 0.0017) };
            float[][] allpassBuffers = new float[2][];
            int[] allpassIndices = new int[2];
            for (int a = 0; a < 2; a++) allpassBuffers[a] = new float[allpassDelays[a]];

            float dry = 1.0f - wet * 0.5f;

            for (int i = 0; i < samples.Length; i++)
            {
                float input = samples[i];
                float combSum = 0f;

                for (int c = 0; c < 4; c++)
                {
                    int idx = combIndices[c];
                    float outC = combBuffers[c][idx];
                    combBuffers[c][idx] = input + outC * feedback;
                    combIndices[c] = (idx + 1) % combDelays[c];
                    combSum += outC;
                }

                combSum *= 0.25f;

                // Allpass 1
                int apIdx0 = allpassIndices[0];
                float apBuf0 = allpassBuffers[0][apIdx0];
                float apOut0 = -combSum + apBuf0;
                allpassBuffers[0][apIdx0] = combSum + apBuf0 * 0.5f;
                allpassIndices[0] = (apIdx0 + 1) % allpassDelays[0];

                // Allpass 2
                int apIdx1 = allpassIndices[1];
                float apBuf1 = allpassBuffers[1][apIdx1];
                float apOut1 = -apOut0 + apBuf1;
                allpassBuffers[1][apIdx1] = apOut0 + apBuf1 * 0.5f;
                allpassIndices[1] = (apIdx1 + 1) % allpassDelays[1];

                samples[i] = input * dry + apOut1 * wet;
            }
        }
    }
}
