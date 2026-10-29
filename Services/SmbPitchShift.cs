using System;

namespace NMS_Reader.Services
{
    /****************************************************************************
    * COPYRIGHT 1999-2015 Stephan M. Bernsee <smb [AT] dspdimension [DOT] com>
    * Ported to C# for high quality offline pitch shifting
    ****************************************************************************/
    public static class SmbPitchShift
    {
        private const int MAX_FRAME_LENGTH = 8192;
        private static readonly float[] gInFIFO = new float[MAX_FRAME_LENGTH];
        private static readonly float[] gOutFIFO = new float[MAX_FRAME_LENGTH];
        private static readonly float[] gFFTworksp = new float[2 * MAX_FRAME_LENGTH];
        private static readonly float[] gLastPhase = new float[MAX_FRAME_LENGTH / 2 + 1];
        private static readonly float[] gSumPhase = new float[MAX_FRAME_LENGTH / 2 + 1];
        private static readonly float[] gOutputAccum = new float[2 * MAX_FRAME_LENGTH];
        private static readonly float[] gAnaFreq = new float[MAX_FRAME_LENGTH];
        private static readonly float[] gAnaMagn = new float[MAX_FRAME_LENGTH];
        private static readonly float[] gSynFreq = new float[MAX_FRAME_LENGTH];
        private static readonly float[] gSynMagn = new float[MAX_FRAME_LENGTH];
        private static long gRover = 0;

        public static float[] Process(float pitchShift, float[] indata, float sampleRate, long fftFrameSize = 1024, long osamp = 4)
        {
            Array.Clear(gInFIFO, 0, gInFIFO.Length);
            Array.Clear(gOutFIFO, 0, gOutFIFO.Length);
            Array.Clear(gLastPhase, 0, gLastPhase.Length);
            Array.Clear(gSumPhase, 0, gSumPhase.Length);
            Array.Clear(gOutputAccum, 0, gOutputAccum.Length);
            gRover = 0;

            float[] outdata = new float[indata.Length];
            long numSampsToProcess = indata.Length;

            double magn, phase, tmp, window, real, imag;
            double freqPerBin, expct;
            long i, k, qpd, index, inFifoLatency, stepSize, fftFrameSize2;

            fftFrameSize2 = fftFrameSize / 2;
            stepSize = fftFrameSize / osamp;
            freqPerBin = sampleRate / (double)fftFrameSize;
            expct = 2.0 * Math.PI * (double)stepSize / (double)fftFrameSize;
            inFifoLatency = fftFrameSize - stepSize;
            if (gRover == 0) gRover = inFifoLatency;

            for (i = 0; i < numSampsToProcess; i++)
            {
                gInFIFO[gRover] = indata[i];
                outdata[i] = gOutFIFO[gRover - inFifoLatency];
                gRover++;

                if (gRover >= fftFrameSize)
                {
                    gRover = inFifoLatency;

                    for (k = 0; k < fftFrameSize; k++)
                    {
                        window = -0.5 * Math.Cos(2.0 * Math.PI * (double)k / (double)fftFrameSize) + 0.5;
                        gFFTworksp[2 * k] = (float)(gInFIFO[k] * window);
                        gFFTworksp[2 * k + 1] = 0.0f;
                    }

                    ShortTimeFourierTransform(gFFTworksp, fftFrameSize, -1);

                    for (k = 0; k <= fftFrameSize2; k++)
                    {
                        real = gFFTworksp[2 * k];
                        imag = gFFTworksp[2 * k + 1];

                        magn = 2.0 * Math.Sqrt(real * real + imag * imag);
                        phase = Math.Atan2(imag, real);

                        tmp = phase - gLastPhase[k];
                        gLastPhase[k] = (float)phase;

                        tmp -= (double)k * expct;

                        qpd = (long)(tmp / Math.PI);
                        if (qpd >= 0) qpd += qpd & 1;
                        else qpd -= qpd & 1;
                        tmp -= Math.PI * (double)qpd;

                        tmp = osamp * tmp / (2.0 * Math.PI);
                        tmp = (double)k * freqPerBin + tmp * freqPerBin;

                        gAnaMagn[k] = (float)magn;
                        gAnaFreq[k] = (float)tmp;
                    }

                    Array.Clear(gSynMagn, 0, (int)fftFrameSize);
                    Array.Clear(gSynFreq, 0, (int)fftFrameSize);
                    for (k = 0; k <= fftFrameSize2; k++)
                    {
                        index = (long)(k * pitchShift);
                        if (index <= fftFrameSize2)
                        {
                            gSynMagn[index] += gAnaMagn[k];
                            gSynFreq[index] = gAnaFreq[k] * pitchShift;
                        }
                    }

                    for (k = 0; k <= fftFrameSize2; k++)
                    {
                        magn = gSynMagn[k];
                        tmp = gSynFreq[k];

                        tmp -= (double)k * freqPerBin;
                        tmp /= freqPerBin;
                        tmp = 2.0 * Math.PI * tmp / osamp;
                        tmp += (double)k * expct;

                        gSumPhase[k] += (float)tmp;
                        phase = gSumPhase[k];

                        gFFTworksp[2 * k] = (float)(magn * Math.Cos(phase));
                        gFFTworksp[2 * k + 1] = (float)(magn * Math.Sin(phase));
                    }

                    for (k = fftFrameSize + 2; k < 2 * fftFrameSize; k++) gFFTworksp[k] = 0.0f;

                    ShortTimeFourierTransform(gFFTworksp, fftFrameSize, 1);

                    for (k = 0; k < fftFrameSize; k++)
                    {
                        window = -0.5 * Math.Cos(2.0 * Math.PI * (double)k / (double)fftFrameSize) + 0.5;
                        gOutputAccum[k] += (float)(2.0 * window * gFFTworksp[2 * k] / (fftFrameSize2 * osamp));
                    }
                    for (k = 0; k < stepSize; k++) gOutFIFO[k] = gOutputAccum[k];

                    Array.Copy(gOutputAccum, stepSize, gOutputAccum, 0, fftFrameSize);
                    Array.Clear(gOutputAccum, (int)fftFrameSize, (int)stepSize);

                    Array.Copy(gInFIFO, stepSize, gInFIFO, 0, inFifoLatency);
                }
            }

            return outdata;
        }

