using System;
using System.Drawing;
using System.Text.RegularExpressions;
using PackVisionApp.Models;
using Tesseract;

namespace PackVisionApp.Vision
{
    public class DateReader
    {
        public DateResult ReadDate(Bitmap frame, Rectangle roi)
        {
            if (frame == null || roi == Rectangle.Empty)
                return DateResult.Fail("date_ocr_fail");

            Rectangle safeRoi = ClampRect(roi, frame.Width, frame.Height);
            if (safeRoi == Rectangle.Empty)
                return DateResult.Fail("date_ocr_fail");

            try
            {
                Bitmap cropped = frame.Clone(safeRoi, frame.PixelFormat);
                SaveDebug(cropped, "date_roi_raw");

                RotateFlipType[] rotations = new[]
                {
                    RotateFlipType.RotateNoneFlipNone,
                    RotateFlipType.Rotate90FlipNone,
                    RotateFlipType.Rotate180FlipNone,
                    RotateFlipType.Rotate270FlipNone
                };

                foreach (var rotation in rotations)
                {
                    Bitmap testImg = (Bitmap)cropped.Clone();
                    testImg.RotateFlip(rotation);

                    Bitmap binaryImg = ToBinary(testImg);
                    SaveDebug(binaryImg, $"date_try_{rotation}");

                    using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
                    {
                        engine.SetVariable("tessedit_char_whitelist", "0123456789.-/");
                        engine.DefaultPageSegMode = PageSegMode.SingleLine;

                        using (var img = PixConverter.ToPix(binaryImg))
                        using (var page = engine.Process(img))
                        {
                            string raw = page.GetText();
                            string corrected = CorrectCommonMistakes(raw);
                            string filtered = FilterDate(corrected);
                            string normalized = NormalizeDate(filtered);

                            if (!string.IsNullOrEmpty(normalized))
                                return DateResult.Ok(normalized);
                        }
                    }
                }

                return DateResult.Fail("date_ocr_fail");
            }
            catch
            {
                return DateResult.Fail("date_ocr_fail");
            }
        }

        private Rectangle ClampRect(Rectangle roi, int frameWidth, int frameHeight)
        {
            int x = Math.Max(0, roi.X);
            int y = Math.Max(0, roi.Y);
            int right = Math.Min(frameWidth, roi.Right);
            int bottom = Math.Min(frameHeight, roi.Bottom);

            if (right <= x || bottom <= y)
                return Rectangle.Empty;

            return new Rectangle(x, y, right - x, bottom - y);
        }

        private Bitmap ToBinary(Bitmap original)
        {
            Bitmap binary = new Bitmap(original.Width, original.Height);

            for (int y = 0; y < original.Height; y++)
            {
                for (int x = 0; x < original.Width; x++)
                {
                    Color c = original.GetPixel(x, y);
                    int gray = (c.R + c.G + c.B) / 3;
                    binary.SetPixel(x, y, gray > 100 ? Color.White : Color.Black);
                }
            }

            return binary;
        }

        private string CorrectCommonMistakes(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";

            return raw
              .Replace('O', '0')
              .Replace('o', '0')
              .Replace('I', '1')
              .Replace('l', '1')
              .Replace('Z', '2')
              .Replace('S', '5')
              .Replace('B', '8');
        }

        private string FilterDate(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";

            return Regex.Replace(raw, @"[^0-9\.\-\/]", "").Trim();
        }

        private string NormalizeDate(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return null;

            string digits = Regex.Replace(raw, @"[^0-9]", "");

            if (digits.Length == 8)
            {
                string y = digits.Substring(0, 4);
                string m = digits.Substring(4, 2);
                string d = digits.Substring(6, 2);
                return $"{y}-{m}-{d}";
            }

            if (digits.Length == 6)
            {
                string y = "20" + digits.Substring(0, 2);
                string m = digits.Substring(2, 2);
                string d = digits.Substring(4, 2);
                return $"{y}-{m}-{d}";
            }

            return null;
        }

        private void SaveDebug(Bitmap img, string name)
        {
            try
            {
                System.IO.Directory.CreateDirectory("DebugImages");
                img.Save($"DebugImages/{name}.png");
            }
            catch { }
        }
    }
}