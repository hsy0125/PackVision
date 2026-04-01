using System;
using System.Drawing;
using PackVisionApp.Models;
using ZXing;
using ZXing.Windows.Compatibility;

namespace PackVisionApp.Vision
{
    public class BarcodeReader
    {
        private readonly BarcodeReader<Bitmap> _reader;

        public BarcodeReader()
        {
            _reader = new BarcodeReader<Bitmap>(
                bitmap => new BitmapLuminanceSource(bitmap)
            );

            _reader.AutoRotate = true;
            _reader.Options.TryInverted = true;
        }

        public BarcodeResult ReadBarcode(Bitmap frame, Rectangle roi)
        {
            if (frame == null || roi == Rectangle.Empty)
                return BarcodeResult.Fail("barcode_decode_fail");

            Rectangle safeRoi = ClampRect(roi, frame.Width, frame.Height);
            if (safeRoi == Rectangle.Empty)
                return BarcodeResult.Fail("barcode_decode_fail");

            try
            {
                Bitmap cropped = frame.Clone(safeRoi, frame.PixelFormat);
                SaveDebug(cropped, "barcode_roi_raw");

                var result = _reader.Decode(cropped);
                if (result != null)
                    return BarcodeResult.Ok(FormatBarcode(result.Text));

                Bitmap gray = ToGrayscale(cropped);
                SaveDebug(gray, "barcode_roi_gray");

                result = _reader.Decode(gray);
                if (result != null)
                    return BarcodeResult.Ok(FormatBarcode(result.Text));

                Bitmap resized = new Bitmap(cropped, new Size(cropped.Width * 2, cropped.Height * 2));
                SaveDebug(resized, "barcode_roi_resized");

                result = _reader.Decode(resized);
                if (result != null)
                    return BarcodeResult.Ok(FormatBarcode(result.Text));

                return BarcodeResult.Fail("barcode_decode_fail");
            }
            catch
            {
                return BarcodeResult.Fail("barcode_decode_fail");
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

        private Bitmap ToGrayscale(Bitmap original)
        {
            Bitmap gray = new Bitmap(original.Width, original.Height);

            for (int y = 0; y < original.Height; y++)
            {
                for (int x = 0; x < original.Width; x++)
                {
                    Color c = original.GetPixel(x, y);
                    int g = (int)(c.R * 0.3 + c.G * 0.59 + c.B * 0.11);
                    gray.SetPixel(x, y, Color.FromArgb(g, g, g));
                }
            }
            return gray;
        }

        private string FormatBarcode(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return raw;

            if (raw.Length == 13)
                return $"{raw.Substring(0, 1)} {raw.Substring(1, 6)} {raw.Substring(7, 6)}";

            return raw;
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