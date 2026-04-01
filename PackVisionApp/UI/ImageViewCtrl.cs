using System;
using System.Drawing;
using System.Windows.Forms;

namespace PackVisionApp.UI
{
	public partial class ImageViewCtrl : UserControl
	{
		// 현재 표시할 이미지
		private Bitmap _bitmapImage = null;

		// ROI 드래그 상태
		private bool _isDrawingRoi = false;
		private Point _roiStartPoint = Point.Empty;
		private Rectangle _currentRoi = Rectangle.Empty;

		// 저장된 ROI
		private Rectangle _packageRect = Rectangle.Empty;
		private Rectangle _dateRoi = Rectangle.Empty;
		private Rectangle _barcodeRoi = Rectangle.Empty;

		// 현재 ROI 모드
		private string _roiMode = "";

		public ImageViewCtrl()
		{
			InitializeComponent();

			this.DoubleBuffered = true;

			this.MouseDown += ImageViewCtrl_MouseDown;
			this.MouseMove += ImageViewCtrl_MouseMove;
			this.MouseUp += ImageViewCtrl_MouseUp;
			this.Paint += ImageViewCtrl_Paint;
		}

		/*
         * 역할:
         * - MainForm에서 ROI 종류(package/date/barcode)를 선택하면
         *   현재 ROI 그리기 모드를 설정
         */
		public void SetRoiMode(string roiMode)
		{
			_roiMode = roiMode;
		}

		/*
         * 역할:
         * - MainForm에서 이미지를 넘겨주면 화면에 표시
         */
		public void LoadBitmap(Bitmap bitmap)
		{
			if (_bitmapImage != null)
			{
				_bitmapImage.Dispose();
				_bitmapImage = null;
			}

			if (bitmap != null)
			{
				_bitmapImage = new Bitmap(bitmap);
			}

			Invalidate();
		}

		public Rectangle GetPackageRoi()
		{
			return _packageRect;
		}

		public Rectangle GetDateRoi()
		{
			return _dateRoi;
		}

		public Rectangle GetBarcodeRoi()
		{
			return _barcodeRoi;
		}

		private void ImageViewCtrl_MouseDown(object sender, MouseEventArgs e)
		{
			if (string.IsNullOrEmpty(_roiMode))
				return;

			if (_bitmapImage == null)
				return;

			if (e.Button == MouseButtons.Left)
			{
				_isDrawingRoi = true;
				_roiStartPoint = e.Location;
				_currentRoi = Rectangle.Empty;
			}
		}

		private void ImageViewCtrl_MouseMove(object sender, MouseEventArgs e)
		{
			if (!_isDrawingRoi)
				return;

			_currentRoi = MakeRectangle(_roiStartPoint, e.Location);
			Invalidate();
		}

		private void ImageViewCtrl_MouseUp(object sender, MouseEventArgs e)
		{
			if (!_isDrawingRoi)
				return;

			_isDrawingRoi = false;

			if (_currentRoi.Width < 5 || _currentRoi.Height < 5)
			{
				_currentRoi = Rectangle.Empty;
				Invalidate();
				return;
			}

			if (_roiMode == "PACKAGE")
			{
				_packageRect = _currentRoi;
			}
			else if (_roiMode == "DATE")
			{
				_dateRoi = _currentRoi;
			}
			else if (_roiMode == "BARCODE")
			{
				_barcodeRoi = _currentRoi;
			}

			_currentRoi = Rectangle.Empty;
			Invalidate();
		}

		private void ImageViewCtrl_Paint(object sender, PaintEventArgs e)
		{
			Graphics g = e.Graphics;

			// 이미지 그리기
			if (_bitmapImage != null)
			{
				g.DrawImage(_bitmapImage, 0, 0, this.Width, this.Height);
			}
			else
			{
				g.Clear(Color.Black);
			}

			// package ROI
			if (!_packageRect.IsEmpty)
			{
				using (Pen pen = new Pen(Color.Blue, 2))
				{
					g.DrawRectangle(pen, _packageRect);
				}
			}

			// date ROI
			if (!_dateRoi.IsEmpty)
			{
				using (Pen pen = new Pen(Color.Lime, 2))
				{
					g.DrawRectangle(pen, _dateRoi);
				}
			}

			// barcode ROI
			if (!_barcodeRoi.IsEmpty)
			{
				using (Pen pen = new Pen(Color.Orange, 2))
				{
					g.DrawRectangle(pen, _barcodeRoi);
				}
			}

			// 현재 드래그 중인 ROI
			if (_isDrawingRoi && !_currentRoi.IsEmpty)
			{
				using (Pen pen = new Pen(Color.Red, 2))
				{
					g.DrawRectangle(pen, _currentRoi);
				}
			}
		}

		/*
         * 역할:
         * - 마우스 시작점과 끝점을 기준으로
         *   항상 정상적인 Rectangle 생성
         */
		private Rectangle MakeRectangle(Point start, Point end)
		{
			int x = Math.Min(start.X, end.X);
			int y = Math.Min(start.Y, end.Y);
			int width = Math.Abs(start.X - end.X);
			int height = Math.Abs(start.Y - end.Y);

			return new Rectangle(x, y, width, height);
		}
	}
}