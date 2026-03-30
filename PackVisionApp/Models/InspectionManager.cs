using PackVisionApp.Models;

namespace PackVisionApp.Managers
{
	/*
	 * InspectionManager
	 * 
	 * 역할: 판정 담당 클래스
	 * - 기준값(날짜, 바코드)과 실제 검사값을 비교하여 검사 결과를 생성하는 클래스
	 * - 바코드, 날짜, 인쇄 상태를 각각 판정
	 * - 실패 원인을 분석하여 FailReason 목록에 추가
	 * - 최종 OK/NOK 결과를 계산하여 InspectionResult 객체로 반환
	 * 
	 * 목적:
	 * - UI(MainForm)와 검사 로직을 분리하여 코드 구조를 명확하게 유지
	 * - 검사 기준 변경 시 Manager만 수정하면 되도록 설계
	 */
	public class InspectionManager
	{
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
			{
				result.FailReasons.Add("B"); // Barcode mismatch
			}

			if (!result.IsDateOk)
			{
				result.FailReasons.Add("D"); // Date mismatch
			}

			if (!result.IsPrintOk)
			{
				result.FailReasons.Add("P"); // Print quality issue
			}

			// 최종 결과 계산
			result.UpdateOverallResult();

			return result;
		}
	}
}