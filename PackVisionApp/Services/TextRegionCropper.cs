using System;
using System.Collections.Generic;
using System.Text;

namespace PackVisionApp.Service
{
	/*
	 * TextRegionCropper
	 * 
	 * 역할:
	 * - 원본 이미지에서 지정된 ROI 영역을 잘라 Bitmap으로 반환
	 * 
	 * 사용 목적:
	 * - 바코드, 날짜, 숫자 등 특정 영역만 분리하여 후속 처리(OCR, 검사)에 활용
	 * - 불필요한 영역 제거로 처리 속도 및 정확도 향상
	 * 
	 * 동작 방식:
	 * - 입력 ROI를 이미지 범위와 교집합 처리하여 유효 영역 계산
	 * - Graphics.DrawImage를 이용해 해당 영역만 잘라 새로운 Bitmap 생성
	 * 
	 * 특징:
	 * - ROI가 이미지 밖을 벗어나지 않도록 안전하게 보정
	 * - 잘못된 ROI 입력 시 예외 처리로 안정성 확보
	 * - 범용적으로 사용 가능한 공통 유틸 클래스
	 * 
	 * 출력:
	 * - ROI 영역이 잘린 Bitmap 반환
	 */
	public static class TextRegionCropper
	{
		public static Bitmap Crop(Bitmap sourceBitmap, Rectangle roi)
		{
			if (sourceBitmap == null)
				throw new ArgumentNullException(nameof(sourceBitmap));

			if (roi.Width <= 0 || roi.Height <= 0)
				throw new ArgumentException("ROI width and height must be greater than 0.");

			Rectangle imageBounds = new Rectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height);
			Rectangle validRoi = Rectangle.Intersect(imageBounds, roi);

			if (validRoi.Width <= 0 || validRoi.Height <= 0)
				throw new ArgumentException("ROI is outside the image bounds.");

			Bitmap croppedBitmap = new Bitmap(validRoi.Width, validRoi.Height);

			using (Graphics g = Graphics.FromImage(croppedBitmap))
			{
				g.DrawImage(
					sourceBitmap,
					new Rectangle(0, 0, validRoi.Width, validRoi.Height),
					validRoi,
					GraphicsUnit.Pixel);
			}

			return croppedBitmap;
		}
	}
}
