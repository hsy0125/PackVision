using System;
using System.Drawing;
using System.Text.RegularExpressions;
using PackVisionApp.Models;
using Tesseract;
using System.Diagnostics;

namespace PackVisionApp.Vision
{
    /// <summary>
    /// DateReader
    /// 
    /// 역할:
    /// - 날짜 ROI 영역에서 OCR로 날짜 문자열을 읽음
    /// - 날짜 뒤에 붙는 코드(A1 F1, B2 F2 등)도 같이 읽음
    /// - 축소 OCR 방식 사용
    /// - 결과는 InspectionManager 쪽에서 다시 비교하기 쉽게
    ///   "27-01-28 A1 F1" 또는 "2027-01-28 A1 F1" 형태로 정리해서 반환
    /// </summary>
    public class DateReader
    {
        public DateResult ReadDate(Bitmap frame, Rectangle roi)
        {
            // 원본 이미지가 없거나 ROI가 비었으면 실패
            if (frame == null || roi == Rectangle.Empty)
                return DateResult.Fail("date_ocr_fail");

            // ROI가 이미지 밖으로 벗어나지 않게 보정
            Rectangle safeRoi = ClampRect(roi, frame.Width, frame.Height);
            if (safeRoi == Rectangle.Empty)
                return DateResult.Fail("date_ocr_fail");

            try
            {
                // 날짜 ROI만 잘라냄
                Bitmap cropped = frame.Clone(safeRoi, frame.PixelFormat);
                SaveDebug(cropped, "date_roi_raw");

                // 날짜가 회전되어 들어올 가능성 때문에 4방향 시도
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

                        // -------------------------------------------------
                        // 1차 OCR : grayscale + 대비강화 + 축소
                        // -------------------------------------------------
                        using (Bitmap grayImg = ToGrayscale(testImg))
                        using (Bitmap enhancedImg = EnhanceContrast(grayImg))
                        using (Bitmap resizedGrayImg = ResizeBitmap(enhancedImg, 0.7))
                        {
                            SaveDebug(grayImg, $"date_gray_{rotation}");
                            SaveDebug(enhancedImg, $"date_enhanced_{rotation}");
                            SaveDebug(resizedGrayImg, $"date_gray_resized_{rotation}");

                            using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
                            {
                                // 숫자 + 영문 + 날짜구분자 + 공백 허용
                                engine.SetVariable("tessedit_char_whitelist",
                                    "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz.-/ ");
                                engine.DefaultPageSegMode = PageSegMode.SingleLine;

                                using (var img = PixConverter.ToPix(resizedGrayImg))
                                using (var page = engine.Process(img))
                                {
                                    string raw = page.GetText();
                                    string corrected = CorrectCommonMistakes(raw);
                                    string filtered = FilterDate(corrected);
                                    string normalized = NormalizeDateText(filtered);

                                    Debug.WriteLine($"[DateOCR-Gray] rotation={rotation} | raw='{raw}' | corrected='{corrected}' | filtered='{filtered}' | normalized='{normalized}'");

                                    if (!string.IsNullOrEmpty(normalized))
                                        return DateResult.Ok(normalized);
                                }
                            }
                        }

                        // -------------------------------------------------
                        // 2차 OCR : binary + 축소
                        // -------------------------------------------------
                        using (Bitmap binaryImg = ToBinary(testImg))
                        using (Bitmap resizedBinaryImg = ResizeBitmap(binaryImg, 0.7))
                        {
                            SaveDebug(binaryImg, $"date_binary_{rotation}");
                            SaveDebug(resizedBinaryImg, $"date_binary_resized_{rotation}");

                            using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
                            {
                                // 숫자 + 영문 + 날짜구분자 + 공백 허용
                                engine.SetVariable("tessedit_char_whitelist",
                                    "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz.-/ ");
                                engine.DefaultPageSegMode = PageSegMode.SingleLine;

                                using (var img = PixConverter.ToPix(resizedBinaryImg))
                                using (var page = engine.Process(img))
                                {
                                    string raw = page.GetText();
                                    string corrected = CorrectCommonMistakes(raw);
                                    string filtered = FilterDate(corrected);
                                    string normalized = NormalizeDateText(filtered);

                                    Debug.WriteLine($"[DateOCR-Bin] rotation={rotation} | raw='{raw}' | corrected='{corrected}' | filtered='{filtered}' | normalized='{normalized}'");

                                    if (!string.IsNullOrEmpty(normalized))
                                        return DateResult.Ok(normalized);
                                }
                            }
                        }
                    }
                }

