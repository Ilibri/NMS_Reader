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
using NMS_Reader.Models;

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
                if (skipRightSideChoices && line.Words.Count > 0)
                {
                    double minX = double.MaxValue;
                    foreach (var w in line.Words)
                    {
                        if (w.BoundingRect.X < minX) minX = w.BoundingRect.X;
                    }

                    // In No Man's Sky dialogue with interactive choices:
                    // Left dialogue panel starts at X <= 0.30 * bmpWidth.
                    // Right choices menu column starts at X >= 0.65 * bmpWidth.
                    // If a line starts strictly on the right side, it belongs to the choice buttons -> skip!
                    if (minX >= bmpWidth * 0.65)
                    {
                        continue;
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

        public async Task<List<DialogueSegment>> RecognizeDialogueSegmentsAsync(Bitmap bitmap, bool skipTitle = true, bool skipChoices = true)
        {
            var segments = new List<DialogueSegment>();
            if (_ocrEngine == null || bitmap == null)
                return segments;

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

            double bmpWidth = bitmap.Width;

            // 1. Collect valid dialogue lines
            var rawLines = new List<(string Text, Windows.Foundation.Rect Rect)>();
            foreach (var line in ocrResult.Lines)
            {
                if (line.Words.Count == 0) continue;
                string lineText = line.Text.Trim();
                if (string.IsNullOrWhiteSpace(lineText)) continue;

                double minX = line.Words.Min(w => w.BoundingRect.X);
                double maxX = line.Words.Max(w => w.BoundingRect.X + w.BoundingRect.Width);
                double minY = line.Words.Min(w => w.BoundingRect.Y);
                double maxY = line.Words.Max(w => w.BoundingRect.Y + w.BoundingRect.Height);

                // Skip choices menu column on the right
                if (skipChoices && minX >= bmpWidth * 0.65)
                {
                    continue;
                }

                rawLines.Add((lineText, new Windows.Foundation.Rect(minX, minY, maxX - minX, maxY - minY)));
            }

            if (rawLines.Count == 0) return segments;

            // 2. Reject menus, trade terminal, HUD
            var texts = rawLines.Select(r => r.Text).ToList();
            if (NmsTextCleaner.IsInvalidDialogue(texts))
            {
                return segments;
            }

            int startIdx = 0;
            // 3. Skip NPC Title if requested
            if (skipTitle && rawLines.Count > 1 && NmsTextCleaner.IsTitleLine(rawLines[0].Text))
            {
                startIdx = 1;
            }

            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                int stride = Math.Abs(bmpData.Stride);
                byte[] pixels = new byte[stride * bitmap.Height];
                Marshal.Copy(bmpData.Scan0, pixels, 0, pixels.Length);

                for (int i = startIdx; i < rawLines.Count; i++)
                {
                    var item = rawLines[i];
                    if (skipChoices && NmsTextCleaner.IsChoiceLine(item.Text))
                    {
                        continue;
                    }

                    string cleanedLine = NmsTextCleaner.FilterAlienGibberish(item.Text).Trim();
                    if (string.IsNullOrWhiteSpace(cleanedLine) || cleanedLine.Length < 2)
                    {
                        continue;
                    }

                    int x0 = Math.Max(0, (int)item.Rect.X);
                    int x1 = Math.Min(bitmap.Width - 1, (int)(item.Rect.X + item.Rect.Width));
                    int y0 = Math.Max(0, (int)item.Rect.Y);
                    int y1 = Math.Min(bitmap.Height - 1, (int)(item.Rect.Y + item.Rect.Height));

                    int white = 0, gek = 0, vykeen = 0, korvax = 0;

                    for (int y = y0; y <= y1; y++)
                    {
                        int rowOffset = y * stride;
                        for (int x = x0; x <= x1; x++)
                        {
                            int idx = rowOffset + (x * 4);
                            if (idx + 2 >= pixels.Length) break;

                            int b = pixels[idx];
                            int g = pixels[idx + 1];
                            int r = pixels[idx + 2];

                            int max = Math.Max(r, Math.Max(g, b));
                            if (max < 130) continue;

                            if (Math.Abs(r - g) < 22 && Math.Abs(r - b) < 22 && Math.Abs(g - b) < 22 && r > 140)
                            {
                                white++;
                            }
                            else if (b > g + 20 && r > g + 15 && b > 135)
                            {
                                korvax++;
                            }
                            else if (g > r + 30 && g > b + 20 && g > 140)
                            {
                                gek++;
                            }
                            else if (r > g + 45 && r > b + 45 && r > 140)
                            {
                                vykeen++;
                            }
                        }
                    }

                    string lineRace = "suit";
                    if (korvax > 20 && korvax >= white && korvax >= gek && korvax >= vykeen)
                        lineRace = "korvax";
                    else if (gek > 25 && gek >= white && gek >= vykeen)
                        lineRace = "gek";
                    else if (vykeen > 25 && vykeen >= white)
                        lineRace = "vykeen";
                    else
                        lineRace = "suit"; // White text: thoughts of protagonist!

                    if (segments.Count > 0 && segments[^1].Race == lineRace)
                    {
                        segments[^1].Text += " " + cleanedLine;
                    }
                    else
                    {
                        segments.Add(new DialogueSegment(lineRace, cleanedLine));
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }

            return segments;
        }

        /// <summary>
        /// Detects the "Page Ready / Typewriter Finished" prompt icon (mouse + white downward chevron \/)
        /// located at the bottom-center of the dialogue panel in No Man's Sky.
        /// Returns true immediately when the entire dialogue page is typed and waiting for user input.
        /// </summary>
                public bool DetectDialogueReadyIcon(Bitmap bmp)
        {
            if (bmp == null) return false;

            int w = bmp.Width;
            int h = bmp.Height;

            int yMin = (int)(h * 0.50);
            int yMax = (int)(h * 0.99);
            int xMin = (int)(w * 0.20);
            int xMax = (int)(w * 0.80);

            var rect = new Rectangle(0, 0, w, h);
            BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                int stride = Math.Abs(data.Stride);
                byte[] pixels = new byte[stride * h];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

                bool IsWhite(int px, int py)
                {
                    if (px < 0 || px >= w || py < 0 || py >= h) return false;
                    int idx = (py * stride) + (px * 4);
                    if (idx + 2 >= pixels.Length) return false;
                    return pixels[idx + 2] >= 190 && pixels[idx + 1] >= 190 && pixels[idx] >= 190;
                }

                bool IsDark(int px, int py)
                {
                    if (px < 0 || px >= w || py < 0 || py >= h) return true;
                    int idx = (py * stride) + (px * 4);
                    if (idx + 2 >= pixels.Length) return true;
                    return pixels[idx + 2] < 155 && pixels[idx + 1] < 155 && pixels[idx] < 155;
                }

                for (int y = yMax - 4; y >= yMin + 10; y--)
                {
                    for (int x = xMin + 10; x < xMax - 10; x++)
                    {
                        if (!IsWhite(x, y)) continue;

                        if (!IsDark(x, y + 3)) continue;
                        if (!IsDark(x, y - 7)) continue;

                        int armMatches = 0;
                        int[] dists = { 4, 5, 6, 7, 8 };
                        foreach (int d in dists)
                        {
                            int lx = x - d;
                            int rx = x + d;
                            int uy = y - d;
                            if (lx < 0 || rx >= w || uy < 0) break;

                            if (IsWhite(lx, uy) && IsWhite(rx, uy))
                            {
                                armMatches++;
                            }
                        }

                        if (armMatches >= 3)
                        {
                            return true;
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            return false;
        }

        public string DetectRaceFromBitmap(Bitmap bmp, string? textHint = null)
        {
            if (bmp == null) return "suit";

            int w = bmp.Width;
            int h = bmp.Height;

            // Scan dialogue subtitle text area strictly on the left: X: 0.08 * w to 0.65 * w, Y: 0.78 * h to 0.95 * h
            // This prevents sampling HUD buttons, choice buttons, and background scenery/lights on the right!
            int yStart = (int)(h * 0.78);
            int yEnd = (int)(h * 0.95);
            int xStart = (int)(w * 0.08);
            int xEnd = (int)(w * 0.65);

            int whitePixels = 0;
            int gekPixels = 0;
            int vykeenPixels = 0;
            int korvaxPixels = 0;

            var rect = new Rectangle(0, 0, w, h);
            var bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = Math.Abs(bmpData.Stride);
                byte[] pixels = new byte[stride * h];
                Marshal.Copy(bmpData.Scan0, pixels, 0, pixels.Length);

                for (int y = yStart; y < yEnd; y += 2)
                {
                    int rowOffset = y * stride;
                    for (int x = xStart; x < xEnd; x += 2)
                    {
                        int idx = rowOffset + (x * 4);
                        if (idx + 2 >= pixels.Length) break;

                        int b = pixels[idx];
                        int g = pixels[idx + 1];
                        int r = pixels[idx + 2];

                        int max = Math.Max(r, Math.Max(g, b));
                        if (max < 130) continue; // Ignore dark background / translucent box

                        // 0. White / Neutral text (Narrator observations / Protagonist thoughts)
                        bool isWhite = Math.Abs(r - g) < 22 && Math.Abs(r - b) < 22 && Math.Abs(g - b) < 22 && r > 140;
                        if (isWhite)
                        {
                            whitePixels++;
                        }
                        // 1. Korvax: Lavender / Lilac / Purple (B and R both significantly higher than G)
                        else if (b > g + 20 && r > g + 15 && b > 135)
                        {
                            korvaxPixels++;
                        }
                        // 2. Gek: Lime Green (G dominant)
                        else if (g > r + 30 && g > b + 20 && g > 140)
                        {
                            gekPixels++;
                        }
                        // 3. Vy'keen: Coral Red (R strongly dominant, avoids warm ambient floor lights)
                        else if (r > g + 45 && r > b + 45 && r > 140)
                        {
                            vykeenPixels++;
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

            // 1. White narrative text (Protagonist observations / thoughts) ALWAYS has top priority
            if (whitePixels >= 50 && whitePixels >= korvaxPixels && whitePixels >= gekPixels && whitePixels >= vykeenPixels)
                return "suit";

            // 2. Clear alien speech pixels
            if (gekPixels >= 40 && gekPixels >= vykeenPixels && gekPixels >= korvaxPixels)
                return "gek";

            // Disambiguate between Korvax and Vy'keen if colored pixels are present
            if (!string.IsNullOrWhiteSpace(textHint))
            {
                string lower = textHint.ToLowerInvariant();
                if (korvaxPixels >= 25 && (lower.Contains("корвакс") || lower.Contains("сущность") || lower.Contains("синтез") || lower.Contains("нада")))
                    return "korvax";
                if (vykeenPixels >= 25 && (lower.Contains("вай'кин") || lower.Contains("вайкин") || lower.Contains("воин") || lower.Contains("грах")))
                    return "vykeen";
            }

            if (korvaxPixels >= 35 && korvaxPixels >= gekPixels && korvaxPixels >= vykeenPixels)
                return "korvax";

            if (gekPixels >= 40 && gekPixels >= vykeenPixels)
                return "gek";

            if (vykeenPixels >= 40)
                return "vykeen";

            return "suit"; // Fallback to protagonist thoughts
        }
    }
}
