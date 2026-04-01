using System.Drawing;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace PackVisionApp.Services
{
	/*
	 * DateLabelDetector
	 * 
	 * 역할:
	 * - 이미지에서 날짜 라벨(흰색 스티커 영역)을 자동으로 검출
	 * 
	 * 사용 목적:
	 * - 날짜 OCR 수행 전, 날짜가 인쇄된 스티커 영역을 정확히 추출
	 * - 바코드와 별개로 날짜 검사 전용 ROI 생성
	 * 
	 * 동작 방식:
	 * - HSV 색공간으로 변환 후 흰색 영역 마스킹
	 * - Dilate를 반복 적용하여 끊긴 영역 연결 및 내부 구멍 제거
	 * - Contour 기반으로 가장 큰 라벨 후보 선택
	 * 
	 * 특징:
	 * - 조명 변화에 강한 HSV 기반 흰색 검출
	 * - 강한 Dilate로 라벨을 하나의 덩어리로 안정화
	 * - 팽창된 영역을 다시 축소하여 실제 라벨 크기로 보정
	 * - searchRoi 기반으로 탐색 범위 제한 → 성능 최적화
	 * 
	 * 출력:
	 * - 원본 이미지 기준 날짜 라벨 영역 Rectangle 반환
	 * - 검출 실패 시 Rectangle.Empty 반환
	 */
	public static class DateLabelDetector
	{
		public static Rectangle FindDateLabelRect(Bitmap source, Rectangle searchRoi)
		{
			using (Mat full = BitmapConverter.ToMat(source))
			using (Mat roiMat = new Mat(full, new Rect(
									searchRoi.X, searchRoi.Y,
									searchRoi.Width, searchRoi.Height)))
			using (Mat hsv = new Mat())
			using (Mat mask = new Mat())
			using (Mat kernel = Cv2.GetStructuringElement(
									MorphShapes.Rect, new OpenCvSharp.Size(20, 20)))
			{
				Cv2.CvtColor(roiMat, hsv, ColorConversionCodes.BGR2HSV);

				// 흰색 마스크
				Cv2.InRange(hsv,
					new Scalar(0, 0, 120),
					new Scalar(180, 90, 255),
					mask);

				// ── Dilate만 강하게 반복
				// 흰색 픽셀이 사방으로 팽창하면서 끊긴 부분이 연결되고
				// 내부 구멍도 자연스럽게 메워짐
				Cv2.Dilate(mask, mask, kernel,
						   new OpenCvSharp.Point(-1, -1), iterations: 10);

				// Contour → 가장 큰 bounding rect
				OpenCvSharp.Point[][] contours;
				HierarchyIndex[] hierarchy;
				Cv2.FindContours(mask, out contours, out hierarchy,
					RetrievalModes.External, ContourApproximationModes.ApproxSimple);

				if (contours.Length == 0)
					return Rectangle.Empty;

				Rect best = new Rect();
				double bestArea = 0;

				foreach (var contour in contours)
				{
					Rect r = Cv2.BoundingRect(contour);
					double area = r.Width * r.Height;

					if (r.Height < searchRoi.Height * 0.30) continue;

					if (area > bestArea)
					{
						bestArea = area;
						best = r;
					}
				}

				if (bestArea == 0)
					return Rectangle.Empty;

				// Dilate로 팽창된 만큼 다시 축소해서 원래 스티커 크기로 복원
				int shrink = 20 * 10 / 2; // kernel크기 * iterations / 2 (대략)
				return new Rectangle(
					searchRoi.X + Math.Max(0, best.X + shrink),
					searchRoi.Y + Math.Max(0, best.Y + shrink),
					Math.Max(10, best.Width - shrink * 2),
					Math.Max(10, best.Height - shrink * 2));
			}
		}

		///// <summary>
		///// 투영 배열에서 임계값 이상인 값이 가장 길게 연속된 구간의 start/end 반환
		///// </summary>
		//private static void FindLongestRun(
		//	int[] projection, int threshold,
		//	out int start, out int end)
		//{
		//	start = -1;
		//	end = -1;

		//	int bestLen = 0;
		//	int bestStart = -1;
		//	int curStart = -1;

		//	for (int i = 0; i < projection.Length; i++)
		//	{
		//		if (projection[i] >= threshold)
		//		{
		//			if (curStart < 0) curStart = i;
		//			int len = i - curStart + 1;
		//			if (len > bestLen)
		//			{
		//				bestLen = len;
		//				bestStart = curStart;
		//				end = i;
		//			}
		//		}
		//		else
		//		{
		//			curStart = -1;
		//		}
		//	}

		//	start = bestStart;
		//}
	}
}