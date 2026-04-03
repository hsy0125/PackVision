using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PackVisionApp.Managers;
using PackVisionApp.Models;
using PackVisionApp.Service;
using PackVisionApp.Services;
using PackVisionApp.Vision;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace PackVisionApp.UI
{
	public partial class MainForm : Form
	{
		// ═══════════════════════════════════════
		// 공통 필드
		// ═══════════════════════════════════════
		private string _expectedDate = "";
		private string _expectedBarcode = "";
		private int _totalInspectionCount = 0;
		private int _okInspectionCount = 0;

		// ═══════════════════════════════════════
		// 소영 필드 — 이미지 파일 / 오버레이
		// ═══════════════════════════════════════
		private InspectionManager _inspectionManager = new InspectionManager();
		private CsvLogManager _csvLogManager = new CsvLogManager();
		private Bitmap _originalFrame = null;

		// ═══════════════════════════════════════
		// 민영 필드 — 카메라 / 트래킹
		// ═══════════════════════════════════════
		private InspectionManager _inspectionMgr = new InspectionManager();
		private CameraManager _cameraMgr = new CameraManager();
		private PackageTracker _packageTracker = new PackageTracker();
		private Stopwatch _fpsSw = new Stopwatch();
		private int _isTrackingBusy = 0;

		// 최신 프레임 저장용
		private Bitmap _latestFrame = null;
		private readonly object _frameLock = new object();

		// ═══════════════════════════════════════
		// 실시간 바코드 + 날짜 읽어온 값
		// ═══════════════════════════════════════
		private readonly BarcodeReader _barcodeReader = new BarcodeReader();
		private readonly DateReader _dateReader = new DateReader();

		// ═══════════════════════════════════════
		// 선준 필드 — 자동 검사
		// ═══════════════════════════════════════
		private bool _isAutoInspecting = false;
		private int _isInspectionBusy = 0;
		private DateTime _lastInspectionTime = DateTime.MinValue;
		// 검사 버튼 1회 클릭 후 자동 검사 주기(200ms)
		private readonly TimeSpan _inspectionInterval = TimeSpan.FromMilliseconds(200);

		// ═══════════════════════════════════════
		// ROI 필드
		// ═══════════════════════════════════════
		private System.Drawing.Point _startPoint = System.Drawing.Point.Empty;
		private Rectangle _selectionRect = Rectangle.Empty;
		private bool _isSelecting = false;
		private string _roiMode = "none";

		private Rectangle _packageImageRect = Rectangle.Empty;
		private Rectangle _dateImageRect = Rectangle.Empty;
		private Rectangle _barcodeImageRect = Rectangle.Empty;
		private Rectangle _packageScreenRect = Rectangle.Empty;
		private Rectangle _dateScreenRect = Rectangle.Empty;
		private Rectangle _barcodeScreenRect = Rectangle.Empty;

		// 트래킹으로 인한 ROI 미세 흔들림 완화용 스무딩
		private Rectangle _prevDateImageRect = Rectangle.Empty;
		private Rectangle _prevBarcodeImageRect = Rectangle.Empty;
		private bool _hasPrevTrackedRects = false;

		// 티칭으로 잡아둔 ROI를 고정 (트래킹으로 인해 미세 이동하는 문제 방지)
		private bool _freezeTaughtRois = false;

		// ═══════════════════════════════════════
		// 실시간 오버레이 필드
		// ═══════════════════════════════════════
		private readonly object _overlayLock = new object();

		private Rectangle _liveBarcodeRect = Rectangle.Empty;
		private Rectangle _liveDateRect = Rectangle.Empty;
		private string _liveActualBarcode = string.Empty;
		private string _liveActualDate = string.Empty;
		private bool _liveBarcodeSuccess = false;
		private bool _liveDateSuccess = false;
		private List<Rectangle> _liveBarcodeBlobRects = new List<Rectangle>();
		private List<Rectangle> _liveDateBlobRects = new List<Rectangle>();

		// ═══════════════════════════════════════
		// OCR 값 안정화(프레임간 흔들림 방지)
		// ═══════════════════════════════════════
		private readonly object _ocrStabilityLock = new object();
		private string _stableBarcodeValue = string.Empty;
		private string _stableDateValue = string.Empty;
		private string _stableBarcodeNorm = string.Empty;
		private string _stableDateNorm = string.Empty;

		private string _candidateBarcodeNorm = string.Empty;
		private string _candidateBarcodeValue = string.Empty;
		private int _barcodeCandidateStableFrames = 0;

		private string _candidateDateNorm = string.Empty;
		private string _candidateDateValue = string.Empty;
		private int _dateCandidateStableFrames = 0;

		private const int StableFramesToCommit = 3;

	// OCR 값 안정화 헬퍼
	private string GetBarcodeNorm(string raw)
	{
		return NormalizeBarcodeForOverlay(raw ?? string.Empty);
	}

	private string GetDateNorm(string raw)
	{
		// DateReader가 normalize한 값일 가능성이 높지만,
		// 구분자 오차가 있어도 비교되도록 문자 추출/치환을 사용
		return ExtractDateChars(raw ?? string.Empty);
	}

	private void UpdateStableOcrValues(
		string barcodeCandidateValue,
		string dateCandidateValue,
		out string stableBarcodeOut,
		out string stableDateOut)
	{
		string barcodeCandidateNorm = GetBarcodeNorm(barcodeCandidateValue);
		string dateCandidateNorm = GetDateNorm(dateCandidateValue);

		lock (_ocrStabilityLock)
		{
			// 바코드 안정화
			if (string.IsNullOrWhiteSpace(barcodeCandidateNorm))
			{
				_barcodeCandidateStableFrames = 0;
				_candidateBarcodeNorm = string.Empty;
				_candidateBarcodeValue = string.Empty;
			}
			else if (barcodeCandidateNorm == _stableBarcodeNorm)
			{
				// 이미 stable 값과 같으면 유지
			}
			else
			{
				// 후보 값 카운트 누적
				if (barcodeCandidateNorm != _candidateBarcodeNorm)
				{
					_candidateBarcodeNorm = barcodeCandidateNorm;
					_candidateBarcodeValue = barcodeCandidateValue ?? string.Empty;
					_barcodeCandidateStableFrames = 1;
				}
				else
				{
					_barcodeCandidateStableFrames++;
				}

				if (_barcodeCandidateStableFrames >= StableFramesToCommit)
				{
					_stableBarcodeValue = _candidateBarcodeValue;
					_stableBarcodeNorm = _candidateBarcodeNorm;
					_barcodeCandidateStableFrames = 0;
				}
			}

			// 날짜 안정화
			if (string.IsNullOrWhiteSpace(dateCandidateNorm))
			{
				_dateCandidateStableFrames = 0;
				_candidateDateNorm = string.Empty;
				_candidateDateValue = string.Empty;
			}
			else if (dateCandidateNorm == _stableDateNorm)
			{
				// 이미 stable 값과 같으면 유지
			}
			else
			{
				if (dateCandidateNorm != _candidateDateNorm)
				{
					_candidateDateNorm = dateCandidateNorm;
					_candidateDateValue = dateCandidateValue ?? string.Empty;
					_dateCandidateStableFrames = 1;
				}
				else
				{
					_dateCandidateStableFrames++;
				}

				if (_dateCandidateStableFrames >= StableFramesToCommit)
				{
					_stableDateValue = _candidateDateValue;
					_stableDateNorm = _candidateDateNorm;
					_dateCandidateStableFrames = 0;
				}
			}

			// 아직 stable이 없으면 candidate를 임시로 사용(초기 수렴)
			stableBarcodeOut = string.IsNullOrEmpty(_stableBarcodeValue)
				? (string.IsNullOrWhiteSpace(barcodeCandidateValue) ? string.Empty : barcodeCandidateValue)
				: _stableBarcodeValue;

			stableDateOut = string.IsNullOrEmpty(_stableDateValue)
				? (string.IsNullOrWhiteSpace(dateCandidateValue) ? string.Empty : dateCandidateValue)
				: _stableDateValue;
		}
	}

		// ═══════════════════════════════════════
		// 생성자
		// ═══════════════════════════════════════
		public MainForm()
		{
			InitializeComponent();

			// 기본값 세팅
			txtDate.Text = "27-01-28";
			txtBarcode.Text = "8 801062 628476";

			// 카메라 이벤트
			_cameraMgr.FrameUpdated += OnFrameUpdated;
			btnDateRoi.Click += btnDateRoi_Click;
			btnBarcodeRoi.Click += btnBarcodeRoi_Click;

			// 디버그 클릭 좌표
			pictureBoxFrame.MouseClick += (s, e) =>
			{
				if (_originalFrame == null) return;

				float imgW = _originalFrame.Width;
				float imgH = _originalFrame.Height;
				float boxW = pictureBoxFrame.Width;
				float boxH = pictureBoxFrame.Height;

				float scale = Math.Min(boxW / imgW, boxH / imgH);
				float displayW = imgW * scale;
				float displayH = imgH * scale;
				float offsetX = (boxW - displayW) / 2f;
				float offsetY = (boxH - displayH) / 2f;

				float imgX = (e.X - offsetX) / scale;
				float imgY = (e.Y - offsetY) / scale;

				if (imgX < 0 || imgY < 0 || imgX >= imgW || imgY >= imgH)
				{
					this.Text = "이미지 영역 밖 클릭";
					return;
				}

				float ratioX = imgX / imgW;
				float ratioY = imgY / imgH;
				this.Text = $"X:{(int)imgX} Y:{(int)imgY} | ratioX:{ratioX:F2} ratioY:{ratioY:F2}";
			};

			UpdateInspectionRate();
			ApplyUiTheme();
		}

		private void ApplyUiTheme()
		{
			Color appBack = Color.FromArgb(0x40, 0x40, 0x40);
			Color accentBlue = Color.FromArgb(0x4A, 0x76, 0xFD);
			Color runGreen = Color.FromArgb(0x00, 0xFF, 0x00);
			Color stopRed = Color.FromArgb(0xFF, 0x00, 0x00);
			Color submitDark = Color.FromArgb(0x1A, 0x1A, 0x1A);

			BackColor = appBack;
			ForeColor = Color.White;

			panel1.BackColor = appBack;
			panelBottom.BackColor = appBack;

			label1.ForeColor = Color.White;
			label2.ForeColor = Color.White;

			_imagePanel.BackColor = Color.White;
			pictureBoxFrame.BackColor = Color.White;

			panelStatus.BackColor = Color.White;
			panelLog.BackColor = Color.White;
			lvLogs.BackColor = Color.White;
			lvLogs.ForeColor = Color.Black;

			lblInspectionSummary.ForeColor = Color.Black;
			lblInspectionCount.ForeColor = Color.Black;
			lblInspectionRate.ForeColor = Color.FromArgb(0x00, 0xC8, 0x00);
			lblInspectionRate.Font = new Font("맑은 고딕", 22F, FontStyle.Bold, GraphicsUnit.Point);

			StyleFillButton(btnRun, runGreen, Color.White);
			StyleFillButton(btnStop, stopRed, Color.White);
			StyleFillButton(btnInspect, accentBlue, Color.White);
			StyleFillButton(btnDateRoi, accentBlue, Color.White);
			StyleFillButton(btnBarcodeRoi, accentBlue, Color.White);
			StyleFillButton(btnDate, submitDark, Color.White);
			StyleFillButton(btnBarcode, submitDark, Color.White);

			txtDate.BackColor = Color.White;
			txtDate.ForeColor = Color.Black;
			txtBarcode.BackColor = Color.White;
			txtBarcode.ForeColor = Color.Black;

			menuStrip1.BackColor = Color.FromArgb(0x35, 0x35, 0x35);
			menuStrip1.ForeColor = Color.White;
			foreach (ToolStripItem item in menuStrip1.Items)
				ApplyMenuItemColors(item);

			lblDebug.ForeColor = Color.Silver;
			lblDebug.BackColor = appBack;
		}

		private static void ApplyMenuItemColors(ToolStripItem item)
		{
			item.ForeColor = Color.White;
			item.BackColor = Color.FromArgb(0x35, 0x35, 0x35);
			if (item is ToolStripDropDownItem drop)
			{
				foreach (ToolStripItem sub in drop.DropDownItems)
					ApplyMenuItemColors(sub);
			}
		}

		private static void StyleFillButton(Button b, Color back, Color fore)
		{
			b.UseVisualStyleBackColor = false;
			b.BackColor = back;
			b.ForeColor = fore;
			b.FlatStyle = FlatStyle.Flat;
			b.FlatAppearance.BorderSize = 0;
			b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, 0.2f);
		}

		// ═══════════════════════════════════════
		// 리사이즈
		// ═══════════════════════════════════════
		private void MainForm_Resize(object sender, EventArgs e)
		{
			int margin = 10;
			int formW = this.ClientSize.Width;
			int formH = this.ClientSize.Height;
			int menuH = menuStrip1.Height;
			int topPanelH = 90;
			int bottomPanelH = 230;
			int statusPanelW = 250;

			panel1.Left = margin;
			panel1.Top = menuH + margin;
			panel1.Width = formW - margin * 2;
			panel1.Height = topPanelH;

			// 오른쪽 고정: ROI → 검사 → STOP → RUN
			int g = 10;
			int roiGap = 6;
			int actionY = 13;
			int xRight = panel1.Width - margin;
			int xRoi = xRight - btnBarcodeRoi.Width;
			btnBarcodeRoi.Left = xRoi;
			btnBarcodeRoi.Top = 8;
			btnDateRoi.Left = xRoi;
			btnDateRoi.Top = btnBarcodeRoi.Bottom + roiGap;

			int xInspect = xRoi - g - btnInspect.Width;
			btnInspect.Left = xInspect;
			btnInspect.Top = actionY;

			int xStop = xInspect - g - btnStop.Width;
			btnStop.Left = xStop;
			btnStop.Top = actionY;

			int xRun = xStop - g - btnRun.Width;
			btnRun.Left = xRun;
			btnRun.Top = actionY;

			// 입력 행: RUN 왼쪽까지 Date/Barcode 텍스트 폭 분배
			int innerG = 12;
			int leftX = 11;
			int rowY = 35;
			int btnDW = btnDate.Width;
			int btnBW = btnBarcode.Width;
			int maxBarRight = xRun - margin;
			int capacity = maxBarRight - leftX - btnDW - btnBW - innerG * 3;
			capacity = Math.Max(200, capacity);
			int dateW = Math.Max(100, Math.Min(380, capacity * 2 / 5));
			int barW = Math.Max(100, capacity - dateW);

			txtDate.SetBounds(leftX, rowY, dateW, txtDate.Height);
			btnDate.Left = txtDate.Right + innerG;
			btnDate.Top = rowY;

			label1.Left = btnDate.Right + innerG;
			label1.Top = 3;

			txtBarcode.SetBounds(label1.Left, rowY, barW, txtBarcode.Height);
			btnBarcode.Left = txtBarcode.Right + innerG;
			btnBarcode.Top = rowY;

			if (btnBarcode.Right > maxBarRight)
			{
				int over = btnBarcode.Right - maxBarRight;
				txtBarcode.Width = Math.Max(80, txtBarcode.Width - over);
				btnBarcode.Left = txtBarcode.Right + innerG;
			}

			int imagePanelTop = panel1.Bottom + margin;
			int imagePanelH = formH - imagePanelTop - bottomPanelH - margin * 2;

			_imagePanel.Left = margin;
			_imagePanel.Top = imagePanelTop;
			_imagePanel.Width = formW - margin * 2;
			_imagePanel.Height = Math.Max(100, imagePanelH);

			pictureBoxFrame.Left = 0;
			pictureBoxFrame.Top = 0;
			pictureBoxFrame.Width = _imagePanel.Width;
			pictureBoxFrame.Height = _imagePanel.Height;

			lblResult.Left = 20;
			lblResult.Top = 20;

			panelBottom.Left = margin;
			panelBottom.Top = _imagePanel.Bottom + margin;
			panelBottom.Width = formW - margin * 2;
			panelBottom.Height = bottomPanelH;

			panelStatus.Left = 0;
			panelStatus.Top = 0;
			panelStatus.Width = statusPanelW;
			panelStatus.Height = panelBottom.Height;

			panelLog.Left = panelStatus.Right + margin;
			panelLog.Top = 0;
			panelLog.Width = panelBottom.Width - panelStatus.Width - margin;
			panelLog.Height = panelBottom.Height;

			lvLogs.Left = 0;
			lvLogs.Top = 0;
			lvLogs.Width = panelLog.Width - margin;
			lvLogs.Height = panelLog.Height - margin;

			lblDebug.Left = margin;
			lblDebug.Top = Math.Min(formH - lblDebug.Height - margin, panelBottom.Bottom + 4);
			lblDebug.Width = Math.Max(100, formW - margin * 2);
		}

		// ═══════════════════════════════════════
		// 폼 로드
		// ═══════════════════════════════════════
		private void MainForm_Load(object sender, EventArgs e)
		{
			lvLogs.Anchor = AnchorStyles.None;
			panel1.Anchor = AnchorStyles.None;
			_imagePanel.Anchor = AnchorStyles.None;
			panelBottom.Anchor = AnchorStyles.None;
			panelLog.Anchor = AnchorStyles.None;
			panelStatus.Anchor = AnchorStyles.None;

			lvLogs.Columns.Clear();
			lvLogs.View = View.Details;
			lvLogs.FullRowSelect = true;
			lvLogs.GridLines = true;
			lvLogs.Columns.Add("Result", 80);
			lvLogs.Columns.Add("Time", 100);
			lvLogs.Columns.Add("Reason", 100);
			lvLogs.Columns.Add("Date", 120);
			lvLogs.Columns.Add("Barcode", 180);
		}

		// ═══════════════════════════════════════
		// 민영 — 카메라 프레임 수신
		// ═══════════════════════════════════════
		private void OnFrameUpdated(Bitmap bmp)
		{
			if (bmp == null) return;

			if (this.InvokeRequired)
			{
				Bitmap copy = (Bitmap)bmp.Clone();
				this.BeginInvoke(new Action(() => OnFrameUpdated(copy)));
				return;
			}

			lock (_frameLock)
			{
				_latestFrame?.Dispose();
				_latestFrame = (Bitmap)bmp.Clone();
			}

			Image oldImage = pictureBoxFrame.Image;
			pictureBoxFrame.Image = (Bitmap)bmp.Clone();
			oldImage?.Dispose();

			// 티칭 ROI 고정 모드에서는 CSRT 트래킹/ROI 갱신을 스킵해서
			// 사용자가 잡아둔 ROI가 프레임마다 움직이지 않게 한다.
			if (_packageTracker.IsTracking && !_freezeTaughtRois)
			{
				if (Interlocked.CompareExchange(ref _isTrackingBusy, 1, 0) == 0)
				{
					Bitmap trackingBmp = (Bitmap)bmp.Clone();
					Task.Run(() =>
					{
						try { _packageTracker.Track(trackingBmp); }
						finally
						{
							trackingBmp.Dispose();
							Interlocked.Exchange(ref _isTrackingBusy, 0);
						}

						this.BeginInvoke(new Action(() =>
						{
							UpdateTrackedRois();
							pictureBoxFrame.Invalidate();
						}));
					});
				}
				else
				{
					UpdateTrackedRois();
				}
			}

			TryAutoInspection();
			pictureBoxFrame.Invalidate();
			bmp.Dispose();
		}

		// ═══════════════════════════════════════
		// 민영 — ROI 드래그
		// ═══════════════════════════════════════
		private void btnDateRoi_Click(object sender, EventArgs e)
		{
			if (_packageImageRect == Rectangle.Empty)
			{
				MessageBox.Show("먼저 포장지 전체를 드래그해서 잡아주세요!", "안내");
				return;
			}
			_roiMode = "date";
			MessageBox.Show("날짜 영역을 드래그해주세요!", "날짜 ROI");
		}

		private void btnBarcodeRoi_Click(object sender, EventArgs e)
		{
			if (_packageImageRect == Rectangle.Empty)
			{
				MessageBox.Show("먼저 포장지 전체를 드래그해서 잡아주세요!", "안내");
				return;
			}
			_roiMode = "barcode";
			MessageBox.Show("바코드 영역을 드래그해주세요!", "바코드 ROI");
		}

		private void pbCamera_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				_isSelecting = true;
				_startPoint = e.Location;
				_selectionRect = new Rectangle(e.X, e.Y, 0, 0);
			}
		}

		private void pbCamera_MouseMove(object sender, MouseEventArgs e)
		{
			if (_isSelecting)
			{
				int x = Math.Min(_startPoint.X, e.X);
				int y = Math.Min(_startPoint.Y, e.Y);
				int w = Math.Abs(_startPoint.X - e.X);
				int h = Math.Abs(_startPoint.Y - e.Y);
				_selectionRect = new Rectangle(x, y, w, h);
				pictureBoxFrame.Invalidate();
			}
		}

		private void pbCamera_MouseUp(object sender, MouseEventArgs e)
		{
			if (e.Button != MouseButtons.Left) return;

			_isSelecting = false;

			if (_selectionRect.Width > 10 && _selectionRect.Height > 10
				&& pictureBoxFrame.Image != null)
			{
				Rectangle imageRect = ScreenRectToImageRect(_selectionRect);

				if (imageRect.Width > 5 && imageRect.Height > 5)
				{
					if (_roiMode == "date")
					{
						_freezeTaughtRois = false;
						_dateImageRect = imageRect;
						_packageTracker.SetDateRoi(imageRect);
						_roiMode = "none";
					}
					else if (_roiMode == "barcode")
					{
						_freezeTaughtRois = false;
						_barcodeImageRect = imageRect;
						_packageTracker.SetBarcodeRoi(imageRect);
						_roiMode = "none";
					}
					else
					{
						_freezeTaughtRois = false;
						_packageImageRect = imageRect;

						using (Bitmap currentImg = (Bitmap)pictureBoxFrame.Image.Clone())
						{
							_packageTracker.SetTarget(currentImg, imageRect);
						}
					}

					if (_packageImageRect != Rectangle.Empty &&
						_dateImageRect != Rectangle.Empty &&
						_barcodeImageRect != Rectangle.Empty)
					{
						_inspectionMgr.SetRoiRatios(
							_packageImageRect,
							_dateImageRect,
							_barcodeImageRect);

						// 3개 ROI 티칭이 끝났으므로, 이후 프레임에서는 해당 ROI를 고정한다.
						_freezeTaughtRois = true;
					}

					_packageScreenRect = ImageRectToScreenRect(_packageImageRect);
					_dateScreenRect = ImageRectToScreenRect(_dateImageRect);
					_barcodeScreenRect = ImageRectToScreenRect(_barcodeImageRect);
				}
			}

			_selectionRect = Rectangle.Empty;
			pictureBoxFrame.Invalidate();
		}

		private void pbCamera_Paint(object sender, PaintEventArgs e)
		{
			if (_selectionRect.Width > 0 && _selectionRect.Height > 0)
			{
				using (Pen pen = new Pen(Color.Red, 2))
					e.Graphics.DrawRectangle(pen, _selectionRect);
			}

			if (_packageScreenRect != Rectangle.Empty)
			{
				using (Pen pen = new Pen(Color.Lime, 3))
					e.Graphics.DrawRectangle(pen, _packageScreenRect);
			}

			if (_dateScreenRect != Rectangle.Empty)
			{
				using (Pen pen = new Pen(Color.Blue, 3))
					e.Graphics.DrawRectangle(pen, _dateScreenRect);
			}

			if (_barcodeScreenRect != Rectangle.Empty)
			{
				using (Pen pen = new Pen(Color.Yellow, 3))
					e.Graphics.DrawRectangle(pen, _barcodeScreenRect);
			}

			DrawLiveOverlayOnScreen(e.Graphics);
		}

		private Rectangle ScreenRectToImageRect(Rectangle screenRect)
		{
			if (pictureBoxFrame.Image == null) return Rectangle.Empty;
			if (!GetZoomTransform(out float scale, out float offsetX, out float offsetY))
				return Rectangle.Empty;

			float left = (screenRect.Left - offsetX) / scale;
			float top = (screenRect.Top - offsetY) / scale;
			float right = (screenRect.Right - offsetX) / scale;
			float bottom = (screenRect.Bottom - offsetY) / scale;

			float x1 = Math.Min(left, right);
			float y1 = Math.Min(top, bottom);
			float x2 = Math.Max(left, right);
			float y2 = Math.Max(top, bottom);

			x1 = Math.Max(0, x1);
			y1 = Math.Max(0, y1);
			x2 = Math.Min(pictureBoxFrame.Image.Width, x2);
			y2 = Math.Min(pictureBoxFrame.Image.Height, y2);

			int x = (int)Math.Round(x1);
			int y = (int)Math.Round(y1);
			int w = (int)Math.Round(x2 - x1);
			int h = (int)Math.Round(y2 - y1);

			if (w <= 0 || h <= 0)
				return Rectangle.Empty;

			return new Rectangle(x, y, w, h);
		}

		private Rectangle ImageRectToScreenRect(Rectangle imageRect)
		{
			if (pictureBoxFrame.Image == null) return Rectangle.Empty;
			if (!GetZoomTransform(out float scale, out float offsetX, out float offsetY))
				return Rectangle.Empty;

			float x = imageRect.X * scale + offsetX;
			float y = imageRect.Y * scale + offsetY;
			float w = imageRect.Width * scale;
			float h = imageRect.Height * scale;

			return new Rectangle(
				(int)Math.Round(x),
				(int)Math.Round(y),
				(int)Math.Round(w),
				(int)Math.Round(h));
		}

		private void UpdateTrackedRois()
		{
			if (!_packageTracker.IsTracking) return;
			if (_freezeTaughtRois) return;

			_packageImageRect = ClampToFrame(_packageTracker.GetPackageRect(),
				pictureBoxFrame.Image?.Width ?? 0,
				pictureBoxFrame.Image?.Height ?? 0);

			if (_inspectionMgr.DateRatioRect != RectangleF.Empty)
			{
				Rectangle nextDate = ClampToFrame(
					_inspectionMgr.GetDateRect(_packageImageRect),
					pictureBoxFrame.Image?.Width ?? 0,
					pictureBoxFrame.Image?.Height ?? 0);
				_dateImageRect = _hasPrevTrackedRects
					? SmoothRect(_prevDateImageRect, nextDate, 0.35f)
					: nextDate;
			}

			if (_inspectionMgr.BarcodeRatioRect != RectangleF.Empty)
			{
				Rectangle nextBarcode = ClampToFrame(
					_inspectionMgr.GetBarcodeRect(_packageImageRect),
					pictureBoxFrame.Image?.Width ?? 0,
					pictureBoxFrame.Image?.Height ?? 0);
				_barcodeImageRect = _hasPrevTrackedRects
					? SmoothRect(_prevBarcodeImageRect, nextBarcode, 0.35f)
					: nextBarcode;
			}

			_packageScreenRect = ImageRectToScreenRect(_packageImageRect);
			_dateScreenRect = ImageRectToScreenRect(_dateImageRect);
			_barcodeScreenRect = ImageRectToScreenRect(_barcodeImageRect);

			_prevDateImageRect = _dateImageRect;
			_prevBarcodeImageRect = _barcodeImageRect;
			_hasPrevTrackedRects = true;

			lblDebug.Text =
				$"P: X={_packageImageRect.X},Y={_packageImageRect.Y} | " +
				$"D: X={_dateImageRect.X},Y={_dateImageRect.Y} | " +
				$"B: X={_barcodeImageRect.X},Y={_barcodeImageRect.Y}";
		}

		// ═══════════════════════════════════════
		// 자동 검사
		// ═══════════════════════════════════════
		private void TryAutoInspection()
		{
			if (!_isAutoInspecting) return;
			if (string.IsNullOrWhiteSpace(_expectedDate) ||
				string.IsNullOrWhiteSpace(_expectedBarcode)) return;
			if (!_packageTracker.IsTracking) return;
			if (!_packageTracker.IsDateRoiSet || !_packageTracker.IsBarcodeRoiSet) return;
			if (DateTime.Now - _lastInspectionTime < _inspectionInterval) return;
			if (Interlocked.CompareExchange(ref _isInspectionBusy, 1, 0) != 0) return;

			Bitmap currentFrame;

			lock (_frameLock)
			{
				if (_latestFrame == null)
				{
					Interlocked.Exchange(ref _isInspectionBusy, 0);
					return;
				}

				currentFrame = (Bitmap)_latestFrame.Clone();
			}

			Rectangle dateRectForRead = _dateImageRect;
			Rectangle barcodeRectForRead = _barcodeImageRect;

			Rectangle dateRectForDraw = _dateImageRect;
			Rectangle barcodeRectForDraw = _barcodeImageRect;

			dateRectForRead = ClampToFrame(dateRectForRead, currentFrame.Width, currentFrame.Height);
			barcodeRectForRead = ClampToFrame(barcodeRectForRead, currentFrame.Width, currentFrame.Height);

			// 사용자가 ROI를 대충 잡아도 OCR 실패가 줄도록 read/draw ROI를 약간 확장
			// 단, 날짜는 "위" 쪽을 더 크게 확장하면 원치 않는 영역까지 잡히므로(사용자 불만),
			// 위쪽(top) 확장은 최소화하고 아래(bottom) 확장은 조금만 주도록 비대칭 확장을 적용.
			int barcodeMarginX = Math.Max(8, (int)Math.Round(barcodeRectForRead.Width * 0.05));
			int barcodeMarginY = Math.Max(8, (int)Math.Round(barcodeRectForRead.Height * 0.05));

			int dateMarginLeft = Math.Max(6, (int)Math.Round(dateRectForRead.Width * 0.05));
			int dateMarginRight = Math.Max(6, (int)Math.Round(dateRectForRead.Width * 0.05));
			int dateMarginTop = Math.Max(0, (int)Math.Round(dateRectForRead.Height * 0.01));
			int dateMarginBottom = Math.Max(8, (int)Math.Round(dateRectForRead.Height * 0.06));

			dateRectForRead = ExpandAndClampRectAsymmetric(
				dateRectForRead,
				currentFrame.Width,
				currentFrame.Height,
				dateMarginLeft,
				dateMarginTop,
				dateMarginRight,
				dateMarginBottom);
			barcodeRectForRead = ExpandAndClampRect(
				barcodeRectForRead,
				currentFrame.Width,
				currentFrame.Height,
				barcodeMarginX,
				barcodeMarginY);

			dateRectForDraw = ExpandAndClampRectAsymmetric(
				dateRectForDraw,
				currentFrame.Width,
				currentFrame.Height,
				dateMarginLeft,
				dateMarginTop,
				dateMarginRight,
				dateMarginBottom);
			barcodeRectForDraw = ExpandAndClampRect(
				barcodeRectForDraw,
				currentFrame.Width,
				currentFrame.Height,
				barcodeMarginX,
				barcodeMarginY);

			_lastInspectionTime = DateTime.Now;

			Task.Run(() =>
			{
				try
				{
					BarcodeResult barcodeResult;
					DateResult dateResult;
					List<Rectangle> barcodeBlobRects;
					List<Rectangle> dateBlobRects;
					string stableBarcodeValue = string.Empty;
					string stableDateValue = string.Empty;
					string barcodeCandidateValue = string.Empty;
					string dateCandidateValue = string.Empty;

					using (currentFrame)
					{
						barcodeResult = _barcodeReader.ReadBarcode(currentFrame, barcodeRectForRead);
						dateResult = _dateReader.ReadDate(currentFrame, dateRectForRead);
						barcodeCandidateValue = barcodeResult.Success ? barcodeResult.Value : string.Empty;
						dateCandidateValue = dateResult.Success ? dateResult.Value : string.Empty;

						// OCR 값 안정화 (프레임 간 흔들림 제거)
						UpdateStableOcrValues(
							barcodeCandidateValue,
							dateCandidateValue,
							out stableBarcodeValue,
							out stableDateValue);

						// 화면에는 "안정화된 값과 동일한 후보"에서만 blob union을 그려서
						// (숫자가 바뀌는 동안) blob 오버레이가 흔들리는 것을 줄임.
						bool useBarcodeBlobs =
							barcodeResult.Success &&
							GetBarcodeNorm(barcodeCandidateValue) == GetBarcodeNorm(stableBarcodeValue);

						bool useDateBlobs =
							dateResult.Success &&
							GetDateNorm(dateCandidateValue) == GetDateNorm(stableDateValue);

						barcodeBlobRects = useBarcodeBlobs
							? GetBarcodeBlobRects(currentFrame, barcodeRectForDraw)
							: new List<Rectangle>();

						dateBlobRects = useDateBlobs
							? GetDateBlobRects(currentFrame, dateRectForDraw)
							: new List<Rectangle>();
					}

					string actualBarcode = stableBarcodeValue;
					string actualDate = stableDateValue;

					string logLine = $"[{DateTime.Now:HH:mm:ss.fff}] " +
									 $"DateROI:{dateRectForRead} | BarcodeROI:{barcodeRectForRead} | " +
						$"바코드:{barcodeResult.Success}/{barcodeCandidateValue}/{barcodeResult.FailReason} | " +
						$"날짜:{dateResult.Success}/{dateCandidateValue}/{dateResult.FailReason} | " +
						$"stableBarcode:{actualBarcode} | stableDate:{actualDate}";

					System.IO.File.AppendAllText(
						System.IO.Path.Combine(
							Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
							"debug_log.txt"),
						logLine + "\n");

					string debugMsg = $"바코드:{barcodeResult.Success}({barcodeCandidateValue}) | stable:{actualBarcode} | 날짜:{dateResult.Success}({dateCandidateValue}) | stable:{actualDate}";

					InspectionResult result = _inspectionMgr.Inspect(
						_expectedBarcode, actualBarcode,
						_expectedDate, actualDate,
						true);

					this.BeginInvoke(new Action(() =>
					{
						lblDebug.Text = debugMsg;

						UpdateLiveOverlay(
							barcodeRectForDraw,
							dateRectForDraw,
							actualBarcode,
							actualDate,
							result.IsBarcodeOk,
							result.IsDateOk,
							barcodeBlobRects,
							dateBlobRects);

						pictureBoxFrame.Invalidate();

						ApplyInspectionResult(result);
					}));
				}
				catch (Exception ex)
				{
					this.BeginInvoke(new Action(() =>
						lblDebug.Text = "검사 오류: " + ex.Message));
				}
				finally
				{
					Interlocked.Exchange(ref _isInspectionBusy, 0);
				}
			});
		}

		// ═══════════════════════════════════════
		// 공통 유틸
		// ═══════════════════════════════════════
		private bool GetZoomTransform(out float scale, out float offsetX, out float offsetY)
		{
			scale = 1f;
			offsetX = 0f;
			offsetY = 0f;

			if (pictureBoxFrame.Image == null) return false;

			float imgW = pictureBoxFrame.Image.Width;
			float imgH = pictureBoxFrame.Image.Height;
			float boxW = pictureBoxFrame.ClientSize.Width;
			float boxH = pictureBoxFrame.ClientSize.Height;

			if (imgW <= 0 || imgH <= 0 || boxW <= 0 || boxH <= 0)
				return false;

			scale = Math.Min(boxW / imgW, boxH / imgH);

			float drawW = imgW * scale;
			float drawH = imgH * scale;

			offsetX = (boxW - drawW) / 2f;
			offsetY = (boxH - drawH) / 2f;

			return true;
		}

		private Rectangle ClampToFrame(Rectangle roi, int frameWidth, int frameHeight)
		{
			int x = Math.Max(0, roi.X);
			int y = Math.Max(0, roi.Y);
			int right = Math.Min(frameWidth, roi.Right);
			int bottom = Math.Min(frameHeight, roi.Bottom);

			if (right <= x || bottom <= y) return Rectangle.Empty;
			return new Rectangle(x, y, right - x, bottom - y);
		}

		private Rectangle SmoothRect(Rectangle prev, Rectangle next, float alpha)
		{
			if (prev == Rectangle.Empty)
				return next;

			float x = prev.X * (1f - alpha) + next.X * alpha;
			float y = prev.Y * (1f - alpha) + next.Y * alpha;
			float w = prev.Width * (1f - alpha) + next.Width * alpha;
			float h = prev.Height * (1f - alpha) + next.Height * alpha;

			int xi = Math.Max(0, (int)Math.Round(x));
			int yi = Math.Max(0, (int)Math.Round(y));
			int wi = Math.Max(1, (int)Math.Round(w));
			int hi = Math.Max(1, (int)Math.Round(h));

			return new Rectangle(xi, yi, wi, hi);
		}

		private Rectangle ExpandAndClampRect(Rectangle roi, int frameWidth, int frameHeight, int marginX, int marginY)
		{
			if (roi == Rectangle.Empty)
				return Rectangle.Empty;

			Rectangle expanded = new Rectangle(
				roi.X - marginX,
				roi.Y - marginY,
				roi.Width + marginX * 2,
				roi.Height + marginY * 2);

			return ClampToFrame(expanded, frameWidth, frameHeight);
		}

		private Rectangle ExpandAndClampRectAsymmetric(
			Rectangle roi,
			int frameWidth,
			int frameHeight,
			int marginLeft,
			int marginTop,
			int marginRight,
			int marginBottom)
		{
			if (roi == Rectangle.Empty)
				return Rectangle.Empty;

			Rectangle expanded = new Rectangle(
				roi.X - marginLeft,
				roi.Y - marginTop,
				roi.Width + marginLeft + marginRight,
				roi.Height + marginTop + marginBottom);

			return ClampToFrame(expanded, frameWidth, frameHeight);
		}

		private void ApplyInspectionResult(InspectionResult result)
		{
			if (result == null) return;

			_totalInspectionCount++;

			if (result.IsOverallOk)
			{
				_okInspectionCount++;
				lblResult.Text = "OK";
				lblResult.ForeColor = Color.LimeGreen;
				AddLogItem("OK", "-", result.ActualDate, result.ActualBarcode, Color.Green);
			}
			else
			{
				lblResult.Text = "NOK";
				lblResult.ForeColor = Color.Red;
				AddLogItem("NOK", result.FailReasonText,
					result.ActualDate, result.ActualBarcode, Color.Red);
			}

			_csvLogManager.SaveLog(result);
			UpdateInspectionRate();
		}

		private string NormalizeBarcodeForOverlay(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw))
				return string.Empty;

			return new string(raw.Where(char.IsLetterOrDigit).ToArray());
		}

		// 날짜 OCR 결과를 "숫자/구분자(-)" 문자 시퀀스로 변환해서,
		// 자리별 ROI(블랍)과 글자 인덱스를 맞추기 위함.
		// '.' 또는 '/' 같은 구분자는 '-'로 취급한다.
		private static string ExtractDateChars(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw))
				return string.Empty;

			var chars = raw
				.Where(ch =>
					char.IsDigit(ch) || ch == '-' || ch == '.' || ch == '/')
				.Select(ch =>
					ch == '.' || ch == '/' ? '-' : ch);

			return new string(chars.ToArray());
		}

		private static Rectangle GetUnionRect(List<Rectangle> rects)
		{
			if (rects == null || rects.Count == 0)
				return Rectangle.Empty;

			int left = int.MaxValue;
			int top = int.MaxValue;
			int right = int.MinValue;
			int bottom = int.MinValue;

			for (int i = 0; i < rects.Count; i++)
			{
				Rectangle r = rects[i];
				if (r == Rectangle.Empty) continue;

				if (r.Left < left) left = r.Left;
				if (r.Top < top) top = r.Top;
				if (r.Right > right) right = r.Right;
				if (r.Bottom > bottom) bottom = r.Bottom;
			}

			if (left == int.MaxValue || top == int.MaxValue)
				return Rectangle.Empty;

			int w = right - left;
			int h = bottom - top;
			if (w <= 0 || h <= 0) return Rectangle.Empty;

			return new Rectangle(left, top, w, h);
		}

		private void UpdateLiveOverlay(
			Rectangle barcodeRect,
			Rectangle dateRect,
			string actualBarcode,
			string actualDate,
			bool barcodeSuccess,
			bool dateSuccess,
			List<Rectangle> barcodeBlobRects,
			List<Rectangle> dateBlobRects)
		{
			lock (_overlayLock)
			{
				_liveBarcodeRect = barcodeRect;
				_liveDateRect = dateRect;
				_liveActualBarcode = actualBarcode ?? string.Empty;
				_liveActualDate = actualDate ?? string.Empty;
				_liveBarcodeSuccess = barcodeSuccess;
				_liveDateSuccess = dateSuccess;
				_liveBarcodeBlobRects = barcodeBlobRects ?? new List<Rectangle>();
				_liveDateBlobRects = dateBlobRects ?? new List<Rectangle>();
			}
		}

		private List<Rectangle> GetBarcodeBlobRects(Bitmap source, Rectangle barcodeRect)
		{
			List<Rectangle> result = new List<Rectangle>();

			if (source == null || barcodeRect == Rectangle.Empty)
				return result;

			Rectangle safeRect = ClampToFrame(barcodeRect, source.Width, source.Height);
			if (safeRect == Rectangle.Empty)
				return result;

			try
			{
				using (Bitmap barcodeCrop = source.Clone(safeRect, source.PixelFormat))
				{
					// 1) 바코드 ROI 안에서 숫자 영역만 다시 찾기
					Rectangle numberRegion = BarcodeNumberRegionDetector.GetNumberRegion(barcodeCrop);
					if (numberRegion == Rectangle.Empty)
						return result;

					// 디버그 확인용
					using (Bitmap debugNumber = TextRegionCropper.Crop(barcodeCrop, numberRegion))
					{
						try
						{
							Directory.CreateDirectory("DebugImages");
							debugNumber.Save(Path.Combine("DebugImages", "barcode_number_region.png"));
						}
						catch { }
					}

					// 2) 숫자 영역만 crop
					using (Bitmap numberCrop = TextRegionCropper.Crop(barcodeCrop, numberRegion))
					{
						// 🔥 여기 추가 (핵심)
						Mat mat = BitmapConverter.ToMat(numberCrop);

						// grayscale
						Cv2.CvtColor(mat, mat, ColorConversionCodes.BGR2GRAY);

						// threshold (중요)
						Cv2.Threshold(mat, mat, 0, 255, ThresholdTypes.Otsu);

						Bitmap binBmp = BitmapConverter.ToBitmap(mat);

						// 디버그 저장
						binBmp.Save("DebugImages/bin.png");

						// 👉 이진화된 이미지로 blob 찾기
						List<Rectangle> charBoxes = CharBlobDetector.FindCharBoxes(binBmp)
							.Where(r => r.Width >= 5 && r.Width <= 60)
							.Where(r => r.Height >= 15 && r.Height <= 80)
							.OrderBy(r => r.X)
							.ToList();

						// charBoxes는 numberCrop 좌표계 기준이므로, source 좌표계로 변환해서 반환
						foreach (var box in charBoxes)
						{
							result.Add(new Rectangle(
								safeRect.X + numberRegion.X + box.X,
								safeRect.Y + numberRegion.Y + box.Y,
								box.Width,
								box.Height));
						}

						// [디버깅] - 확인용 출력
						Debug.WriteLine("numberRegion = " + numberRegion);
						Debug.WriteLine("charBoxes count = " + charBoxes.Count);
					}

				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine("[GetBarcodeBlobRects ERROR] " + ex.ToString());
			}

			return result;
		}

		private List<Rectangle> GetDateBlobRects(Bitmap source, Rectangle dateRect)
		{
			List<Rectangle> result = new List<Rectangle>();

			if (source == null || dateRect == Rectangle.Empty)
				return result;

			Rectangle safeRect = ClampToFrame(dateRect, source.Width, source.Height);
			if (safeRect == Rectangle.Empty)
				return result;

			try
			{
				// 사용자가 지정한 dateRect "안"에서만 날짜 텍스트를 찾도록 제한한다.
				// (라벨 탐색까지 하면 dateRect 밖의 위 영역을 잡는 경우가 생김)
				Rectangle dateTextRect = DateTextRegionDetector.GetDateTextRegion(safeRect);
				dateTextRect = Rectangle.Intersect(dateTextRect, safeRect);
				dateTextRect = ClampToFrame(dateTextRect, source.Width, source.Height);
				if (dateTextRect == Rectangle.Empty)
					dateTextRect = safeRect;

				using (Bitmap dateTextCrop = source.Clone(dateTextRect, source.PixelFormat))
				{
					Mat mat = BitmapConverter.ToMat(dateTextCrop);

					// grayscale
					Cv2.CvtColor(mat, mat, ColorConversionCodes.BGR2GRAY);
					// threshold
					Cv2.Threshold(mat, mat, 0, 255, ThresholdTypes.Otsu);

					Bitmap binBmp = BitmapConverter.ToBitmap(mat);

					// 자리별 문자(주로 숫자) 블랍 찾기
					List<Rectangle> charBoxes = CharBlobDetector.FindCharBoxes(binBmp)
						// 하이픈/구분자도 포함하기 위해 최소 크기 조건을 완화
						.Where(r => r.Width >= 2 && r.Width <= 45)
						.Where(r => r.Height >= 6 && r.Height <= 70)
						.Where(r => r.Width >= r.Height * 0.10)
						.OrderBy(r => r.X)
						.ToList();

					foreach (var box in charBoxes)
					{
						result.Add(new Rectangle(
							dateTextRect.X + box.X,
							dateTextRect.Y + box.Y,
							box.Width,
							box.Height));
					}
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine("[GetDateBlobRects ERROR] " + ex);
			}

			return result;
		}

		private void DrawLiveOverlayOnScreen(Graphics g)
		{
			if (pictureBoxFrame.Image == null)
				return;

			Rectangle barcodeRect;
			Rectangle dateRect;
			string actualBarcode;
			string actualDate;
			bool barcodeSuccess;
			bool dateSuccess;
			List<Rectangle> blobRects;
			List<Rectangle> dateBlobRects;

			lock (_overlayLock)
			{
				barcodeRect = _liveBarcodeRect;
				dateRect = _liveDateRect;
				actualBarcode = _liveActualBarcode;
				actualDate = _liveActualDate;
				barcodeSuccess = _liveBarcodeSuccess;
				dateSuccess = _liveDateSuccess;
				blobRects = new List<Rectangle>(_liveBarcodeBlobRects);
				dateBlobRects = new List<Rectangle>(_liveDateBlobRects);
			}

			// 1) 바코드: "블랍 1개" = (자리별 blob들의 union) 또는 실패 시 barcode ROI
			if (barcodeRect != Rectangle.Empty)
			{
				Rectangle barcodeBlobUnion = GetUnionRect(blobRects);
				if (barcodeBlobUnion == Rectangle.Empty)
					barcodeBlobUnion = barcodeRect;

				Rectangle screenBarcodeBlob = ImageRectToScreenRect(barcodeBlobUnion);

				Color overlayColor = barcodeSuccess ? Color.Lime : Color.Red;

				using (Pen pen = new Pen(overlayColor, 3))
				using (Font font = new Font("Arial", 16, FontStyle.Bold))
				using (Brush brush = new SolidBrush(overlayColor))
				{
					g.DrawRectangle(pen, screenBarcodeBlob);
					g.DrawString(
						string.IsNullOrEmpty(actualBarcode) ? "읽기 실패" : actualBarcode,
						font,
						brush,
						screenBarcodeBlob.X,
						Math.Max(0, screenBarcodeBlob.Y - 26));
				}
			}

			// 2) 날짜: "블랍 1개" = (자리별 blob들의 union) 또는 실패 시 date ROI
			if (dateRect != Rectangle.Empty)
			{
				Rectangle dateBlobUnion = GetUnionRect(dateBlobRects);
				if (dateBlobUnion == Rectangle.Empty)
					dateBlobUnion = dateRect;

				Rectangle screenDateBlob = ImageRectToScreenRect(dateBlobUnion);

				Color overlayColor = dateSuccess ? Color.Lime : Color.Red;

				using (Pen pen = new Pen(overlayColor, 3))
				using (Font font = new Font("Arial", 16, FontStyle.Bold))
				using (Brush brush = new SolidBrush(overlayColor))
				{
					g.DrawRectangle(pen, screenDateBlob);

					g.TranslateTransform(
						screenDateBlob.X - 5,
						screenDateBlob.Y + screenDateBlob.Height);
					g.RotateTransform(-90);
					g.DrawString(
						string.IsNullOrEmpty(actualDate) ? "읽기 실패" : actualDate,
						font,
						brush,
						0,
						0);
					g.ResetTransform();
				}
			}
		}

		// ═══════════════════════════════════════
		// RUN / STOP / 검사 버튼
		// ═══════════════════════════════════════
		private void btnRun_Click(object sender, EventArgs e)
		{
			bool success = _cameraMgr.StartCamera();
			if (success)
			{
				btnRun.Enabled = false;
				btnStop.Enabled = true;
				_fpsSw.Restart();
				return;
			}
			MessageBox.Show("카메라 연결 실패");
		}

		private async void btnStop_Click(object sender, EventArgs e)
		{
			btnRun.Enabled = true;
			btnStop.Enabled = false;

			await _cameraMgr.StopCameraAsync();

			_freezeTaughtRois = false;

			lock (_ocrStabilityLock)
			{
				_stableBarcodeValue = string.Empty;
				_stableDateValue = string.Empty;
				_stableBarcodeNorm = string.Empty;
				_stableDateNorm = string.Empty;

				_candidateBarcodeNorm = string.Empty;
				_candidateBarcodeValue = string.Empty;
				_barcodeCandidateStableFrames = 0;

				_candidateDateNorm = string.Empty;
				_candidateDateValue = string.Empty;
				_dateCandidateStableFrames = 0;
			}

			_packageScreenRect = Rectangle.Empty;
			_dateScreenRect = Rectangle.Empty;
			_barcodeScreenRect = Rectangle.Empty;
			_selectionRect = Rectangle.Empty;
			_roiMode = "none";

			_packageImageRect = Rectangle.Empty;
			_dateImageRect = Rectangle.Empty;
			_barcodeImageRect = Rectangle.Empty;

			_packageTracker.Reset();

			UpdateLiveOverlay(
				Rectangle.Empty,
				Rectangle.Empty,
				string.Empty,
				string.Empty,
				false,
				false,
				new List<Rectangle>(),
				new List<Rectangle>());

			lock (_frameLock)
			{
				_latestFrame?.Dispose();
				_latestFrame = null;
			}

			Image oldImage = pictureBoxFrame.Image;
			pictureBoxFrame.Image = null;
			oldImage?.Dispose();

			pictureBoxFrame.Invalidate();
		}

		private void btnInspect_Click(object sender, EventArgs e)
		{
			_expectedDate = txtDate.Text.Trim();
			_expectedBarcode = txtBarcode.Text.Trim();

			if (string.IsNullOrWhiteSpace(_expectedDate) ||
				string.IsNullOrWhiteSpace(_expectedBarcode))
			{
				MessageBox.Show("먼저 기준 날짜와 기준 바코드를 입력하세요.");
				return;
			}

			if (pictureBoxFrame.Image == null)
			{
				MessageBox.Show("먼저 카메라를 실행하세요.");
				return;
			}

			if (_packageImageRect == Rectangle.Empty)
			{
				MessageBox.Show("먼저 포장지 ROI를 지정하세요.");
				return;
			}

			if (_dateImageRect == Rectangle.Empty)
			{
				MessageBox.Show("먼저 날짜 ROI를 지정하세요.");
				return;
			}

			if (_barcodeImageRect == Rectangle.Empty)
			{
				MessageBox.Show("먼저 바코드 ROI를 지정하세요.");
				return;
			}

			_inspectionMgr.SetRoiRatios(
				_packageImageRect, _dateImageRect, _barcodeImageRect);

			// 검사 시작 시 stable/candidate 값을 초기화해서
			// 이전 프레임/이전 검사 잔상이 남지 않게 함
			lock (_ocrStabilityLock)
			{
				_stableBarcodeValue = string.Empty;
				_stableDateValue = string.Empty;
				_stableBarcodeNorm = string.Empty;
				_stableDateNorm = string.Empty;

				_candidateBarcodeNorm = string.Empty;
				_candidateBarcodeValue = string.Empty;
				_barcodeCandidateStableFrames = 0;

				_candidateDateNorm = string.Empty;
				_candidateDateValue = string.Empty;
				_dateCandidateStableFrames = 0;
			}

			_isAutoInspecting = true;
			MessageBox.Show("실시간 검사 시작");
		}

		// ═══════════════════════════════════════
		// 기준값 제출
		// ═══════════════════════════════════════
		private void btnDate_Click(object sender, EventArgs e)
		{
			_expectedDate = txtDate.Text.Trim();
			if (string.IsNullOrWhiteSpace(_expectedDate))
			{
				MessageBox.Show("날짜를 입력하세요.");
				return;
			}
			MessageBox.Show("기준 날짜 저장 완료: " + _expectedDate);
		}

		private void btnBarcode_Click(object sender, EventArgs e)
		{
			_expectedBarcode = txtBarcode.Text.Trim();
			if (string.IsNullOrWhiteSpace(_expectedBarcode))
			{
				MessageBox.Show("바코드를 입력하세요.");
				return;
			}
			MessageBox.Show("기준 바코드 저장 완료: " + _expectedBarcode);
		}

		// ═══════════════════════════════════════
		// 이미지 파일 열기/저장 (메뉴)
		// ═══════════════════════════════════════
		private void imageToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (OpenFileDialog ofd = new OpenFileDialog())
			{
				ofd.Title = "이미지 선택";
				ofd.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";

				if (ofd.ShowDialog() == DialogResult.OK)
				{
					try
					{
						Bitmap bmp = new Bitmap(ofd.FileName);

						_originalFrame?.Dispose();
						_originalFrame = new Bitmap(bmp);

						pictureBoxFrame.Image?.Dispose();
						pictureBoxFrame.Image = bmp;
					}
					catch (Exception ex)
					{
						MessageBox.Show("이미지 로드 실패: " + ex.Message);
					}
				}
			}
		}

		private void imageSaveToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (pictureBoxFrame.Image == null)
			{
				MessageBox.Show("저장할 이미지가 없습니다.");
				return;
			}

			using (SaveFileDialog sfd = new SaveFileDialog())
			{
				sfd.Title = "이미지 저장";
				sfd.Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap Image|*.bmp";
				sfd.DefaultExt = "png";
				sfd.FileName = "saved_image";

				if (sfd.ShowDialog() == DialogResult.OK)
				{
					try
					{
						pictureBoxFrame.Image.Save(sfd.FileName);
						MessageBox.Show("이미지 저장 완료");
					}
					catch (Exception ex)
					{
						MessageBox.Show("이미지 저장 실패: " + ex.Message);
					}
				}
			}
		}

		private void imageOpenToolStripMenuItem_Click(object sender, EventArgs e) { }

		// ═══════════════════════════════════════
		// 이미지 파일 기반 수동 검사
		// ═══════════════════════════════════════
		private void RunImageInspection()
		{
			if (pictureBoxFrame.Image == null || _originalFrame == null)
			{
				MessageBox.Show("먼저 이미지를 불러오세요.");
				return;
			}

			if (string.IsNullOrWhiteSpace(_expectedDate) ||
				string.IsNullOrWhiteSpace(_expectedBarcode))
			{
				MessageBox.Show("먼저 기준 날짜와 기준 바코드를 제출하세요.");
				return;
			}

			string readBarcode = "8801062628476";
			string readDate = "27.01.27  B5 F1";
			bool isPrintOk = true;

			InspectionResult result = _inspectionManager.Inspect(
				_expectedBarcode, readBarcode,
				_expectedDate, readDate,
				isPrintOk);

			_totalInspectionCount++;

			Bitmap workBitmap = new Bitmap(_originalFrame);
			ProcessBarcodeOverlay(workBitmap, readBarcode, _expectedBarcode);

			if (pictureBoxFrame.Image != null)
			{
				Bitmap current = new Bitmap(pictureBoxFrame.Image);
				ProcessDateOverlay(current, readDate, _expectedDate);
			}

			if (result.IsOverallOk)
			{
				_okInspectionCount++;
				lblResult.Text = "OK";
				lblResult.ForeColor = Color.LimeGreen;
				AddLogItem("OK", "-", result.ActualDate, result.ActualBarcode, Color.Green);
			}
			else
			{
				lblResult.Text = "NOK";
				lblResult.ForeColor = Color.Red;
				AddLogItem("NOK", result.FailReasonText,
					result.ActualDate, result.ActualBarcode, Color.Red);
			}

			_csvLogManager.SaveLog(result);
			UpdateInspectionRate();
		}

		private void ProcessBarcodeOverlay(Bitmap source, string readValue, string expectedValue)
		{
			int x = (int)(source.Width * 0.20);
			int y = (int)(source.Height * 0.55);
			int w = (int)(source.Width * 0.60);
			int h = (int)(source.Height * 0.25);

			Rectangle searchRoi = new Rectangle(x, y, w, h);
			Rectangle fittedRoi = BarcodeLabelDetector.FindWhiteLabelRect(source, searchRoi);

			if (fittedRoi == Rectangle.Empty)
			{
				MessageBox.Show("흰색 라벨 ROI를 찾지 못했습니다.");
				return;
			}

			using (Bitmap labelCrop = TextRegionCropper.Crop(source, fittedRoi))
			{
				Rectangle numberRegion = BarcodeNumberRegionDetector.GetNumberRegion(labelCrop);
				using (Bitmap numberCrop = TextRegionCropper.Crop(labelCrop, numberRegion))
				{
					List<Rectangle> charBoxes = CharBlobDetector.FindCharBoxes(numberCrop)
						.Where(r => r.Width >= 4 && r.Width <= 45)
						.Where(r => r.Height >= 12 && r.Height <= 55)
						.Where(r => r.Width >= r.Height * 0.22)
						.OrderBy(r => r.X)
						.ToList();

					Bitmap debugImage = new Bitmap(source);
					using (Graphics g = Graphics.FromImage(debugImage))
					using (Font font = new Font("Arial", 18, FontStyle.Bold))
					{
						for (int i = 0; i < charBoxes.Count; i++)
						{
							Rectangle box = charBoxes[i];
							Rectangle originalBox = new Rectangle(
								fittedRoi.X + numberRegion.X + box.X,
								fittedRoi.Y + numberRegion.Y + box.Y,
								box.Width, box.Height);

							bool isMatch = i < readValue.Length &&
										   i < expectedValue.Length &&
										   readValue[i] == expectedValue[i];

							Color color = isMatch ? Color.Lime : Color.Red;

							using (Pen pen = new Pen(color, 2))
							using (Brush brush = new SolidBrush(color))
							{
								g.DrawRectangle(pen, originalBox);
								g.DrawString(
									i < readValue.Length ? readValue[i].ToString() : "?",
									font, brush,
									originalBox.X,
									Math.Max(0, originalBox.Y - 22));
							}
						}
					}

					Image oldImage = pictureBoxFrame.Image;
					pictureBoxFrame.Image = debugImage;
					oldImage?.Dispose();
				}
			}
		}

		private void ProcessDateOverlay(Bitmap source, string readDate, string expectedDate)
		{
			int x = (int)(source.Width * 0.15);
			int y = (int)(source.Height * 0.23);
			int w = (int)(source.Width * 0.11);
			int h = (int)(source.Height * 0.57);
			Rectangle searchRoi = new Rectangle(x, y, w, h);

			Rectangle labelRect = DateLabelDetector.FindDateLabelRect(source, searchRoi);
			if (labelRect == Rectangle.Empty) labelRect = searchRoi;

			Rectangle dateTextRect = DateTextRegionDetector.GetDateTextRegion(labelRect);

			bool isMatch = DateNormalizer.IsMatch(readDate, expectedDate);
			Color overlayColor = isMatch ? Color.Lime : Color.Red;

			string readChars = ExtractDateChars(readDate);
			string expectedChars = ExtractDateChars(expectedDate);

			List<Rectangle> charBoxes = new List<Rectangle>();
			using (Bitmap dateCrop = source.Clone(dateTextRect, source.PixelFormat))
			{
				Mat mat = BitmapConverter.ToMat(dateCrop);
				Cv2.CvtColor(mat, mat, ColorConversionCodes.BGR2GRAY);
				Cv2.Threshold(mat, mat, 0, 255, ThresholdTypes.Otsu);

				using (Bitmap binBmp = BitmapConverter.ToBitmap(mat))
				{
					charBoxes = CharBlobDetector.FindCharBoxes(binBmp)
						.Where(r => r.Width >= 2 && r.Width <= 45)
						.Where(r => r.Height >= 6 && r.Height <= 70)
						.Where(r => r.Width >= r.Height * 0.10)
						.OrderBy(r => r.X)
						.ToList();
				}
			}

			using (Graphics g = Graphics.FromImage(source))
			using (Pen pen = new Pen(overlayColor, 3))
			using (Font font = new Font("Arial", 30, FontStyle.Bold))
			using (Brush brush = new SolidBrush(overlayColor))
			{
				g.DrawRectangle(pen, dateTextRect);

				g.TranslateTransform(dateTextRect.X - 5, dateTextRect.Y + dateTextRect.Height);
				g.RotateTransform(-90);
				g.DrawString(readDate, font, brush, 0, 0);
				g.ResetTransform();

				using (Font digitFont = new Font("Arial", 18, FontStyle.Bold))
				{
					for (int i = 0; i < charBoxes.Count; i++)
					{
						Rectangle box = charBoxes[i];
						Rectangle originalBox = new Rectangle(
							dateTextRect.X + box.X,
							dateTextRect.Y + box.Y,
							box.Width,
							box.Height);

						bool digitMatch =
							i < readChars.Length &&
							i < expectedChars.Length &&
							readChars[i] == expectedChars[i];

						Color digitColor = digitMatch ? Color.Lime : Color.Red;

						using (Pen digitPen = new Pen(digitColor, 2))
						using (Brush digitBrush = new SolidBrush(digitColor))
						{
							g.DrawRectangle(digitPen, originalBox);

							string ch = i < readChars.Length ? readChars[i].ToString() : "?";

							g.TranslateTransform(originalBox.X - 5, originalBox.Y + originalBox.Height);
							g.RotateTransform(-90);
							g.DrawString(ch, digitFont, digitBrush, 0, 0);
							g.ResetTransform();
						}
					}
				}
			}

			Image old = pictureBoxFrame.Image;
			pictureBoxFrame.Image = source;
			old?.Dispose();
		}

		// ═══════════════════════════════════════
		// 공통 — 로그 / 검사율
		// ═══════════════════════════════════════
		private void UpdateInspectionRate()
		{
			int rate = _totalInspectionCount > 0
				? (int)Math.Round((_okInspectionCount / (double)_totalInspectionCount) * 100.0)
				: 0;

			lblInspectionRate.Text = rate + "%";
			lblInspectionCount.Text = "총 검사 개수";
			lblInspectionSummary.Text = $"{_okInspectionCount}/{_totalInspectionCount}";
		}

		private void AddLogItem(string result, string reason,
			string date, string barcode, Color color)
		{
			ListViewItem item = new ListViewItem(result);
			item.SubItems.Add(DateTime.Now.ToString("HH:mm:ss"));
			item.SubItems.Add(reason);
			item.SubItems.Add(date);
			item.SubItems.Add(barcode);
			item.ForeColor = color;
			lvLogs.Items.Insert(0, item);
		}
	}
}