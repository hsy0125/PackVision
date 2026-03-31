using System;
using System.Drawing;
using System.Diagnostics;

namespace PackVisionApp.Managers
{
    public class InspectionManager
    {
        public void RunInspection(Bitmap frame, Rectangle dateRect, Rectangle barcodeRect)
        {
            if (frame == null) return;

            Debug.WriteLine($"[InspectionManager] 날짜ROI: X={dateRect.X}, Y={dateRect.Y} | 바코드ROI: X={barcodeRect.X}, Y={barcodeRect.Y}");

            // 소영씨가 여기에 ROI 계산 추가 예정
            // 선준씨가 여기에 바코드/날짜 인식 추가 예정
        }
    }
}
