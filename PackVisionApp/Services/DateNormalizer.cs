using System.Text.RegularExpressions;

namespace PackVisionApp.Services
	{/*
	 * DateNormalizer
	 * 
	 * 역할:
	 * - 날짜 문자열을 비교 가능하도록 정규화(Normalize) 처리
	 * 
	 * 사용 목적:
	 * - OCR 결과와 기준 날짜를 형식 차이 없이 정확하게 비교
	 * - ".", "-", 공백 등 다양한 구분자 문제 해결
	 * 
	 * 동작 방식:
	 * - 입력 문자열에서 구분자(. - 공백)를 제거
	 * - 숫자만 남겨 동일한 형식으로 변환
	 * 
	 * 특징:
	 * - "27.01.27", "27-01-27", "27 01 27" → 모두 "270127"로 통일
	 * - OCR 결과의 포맷 불일치 문제 해결
	 * - 간단하고 빠른 문자열 처리
	 * 
	 * 주요 기능:
	 * - Normalize(): 날짜 문자열 정규화
	 * - IsMatch(): 정규화 후 두 날짜 문자열 비교
	 * 
	 * 출력:
	 * - Normalize: 숫자만 남은 날짜 문자열 반환
	 * - IsMatch: 두 날짜가 동일하면 true, 아니면 false
	 */	
	public static class DateNormalizer
	{
		/// <summary>
		/// 날짜 문자열에서 구분자(. - 공백)를 제거하고 숫자만 추출
		/// "27.01.27" / "27-01-27" / "27 01 27" → "270127"
		/// </summary>
		public static string Normalize(string dateStr)
		{
			if (string.IsNullOrWhiteSpace(dateStr))
				return "";

			// 점, 하이픈, 공백 제거 후 숫자만 남김
			return Regex.Replace(dateStr.Trim(), @"[.\-\s]", "");
		}

		public static bool IsMatch(string readDate, string expectedDate)
		{
			return Normalize(readDate) == Normalize(expectedDate);
		}
	}
}