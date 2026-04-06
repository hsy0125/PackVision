using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PackVisionApp.Core;
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
		private readonly InspectStage _inspectStage;

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

		// 같은 후보가 연속으로 나와야 확정 (오버레이용). 12는 너무 까다로워서 4로 낮춤.
		private const int StableFramesToCommit = 4;

		// 큰 OK/NOK 라벨만 연속 몇 번 같을 때만 바꿈 (숫자 작을수록 화면이 빨리 따라감)
		private const int JudgementHysteresisFrames = 3;

		// 최근 N번 읽기 다수결 (판정용). SimpleMajorityBuffer 참고.
		private readonly SimpleMajorityBuffer _barcodeReadVote = new SimpleMajorityBuffer(5);
		private readonly SimpleMajorityBuffer _dateReadVote = new SimpleMajorityBuffer(5);
		private bool _judgementHysteresisPrimed;
		private bool _lastAppliedOverallOk;
		private int _verdictFlipStreak;
		private bool _verdictFlipTowardOk;

		// 산업용 UI 팔레트 (다크 베이스 + 네온 시안)
		private static readonly Color UiBg = Color.FromArgb(0x1E, 0x22, 0x2D);
		private static readonly Color UiSurface = Color.FromArgb(0x28, 0x2E, 0x3C);
		private static readonly Color UiDeep = Color.FromArgb(0x15, 0x18, 0x21);
		private static readonly Color UiAccent = Color.FromArgb(0x00, 0xFF, 0xCC);
		private static readonly Color UiAccentGlow = Color.FromArgb(0x00, 0xD4, 0xB0);
		private static readonly Color UiDanger = Color.FromArgb(0xFF, 0x44, 0x66);
		private static readonly Color UiDangerDark = Color.FromArgb(0x55, 0x22, 0x2C);
		private static readonly Color UiText = Color.FromArgb(0xE8, 0xEC, 0xF2);
		private static readonly Color UiMuted = Color.FromArgb(0x9A, 0xA4, 0xB8);
		private const int ImagePanelFramePx = 2;
		/// <summary>이미지 뷰 좌상단(검은 표시 영역 모서리)에 붙이는 여백.</summary>
		private const int ImageOverlayCornerPad = 10;

		/// <summary>이미지 뷰 좌상단 OK/NOK·검사율은 PaintOverlay로만 그립니다. (라벨은 UserControl 아래로 깔려 안 보이는 경우가 있음)</summary>
		private string _imageHudVerdict = "대기";
		private Color _imageHudVerdictColor = UiMuted;
		private string _imageHudRatePercent = "0%";
		private const float ImageHudFontPt = 60F;

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

			// 판정용: 확정(stable)이 있으면 그 값만 사용. 없으면 비워 두고 판정은 보류(히스테리시스·표시와 분리)
			stableBarcodeOut = _stableBarcodeValue ?? string.Empty;
			stableDateOut = _stableDateValue ?? string.Empty;
		}
	}

		// ═══════════════════════════════════════
		// 생성자
		// ═══════════════════════════════════════
		public MainForm()
		{
			InitializeComponent();

			imageViewCtrl.PaintOverlay += pbCamera_Paint;

			_inspectStage = new InspectStage(_cameraMgr);
			_inspectStage.RunInspectSync = ExecuteInspectFromGrab;
			FormClosing += (_, _) => _inspectStage.Dispose();

			// 기본값 세팅
			txtDate.Text = "27-02-22 A2 F2";
			txtBarcode.Text = "8 801062 628476";

			// 카메라 이벤트
			_cameraMgr.FrameUpdated += OnFrameUpdated;
			btnDateRoi.Click += btnDateRoi_Click;
			btnBarcodeRoi.Click += btnBarcodeRoi_Click;

			// 디버그 클릭 좌표 (줌·팬 반영)
			imageViewCtrl.MouseClick += (s, e) =>
			{
				if (!imageViewCtrl.HasImage) return;
				if (!imageViewCtrl.TryClientPointToImage(e.Location, out System.Drawing.Point imgPt))
				{
					this.Text = "이미지 영역 밖 클릭";
					return;
				}

				float imgW = imageViewCtrl.ImagePixelWidth;
				float imgH = imageViewCtrl.ImagePixelHeight;
				float ratioX = imgPt.X / imgW;
				float ratioY = imgPt.Y / imgH;
				this.Text = $"X:{imgPt.X} Y:{imgPt.Y} | ratioX:{ratioX:F2} ratioY:{ratioY:F2}";
			};

			UpdateInspectionRate();
			ApplyUiTheme();
		}

		private void ApplyUiTheme()
		{
			BackColor = UiBg;
			ForeColor = UiText;

			panel1.BackColor = UiSurface;
			panelBottom.BackColor = UiBg;

			label1.ForeColor = UiMuted;
			label2.ForeColor = UiMuted;

			_imagePanel.BackColor = UiAccent;
			imageViewCtrl.BackColor = UiDeep;

			panelStatus.BackColor = UiSurface;
			panelLog.BackColor = UiSurface;
			lvLogs.BackColor = UiDeep;
			lvLogs.ForeColor = UiText;
			lvLogs.BorderStyle = BorderStyle.FixedSingle;

			lblInspectionSummary.ForeColor = UiMuted;
			lblInspectionCount.ForeColor = UiText;
			lblInspectionRate.ForeColor = UiAccent;
			lblInspectionRate.Font = new Font("맑은 고딕", 44F, FontStyle.Bold, GraphicsUnit.Point);

			// 좌상단 판정·검사율은 ImageViewCtrl.PaintOverlay에서 그림 (WinForms에서 라벨이 뷰어에 가려짐)
			_imageHudVerdict = "대기";
			_imageHudVerdictColor = UiMuted;
			lblResult.Visible = false;
			lblImageAccuracy.Visible = false;
			imageViewCtrl.Invalidate();

			// 가동: 시안 솔리드 / 정지: 다크 레드 악센트
			StyleIndustrialSolidButton(btnRun, UiAccentGlow, Color.FromArgb(12, 20, 24), UiAccent);
			StyleIndustrialSolidButton(btnStop, UiDangerDark, Color.FromArgb(255, 180, 190), UiDanger);
			StyleIndustrialSolidButton(btnInspect, Color.FromArgb(0, 188, 155), Color.FromArgb(10, 18, 22), UiAccent);
			StyleIndustrialOutlineButton(btnDateRoi, UiAccent, UiAccent, UiSurface);
			StyleIndustrialOutlineButton(btnBarcodeRoi, UiAccent, UiAccent, UiSurface);
			StyleIndustrialOutlineButton(btnDate, UiAccentGlow, UiText, UiDeep);
			StyleIndustrialOutlineButton(btnBarcode, UiAccentGlow, UiText, UiDeep);
			StyleIndustrialOutlineButton(btnLogReset, UiAccentGlow, UiText, UiDeep);

			txtDate.BackColor = UiDeep;
			txtDate.ForeColor = UiText;
			txtDate.BorderStyle = BorderStyle.FixedSingle;
			txtBarcode.BackColor = UiDeep;
			txtBarcode.ForeColor = UiText;
			txtBarcode.BorderStyle = BorderStyle.FixedSingle;

			menuStrip1.BackColor = UiDeep;
			menuStrip1.ForeColor = UiText;
			menuStrip1.RenderMode = ToolStripRenderMode.System;
			foreach (ToolStripItem item in menuStrip1.Items)
				ApplyMenuItemColors(item);

			lblDebug.ForeColor = UiMuted;
			lblDebug.BackColor = UiBg;
		}

		private static void ApplyMenuItemColors(ToolStripItem item)
		{
			item.ForeColor = UiText;
			item.BackColor = UiDeep;
			if (item is ToolStripDropDownItem drop)
			{
				foreach (ToolStripItem sub in drop.DropDownItems)
					ApplyMenuItemColors(sub);
			}
		}

		private static void StyleIndustrialOutlineButton(Button b, Color border, Color fore, Color back)
		{
			b.UseVisualStyleBackColor = false;
			b.FlatStyle = FlatStyle.Flat;
			b.FlatAppearance.BorderSize = 2;
			b.FlatAppearance.BorderColor = border;
			b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, 0.12f);
			b.FlatAppearance.MouseDownBackColor = ControlPaint.Light(back, 0.22f);
			b.BackColor = back;
			b.ForeColor = fore;
			b.Cursor = Cursors.Hand;
			b.Font = new Font("맑은 고딕", 9.75f, FontStyle.Bold, GraphicsUnit.Point);
		}

		private static void StyleIndustrialSolidButton(Button b, Color fill, Color fore, Color border)
		{
			b.UseVisualStyleBackColor = false;
			b.FlatStyle = FlatStyle.Flat;
			b.FlatAppearance.BorderSize = 2;
			b.FlatAppearance.BorderColor = border;
			b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(fill, 0.18f);
			b.FlatAppearance.MouseDownBackColor = ControlPaint.Light(fill, 0.28f);
			b.BackColor = fill;
			b.ForeColor = fore;
			b.Cursor = Cursors.Hand;
			b.Font = new Font("맑은 고딕", 10f, FontStyle.Bold, GraphicsUnit.Point);
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
			int statusPanelW = 272;

			panel1.Left = margin;
			panel1.Top = menuH + margin;
			panel1.Width = formW - margin * 2;
			panel1.Height = topPanelH;

			// 입력 행 먼저 계산 → 가동/정지/검사 버튼을 텍스트 행과 세로 중앙 정렬
			int actionGap = 10;
			int roiGap = 6;
			int innerG = 10;
			int leftX = 11;
			int rowY = 32;
			int xRight = panel1.Width - margin;
			int xRoi = xRight - btnBarcodeRoi.Width;
			int maxBarRight = xRoi - actionGap - btnInspect.Width - actionGap - btnStop.Width - actionGap - btnRun.Width - actionGap;

			int btnDW = btnDate.Width;
			int btnBW = btnBarcode.Width;
			int fixedParts = leftX + btnDW + btnBW + innerG * 3;
			int pairCapacity = maxBarRight - fixedParts;
			int fieldW = Math.Max(100, pairCapacity / 2);
			while (leftX + fieldW + innerG + btnDW + innerG + fieldW + innerG + btnBW > maxBarRight && fieldW > 72)
				fieldW--;

			label2.Left = leftX;
			label2.Top = 4;

			int txtH = txtDate.Height;
			txtDate.SetBounds(leftX, rowY, fieldW, txtH);
			btnDate.SetBounds(txtDate.Right + innerG, rowY, btnDW, txtH);

			label1.Left = btnDate.Right + innerG;
			label1.Top = 4;

			txtBarcode.SetBounds(label1.Left, rowY, fieldW, txtH);
			btnBarcode.SetBounds(txtBarcode.Right + innerG, rowY, btnBW, txtH);

			int xRun = maxBarRight + actionGap;
			btnRun.Left = xRun;
			btnStop.Left = xRun + btnRun.Width + actionGap;
			btnInspect.Left = btnStop.Left + btnStop.Width + actionGap;

			int actionTop = rowY + (txtH - btnRun.Height) / 2;
			if (actionTop < 6) actionTop = 6;
			btnRun.Top = actionTop;
			btnStop.Top = actionTop;
			btnInspect.Top = actionTop;

			int roiStackH = btnBarcodeRoi.Height + roiGap + btnDateRoi.Height;
			int roiTop = Math.Max(6, rowY + (txtH - roiStackH) / 2);
			btnBarcodeRoi.Left = xRoi;
			btnBarcodeRoi.Top = roiTop;
			btnDateRoi.Left = xRoi;
			btnDateRoi.Top = roiTop + btnBarcodeRoi.Height + roiGap;

			int imagePanelTop = panel1.Bottom + margin;
			int imagePanelH = formH - imagePanelTop - bottomPanelH - margin * 2;

			_imagePanel.Left = margin;
			_imagePanel.Top = imagePanelTop;
			_imagePanel.Width = formW - margin * 2;
			_imagePanel.Height = Math.Max(100, imagePanelH);

			int f = ImagePanelFramePx;
			imageViewCtrl.Left = f;
			imageViewCtrl.Top = f;
			imageViewCtrl.Width = Math.Max(1, _imagePanel.Width - f * 2);
			imageViewCtrl.Height = Math.Max(1, _imagePanel.Height - f * 2);

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

			// 하단 상태 패널: 검사율 + 요약 + 로그 리셋
			int psPad = 10;
			int sy = psPad;
			lblInspectionRate.Location = new System.Drawing.Point(psPad, sy);
			lblInspectionSummary.Left = lblInspectionRate.Right + 12;
			lblInspectionSummary.Top = sy + 4;
			sy = Math.Max(lblInspectionRate.Bottom, lblInspectionSummary.Bottom) + 12;
			lblInspectionCount.Location = new System.Drawing.Point(psPad, sy);
			sy = lblInspectionCount.Bottom + 14;
			int btnResetH = 40;
			btnLogReset.SetBounds(psPad, sy, Math.Max(120, panelStatus.Width - psPad * 2), btnResetH);

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
			lvLogs.Columns.Add("결과", 80);
			lvLogs.Columns.Add("시각", 100);
			lvLogs.Columns.Add("사유", 100);
			lvLogs.Columns.Add("날짜", 120);
			lvLogs.Columns.Add("바코드", 180);
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

			imageViewCtrl.LoadBitmap((Bitmap)bmp.Clone());

			TryApplyPersistedRoiFromFrame(bmp);

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
							imageViewCtrl.Invalidate();
						}));
					});
				}
				else
				{
					UpdateTrackedRois();
				}
			}

			imageViewCtrl.Invalidate();
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
				imageViewCtrl.Invalidate();
			}
		}

		private void pbCamera_MouseUp(object sender, MouseEventArgs e)
		{
			if (e.Button != MouseButtons.Left) return;

			_isSelecting = false;

			if (_selectionRect.Width > 10 && _selectionRect.Height > 10
				&& imageViewCtrl.HasImage)
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

						using (Bitmap? currentImg = imageViewCtrl.CloneDisplayBitmap())
						{
							if (currentImg != null)
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

						try
						{
							if (imageViewCtrl.HasImage)
							{
								RoiTeachPersistence.Save(
									_packageImageRect,
									_inspectionMgr.DateRatioRect,
									_inspectionMgr.BarcodeRatioRect,
									imageViewCtrl.ImagePixelWidth,
									imageViewCtrl.ImagePixelHeight);
							}
						}
						catch (Exception ex)
						{
							Debug.WriteLine("[ROI save] " + ex.Message);
						}

						// ROI가 모두 잡히면 검사 버튼 없이도 연속 검사 자동 시작(카메라가 RUN 중일 때)
						TryStartContinuousInspect(showErrorDialogs: false, requirePreviewImage: false);
					}

					_packageScreenRect = ImageRectToScreenRect(_packageImageRect);
					_dateScreenRect = ImageRectToScreenRect(_dateImageRect);
					_barcodeScreenRect = ImageRectToScreenRect(_barcodeImageRect);
				}
			}

			_selectionRect = Rectangle.Empty;
			imageViewCtrl.Invalidate();
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
			DrawImageHud(e.Graphics);
		}

		private void DrawImageHud(Graphics g)
		{
			TextRenderingHint prev = g.TextRenderingHint;
			g.TextRenderingHint = TextRenderingHint.AntiAlias;
			try
			{
				float pad = ImageOverlayCornerPad;
				using Font font = new Font("맑은 고딕", ImageHudFontPt, FontStyle.Bold, GraphicsUnit.Point);
				string verdict = string.IsNullOrEmpty(_imageHudVerdict) ? "대기" : _imageHudVerdict;
				float y = pad;
				using (Brush brush = new SolidBrush(_imageHudVerdictColor))
					g.DrawString(verdict, font, brush, pad, y);
				SizeF verdictH = g.MeasureString(verdict, font);
				y += verdictH.Height + 4f;
				string rate = string.IsNullOrEmpty(_imageHudRatePercent) ? "0%" : _imageHudRatePercent;
				using (Brush brush = new SolidBrush(UiAccent))
					g.DrawString(rate, font, brush, pad, y);
			}
			finally
			{
				g.TextRenderingHint = prev;
			}
		}

		private Rectangle ScreenRectToImageRect(Rectangle screenRect)
		{
			if (!imageViewCtrl.HasImage) return Rectangle.Empty;
			return imageViewCtrl.ClientRectToImageRect(screenRect);
		}

		private Rectangle ImageRectToScreenRect(Rectangle imageRect)
		{
			if (!imageViewCtrl.HasImage) return Rectangle.Empty;
			return imageViewCtrl.ImageRectToClientRect(imageRect);
		}

		/// <summary>저장된 ROI가 있으면 첫 프레임(또는 이미지 로드)에서 트래커·검사 ROI를 복원합니다. 수동 티칭 중이면 건드리지 않습니다.</summary>
		private void TryApplyPersistedRoiFromFrame(Bitmap frameSource)
		{
			if (frameSource == null || frameSource.Width <= 0 || frameSource.Height <= 0) return;
			if (_packageTracker.IsTracking) return;
			if (!RoiTeachPersistence.TryLoad(out RoiTeachPersistence.Snapshot? snap) || snap is null) return;

			int w = frameSource.Width;
			int h = frameSource.Height;
			Rectangle pkg = RoiTeachPersistence.DenormalizePackage(snap, w, h);
			if (pkg.Width < 5 || pkg.Height < 5) return;

			RectangleF dateRatio = RoiTeachPersistence.DateRatio(snap);
			RectangleF barcodeRatio = RoiTeachPersistence.BarcodeRatio(snap);
			var mapper = new RoiMapper();
			Rectangle dateAbs = Rectangle.Intersect(mapper.RatioToRect(dateRatio, pkg), pkg);
			Rectangle barcodeAbs = Rectangle.Intersect(mapper.RatioToRect(barcodeRatio, pkg), pkg);
			if (dateAbs.Width < 5 || dateAbs.Height < 5 || barcodeAbs.Width < 5 || barcodeAbs.Height < 5) return;

			using (Bitmap init = (Bitmap)frameSource.Clone())
				_packageTracker.SetTarget(init, pkg);

			_packageTracker.SetDateRoi(dateAbs);
			_packageTracker.SetBarcodeRoi(barcodeAbs);
			_inspectionMgr.SetRoiRatios(pkg, dateAbs, barcodeAbs);

			_packageImageRect = pkg;
			_dateImageRect = dateAbs;
			_barcodeImageRect = barcodeAbs;
			_freezeTaughtRois = true;
			_hasPrevTrackedRects = true;
			_prevDateImageRect = dateAbs;
			_prevBarcodeImageRect = barcodeAbs;

			if (imageViewCtrl.HasImage)
			{
				_packageScreenRect = ImageRectToScreenRect(pkg);
				_dateScreenRect = ImageRectToScreenRect(dateAbs);
				_barcodeScreenRect = ImageRectToScreenRect(barcodeAbs);
			}

			imageViewCtrl.Invalidate();

			TryStartContinuousInspect(showErrorDialogs: false, requirePreviewImage: false);
		}

		private void UpdateTrackedRois()
		{
			if (!_packageTracker.IsTracking) return;
			if (_freezeTaughtRois) return;

			_packageImageRect = ClampToFrame(_packageTracker.GetPackageRect(),
				imageViewCtrl.ImagePixelWidth,
				imageViewCtrl.ImagePixelHeight);

			if (_inspectionMgr.DateRatioRect != RectangleF.Empty)
			{
				Rectangle nextDate = ClampToFrame(
					_inspectionMgr.GetDateRect(_packageImageRect),
					imageViewCtrl.ImagePixelWidth,
					imageViewCtrl.ImagePixelHeight);
				_dateImageRect = _hasPrevTrackedRects
					? SmoothRect(_prevDateImageRect, nextDate, 0.35f)
					: nextDate;
			}

			if (_inspectionMgr.BarcodeRatioRect != RectangleF.Empty)
			{
				Rectangle nextBarcode = ClampToFrame(
					_inspectionMgr.GetBarcodeRect(_packageImageRect),
					imageViewCtrl.ImagePixelWidth,
					imageViewCtrl.ImagePixelHeight);
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
		// 자동 검사 — InspectStage: Transfer 완료 → 동기 검사(워커) → UI Invoke (촬영↔검사 싱크)
		// ═══════════════════════════════════════
		private void ExecuteInspectFromGrab(Bitmap currentFrame)
		{
			if (currentFrame == null)
			{
				InspectFlowLog.Write("INSPECT_SKIP", "null frame");
				return;
			}

			if (!_isAutoInspecting)
			{
				InspectFlowLog.Write("INSPECT_SKIP", "_isAutoInspecting=false");
				return;
			}

			if (string.IsNullOrWhiteSpace(_expectedDate) ||
				string.IsNullOrWhiteSpace(_expectedBarcode))
			{
				InspectFlowLog.Write("INSPECT_SKIP", "expected date/barcode empty");
				return;
			}

			if (!_packageTracker.IsTracking)
			{
				InspectFlowLog.Write("INSPECT_SKIP", "IsTracking=false");
				return;
			}

			if (!_packageTracker.IsDateRoiSet || !_packageTracker.IsBarcodeRoiSet)
			{
				InspectFlowLog.Write("INSPECT_SKIP", "date or barcode ROI not set on tracker");
				return;
			}

			try
			{

			Rectangle dateRectForRead = _dateImageRect;
			Rectangle barcodeRectForRead = _barcodeImageRect;

			Rectangle dateRectForDraw = _dateImageRect;
			Rectangle barcodeRectForDraw = _barcodeImageRect;

			dateRectForRead = ClampToFrame(dateRectForRead, currentFrame.Width, currentFrame.Height);
			barcodeRectForRead = ClampToFrame(barcodeRectForRead, currentFrame.Width, currentFrame.Height);

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

			BarcodeResult barcodeResult;
			DateResult dateResult;
			List<Rectangle> barcodeBlobRects;
			List<Rectangle> dateBlobRects;
			string stableBarcodeValue = string.Empty;
			string stableDateValue = string.Empty;
			string barcodeCandidateValue = string.Empty;
			string dateCandidateValue = string.Empty;

			barcodeResult = _barcodeReader.ReadBarcode(currentFrame, barcodeRectForRead);
			dateResult = _dateReader.ReadDate(currentFrame, dateRectForRead);
			barcodeCandidateValue = barcodeResult.Success ? barcodeResult.Value : string.Empty;
			dateCandidateValue = dateResult.Success ? dateResult.Value : string.Empty;

			UpdateStableOcrValues(
				barcodeCandidateValue,
				dateCandidateValue,
				out stableBarcodeValue,
				out stableDateValue);

			if (barcodeResult.Success && !string.IsNullOrWhiteSpace(barcodeCandidateValue))
				_barcodeReadVote.Add(GetBarcodeNorm(barcodeCandidateValue), barcodeCandidateValue);
			if (dateResult.Success && !string.IsNullOrWhiteSpace(dateCandidateValue))
				_dateReadVote.Add(GetDateNorm(dateCandidateValue), dateCandidateValue);

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

			// 판정용 문자열: ① 다수결 → ② stable → ③ 이번 프레임 (성공한 읽기만 버퍼에 쌓임)
			string judgeBarcode = barcodeCandidateValue;
			if (_barcodeReadVote.TryGetMajorityRaw(out string majBarcode))
				judgeBarcode = majBarcode;
			else if (!string.IsNullOrEmpty(stableBarcodeValue))
				judgeBarcode = stableBarcodeValue;

			string judgeDate = dateCandidateValue;
			if (_dateReadVote.TryGetMajorityRaw(out string majDate))
				judgeDate = majDate;
			else if (!string.IsNullOrEmpty(stableDateValue))
				judgeDate = stableDateValue;

			// 이번 촬영에서 디코드가 실패했으면, 예전 다수결 값으로는 OK 금지 (빈 컨베이어인데 OK 나오는 버그 방지)
			string barcodeForVerdict = barcodeResult.Success ? judgeBarcode : string.Empty;
			string dateForVerdict = dateResult.Success ? judgeDate : string.Empty;

			string logLine = $"[{DateTime.Now:HH:mm:ss.fff}] " +
							 $"DateROI:{dateRectForRead} | BarcodeROI:{barcodeRectForRead} | " +
				$"바코드:{barcodeResult.Success}/{barcodeCandidateValue}/{barcodeResult.FailReason} | " +
				$"날짜:{dateResult.Success}/{dateCandidateValue}/{dateResult.FailReason} | " +
				$"stableB:{stableBarcodeValue} | stableD:{stableDateValue} | judgeB:{judgeBarcode} | judgeD:{judgeDate} | " +
				$"verdictB:{barcodeForVerdict} | verdictD:{dateForVerdict}";

			System.IO.File.AppendAllText(
				System.IO.Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
					"debug_log.txt"),
				logLine + "\n");

			string debugMsg =
				$"바코드:{barcodeResult.Success}({barcodeCandidateValue}) | 판정에씀:{barcodeForVerdict} | 다수결:{judgeBarcode} | " +
				$"날짜:{dateResult.Success}({dateCandidateValue}) | 판정에씀:{dateForVerdict} | 다수결:{judgeDate} | " +
				$"voteB:[{_barcodeReadVote.FormatKeysForDebug()}] voteD:[{_dateReadVote.FormatKeysForDebug()}]";

			InspectionResult result = _inspectionMgr.Inspect(
				_expectedBarcode, barcodeForVerdict,
				_expectedDate, dateForVerdict,
				true);

			InspectFlowLog.Write("INSPECT_VERDICT",
				$"overall={(result.IsOverallOk ? "OK" : "NOK")} bcOk={result.IsBarcodeOk} dtOk={result.IsDateOk} " +
				$"jB={barcodeForVerdict} jD={dateForVerdict} expB={_expectedBarcode} expD={_expectedDate} " +
				$"readOk_b={barcodeResult.Success} readOk_d={dateResult.Success}");

			if (IsDisposed)
				return;

			void ApplyUi()
			{
				lblDebug.Text =
					debugMsg +
					$" | 처리프레임:{_inspectStage.AcceptedForInspectCount} 드롭(바쁨):{_inspectStage.DroppedWhileBusyCount}";

				UpdateLiveOverlay(
					barcodeRectForDraw,
					dateRectForDraw,
					barcodeForVerdict,
					dateForVerdict,
					result.IsBarcodeOk,
					result.IsDateOk,
					barcodeBlobRects,
					dateBlobRects);

				lock (_frameLock)
				{
					_latestFrame?.Dispose();
					_latestFrame = (Bitmap)currentFrame.Clone();
				}

				imageViewCtrl.LoadBitmap((Bitmap)currentFrame.Clone());

				imageViewCtrl.Invalidate();

				RecordInspectionCycle(result);
				UpdateVerdictLabelWithHysteresis(result);
				InspectFlowLog.Write("UI_APPLY_DONE", "RecordInspectionCycle+overlay");
			}

			InspectFlowLog.Write("UI_INVOKE", InvokeRequired ? "Invoke(ApplyUi)" : "ApplyUi direct");
			if (InvokeRequired)
				Invoke(ApplyUi);
			else
				ApplyUi();
			}
			catch (Exception ex)
			{
				if (IsDisposed) return;
				void SetErr() => lblDebug.Text = "검사 오류: " + ex.Message;
				if (InvokeRequired)
					Invoke(SetErr);
				else
					SetErr();
			}
		}

		// ═══════════════════════════════════════
		// 공통 유틸
		// ═══════════════════════════════════════
		private bool GetZoomTransform(out float scale, out float offsetX, out float offsetY)
		{
			return imageViewCtrl.TryGetZoomTransform(out scale, out offsetX, out offsetY);
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

		/// <summary>검사 1회마다 호출: 건수·리스트·CSV는 항상 반영 (히스테리시스 없음).</summary>
		private void RecordInspectionCycle(InspectionResult result)
		{
			if (result == null) return;

			_totalInspectionCount++;

			if (result.IsOverallOk)
			{
				_okInspectionCount++;
				AddLogItem("OK", "-", result.ActualDate, result.ActualBarcode, UiAccentGlow);
			}
			else
			{
				AddLogItem("NOK", result.FailReasonText,
					result.ActualDate, result.ActualBarcode, UiDanger);
			}

			_csvLogManager.SaveLog(result);
			UpdateInspectionRate();
		}

		/// <summary>이미지 패널 좌상단 OK/NOK (30pt, PaintOverlay).</summary>
		private void SetOverlayVerdictLabel(bool overallOk)
		{
			if (overallOk)
			{
				_imageHudVerdict = "OK";
				_imageHudVerdictColor = UiAccent;
			}
			else
			{
				_imageHudVerdict = "NOK";
				_imageHudVerdictColor = UiDanger;
			}

			imageViewCtrl.Invalidate();
		}

		private void ResetJudgementHysteresis()
		{
			_judgementHysteresisPrimed = false;
			_verdictFlipStreak = 0;
		}

		/// <summary>상단 큰 OK/NOK만 연속 프레임 일치 시 변경 (로그·건수와 별개).</summary>
		private void UpdateVerdictLabelWithHysteresis(InspectionResult result)
		{
			if (result == null) return;

			if (!_judgementHysteresisPrimed)
			{
				_judgementHysteresisPrimed = true;
				_lastAppliedOverallOk = result.IsOverallOk;
				_verdictFlipStreak = 1;
				_verdictFlipTowardOk = result.IsOverallOk;
				SetOverlayVerdictLabel(result.IsOverallOk);
				return;
			}

			if (result.IsOverallOk == _lastAppliedOverallOk)
			{
				_verdictFlipStreak = 0;
				return;
			}

			if (result.IsOverallOk == _verdictFlipTowardOk)
				_verdictFlipStreak++;
			else
			{
				_verdictFlipTowardOk = result.IsOverallOk;
				_verdictFlipStreak = 1;
			}

			if (_verdictFlipStreak < JudgementHysteresisFrames)
				return;

			_lastAppliedOverallOk = result.IsOverallOk;
			_verdictFlipStreak = 0;
			SetOverlayVerdictLabel(result.IsOverallOk);
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
			if (!imageViewCtrl.HasImage)
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
		// 연속 검사 시작(조건 만족 시). 프레임마다 InspectStage → 이전 검사 종료 후 다음 프레임 검사.
		// ═══════════════════════════════════════
		private bool TryStartContinuousInspect(bool showErrorDialogs, bool requirePreviewImage)
		{
			_expectedDate = txtDate.Text.Trim();
			_expectedBarcode = txtBarcode.Text.Trim();

			if (string.IsNullOrWhiteSpace(_expectedDate) ||
				string.IsNullOrWhiteSpace(_expectedBarcode))
			{
				if (showErrorDialogs)
					MessageBox.Show("먼저 기준 날짜와 기준 바코드를 입력하세요.", "연속 검사");
				return false;
			}

			if (!_cameraMgr.IsStreaming)
			{
				if (showErrorDialogs)
					MessageBox.Show("먼저 카메라(RUN)를 실행하세요.", "연속 검사");
				return false;
			}

			if (requirePreviewImage && !imageViewCtrl.HasImage)
			{
				if (showErrorDialogs)
					MessageBox.Show("카메라 프레임이 들어올 때까지 잠시 후 다시 눌러 주세요.", "연속 검사");
				return false;
			}

			if (_packageImageRect == Rectangle.Empty ||
				_dateImageRect == Rectangle.Empty ||
				_barcodeImageRect == Rectangle.Empty)
			{
				if (showErrorDialogs)
					MessageBox.Show("포장지·날짜·바코드 ROI를 모두 지정하세요.", "연속 검사");
				return false;
			}

			if (!_packageTracker.IsTracking ||
				!_packageTracker.IsDateRoiSet ||
				!_packageTracker.IsBarcodeRoiSet)
			{
				if (showErrorDialogs)
					MessageBox.Show("포장지 ROI로 트래킹을 먼저 잡은 뒤 날짜/바코드 ROI를 지정하세요.", "연속 검사");
				return false;
			}

			if (_inspectStage.IsInspectCycleActive)
				return true;

			_inspectionMgr.SetRoiRatios(
				_packageImageRect, _dateImageRect, _barcodeImageRect);

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

			_barcodeReadVote.Clear();
			_dateReadVote.Clear();

			ResetJudgementHysteresis();
			_isAutoInspecting = true;
			_inspectStage.StartInspectCycle();
			return true;
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
				// 이미 ROI·기준값이 있으면 검사 버튼 없이 연속 검사 시작
				TryStartContinuousInspect(showErrorDialogs: false, requirePreviewImage: false);
				return;
			}
			MessageBox.Show("카메라 연결 실패");
		}

		private async void btnStop_Click(object sender, EventArgs e)
		{
			btnRun.Enabled = true;
			btnStop.Enabled = false;

			_isAutoInspecting = false;
			_inspectStage.StopInspectCycle();
			_barcodeReadVote.Clear();
			_dateReadVote.Clear();
			ResetJudgementHysteresis();

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

			imageViewCtrl.ClearImage();

			imageViewCtrl.Invalidate();
		}

		private void btnInspect_Click(object sender, EventArgs e)
		{
			bool wasAlreadyRunning = _inspectStage.IsInspectCycleActive;
			if (!TryStartContinuousInspect(showErrorDialogs: true, requirePreviewImage: true))
				return;

			if (!wasAlreadyRunning)
			{
				MessageBox.Show(
					"연속 검사를 시작했습니다.\n검사가 끝날 때마다 다음 프레임으로 자동 이어집니다.\n중지: STOP",
					"연속 검사");
			}
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
						using Mat loaded = Cv2.ImRead(ofd.FileName, ImreadModes.Color);
						if (loaded.Empty())
						{
							MessageBox.Show("이미지를 읽을 수 없습니다.");
							return;
						}

						_originalFrame?.Dispose();
						_originalFrame = BitmapConverter.ToBitmap(loaded.Clone());

						imageViewCtrl.LoadMat(loaded);
						if (_originalFrame != null)
							TryApplyPersistedRoiFromFrame(_originalFrame);
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
			using (Bitmap? snap = imageViewCtrl.CloneDisplayBitmap())
			{
				if (snap == null)
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
						snap.Save(sfd.FileName);
						MessageBox.Show("이미지 저장 완료");
					}
					catch (Exception ex)
					{
						MessageBox.Show("이미지 저장 실패: " + ex.Message);
					}
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
			if (!imageViewCtrl.HasImage || _originalFrame == null)
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

			using (Bitmap? viewSnap = imageViewCtrl.CloneDisplayBitmap())
			{
				if (viewSnap != null)
				{
					Bitmap current = new Bitmap(viewSnap);
					ProcessDateOverlay(current, readDate, _expectedDate);
				}
			}

			if (result.IsOverallOk)
			{
				_okInspectionCount++;
				SetOverlayVerdictLabel(true);
				AddLogItem("OK", "-", result.ActualDate, result.ActualBarcode, UiAccentGlow);
			}
			else
			{
				SetOverlayVerdictLabel(false);
				AddLogItem("NOK", result.FailReasonText,
					result.ActualDate, result.ActualBarcode, UiDanger);
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

					imageViewCtrl.LoadBitmap(debugImage);
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

			imageViewCtrl.LoadBitmap(source);
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
			_imageHudRatePercent = rate + "%";
			imageViewCtrl.Invalidate();
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

		private void btnLogReset_Click(object sender, EventArgs e)
		{
			var rows = new List<string?[]>(lvLogs.Items.Count);
			foreach (ListViewItem it in lvLogs.Items)
			{
				string t0 = it.Text ?? string.Empty;
				string t1 = it.SubItems.Count > 1 ? it.SubItems[1].Text : string.Empty;
				string t2 = it.SubItems.Count > 2 ? it.SubItems[2].Text : string.Empty;
				string t3 = it.SubItems.Count > 3 ? it.SubItems[3].Text : string.Empty;
				string t4 = it.SubItems.Count > 4 ? it.SubItems[4].Text : string.Empty;
				rows.Add(new[] { t0, t1, t2, t3, t4 });
			}

			string? savedPath = _csvLogManager.SaveUiLogSnapshot(rows);
			lvLogs.Items.Clear();

			if (savedPath != null)
				MessageBox.Show(this, $"로그를 저장했습니다.\n{savedPath}", "로그 리셋",
					MessageBoxButtons.OK, MessageBoxIcon.Information);
			else if (rows.Count == 0)
				MessageBox.Show(this, "저장할 로그가 없습니다.", "로그 리셋",
					MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
	}
}