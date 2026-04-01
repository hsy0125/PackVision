using System.Drawing;

namespace PackVisionApp.Vision
{

    //ROI맵퍼 추가
    public class RoiMapper
    {
        public RectangleF RectToRatio(Rectangle rect, Rectangle parentRect)
        {
            if (parentRect.Width <= 0 || parentRect.Height <= 0)
                return RectangleF.Empty;

            float x = (float)(rect.X - parentRect.X) / parentRect.Width;
            float y = (float)(rect.Y - parentRect.Y) / parentRect.Height;
            float w = (float)rect.Width / parentRect.Width;
            float h = (float)rect.Height / parentRect.Height;

            return new RectangleF(x, y, w, h);
        }

        public Rectangle RatioToRect(RectangleF ratio, Rectangle parentRect)
        {
            if (ratio == RectangleF.Empty || parentRect == Rectangle.Empty)
                return Rectangle.Empty;

            int x = parentRect.X + (int)(parentRect.Width * ratio.X);
            int y = parentRect.Y + (int)(parentRect.Height * ratio.Y);
            int w = (int)(parentRect.Width * ratio.Width);
            int h = (int)(parentRect.Height * ratio.Height);

            return new Rectangle(x, y, w, h);
        }
    }
}