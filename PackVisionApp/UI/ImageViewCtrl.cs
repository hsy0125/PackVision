using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace PackVisionApp.UI;

/// <summary>
/// JidamVision ImageViewCtrl과 같이 휠 줌·더블클릭 맞춤 표시를 하는 뷰어.
/// 내부 표시는 GDI+용 Bitmap, 입력은 <see cref="LoadMat"/> 또는 <see cref="LoadBitmap"/> 으로 받습니다.
/// </summary>
public class ImageViewCtrl : UserControl
{
	private Bitmap? _bitmapImage;
	private Bitmap? _canvas;
	private RectangleF _imageRect;
	private float _curZoom = 1f;
	private const float ZoomStep = 1.1f;
	private float _minZoom = 1f;
	private const float MaxZoom = 100f;
	private bool _initialized;

	public ImageViewCtrl()
	{
		DoubleBuffered = true;
		BackColor = Color.White;
		TabStop = true;
		MouseWheel += OnMouseWheel;
		MouseDoubleClick += (_, _) => FitImageToScreen();
		SetStyle(ControlStyles.ResizeRedraw, true);
	}

	/// <summary>이미지 그린 뒤 ROI·오버레이를 그릴 때 사용합니다.</summary>
	public event PaintEventHandler? PaintOverlay;

	public bool HasImage => _bitmapImage != null;

	public int ImagePixelWidth => _bitmapImage?.Width ?? 0;

	public int ImagePixelHeight => _bitmapImage?.Height ?? 0;

	/// <summary>표시용 Bitmap을 넘깁니다. 컨트롤이 소유권을 가지며 이전 이미지는 Dispose 합니다.</summary>
	public void LoadBitmap(Bitmap bitmap)
	{
		if (bitmap == null) return;

		if (InvokeRequired)
		{
			BeginInvoke(new Action<Bitmap>(LoadBitmap), bitmap);
			return;
		}

		if (_bitmapImage != null)
		{
			if (_bitmapImage.Width == bitmap.Width && _bitmapImage.Height == bitmap.Height)
			{
				_bitmapImage.Dispose();
				_bitmapImage = bitmap;
				Invalidate();
				return;
			}

			_bitmapImage.Dispose();
			_bitmapImage = null;
		}

		_bitmapImage = bitmap;

		if (!_initialized)
		{
			_initialized = true;
			EnsureCanvas();
			FitImageToScreen();
		}
		else
		{
			EnsureCanvas();
			FitImageToScreen();
		}

		Invalidate();
	}

	/// <summary>OpenCv Mat으로 프레임을 넘깁니다. 내부에서 Bitmap으로 변환합니다.</summary>
	public void LoadMat(Mat mat)
	{
		if (mat == null || mat.Empty()) return;
		using Mat copy = mat.Clone();
		Bitmap bmp = BitmapConverter.ToBitmap(copy);
		LoadBitmap(bmp);
	}

	public void ClearImage()
	{
		if (InvokeRequired)
		{
			BeginInvoke(ClearImage);
			return;
		}

		_bitmapImage?.Dispose();
		_bitmapImage = null;
		_canvas?.Dispose();
		_canvas = null;
		_initialized = false;
		_imageRect = RectangleF.Empty;
		Invalidate();
	}

	/// <summary>현재 화면에 맞는 Bitmap 복사본 (호출측에서 Dispose).</summary>
	public Bitmap? CloneDisplayBitmap()
	{
		return _bitmapImage == null ? null : (Bitmap)_bitmapImage.Clone();
	}

	public bool TryGetZoomTransform(out float scale, out float offsetX, out float offsetY)
	{
		scale = 1f;
		offsetX = 0f;
		offsetY = 0f;
		if (_bitmapImage == null) return false;
		scale = _curZoom;
		offsetX = _imageRect.X;
		offsetY = _imageRect.Y;
		return true;
	}

	public Rectangle ClientRectToImageRect(Rectangle clientRect)
	{
		if (_bitmapImage == null) return Rectangle.Empty;
		Rectangle r = ScreenToVirtual(clientRect);
		return ClampToImage(r);
	}

	public Rectangle ImageRectToClientRect(Rectangle imageRect)
	{
		if (_bitmapImage == null) return Rectangle.Empty;
		return VirtualToScreen(imageRect);
	}

	public bool TryClientPointToImage(System.Drawing.Point client, out System.Drawing.Point imagePt)
	{
		imagePt = default;
		if (_bitmapImage == null) return false;
		PointF v = ScreenToVirtual(new PointF(client.X, client.Y));
		imagePt = new System.Drawing.Point((int)Math.Round(v.X), (int)Math.Round(v.Y));
		return new Rectangle(0, 0, _bitmapImage.Width, _bitmapImage.Height).Contains(imagePt);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_bitmapImage?.Dispose();
			_canvas?.Dispose();
		}
		base.Dispose(disposing);
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		EnsureCanvas();
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		if (_bitmapImage == null || Width <= 0 || Height <= 0)
		{
			e.Graphics.Clear(BackColor);
			var emptyArgs = new PaintEventArgs(e.Graphics, ClientRectangle);
			PaintOverlay?.Invoke(this, emptyArgs);
			return;
		}

