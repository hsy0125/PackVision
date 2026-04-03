using System;
using System.Drawing;

namespace PackVisionApp.Services
{
	/*
	 * BarcodeNumberRegionDetector
	 * 
	 * 역할:
	 * - 바코드 라벨 이미지에서 숫자 영역(하단 숫자 줄)을 추출하기 위한 ROI 계산
	 * 
	 * 사용 목적:
	 * - 바코드 아래 숫자(코드 값)만 OCR 처리하기 위해 영역을 제한
	 * - 불필요한 바코드 패턴(검은 줄 영역)을 제외하고 정확도 향상
	 * 
	 * 동작 방식:
	 * - 라벨 이미지 기준 비율(%)로 숫자 영역 위치를 고정 계산
	 * - 하단 약 70% 위치부터 시작하여 얇은 가로 영역을 ROI로 설정
	 * 
	 * 특징:
	 * - 이미지 크기가 달라도 비율 기반으로 안정적인 영역 추출 가능
	 * - 별도의 영상 처리 없이 빠르게 ROI 계산 (경량 처리)
	 * - ROI가 이미지 범위를 벗어나지 않도록 자동 보정
	 * 
	 * 출력:
	 * - 숫자 영역 Rectangle 반환
	 */

	public static class BarcodeNumberRegionDetector
	{
		public static Rectangle GetNumberRegion(Bitmap barcodeLabelBitmap)
		{
			if (barcodeLabelBitmap == null)
				throw new ArgumentNullException(nameof(barcodeLabelBitmap));

			int width = barcodeLabelBitmap.Width;
			int height = barcodeLabelBitmap.Height;

			// 숫자 줄만 보이도록 더 타이트하게 조정
			int x = (int)(width * 0.04);
			int y = (int)(height * 0.7);
			int w = (int)(width * 0.70);
			int h = (int)(height * 0.14);

			Rectangle roi = new Rectangle(x, y, w, h);
			Rectangle bounds = new Rectangle(0, 0, width, height);

			return Rectangle.Intersect(bounds, roi);
		}
	}
}