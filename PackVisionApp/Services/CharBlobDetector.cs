using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace PackVisionApp.Services
{
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