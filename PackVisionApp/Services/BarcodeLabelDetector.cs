using System;
using System.Collections.Generic;
using System.Text;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Drawing;

namespace PackVisionApp.Services
{
	/*
	 * BarcodeLabelDetector
	 * 
	 * 역할:
	 * - 입력 이미지에서 흰색 라벨(바코드 영역)을 자동으로 검출
	 * - Threshold + Morphology + Contour 기반으로 라벨 후보 영역 추출
	 * 
	 * 사용 목적:
	 * - 바코드 / 날짜 검사 전에 ROI 자동 설정
	 * - 수동 ROI 없이도 검사 위치를 안정적으로 확보
	 * 
	 * 특징:
	 * - searchRoi 기반으로 탐색 범위를 제한하여 성능 최적화
	 * - 가장 큰 흰색 영역을 라벨로 판단
	 * - 잘못된 ROI 입력 시 전체 이미지 fallback 처리
	 * 
	 * 출력:
	 * - 원본 이미지 기준 Rectangle (라벨 위치)
	 * - 검출 실패 시 Rectangle.Empty 반환
	 */
	public static class BarcodeLabelDetector
	{
		public static Rectangle FindWhiteLabelRect(Bitmap sourceBitmap, Rectangle searchRoi)
		{
			if (sourceBitmap == null)
				throw new ArgumentNullException(nameof(sourceBitmap));

			Rectangle imageBounds = new Rectangle(0, 0, sourceBitmap.Width, sourceBitmap.Height);
			Rectangle validRoi = Rectangle.Intersect(imageBounds, searchRoi);
			if (validRoi.Width <= 0 || validRoi.Height <= 0)
			{
				// fallback: 전체 이미지 사용
				validRoi = imageBounds;
			}

			using (Bitmap croppedBitmap = new Bitmap(validRoi.Width, validRoi.Height))
			using (Graphics g = Graphics.FromImage(croppedBitmap))
			{
				g.DrawImage(
					sourceBitmap,
					new Rectangle(0, 0, validRoi.Width, validRoi.Height),
					validRoi,
					GraphicsUnit.Pixel);

				using (Mat src = BitmapConverter.ToMat(croppedBitmap))
				using (Mat gray = new Mat())
				using (Mat binary = new Mat())
				{
					Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

					// 흰색 라벨 검출용 threshold
					Cv2.Threshold(gray, binary, 140, 255, ThresholdTypes.Binary);

					// 작은 잡음 제거 / 흰색 영역 정리
					Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(5, 5));
					Cv2.MorphologyEx(binary, binary, MorphTypes.Close, kernel);

					OpenCvSharp.Point[][] contours;
					HierarchyIndex[] hierarchy;

					Cv2.FindContours(
						binary,
						out contours,
						out hierarchy,
						RetrievalModes.External,
						ContourApproximationModes.ApproxSimple);

					if (contours == null || contours.Length == 0)
						return Rectangle.Empty;

					// 흰색 라벨 후보 중 가장 적절한 사각형 선택
					var candidates = contours
						.Select(c => Cv2.BoundingRect(c))
						.Where(r => r.Width > 150 && r.Height > 50) // 너무 작은 노이즈 제거
						.OrderByDescending(r => r.Width * r.Height)
						.ToList();

					if (candidates.Count == 0)
						return Rectangle.Empty;

					OpenCvSharp.Rect best = candidates[0];

					// searchRoi 기준 좌표를 원본 이미지 기준으로 복원
					return new Rectangle(
						validRoi.X + best.X,
						validRoi.Y + best.Y,
						best.Width,
						best.Height);
				}
			}
		}
	}
}
