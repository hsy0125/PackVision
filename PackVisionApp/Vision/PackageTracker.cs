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
        private TrackerCSRT _packageTracker;
        private Rectangle _packageRect;
        private int _initWidth;  // 처음 드래그한 가로 크기 기억
        private int _initHeight; // 처음 드래그한 세로 크기 기억

        private RectangleF _dateRatio;
        private RectangleF _barcodeRatio;

        private readonly object _lock = new object();

        public bool IsTracking { get; private set; } = false;
        public bool IsDateRoiSet { get; private set; } = false;
        public bool IsBarcodeRoiSet { get; private set; } = false;

        public Rectangle GetPackageRect()
        {
            lock (_lock) { return _packageRect; }
        }

        public Rectangle GetDateRect()
        {
            lock (_lock)
            {
                if (!IsTracking || !IsDateRoiSet) return Rectangle.Empty;
                return RatioToRect(_dateRatio, _packageRect);
            }
        }

        public Rectangle GetBarcodeRect()
        {
            lock (_lock)
            {
                if (!IsTracking || !IsBarcodeRoiSet) return Rectangle.Empty;
                return RatioToRect(_barcodeRatio, _packageRect);
            }
        }

        private Rectangle RatioToRect(RectangleF ratio, Rectangle packageRect)
        {
            int x = packageRect.X + (int)(packageRect.Width * ratio.X);
            int y = packageRect.Y + (int)(packageRect.Height * ratio.Y);
            int w = (int)(packageRect.Width * ratio.Width);
            int h = (int)(packageRect.Height * ratio.Height);
            return new Rectangle(x, y, w, h);
        }

        private RectangleF RectToRatio(Rectangle rect, Rectangle packageRect)
        {
            float x = (float)(rect.X - packageRect.X) / packageRect.Width;
            float y = (float)(rect.Y - packageRect.Y) / packageRect.Height;
            float w = (float)rect.Width / packageRect.Width;
            float h = (float)rect.Height / packageRect.Height;
            return new RectangleF(x, y, w, h);
        }

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

                // [핵심] 처음 크기 기억해두기
                _initWidth = rect.Width;
                _initHeight = rect.Height;

                IsTracking = true;
                Console.WriteLine($"포장지 추적 시작: X={rect.X}, Y={rect.Y}");
            }
        }

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
                    // [핵심] 크기가 처음 크기의 50%~150% 범위 벗어나면 무시
                    bool sizeOk =
                        newRect.Width > _initWidth * 0.5 &&
                        newRect.Width < _initWidth * 1.5 &&
                        newRect.Height > _initHeight * 0.5 &&
                        newRect.Height < _initHeight * 1.5;

                    if (sizeOk)
                    {
                        lock (_lock)
                        {
                            // 위치 보정 (0.5:0.5 — 빠르면서 안정적)
                            int smoothX = (int)(_packageRect.X * 0.5 + newRect.X * 0.5);
                            int smoothY = (int)(_packageRect.Y * 0.5 + newRect.Y * 0.5);

                            // 크기는 처음 드래그한 크기로 고정
                            _packageRect = new Rectangle(
                                smoothX, smoothY,
                                _initWidth,
                                _initHeight);
                        }
                    }
                    // sizeOk 아니면 마지막 위치 유지
                }
                else
                {
                    Console.WriteLine("추적 실패: 마지막 위치 유지 중...");
                }
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                IsTracking = false;
                _packageTracker?.Dispose();
                _packageTracker = null;
                _packageRect = Rectangle.Empty;
                _initWidth = 0;
                _initHeight = 0;

                IsDateRoiSet = false;
                _dateRatio = RectangleF.Empty;

                IsBarcodeRoiSet = false;
                _barcodeRatio = RectangleF.Empty;

                Console.WriteLine("트래커 초기화 완료");
            }
        }
    }
}