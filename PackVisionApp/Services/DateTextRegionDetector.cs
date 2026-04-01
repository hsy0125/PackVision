using System.Drawing;

namespace PackVisionApp.Services
{
	/*
	 * DateTextRegionDetector
	 * 
	 * 역할:
	 * - 검출된 날짜 라벨 영역(labelRect) 내부에서 실제 텍스트 영역 ROI를 생성
	 * 
	 * 사용 목적:
	 * - 라벨 테두리(노이즈)를 제외하고 날짜 텍스트만 OCR 대상으로 추출
	 * - OCR 정확도 향상을 위한 최종 입력 영역 설정
	 * 
	 * 동작 방식:
	 * - 라벨 사각형에서 일정 margin을 내부로 줄여 ROI 생성
	 * - 테두리 및 불필요한 여백을 제거
	 * 
	 * 특징:
	 * - 매우 가벼운 연산 (단순 좌표 계산)
	 * - 라벨 전체를 기반으로 안정적인 텍스트 영역 확보
	 * - margin 조정으로 OCR 성능 튜닝 가능
	 * 
	 * 출력:
	 * - 날짜 텍스트 영역 Rectangle 반환
	 */
	public static class DateTextRegionDetector
	{

		public static Rectangle GetDateTextRegion(Rectangle labelRect)
		{

			int margin = 4;

			// 스티커 전체를 날짜 ROI로 사용
			return new Rectangle(
				labelRect.X + margin,
				labelRect.Y + margin,
				labelRect.Width - margin * 2,
				labelRect.Height - margin * 2);
		}
	}
}