		EnsureCanvas();
		if (_canvas == null) return;

		using (Graphics g = Graphics.FromImage(_canvas))
		{
			g.Clear(BackColor);
			g.InterpolationMode = InterpolationMode.NearestNeighbor;
			g.DrawImage(_bitmapImage, _imageRect);

			var overlayArgs = new PaintEventArgs(g, new Rectangle(0, 0, Width, Height));
			PaintOverlay?.Invoke(this, overlayArgs);
		}

		e.Graphics.DrawImage(_canvas, 0, 0);
	}

	private void EnsureCanvas()
	{
		if (Width <= 0 || Height <= 0) return;
		if (_canvas == null || _canvas.Width != Width || _canvas.Height != Height)
		{
			_canvas?.Dispose();
			_canvas = new Bitmap(Width, Height);
		}
	}

	private void FitImageToScreen()
	{
		if (_bitmapImage == null || Width <= 0 || Height <= 0) return;

		RecalcMinZoom();
		float nw = _bitmapImage.Width * _curZoom;
		float nh = _bitmapImage.Height * _curZoom;
		_imageRect = new RectangleF(
			(Width - nw) / 2f,
			(Height - nh) / 2f,
			nw,
			nh);
		Invalidate();
	}

	private void RecalcMinZoom()
	{
		if (_bitmapImage == null || Width <= 0 || Height <= 0) return;

		float ratioW = (float)Width / _bitmapImage.Width;
		float ratioH = (float)Height / _bitmapImage.Height;
		float fit = Math.Min(ratioW, ratioH);
		_minZoom = fit;
		_curZoom = Math.Max(_minZoom, Math.Min(MaxZoom, fit));
	}

	private void OnMouseWheel(object? sender, MouseEventArgs e)
	{
		if (_bitmapImage == null) return;

		float newZoom = e.Delta < 0 ? _curZoom / ZoomStep : _curZoom * ZoomStep;
		ZoomAroundPoint(newZoom, e.Location);
		if (_bitmapImage != null)
		{
			_imageRect.Width = _bitmapImage.Width * _curZoom;
			_imageRect.Height = _bitmapImage.Height * _curZoom;
		}
		Invalidate();
	}

	private void ZoomAroundPoint(float zoom, System.Drawing.Point zoomOrigin)
	{
		PointF virtualOrigin = ScreenToVirtual(new PointF(zoomOrigin.X, zoomOrigin.Y));
		_curZoom = Math.Max(_minZoom, Math.Min(MaxZoom, zoom));
		if (_curZoom <= _minZoom) return;

		PointF zoomedOrigin = VirtualToScreen(virtualOrigin);
		_imageRect.X -= zoomedOrigin.X - zoomOrigin.X;
		_imageRect.Y -= zoomedOrigin.Y - zoomOrigin.Y;
	}

	private PointF GetScreenOffset() => new PointF(_imageRect.X, _imageRect.Y);

	private Rectangle ScreenToVirtual(Rectangle screenRect)
	{
		PointF offset = GetScreenOffset();
		return new Rectangle(
			(int)((screenRect.X - offset.X) / _curZoom + 0.5f),
			(int)((screenRect.Y - offset.Y) / _curZoom + 0.5f),
			(int)(screenRect.Width / _curZoom + 0.5f),
			(int)(screenRect.Height / _curZoom + 0.5f));
	}

	private Rectangle VirtualToScreen(Rectangle virtualRect)
	{
		PointF offset = GetScreenOffset();
		return new Rectangle(
			(int)(virtualRect.X * _curZoom + offset.X + 0.5f),
			(int)(virtualRect.Y * _curZoom + offset.Y + 0.5f),
			(int)(virtualRect.Width * _curZoom + 0.5f),
			(int)(virtualRect.Height * _curZoom + 0.5f));
	}

	private PointF ScreenToVirtual(PointF screenPos)
	{
		PointF offset = GetScreenOffset();
		return new PointF(
			(screenPos.X - offset.X) / _curZoom,
			(screenPos.Y - offset.Y) / _curZoom);
	}

	private PointF VirtualToScreen(PointF virtualPos)
	{
		PointF offset = GetScreenOffset();
		return new PointF(
			virtualPos.X * _curZoom + offset.X,
			virtualPos.Y * _curZoom + offset.Y);
	}

	private Rectangle ClampToImage(Rectangle r)
	{
		if (_bitmapImage == null) return Rectangle.Empty;
		int x1 = Math.Max(0, Math.Min(r.Left, r.Right));
		int y1 = Math.Max(0, Math.Min(r.Top, r.Bottom));
		int x2 = Math.Min(_bitmapImage.Width, Math.Max(r.Left, r.Right));
		int y2 = Math.Min(_bitmapImage.Height, Math.Max(r.Top, r.Bottom));
		int w = x2 - x1;
		int h = y2 - y1;
		if (w <= 0 || h <= 0) return Rectangle.Empty;
		return new Rectangle(x1, y1, w, h);
	}
}
