using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using PackVisionApp.Managers;
using PackVisionApp.Models;
using PackVisionApp.Service;
using PackVisionApp.Services;
using System.IO;
using System.Linq;

namespace PackVisionApp.UI
{
	public partial class MainForm : Form
	{
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

		// "PACKAGE", "DATE", "BARCODE"
		// 필드 추가
		private InspectionManager _inspectionManager = new InspectionManager();
		private CsvLogManager _csvLogManager = new CsvLogManager();
		private Bitmap _originalFrame = null;

		// 사용자가 제출한 기준 날짜
		private string _expectedDate = "";

		// 사용자가 제출한 기준 바코드
		private string _expectedBarcode = "";

		// 총 검사 횟수
		private int _totalInspectionCount = 0;

		// OK 판정 횟수
		private int _okInspectionCount = 0;

		public object Global { get; private set; }

		public MainForm()
		{
			InitializeComponent();
			// [디버깅용]
			// 
			pictureBoxFrame.MouseClick += (s, e) =>
			{
				if (_originalFrame == null) return;

				// ── Zoom 모드 letterbox 보정
				float imgW = _originalFrame.Width;
				float imgH = _originalFrame.Height;
				float boxW = pictureBoxFrame.Width;
				float boxH = pictureBoxFrame.Height;

				float scale = Math.Min(boxW / imgW, boxH / imgH);

				float displayW = imgW * scale;
				float displayH = imgH * scale;

				// 이미지가 PictureBox 중앙에 표시되므로 offset 계산
				float offsetX = (boxW - displayW) / 2f;
				float offsetY = (boxH - displayH) / 2f;

				// 클릭 좌표 → 실제 이미지 좌표
				float imgX = (e.X - offsetX) / scale;
				float imgY = (e.Y - offsetY) / scale;

				if (imgX < 0 || imgY < 0 || imgX >= imgW || imgY >= imgH)
				{
					this.Text = "이미지 영역 밖 클릭";
					return;
				}

				float ratioX = imgX / imgW;
				float ratioY = imgY / imgH;
				// [디버깅]
				this.Text = $"X:{(int)imgX} Y:{(int)imgY} | ratioX:{ratioX:F2} ratioY:{ratioY:F2}";
			};
		}
		private void MainForm_Resize(object sender, EventArgs e)
		{
			int margin = 10;
			int formW = this.ClientSize.Width;
			int formH = this.ClientSize.Height;

			int menuH = menuStrip1.Height;           // 메뉴바 높이 (~33)
			int topPanelH = 90;                      // panel1 고정 높이
			int bottomPanelH = 230;                  // panelBottom 고정 높이
			int statusPanelW = 250;                  // panelStatus 고정 너비

			// ── 1. panel1 (상단 입력 영역) ──────────────────────
			panel1.Left = margin;
			panel1.Top = menuH + margin;
			panel1.Width = formW - margin * 2;
			panel1.Height = topPanelH;

			// panel1 내부: RUN / STOP 버튼을 오른쪽 끝으로
			btnRun.Left = panel1.Width - btnStop.Width - btnRun.Width - margin * 2;
			btnStop.Left = panel1.Width - btnStop.Width - margin;

			// ── 2. _imagePanel (이미지 영역) ────────────────────
			int imagePanelTop = panel1.Bottom + margin;
			int imagePanelH = formH - imagePanelTop - bottomPanelH - margin * 2;

			_imagePanel.Left = margin;
			_imagePanel.Top = imagePanelTop;
			_imagePanel.Width = formW - margin * 2;
			_imagePanel.Height = Math.Max(100, imagePanelH);

			// pictureBoxFrame은 _imagePanel 전체를 꽉 채움
			pictureBoxFrame.Left = 0;
			pictureBoxFrame.Top = 0;
			pictureBoxFrame.Width = _imagePanel.Width;
			pictureBoxFrame.Height = _imagePanel.Height;

			// lblResult는 pictureBoxFrame 위에 고정
			lblResult.Left = 20;
			lblResult.Top = 20;

			// ── 3. panelBottom (하단 영역) ──────────────────────
			panelBottom.Left = margin;
			panelBottom.Top = _imagePanel.Bottom + margin;
			panelBottom.Width = formW - margin * 2;
			panelBottom.Height = bottomPanelH;

			// ── 4. panelStatus (왼쪽 상태 패널) ─────────────────
			panelStatus.Left = 0;
			panelStatus.Top = 0;
			panelStatus.Width = statusPanelW;
			panelStatus.Height = panelBottom.Height;

			// ── 5. panelLog (오른쪽 로그 패널) ──────────────────
			panelLog.Left = panelStatus.Right + margin;
			panelLog.Top = 0;
			panelLog.Width = panelBottom.Width - panelStatus.Width - margin;
			panelLog.Height = panelBottom.Height;

			// ── 6. lvLogs (ListView) ─────────────────────────────
			lvLogs.Left = 0;
			lvLogs.Top = 0;
			lvLogs.Width = panelLog.Width - margin;
			lvLogs.Height = panelLog.Height - margin;
		}
		private void MainForm_Load(object sender, EventArgs e)
		{

			// Anchor 충돌 방지 - 코드로 직접 제어하므로 None으로 설정
			lvLogs.Anchor = AnchorStyles.None;
			panel1.Anchor = AnchorStyles.None;
			_imagePanel.Anchor = AnchorStyles.None;
			panelBottom.Anchor = AnchorStyles.None;
			panelLog.Anchor = AnchorStyles.None;
			panelStatus.Anchor = AnchorStyles.None;

			// 기존 컬럼 설정 코드
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

		private void imageOpenToolStripMenuItem_Click(object sender, EventArgs e)
		{
		}

		/// <summary>
		/// 메뉴에서 이미지 열기 클릭 시 실행
		/// 선택한 이미지를 pictureBoxFrame에 표시
		/// </summary>
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
						// 기존 이미지가 있으면 메모리 해제
						pictureBoxFrame.Image?.Dispose();

						Bitmap bmp = new Bitmap(ofd.FileName);

						// 기존 원본 이미지 해제
						_originalFrame?.Dispose();
						_originalFrame = new Bitmap(bmp);

						// 기존 화면 이미지 해제
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

		/// <summary>
		/// 현재 PictureBox에 보이는 이미지 저장
		/// </summary>
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

		/// <summary>
		/// 기준 날짜 제출 버튼
		/// TextBox에 입력된 값을 _expectedDate 에 저장
		/// </summary>
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

		/// <summary>
		/// 기준 바코드 제출 버튼
		/// TextBox에 입력된 값을 _expectedBarcode 에 저장
		/// </summary>
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

		/// <summary>
		/// RUN 버튼 클릭 시 실행
		/// 현재는 더미값으로 판정하고, 나중에 실제 ZXing/OCR 결과로 교체하면 됨
		/// </summary>
		private void btnRun_Click(object sender, EventArgs e)
		{
			if (pictureBoxFrame.Image == null)
			{
				MessageBox.Show("먼저 이미지를 불러오세요.");
				return;
			}

			if (_originalFrame == null)
			{
				MessageBox.Show("원본 이미지가 없습니다.");
				return;
			}

			if (string.IsNullOrWhiteSpace(_expectedDate) || string.IsNullOrWhiteSpace(_expectedBarcode))
			{
				MessageBox.Show("먼저 기준 날짜와 기준 바코드를 제출하세요.");
				return;
			}

			// ── 더미값 (나중에 실제 OCR/ZXing 결과로 교체)
			string readBarcode = "8801062628476";
			string readDate = "27.01.27  B5 F1";
			bool isPrintOk = true;

			// ── 검사 판정
			InspectionResult result = _inspectionManager.Inspect(
				_expectedBarcode, readBarcode,
				_expectedDate, readDate,
				isPrintOk
			);

			_totalInspectionCount++;

			// ── 오버레이용 작업 복사본 생성 (원본은 _originalFrame 유지)
			Bitmap workBitmap = new Bitmap(_originalFrame);

			// ── 바코드 오버레이 (기존 메서드 — workBitmap에 직접 그림)
			ProcessBarcodeOverlay(workBitmap, readBarcode, _expectedBarcode);

			// ── 날짜 오버레이 추가
			// ProcessBarcodeOverlay 내부에서 pictureBoxFrame.Image 를 갱신하므로
			// 날짜는 갱신된 이미지 위에 이어서 그려야 함
			// → workBitmap을 공유하거나, 아래처럼 현재 표시 이미지를 받아서 처리
			if (pictureBoxFrame.Image != null)
			{
				Bitmap current = new Bitmap(pictureBoxFrame.Image);
				ProcessDateOverlay(current, readDate, _expectedDate);
				// ProcessDateOverlay 안에서 pictureBoxFrame.Image 갱신됨
			}

			// ── 결과 표시
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
				AddLogItem("NOK", result.FailReasonText, result.ActualDate, result.ActualBarcode, Color.Red);
			}

			_csvLogManager.SaveLog(result);
			UpdateInspectionRate();
		}

