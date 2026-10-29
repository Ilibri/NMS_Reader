using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace NMS_Reader.Services
{
    public class OcrService
    {
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        private readonly OcrEngine? _ocrEngine;

        public OcrService()
        {
            _ocrEngine = OcrEngine.TryCreateFromLanguage(new Language("ru"))
                         ?? OcrEngine.TryCreateFromUserProfileLanguages();
        }

        public async Task<List<string>> RecognizeFromBitmapAsync(Bitmap bitmap, bool skipRightSideChoices = true)
        {
            if (_ocrEngine == null)
                return new List<string>();

            using var stream = new InMemoryRandomAccessStream();
            using (var memStream = new MemoryStream())
            {
                bitmap.Save(memStream, ImageFormat.Bmp);
                var bytes = memStream.ToArray();
                await stream.WriteAsync(bytes.AsBuffer());
            }
            stream.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(stream);
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            var ocrResult = await _ocrEngine.RecognizeAsync(softwareBitmap);
            var lines = new List<string>();
            double bmpWidth = bitmap.Width;

            foreach (var line in ocrResult.Lines)
            {
                if (skipRightSideChoices && bmpWidth > 0)
                {
                    var firstWord = line.Words.FirstOrDefault();
                    if (firstWord != null)
                    {
                        // In NMS dialogues with choices, the choice buttons are on the right side (X >= 64% of dialog width)
                        double xRatio = firstWord.BoundingRect.X / bmpWidth;
                        if (xRatio >= 0.64)
                        {
                            continue;
                        }
                    }
                }

                lines.Add(line.Text);
            }
            return lines;
        }

        public Bitmap CaptureScreen(bool fullscreen, Models.AppConfig config)
        {
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);

            if (screenWidth <= 0) screenWidth = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
            if (screenHeight <= 0) screenHeight = (int)System.Windows.SystemParameters.PrimaryScreenHeight;

            int x = 0;
            int y = 0;
            int w = screenWidth;
            int h = screenHeight;

            if (!fullscreen)
            {
                x = (int)(screenWidth * (config.XMinPercent / 100.0));
                y = (int)(screenHeight * (config.YMinPercent / 100.0));
                w = (int)(screenWidth * ((config.XMaxPercent - config.XMinPercent) / 100.0));
                h = (int)(screenHeight * ((config.YMaxPercent - config.YMinPercent) / 100.0));
            }

            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(x, y, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
            }
            return bmp;
        }
    }
}
