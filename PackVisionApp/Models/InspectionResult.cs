using System;
using System.Collections.Generic;

namespace PackVisionApp.Models
{
	/*
	 * InspectionResult
	 * 
	 * 역할: 결과 데이터 클래스
	 * - 한 번의 검사 결과를 저장하는 데이터 클래스
	 * - 바코드, 날짜, 인쇄 상태의 개별 판정 결과를 보관
	 * - 실패 원인(FailReason)을 리스트 형태로 관리
	 * - 최종 OK/NOK 결과를 포함하여 UI와 로그에서 활용
	 * 
	 * 목적:
	 * - 검사 결과를 하나의 객체로 통합하여 전달 및 관리
	 * - UI 표시, 로그 저장, 통계 계산에 공통적으로 사용
	 */
	public class InspectionResult
	{
		// 검사 시간
		public DateTime InspectTime { get; set; } = DateTime.Now;

		// 개별 검사 결과
		public bool IsBarcodeOk { get; set; }
		public bool IsDateOk { get; set; }
		public bool IsPrintOk { get; set; }

		// 최종 판정
		public bool IsOverallOk { get; set; }

		// 기대값 / 실제값
		public string ExpectedBarcode { get; set; } = string.Empty;
		public string ActualBarcode { get; set; } = string.Empty;

		public string ExpectedDate { get; set; } = string.Empty;
		public string ActualDate { get; set; } = string.Empty;

		// 실패 사유 목록
		public List<string> FailReasons { get; set; } = new List<string>();

		// 화면 표시용 문자열
		public string ResultText
		{
			get
			{
				return IsOverallOk ? "OK" : "NOK";
			}
		}

		public string FailReasonText
		{
			get
			{
				if (FailReasons == null || FailReasons.Count == 0)
					return "NONE";

				return string.Join(", ", FailReasons);
			}
		}

		// 최종 판정 계산
		public void UpdateOverallResult()
		{
			IsOverallOk = IsBarcodeOk && IsDateOk && IsPrintOk;
		}
	}
}