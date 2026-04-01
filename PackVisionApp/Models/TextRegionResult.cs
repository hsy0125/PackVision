using System;
using System.Collections.Generic;
using System.Text;

namespace PackVisionApp.Models
{
	// 텍스트 영역 검사 결과 모델
	// ROI 영역에서 읽은 문자열과 기준 문자열 비교 결과를 담는 모델
	// 선준님이 넘겨준 결과 저장
	public class TextRegionResult
	{
		public string ItemType { get; set; } = string.Empty;      // "BARCODE", "DATE"
		public string ReadValue { get; set; } = string.Empty;     // 실제 읽은 문자열
		public string ExpectedValue { get; set; } = string.Empty; // 기준 문자열
		public Rectangle Roi { get; set; } = Rectangle.Empty;     // 문자열 전체 ROI

		public bool IsOk
		{
			get { return ReadValue == ExpectedValue; }
		}
	}
}
