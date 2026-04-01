using System.Text.RegularExpressions;
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

            // 값 저장
            result.ExpectedBarcode = expectedBarcode ?? "";
            result.ActualBarcode = actualBarcode ?? "";

            result.ExpectedDate = expectedDate ?? "";
            result.ActualDate = actualDate ?? "";

            // 비교
            result.IsBarcodeOk = IsBarcodeMatched(result.ExpectedBarcode, result.ActualBarcode);
            result.IsDateOk = IsDateMatched(result.ExpectedDate, result.ActualDate);

            // 실패 사유
            if (!result.IsBarcodeOk)
                result.FailReasons.Add("barcode_mismatch");

            if (!result.IsDateOk)
                result.FailReasons.Add("date_mismatch");

            // 최종 판정
            result.UpdateOverallResult();

            return result;
        }

        // 바코드 비교 (공백 제거)
        private bool IsBarcodeMatched(string expected, string actual)
        {
            string e = NormalizeBarcode(expected);
            string a = NormalizeBarcode(actual);

            if (string.IsNullOrEmpty(e) || string.IsNullOrEmpty(a))
                return false;

            return e == a;
        }

        private string NormalizeBarcode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            return Regex.Replace(value, @"\s+", "");
        }

        // 날짜 비교
        private bool IsDateMatched(string expected, string actual)
        {
            string e = NormalizeDate(expected);
            string a = NormalizeDate(actual);

            if (string.IsNullOrEmpty(e) || string.IsNullOrEmpty(a))
                return false;

            return e == a;
        }

        private string NormalizeDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            string digits = Regex.Replace(value, @"[^0-9]", "");

            if (digits.Length == 8)
                return $"{digits.Substring(0, 4)}-{digits.Substring(4, 2)}-{digits.Substring(6, 2)}";

            if (digits.Length == 6)
                return $"20{digits.Substring(0, 2)}-{digits.Substring(2, 2)}-{digits.Substring(4, 2)}";

            return "";
        }
    }
}