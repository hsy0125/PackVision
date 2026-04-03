using System;
using System.Drawing;
using System.Text.RegularExpressions;
using PackVisionApp.Models;
using Tesseract;
using System.Diagnostics;

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
                    using (Bitmap testImg = (Bitmap)cropped.Clone())
                    {
                        testImg.RotateFlip(rotation);
                        SaveDebug(testImg, $"date_rot_{rotation}");

                        // 1차: grayscale + 대비강화 + 4배 확대
                        using (Bitmap grayImg = ToGrayscale(testImg))
                        using (Bitmap enhancedImg = EnhanceContrast(grayImg))
                        using (Bitmap resizedGrayImg = ResizeBitmap(enhancedImg, 4.0))
                        {
                            SaveDebug(grayImg, $"date_gray_{rotation}");
                            SaveDebug(enhancedImg, $"date_enhanced_{rotation}");
                            SaveDebug(resizedGrayImg, $"date_gray_resized_{rotation}");

                            using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
                            {
                                engine.SetVariable("tessedit_char_whitelist", "0123456789.-/");
                                engine.DefaultPageSegMode = PageSegMode.SingleLine;

                                using (var img = PixConverter.ToPix(resizedGrayImg))
                                using (var page = engine.Process(img))
                                {
                                    string raw = page.GetText();
                                    string corrected = CorrectCommonMistakes(raw);
                                    string filtered = FilterDate(corrected);
                                    string normalized = NormalizeDate(filtered);

                                    Debug.WriteLine($"[DateOCR-Gray] rotation={rotation} | raw='{raw}' | corrected='{corrected}' | filtered='{filtered}' | normalized='{normalized}'");

                                    if (!string.IsNullOrEmpty(normalized))
                                        return DateResult.Ok(normalized);
                                }
                            }
                        }

                        // 2차: binary + 4배 확대
                        using (Bitmap binaryImg = ToBinary(testImg))
                        using (Bitmap resizedBinaryImg = ResizeBitmap(binaryImg, 4.0))
                        {
                            SaveDebug(binaryImg, $"date_binary_{rotation}");
                            SaveDebug(resizedBinaryImg, $"date_binary_resized_{rotation}");

                            using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
                            {
                                engine.SetVariable("tessedit_char_whitelist", "0123456789.-/");
                                engine.DefaultPageSegMode = PageSegMode.SingleLine;

                                using (var img = PixConverter.ToPix(resizedBinaryImg))
                                using (var page = engine.Process(img))
                                {
                                    string raw = page.GetText();
                                    string corrected = CorrectCommonMistakes(raw);
                                    string filtered = FilterDate(corrected);
                                    string normalized = NormalizeDate(filtered);

                                    Debug.WriteLine($"[DateOCR-Bin] rotation={rotation} | raw='{raw}' | corrected='{corrected}' | filtered='{filtered}' | normalized='{normalized}'");

                                    if (!string.IsNullOrEmpty(normalized))
                                        return DateResult.Ok(normalized);
                                }
                            }
                        }
                    }
                }

                return DateResult.Fail("date_ocr_fail");
            }
			catch (Exception ex)
			{
				Debug.WriteLine("[DateReader ERROR] " + ex.ToString());
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
                    binary.SetPixel(x, y, gray > 90 ? Color.White : Color.Black);
                }
            }

            return binary;
        }

        private Bitmap ToGrayscale(Bitmap original)
        {
            Bitmap gray = new Bitmap(original.Width, original.Height);

            for (int y = 0; y < original.Height; y++)
            {
                for (int x = 0; x < original.Width; x++)
                {
                    Color c = original.GetPixel(x, y);
                    int g = (int)(c.R * 0.299 + c.G * 0.587 + c.B * 0.114);
                    gray.SetPixel(x, y, Color.FromArgb(g, g, g));
                }
            }

            return gray;
        }

        private Bitmap EnhanceContrast(Bitmap original)
        {
            Bitmap result = new Bitmap(original.Width, original.Height);

            for (int y = 0; y < original.Height; y++)
            {
                for (int x = 0; x < original.Width; x++)
                {
                    Color c = original.GetPixel(x, y);
                    int v = c.R;

                    v = (v - 128) * 2 + 128;

                    if (v < 0) v = 0;
                    if (v > 255) v = 255;

                    result.SetPixel(x, y, Color.FromArgb(v, v, v));
                }
            }

            return result;
        }

        private Bitmap ResizeBitmap(Bitmap original, double scale)
        {
            int w = Math.Max(1, (int)(original.Width * scale));
            int h = Math.Max(1, (int)(original.Height * scale));
            return new Bitmap(original, new Size(w, h));
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
            .Replace('B', '8')
            .Replace('g', '9')
            .Replace('q', '9');
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

			// Tesseract 결과에 잡음 숫자가 섞여 digits 길이가 6/8이 아닌 경우가 있어
			// (예: "27.01.27 B5 F1"), 무조건 실패하지 말고 "마지막" 날짜 구간을 사용한다.
			// - digits.Length >= 8 이면 마지막 8자리(YYYYMMDD) 사용
			// - 그 외 digits.Length >= 6 이면 마지막 6자리(YYMMDD) 사용
			if (digits.Length >= 8)
			{
				digits = digits.Substring(digits.Length - 8, 8);
				string y = digits.Substring(0, 4);
				string m = digits.Substring(4, 2);
				string d = digits.Substring(6, 2);
				return $"{y}-{m}-{d}";
			}

			if (digits.Length >= 6)
			{
				digits = digits.Substring(digits.Length - 6, 6);
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