		/// <summary>
		/// 검사율과 총 검사 수 화면 갱신
		/// </summary>
		private void UpdateInspectionRate()
		{
			int rate = 0;

			if (_totalInspectionCount > 0)
			{
				rate = (int)Math.Round((_okInspectionCount / (double)_totalInspectionCount) * 100.0);
			}

			lblInspectionRate.Text = rate + "%";
			lblInspectionCount.Text = "총 검사 개수";
			lblInspectionSummary.Text = $"{_okInspectionCount}/{_totalInspectionCount}";
		}

		/// <summary>
		/// ListView에 로그 한 줄 추가
		/// Result / Time / Reason / Date / Barcode 컬럼에 각각 값 넣음
		/// </summary>
		private void AddLogItem(string result, string reason, string date, string barcode, Color color)
		{
			// 첫 번째 컬럼: Result
			ListViewItem item = new ListViewItem(result);

			// 두 번째 컬럼: 현재 시간
			item.SubItems.Add(DateTime.Now.ToString("HH:mm:ss"));

			// 세 번째 컬럼: 실패 원인
			item.SubItems.Add(reason);

			// 네 번째 컬럼: 읽은 날짜
			item.SubItems.Add(date);

			// 다섯 번째 컬럼: 읽은 바코드
			item.SubItems.Add(barcode);

			// 줄 전체 글자색 지정
			item.ForeColor = color;

			// 가장 위에 삽입
			lvLogs.Items.Insert(0, item);
		}

