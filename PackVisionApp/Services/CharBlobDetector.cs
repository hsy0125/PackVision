using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace PackVisionApp.Services
{
	/*
	 * CharBlobDetector
	 * 
	 * 역할:
	 * - 숫자 영역 이미지에서 개별 문자(숫자) 단위의 바운딩 박스를 검출
	 * 
	 * 사용 목적:
	 * - OCR 전에 각 숫자를 개별 영역으로 분리
	 * - 숫자 단위 인식 정확도 향상 및 후처리(정렬, 필터링)에 활용
	 * 
	 * 동작 방식:
	 * - Gray 변환 → Otsu 이진화(BinaryInv)로 숫자를 흰색으로 강조
	 * - Morphology Close로 끊어진 숫자 획을 연결
	 * - Contour 기반으로 문자 후보 영역 검출
	 * 
	 * 필터링 기준:
	 * - 너무 작은 영역 제거 (노이즈 제거)
	 * - 너무 큰 영역 제거 (배경/오검출 제거)
	 * - 일정 높이 이상만 유지 (숫자 형태 보장)
	 * 
	 * 특징:
	 * - Contour 기반으로 빠르고 직관적인 문자 분리
	 * - X 좌표 기준 정렬 → 숫자 순서 유지
	 * - 다양한 조명/배경에서도 비교적 안정적으로 동작
	 * 
	 * 출력:
	 * - 좌→우 순서로 정렬된 문자 영역 Rectangle 리스트 반환
	 */
	public static class CharBlobDetector
	{
		public static List<Rectangle> FindCharBoxes(Bitmap sourceBitmap)
		{
			if (sourceBitmap == null)
				throw new ArgumentNullException(nameof(sourceBitmap));

			List<Rectangle> results = new List<Rectangle>();

			using (Mat src = BitmapConverter.ToMat(sourceBitmap))
			using (Mat gray = new Mat())
			using (Mat binary = new Mat())
			{
				Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

				// 숫자를 흰색으로 뒤집어서 이진화
				Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

				// 숫자 획을 조금 붙여줌
				using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3)))
				{
					Cv2.MorphologyEx(binary, binary, MorphTypes.Close, kernel);
				}

				OpenCvSharp.Point[][] contours;
				HierarchyIndex[] hierarchy;

				Cv2.FindContours(
					binary,
					out contours,
					out hierarchy,
					RetrievalModes.External,
					ContourApproximationModes.ApproxSimple);

				if (contours == null || contours.Length == 0)
					return results;

				int imageW = sourceBitmap.Width;
				int imageH = sourceBitmap.Height;

				foreach (var contour in contours)
				{
					OpenCvSharp.Rect rect = Cv2.BoundingRect(contour);

					// 너무 작은 점 제거
					if (rect.Width < 4 || rect.Height < 10)
						continue;

					// 너무 큰 덩어리 제거
					if (rect.Width > imageW * 0.30 || rect.Height > imageH * 0.95)
						continue;

					// 숫자는 보통 세로가 어느 정도 있어야 함
					if (rect.Height < imageH * 0.35)
						continue;

					results.Add(new Rectangle(rect.X, rect.Y, rect.Width, rect.Height));
				}
			}

			return results
				.OrderBy(r => r.X)
				.ToList();
		}
	}
}