using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using PackVisionApp.Managers;
using PackVisionApp.Models;

namespace PackVisionApp.UI
{
    public partial class MainForm : Form
    {
        // ─── 소영씨 필드 ───
        private InspectionManager _inspectionMgr = new InspectionManager();
        private string _expectedDate = "";
        private string _expectedBarcode = "";
        private int _totalInspectionCount = 0;
        private int _okInspectionCount = 0;

        // ─── 민영씨 필드 ───
        private CameraManager _cameraMgr = new CameraManager();
        private PackageTracker _packageTracker = new PackageTracker();
        private Stopwatch _fpsSw = new Stopwatch();
        private int _isTrackingBusy = 0;

		public MainForm()
		{
			InitializeComponent();

			// 처음 폼이 열릴 때 검사율 표시 초기화
			UpdateInspectionRate();
		}

        // ═══════════════════════════════════════
        // 민영씨 — 카메라 / 트래킹
        // ═══════════════════════════════════════

        private void OnFrameUpdated(Bitmap bmp)
        {
            if (this.InvokeRequired)
            {
                Bitmap bmpCopy = (Bitmap)bmp.Clone();
                this.BeginInvoke(new Action(() => OnFrameUpdated(bmpCopy)));
                return;
            }

            _fpsSw.Stop();
            double fps = 1000.0 / (_fpsSw.ElapsedMilliseconds + 1);
            _fpsSw.Restart();

            Bitmap bmpForDisplay = (Bitmap)bmp.Clone();
            bmp.Dispose();

            if (Interlocked.CompareExchange(ref _isTrackingBusy, 1, 0) == 0)
            {
                Bitmap bmpForTracking = (Bitmap)bmpForDisplay.Clone();

                Task.Run(() =>
                {
                    try
                    {
                        _packageTracker.Track(bmpForTracking);
                    }
                    finally
                    {
                        bmpForTracking.Dispose();
                        Interlocked.Exchange(ref _isTrackingBusy, 0);
                    }

                    this.BeginInvoke(new Action(() =>
                    {
                        if (_packageTracker.IsTracking)
                            _packageScreenRect = ImageRectToScreenRect(
                                _packageTracker.GetPackageRect());

                        if (_packageTracker.IsDateRoiSet)
                            _dateScreenRect = ImageRectToScreenRect(
                                _packageTracker.GetDateRect());

                        if (_packageTracker.IsBarcodeRoiSet)
                            _barcodeScreenRect = ImageRectToScreenRect(
                                _packageTracker.GetBarcodeRect());

                        // InspectionManager에 전달
                        if (_packageTracker.IsTracking)
                        {
                            Bitmap currentFrame = _cameraMgr.GetCurrentFrame();
                            if (currentFrame != null)
                            {
                                _inspectionMgr.RunInspection(
                                    currentFrame,
                                    _packageTracker.GetDateRect(),
                                    _packageTracker.GetBarcodeRect());
                                currentFrame.Dispose();
                            }
                        }

                        // 디버그 좌표 출력
                        string debugText = "";
                        if (_packageTracker.IsTracking)
                        {
                            var p = _packageTracker.GetPackageRect();
                            debugText += $"포장지: X={p.X}, Y={p.Y}  ";
                        }
                        if (_packageTracker.IsDateRoiSet)
                        {
                            var d = _packageTracker.GetDateRect();
                            debugText += $"날짜: X={d.X}, Y={d.Y}  ";
                        }
                        if (_packageTracker.IsBarcodeRoiSet)
                        {
                            var b = _packageTracker.GetBarcodeRect();
                            debugText += $"바코드: X={b.X}, Y={b.Y}";
                        }
                        lblDebug.Text = debugText;

                        Image oldImage = pictureBoxFrame.Image;
                        pictureBoxFrame.Image = bmpForDisplay;
                        oldImage?.Dispose();

                        pictureBoxFrame.Invalidate();
                    }));
                });
            }
            else
            {
                Image oldImage = pictureBoxFrame.Image;
                pictureBoxFrame.Image = bmpForDisplay;
                oldImage?.Dispose();
                pictureBoxFrame.Invalidate();
            }
        }

        private void btnDateRoi_Click(object sender, EventArgs e)
        {
            if (!_packageTracker.IsTracking)
            {
                MessageBox.Show("먼저 포장지 전체를 드래그해서 잡아주세요!", "안내");
                return;
            }
            _roiMode = "date";
            MessageBox.Show("날짜 영역을 드래그해주세요!", "날짜 ROI");
        }

        private void btnBarcodeRoi_Click(object sender, EventArgs e)
        {
            if (!_packageTracker.IsTracking)
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
                int width = Math.Abs(_startPoint.X - e.X);
                int height = Math.Abs(_startPoint.Y - e.Y);

                _selectionRect = new Rectangle(x, y, width, height);
                pictureBoxFrame.Invalidate();
            }
        }