		/// <summary>
		/// 폼이 처음 열릴 때 ListView 컬럼 설정
		/// </summary>


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
								box.Width,
								box.Height);

							string readChar = i < readValue.Length ? readValue[i].ToString() : "?";
							string expectedChar = i < expectedValue.Length ? expectedValue[i].ToString() : "?";

							bool isMatch = i < readValue.Length &&
										   i < expectedValue.Length &&
										   readValue[i] == expectedValue[i];

							Color color = isMatch ? Color.Lime : Color.Red;

							using (Pen pen = new Pen(color, 2))
							using (Brush brush = new SolidBrush(color))
							{
								g.DrawRectangle(pen, originalBox);
								g.DrawString(
									readChar,
									font,
									brush,
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

		/// <summary>
		/// 원본 이미지 위에 날짜 ROI 박스와 읽은 날짜 문자열을 오버레이
		/// 기준 날짜와 비교해서 같으면 초록, 다르면 빨강
		/// </summary>
		private void ProcessDateOverlay(Bitmap source, string readDate, string expectedDate)
		{
			// ── searchRoi를 스티커 실제 위치에 맞게 수정
			int x = (int)(source.Width * 0.15);  // 좌측 X 여유 2%
			int y = (int)(source.Height * 0.23);  // 상단 Y 여유 3%
			int w = (int)(source.Width * 0.11);  // 0.23+0.02 - 0.15 = 0.10 + 여유
			int h = (int)(source.Height * 0.57);  // 0.76+0.02 - 0.23 = 0.55 + 여유
			Rectangle searchRoi = new Rectangle(x, y, w, h);

			Rectangle labelRect = DateLabelDetector.FindDateLabelRect(source, searchRoi);
			if (labelRect == Rectangle.Empty)
				labelRect = searchRoi;

			Rectangle dateTextRect = DateTextRegionDetector.GetDateTextRegion(labelRect);

			bool isMatch = DateNormalizer.IsMatch(readDate, expectedDate);
			Color overlayColor = isMatch ? Color.Lime : Color.Red;

			using (Graphics g = Graphics.FromImage(source))
			using (Pen pen = new Pen(overlayColor, 3))
			using (Font font = new Font("Arial", 30, FontStyle.Bold))
			using (Brush brush = new SolidBrush(overlayColor))
			{
				//// [디버그] searchRoi — 파란색
				//g.DrawRectangle(new Pen(Color.Blue, 2), searchRoi);

				//// [디버그] labelRect 전체 — 노란색
				//g.DrawRectangle(new Pen(Color.Yellow, 2), labelRect);

				// 날짜 텍스트 ROI — 초록/빨강
				g.DrawRectangle(pen, dateTextRect);

				// 텍스트 세로 회전해서 박스 왼쪽에 표시
				g.TranslateTransform(
					dateTextRect.X - 5,
					dateTextRect.Y + dateTextRect.Height);
				g.RotateTransform(-90);
				g.DrawString(readDate, font, brush, 0, 0);
				g.ResetTransform();

				this.Text = $"search:{searchRoi} | label:{labelRect} | dateText:{dateTextRect}";

			}

			Image old = pictureBoxFrame.Image;
			pictureBoxFrame.Image = source;
			old?.Dispose();
		}
		
		}
	}