        private static void ShortTimeFourierTransform(float[] fftBuffer, long fftFrameSize, long sign)
        {
            float wr, wi, arg, temp;
            float tr, ti, ur, ui;
            long i, bitm, j, le, le2, k;

            for (i = 2; i < 2 * fftFrameSize - 2; i += 2)
            {
                for (bitm = 2, j = 0; bitm < 2 * fftFrameSize; bitm <<= 1)
                {
                    if ((i & bitm) != 0) j++;
                    j <<= 1;
                }
                if (i < j)
                {
                    temp = fftBuffer[i];
                    fftBuffer[i] = fftBuffer[j];
                    fftBuffer[j] = temp;
                    temp = fftBuffer[i + 1];
                    fftBuffer[i + 1] = fftBuffer[j + 1];
                    fftBuffer[j + 1] = temp;
                }
            }

            long max = (long)(Math.Log(fftFrameSize) / Math.Log(2.0) + 0.5);
            for (k = 0, le = 2; k < max; k++)
            {
                le <<= 1;
                le2 = le >> 1;
                ur = 1.0f;
                ui = 0.0f;
                arg = (float)(Math.PI / (le2 >> 1));
                wr = (float)Math.Cos(arg);
                wi = (float)(sign * Math.Sin(arg));
                for (j = 0; j < le2; j += 2)
                {
                    for (i = j; i < 2 * fftFrameSize; i += le)
                    {
                        tr = fftBuffer[i + le2] * ur - fftBuffer[i + le2 + 1] * ui;
                        ti = fftBuffer[i + le2] * ui + fftBuffer[i + le2 + 1] * ur;
                        fftBuffer[i + le2] = fftBuffer[i] - tr;
                        fftBuffer[i + le2 + 1] = fftBuffer[i + 1] - ti;
                        fftBuffer[i] += tr;
                        fftBuffer[i + 1] += ti;
                    }
                    tr = ur * wr - ui * wi;
                    ui = ur * wi + ui * wr;
                    ur = tr;
                }
            }
        }
    }
}