                // 모든 회전 / 전처리 시도 후 실패
                return DateResult.Fail("date_ocr_fail");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[DateReader ERROR] " + ex.ToString());
                return DateResult.Fail("date_ocr_fail");
            }
        }

        /// <summary>
        /// ROI가 이미지 범위를 넘지 않게 보정
        /// </summary>
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

        /// <summary>
        /// 흑백 이진화
        /// </summary>
        private Bitmap ToBinary(Bitmap original)
        {
            Bitmap binary = new Bitmap(original.Width, original.Height);

            for (int y = 0; y < original.Height; y++)
            {
                for (int x = 0; x < original.Width; x++)
                {
                    Color c = original.GetPixel(x, y);
                    int gray = (int)(c.R * 0.299 + c.G * 0.587 + c.B * 0.114);

                    if (gray < 160)
                        binary.SetPixel(x, y, Color.Black);   // 글자
                    else
                        binary.SetPixel(x, y, Color.White);   // 배경
                }
            }

            return binary;
        }

        /// <summary>
        /// 그레이스케일 변환
        /// </summary>
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

        /// <summary>
        /// 단순 대비 강화
        /// </summary>
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

        /// <summary>
        /// 이미지 축소 / 확대
        /// 현재는 네 요청대로 축소(0.7)에서만 사용
        /// </summary>
        private Bitmap ResizeBitmap(Bitmap original, double scale)
        {
            int w = Math.Max(1, (int)(original.Width * scale));
            int h = Math.Max(1, (int)(original.Height * scale));
            return new Bitmap(original, new Size(w, h));
        }

        /// <summary>
        /// OCR이 자주 틀리는 문자 보정
        /// </summary>
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

        /// <summary>
        /// 날짜 OCR에 필요한 문자만 남김
        /// 숫자 / 영문 / 점 / 하이픈 / 슬래시 / 공백 허용
        /// </summary>
        private string FilterDate(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";

            return Regex.Replace(raw, @"[^0-9A-Za-z\.\-\/ ]", "").Trim();
        }

        /// <summary>
        /// OCR 결과를 "날짜 + 뒤 코드" 형태로 정리
        /// 
        /// 예:
        /// 27.01.28 A1 F1 -> 2027-01-28 A1 F1
        /// 27-01-28 B2 F2 -> 2027-01-28 B2 F2
        /// 
        /// 주의:
        /// - 점/하이픈/슬래시는 모두 허용
        /// - 날짜 뒤의 A1 F1 같은 문자열도 같이 반환
        /// - 최종 비교는 InspectionManager에서 날짜/코드 따로 비교함
        /// </summary>
        private string NormalizeDateText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            string text = raw.Trim().ToUpper();
            text = Regex.Replace(text, @"\s+", " ");

            // 날짜 부분 + 뒤 문자열 분리
            Match match = Regex.Match(text, @"(?<date>\d{2,4}[.\-/]\d{2}[.\-/]\d{2})(?<suffix>.*)");
            if (!match.Success)
                return null;

            string datePart = match.Groups["date"].Value;
            string suffixPart = match.Groups["suffix"].Value.Trim();

            string digits = Regex.Replace(datePart, @"[^0-9]", "");
            string normalizedDate = null;

            // YYMMDD -> 20YY-MM-DD
            if (digits.Length == 6)
            {
                string y = "20" + digits.Substring(0, 2);
                string m = digits.Substring(2, 2);
                string d = digits.Substring(4, 2);
                normalizedDate = $"{y}-{m}-{d}";
            }
            // YYYYMMDD -> YYYY-MM-DD
            else if (digits.Length == 8)
            {
                string y = digits.Substring(0, 4);
                string m = digits.Substring(4, 2);
                string d = digits.Substring(6, 2);
                normalizedDate = $"{y}-{m}-{d}";
            }

            if (string.IsNullOrEmpty(normalizedDate))
                return null;

            // 뒤 코드가 있으면 같이 붙임
            if (!string.IsNullOrEmpty(suffixPart))
            {
                suffixPart = Regex.Replace(suffixPart, @"[^A-Z0-9 ]", "");
                suffixPart = Regex.Replace(suffixPart, @"\s+", " ").Trim();

                if (!string.IsNullOrEmpty(suffixPart))
                    return $"{normalizedDate} {suffixPart}";
            }

            return normalizedDate;
        }

        /// <summary>
        /// 디버그 이미지 저장
        /// </summary>
        private void SaveDebug(Bitmap img, string name)
        {
            try
            {
                System.IO.Directory.CreateDirectory("DebugImages");
                img.Save($"DebugImages/{name}.png");
            }
            catch
            {
            }
        }
    }
}