        private void pbCamera_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isSelecting = false;

                if (_selectionRect.Width > 10 && _selectionRect.Height > 10)
                {
                    if (pictureBoxFrame.Image != null)
                    {
                        Rectangle imageRect = ScreenRectToImageRect(_selectionRect);

                        if (imageRect.Width > 5 && imageRect.Height > 5)
                        {
                            if (_roiMode == "date")
                            {
                                _packageTracker.SetDateRoi(imageRect);
                                _roiMode = "none";
                            }
                            else if (_roiMode == "barcode")
                            {
                                _packageTracker.SetBarcodeRoi(imageRect);
                                _roiMode = "none";
                            }
                            else
                            {
                                Bitmap currentImg = (Bitmap)pictureBoxFrame.Image.Clone();
                                _packageTracker.SetTarget(currentImg, imageRect);
                                currentImg.Dispose();
                            }
                        }
                    }
                }

                _selectionRect = Rectangle.Empty;
                pictureBoxFrame.Invalidate();
            }
        }

        private void pbCamera_Paint(object sender, PaintEventArgs e)
        {
            if (_selectionRect.Width > 0 && _selectionRect.Height > 0)
            {
                using (Pen pen = new Pen(Color.Red, 2))
                    e.Graphics.DrawRectangle(pen, _selectionRect);
            }

            if (_packageTracker.IsTracking && _packageScreenRect != Rectangle.Empty)
            {
                using (Pen greenPen = new Pen(Color.Lime, 3))
                    e.Graphics.DrawRectangle(greenPen, _packageScreenRect);
            }

            if (_packageTracker.IsDateRoiSet && _dateScreenRect != Rectangle.Empty)
            {
                using (Pen bluePen = new Pen(Color.Blue, 3))
                    e.Graphics.DrawRectangle(bluePen, _dateScreenRect);
            }

            if (_packageTracker.IsBarcodeRoiSet && _barcodeScreenRect != Rectangle.Empty)
            {
                using (Pen yellowPen = new Pen(Color.Yellow, 3))
                    e.Graphics.DrawRectangle(yellowPen, _barcodeScreenRect);
            }
        }

        private Rectangle ScreenRectToImageRect(Rectangle screenRect)
        {
            if (pictureBoxFrame.Image == null) return Rectangle.Empty;

            float scaleX = (float)pictureBoxFrame.Image.Width / pictureBoxFrame.Width;
            float scaleY = (float)pictureBoxFrame.Image.Height / pictureBoxFrame.Height;

            int x = (int)(screenRect.X * scaleX);
            int y = (int)(screenRect.Y * scaleY);
            int w = (int)(screenRect.Width * scaleX);
            int h = (int)(screenRect.Height * scaleY);

            x = Math.Max(0, Math.Min(x, pictureBoxFrame.Image.Width - 1));
            y = Math.Max(0, Math.Min(y, pictureBoxFrame.Image.Height - 1));
            w = Math.Min(w, pictureBoxFrame.Image.Width - x);
            h = Math.Min(h, pictureBoxFrame.Image.Height - y);

            return new Rectangle(x, y, w, h);
        }

        private Rectangle ImageRectToScreenRect(Rectangle imageRect)
        {
            if (pictureBoxFrame.Image == null) return Rectangle.Empty;

            float scaleX = (float)pictureBoxFrame.Width / pictureBoxFrame.Image.Width;
            float scaleY = (float)pictureBoxFrame.Height / pictureBoxFrame.Image.Height;

            int x = (int)(imageRect.X * scaleX);
            int y = (int)(imageRect.Y * scaleY);
            int w = (int)(imageRect.Width * scaleX);
            int h = (int)(imageRect.Height * scaleY);

            return new Rectangle(x, y, w, h);
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

			if (string.IsNullOrWhiteSpace(_expectedDate) || string.IsNullOrWhiteSpace(_expectedBarcode))
			{
				MessageBox.Show("먼저 기준 날짜와 기준 바코드를 제출하세요.");
				return;
			}

			// TODO: 나중에 실제 결과로 교체
			string readBarcode = "880106262476";
			string readDate = "26-09-21";
			bool isPrintOk = true;

			// 🔥 핵심: Manager 사용
			InspectionResult result = _inspectionManager.Inspect(
				_expectedBarcode,
				readBarcode,
				_expectedDate,
				readDate,
				isPrintOk
			);

			// 검사 횟수 증가
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

				AddLogItem("NOK", result.FailReasonText, result.ActualDate, result.ActualBarcode, Color.Red);
			}

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

        private void MainForm_Load(object sender, EventArgs e)
        {
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
                        pictureBoxFrame.Image?.Dispose();
                        pictureBoxFrame.Image = new Bitmap(ofd.FileName);
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
        private void _pictureBoxFrame_Click(object sender, EventArgs e) { }
    }
}
