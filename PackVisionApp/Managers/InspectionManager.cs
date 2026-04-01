using System;
using System.Drawing;
using System.Diagnostics;
using PackVisionApp.Models;

namespace PackVisionApp.Managers
{
    /*
     * InspectionManager — 통합 버전
     * 
     * 민영씨: RunInspection(frame, dateRect, barcodeRect) 호출
     * 소영씨: Inspect(expectedBarcode, actualBarcode, ...) 호출
     * 두 기능 모두 여기서 처리
     */
    public class InspectionManager
    {
        // ─────────────────────────────────────
        // 민영씨가 사용하는 함수
        // 매 프레임마다 frame + ROI 좌표 받기
        // ─────────────────────────────────────
        public void RunInspection(Bitmap frame, Rectangle dateRect, Rectangle barcodeRect)
        {
            if (frame == null) return;

            Debug.WriteLine($"[InspectionManager] 날짜ROI: X={dateRect.X}, Y={dateRect.Y} | 바코드ROI: X={barcodeRect.X}, Y={barcodeRect.Y}");

            // 선준씨가 여기에 바코드/날짜 인식 추가 예정
        }

        // ─────────────────────────────────────
        // 소영씨가 사용하는 함수
        // 기준값과 실제값 비교해서 OK/NOK 판정
        // ─────────────────────────────────────
        public InspectionResult Inspect(
            string expectedBarcode,
            string actualBarcode,
            string expectedDate,
            string actualDate,
            bool isPrintOk)
        {
            InspectionResult result = new InspectionResult();

            // 기준값 / 실제값 저장
            result.ExpectedBarcode = expectedBarcode ?? string.Empty;
            result.ActualBarcode = actualBarcode ?? string.Empty;
            result.ExpectedDate = expectedDate ?? string.Empty;
            result.ActualDate = actualDate ?? string.Empty;

            // 개별 판정
            result.IsBarcodeOk = result.ExpectedBarcode == result.ActualBarcode;
            result.IsDateOk = result.ExpectedDate == result.ActualDate;
            result.IsPrintOk = isPrintOk;

            // 실패 사유 추가
            if (!result.IsBarcodeOk)
                result.FailReasons.Add("barcode_mismatch");

            if (!result.IsDateOk)
                result.FailReasons.Add("date_mismatch");

            if (!result.IsPrintOk)
                result.FailReasons.Add("print_fail");

            // 최종 결과 계산
            result.UpdateOverallResult();

            return result;
        }
    }
}
