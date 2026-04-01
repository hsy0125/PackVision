using System.Drawing;
using System.Linq;
using PackVisionApp.Models;
using PackVisionApp.Vision;

namespace PackVisionApp.Managers
{
    public class InspectionManager
    {
        private readonly BarcodeReader _barcodeReader;
        private readonly DateReader _dateReader;
        private readonly RoiMapper _roiMapper;

        public RectangleF DateRatioRect { get; private set; } = RectangleF.Empty;
        public RectangleF BarcodeRatioRect { get; private set; } = RectangleF.Empty;

        public InspectionManager()
        {
            _barcodeReader = new BarcodeReader();
            _dateReader = new DateReader();
            _roiMapper = new RoiMapper();
        }

        // 처음 티칭한 package/date/barcode 기준으로
        // 상대 비율을 저장
        public void SetRoiRatios(Rectangle packageRect, Rectangle dateRect, Rectangle barcodeRect)
        {
            if (packageRect == Rectangle.Empty ||
                dateRect == Rectangle.Empty ||
                barcodeRect == Rectangle.Empty)
                return;

            DateRatioRect = _roiMapper.RectToRatio(dateRect, packageRect);
            BarcodeRatioRect = _roiMapper.RectToRatio(barcodeRect, packageRect);
        }

        public Rectangle GetDateRect(Rectangle packageRect)
        {
            if (packageRect == Rectangle.Empty || DateRatioRect == RectangleF.Empty)
                return Rectangle.Empty;

            return _roiMapper.RatioToRect(DateRatioRect, packageRect);
        }

        public Rectangle GetBarcodeRect(Rectangle packageRect)
        {
            if (packageRect == Rectangle.Empty || BarcodeRatioRect == RectangleF.Empty)
                return Rectangle.Empty;

            return _roiMapper.RatioToRect(BarcodeRatioRect, packageRect);
        }

        public InspectionResult Inspect(
            Bitmap frame,
            Rectangle packageRect,
            string expectedBarcode,
            string expectedDate)
        {
            if (frame == null || packageRect == Rectangle.Empty)
            {
                return BuildResult(
                    expectedBarcode,
                    "",
                    expectedDate,
                    "",
                    false,
                    false);
            }

            Rectangle dateRect = GetDateRect(packageRect);
            Rectangle barcodeRect = GetBarcodeRect(packageRect);

            BarcodeResult barcodeResult = _barcodeReader.ReadBarcode(frame, barcodeRect);
            DateResult dateResult = _dateReader.ReadDate(frame, dateRect);

            string actualBarcode = barcodeResult.Success ? barcodeResult.Value : string.Empty;
            string actualDate = dateResult.Success ? dateResult.Value : string.Empty;

            bool isBarcodeOk = barcodeResult.Success &&
                               NormalizeBarcode(expectedBarcode) == NormalizeBarcode(actualBarcode);

            bool isDateOk = dateResult.Success &&
                            NormalizeDate(expectedDate) == NormalizeDate(actualDate);

            return BuildResult(
                expectedBarcode,
                actualBarcode,
                expectedDate,
                actualDate,
                isBarcodeOk,
                isDateOk);
        }

        private InspectionResult BuildResult(
            string expectedBarcode,
            string actualBarcode,
            string expectedDate,
            string actualDate,
            bool isBarcodeOk,
            bool isDateOk)
        {
            InspectionResult result = new InspectionResult
            {
                ExpectedBarcode = expectedBarcode ?? string.Empty,
                ActualBarcode = actualBarcode ?? string.Empty,
                ExpectedDate = NormalizeDate(expectedDate),
                ActualDate = NormalizeDate(actualDate),
                IsBarcodeOk = isBarcodeOk,
                IsDateOk = isDateOk
            };

            if (!isBarcodeOk)
                result.AddFailReason("B");

            if (!isDateOk)
                result.AddFailReason("D");

            result.UpdateOverallResult();
            return result;
        }

        private string NormalizeBarcode(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            return new string(raw.Where(char.IsLetterOrDigit).ToArray());
        }

        private string NormalizeDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            string digits = new string(raw.Where(char.IsDigit).ToArray());

            if (digits.Length == 8)
                return $"{digits.Substring(0, 4)}-{digits.Substring(4, 2)}-{digits.Substring(6, 2)}";

            if (digits.Length == 6)
                return $"20{digits.Substring(0, 2)}-{digits.Substring(2, 2)}-{digits.Substring(4, 2)}";

            return raw.Trim();
        }
    }
}