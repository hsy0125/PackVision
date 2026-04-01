using System;
using System.Collections.Generic;
using System.Text;

namespace PackVisionApp.Service
{
	// 원본 이미지에서 문자열 ROI를 잘라냄
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
