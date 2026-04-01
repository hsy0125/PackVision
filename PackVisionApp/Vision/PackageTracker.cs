using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenCvSharp.Tracking;
using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace PackVisionApp.Vision
{
    public class PackageTracker
    {
        // 포장지 전체 추적용 트래커 (초록 박스)
        private TrackerCSRT _packageTracker;
        private Rectangle _packageRect;

        // 날짜/바코드 비율 저장
        // RectangleF = 소수점 있는 사각형 (비율 저장용, 팀 규칙!)
        private RectangleF _dateRatio;    // 날짜 비율
        private RectangleF _barcodeRatio; // 바코드 비율

        private readonly object _lock = new object();

        public bool IsTracking { get; private set; } = false;
        public bool IsDateRoiSet { get; private set; } = false;
        public bool IsBarcodeRoiSet { get; private set; } = false;

        // 포장지 위치 반환
        public Rectangle GetPackageRect()
        {
            lock (_lock) { return _packageRect; }
        }

        // 날짜 실제 좌표 반환 (비율 → 실제 좌표 변환)
        public Rectangle GetDateRect()
        {
            lock (_lock)
            {
                if (!IsTracking || !IsDateRoiSet) return Rectangle.Empty;
                return RatioToRect(_dateRatio, _packageRect);
            }
        }

        // 바코드 실제 좌표 반환 (비율 → 실제 좌표 변환)
        public Rectangle GetBarcodeRect()
        {
            lock (_lock)
            {
                if (!IsTracking || !IsBarcodeRoiSet) return Rectangle.Empty;
                return RatioToRect(_barcodeRatio, _packageRect);
            }
        }

        // 비율 → 실제 좌표 변환 함수
        // 포장지 박스 안에서 비율로 저장된 좌표를 실제 픽셀 좌표로 변환
        private Rectangle RatioToRect(RectangleF ratio, Rectangle packageRect)
        {
            int x = packageRect.X + (int)(packageRect.Width * ratio.X);
            int y = packageRect.Y + (int)(packageRect.Height * ratio.Y);
            int w = (int)(packageRect.Width * ratio.Width);
            int h = (int)(packageRect.Height * ratio.Height);
            return new Rectangle(x, y, w, h);
        }

        // 실제 좌표 → 비율 변환 함수
        // 드래그한 좌표를 포장지 기준 비율로 저장
        private RectangleF RectToRatio(Rectangle rect, Rectangle packageRect)
        {
            float x = (float)(rect.X - packageRect.X) / packageRect.Width;
            float y = (float)(rect.Y - packageRect.Y) / packageRect.Height;
            float w = (float)rect.Width / packageRect.Width;
            float h = (float)rect.Height / packageRect.Height;
            return new RectangleF(x, y, w, h);
        }

        // Bitmap → BGR Mat 안전 변환
        private Mat ToColorMat(Bitmap bmp)
        {
            Bitmap converted;
            if (bmp.PixelFormat == PixelFormat.Format8bppIndexed)
            {
                converted = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(converted))
                    g.DrawImage(bmp, 0, 0);
            }
            else
            {
                converted = bmp;
            }

            Mat mat = converted.ToMat();
            if (converted != bmp) converted.Dispose();
            return mat;
        }

        // 포장지 전체 영역 지정 (초록 박스)
        public void SetTarget(Bitmap currentFrame, Rectangle rect)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return;
            if (currentFrame == null || currentFrame.Width <= 0) return;

            lock (_lock)
            {
                _packageTracker?.Dispose();
                _packageTracker = TrackerCSRT.Create();

                using (Mat colorImage = ToColorMat(currentFrame))
                {
                    if (colorImage.Empty()) return;
                    Rect initRect = new Rect(rect.X, rect.Y, rect.Width, rect.Height);
                    _packageTracker.Init(colorImage, initRect);
                }

                _packageRect = rect;
                IsTracking = true;
                Console.WriteLine($"포장지 추적 시작: X={rect.X}, Y={rect.Y}");
            }
        }

        // 날짜 ROI 지정 — 포장지 기준 비율로 저장
        public void SetDateRoi(Rectangle dateRect)
        {
            if (!IsTracking) return;
            lock (_lock)
            {
                _dateRatio = RectToRatio(dateRect, _packageRect);
                IsDateRoiSet = true;
                Console.WriteLine($"날짜 비율 저장: X={_dateRatio.X:F2}, Y={_dateRatio.Y:F2}");
            }
        }

        // 바코드 ROI 지정 — 포장지 기준 비율로 저장
        public void SetBarcodeRoi(Rectangle barcodeRect)
        {
            if (!IsTracking) return;
            lock (_lock)
            {
                _barcodeRatio = RectToRatio(barcodeRect, _packageRect);
                IsBarcodeRoiSet = true;
                Console.WriteLine($"바코드 비율 저장: X={_barcodeRatio.X:F2}, Y={_barcodeRatio.Y:F2}");
            }
        }

        // 매 프레임마다 포장지 추적
        public void Track(Bitmap currentFrame)
        {
            if (!IsTracking || _packageTracker == null) return;
            if (currentFrame == null || currentFrame.Width <= 0) return;

            using (Mat colorImage = ToColorMat(currentFrame))
            {
                if (colorImage.Empty()) return;

                Rect newRect = new Rect();
                bool found;

                lock (_lock)
                {
                    if (_packageTracker == null) return;
                    found = _packageTracker.Update(colorImage, ref newRect);
                }

                if (found)
                {
                    lock (_lock)
                    {
                        // [수정] 크기는 트래커 결과 그대로 사용
                        // 위치만 보정 로직 적용 (튀는 현상 방지)
                        int smoothX = (int)(_packageRect.X * 0.7 + newRect.X * 0.3);
                        int smoothY = (int)(_packageRect.Y * 0.7 + newRect.Y * 0.3);

                        // 크기는 보정 없이 그대로 사용
                        int w = newRect.Width;
                        int h = newRect.Height;

                        _packageRect = new Rectangle(smoothX, smoothY, w, h);
                    }
                }
                else
                {
                    Console.WriteLine("추적 실패: 마지막 위치 유지 중...");
                }
            }
        }

        // 초기화
        public void Reset()
        {
            lock (_lock)
            {
                IsTracking = false;
                _packageTracker?.Dispose();
                _packageTracker = null;
                _packageRect = Rectangle.Empty;

                IsDateRoiSet = false;
                _dateRatio = RectangleF.Empty;

                IsBarcodeRoiSet = false;
                _barcodeRatio = RectangleF.Empty;

                Console.WriteLine("트래커 초기화 완료");
            }
        }
    }
}