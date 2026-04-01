using System;
using System.Drawing;

namespace PackVisionApp.Services
{
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