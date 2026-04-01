using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using PackVisionApp.Managers;
using PackVisionApp.Models;
using PackVisionApp.Vision;

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

        private Point _startPoint;
        private Rectangle _selectionRect;
        private bool _isSelecting = false;
        private string _roiMode = "none";

        private Rectangle _packageScreenRect;
        private Rectangle _dateScreenRect;
        private Rectangle _barcodeScreenRect;

        //선준추가
        private bool _isAutoInspecting = false;
        private int _isInspectionBusy = 0;
        private DateTime _lastInspectionTime = DateTime.MinValue;
        private readonly TimeSpan _inspectionInterval = TimeSpan.FromMilliseconds(300);

        private Rectangle _packageImageRect = Rectangle.Empty;
        private Rectangle _dateImageRect = Rectangle.Empty;
        private Rectangle _barcodeImageRect = Rectangle.Empty;
        //여기까지

        public MainForm()
        {
            InitializeComponent();
            _cameraMgr.FrameUpdated += OnFrameUpdated;
            btnDateRoi.Click += btnDateRoi_Click;
            btnBarcodeRoi.Click += btnBarcodeRoi_Click;
            UpdateInspectionRate();
        }

        // ═══════════════════════════════════════
        // 민영씨 — 카메라 / 트래킹
        // ═══════════════════════════════════════

        //선준 수정
        private void OnFrameUpdated(Bitmap bmp)
        {
            if (bmp == null)
                return;

            if (this.InvokeRequired)
            {
                Bitmap copy = (Bitmap)bmp.Clone();
                this.BeginInvoke(new Action(() => OnFrameUpdated(copy)));
                return;
            }

            Image oldImage = pictureBoxFrame.Image;
            pictureBoxFrame.Image = (Bitmap)bmp.Clone();
            oldImage?.Dispose();

            if (_packageTracker.IsTracking)
            {
                if (Interlocked.CompareExchange(ref _isTrackingBusy, 1, 0) == 0)
                {
                    Bitmap trackingBmp = (Bitmap)bmp.Clone();

                    Task.Run(() =>
                    {
                        try
                        {
                            _packageTracker.Track(trackingBmp);
                        }
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
        //여기까지 수정

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
                                _dateImageRect = imageRect;
                                _roiMode = "none";
                            }
                            else if (_roiMode == "barcode")
                            {
                                _barcodeImageRect = imageRect;
                                _roiMode = "none";
                            }
                            else
                            {
                                _packageImageRect = imageRect;

                                Bitmap currentImg = (Bitmap)pictureBoxFrame.Image.Clone();
                                _packageTracker.SetTarget(currentImg, imageRect);
                                currentImg.Dispose();
                            }

                            if (_packageImageRect != Rectangle.Empty &&
                                _dateImageRect != Rectangle.Empty &&
                                _barcodeImageRect != Rectangle.Empty)
                            {
                                _inspectionMgr.SetRoiRatios(
                                    _packageImageRect,
                                    _dateImageRect,
                                    _barcodeImageRect);
                            }

                            _packageScreenRect = ImageRectToScreenRect(_packageImageRect);
                            _dateScreenRect = ImageRectToScreenRect(_dateImageRect);
                            _barcodeScreenRect = ImageRectToScreenRect(_barcodeImageRect);
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

            if (_packageScreenRect != Rectangle.Empty)
            {
                using (Pen greenPen = new Pen(Color.Lime, 3))
                    e.Graphics.DrawRectangle(greenPen, _packageScreenRect);
            }

            if (_dateScreenRect != Rectangle.Empty)
            {
                using (Pen bluePen = new Pen(Color.Blue, 3))
                    e.Graphics.DrawRectangle(bluePen, _dateScreenRect);
            }

            if (_barcodeScreenRect != Rectangle.Empty)
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

        // ═══════════════════════════════════════
        // 소영씨 — RUN/STOP / 로그 / OK/NOK
        // ═══════════════════════════════════════

        //선준 수정
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
        //여기까지 수정

        //선준 추가부분
        private void btnInspect_Click(object sender, EventArgs e)
        {
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

            // package 기준 상대 비율 저장
            _inspectionMgr.SetRoiRatios(
                _packageImageRect,
                _dateImageRect,
                _barcodeImageRect);

            _isAutoInspecting = true;

            MessageBox.Show("실시간 검사 시작");
        }
        private void ApplyInspectionResult(InspectionResult result)
        {
            if (result == null)
                return;

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

            UpdateInspectionRate();
        }

        private void TryAutoInspection()
        {
            if (!_isAutoInspecting)
                return;

            if (string.IsNullOrWhiteSpace(_expectedDate) ||
                string.IsNullOrWhiteSpace(_expectedBarcode))
                return;

            if (!_packageTracker.IsTracking)
                return;

            if (DateTime.Now - _lastInspectionTime < _inspectionInterval)
                return;

            if (System.Threading.Interlocked.CompareExchange(ref _isInspectionBusy, 1, 0) != 0)
                return;

            Bitmap currentFrame = _cameraMgr.GetCurrentFrame();
            if (currentFrame == null)
            {
                System.Threading.Interlocked.Exchange(ref _isInspectionBusy, 0);
                return;
            }

            Rectangle packageRect = _packageTracker.GetPackageRect();
            _lastInspectionTime = DateTime.Now;

            Task.Run(() =>
            {
                try
                {
                    using (currentFrame)
                    {
                        InspectionResult result = _inspectionMgr.Inspect(
                            currentFrame,
                            packageRect,
                            _expectedBarcode,
                            _expectedDate
                        );

                        this.BeginInvoke(new Action(() =>
                        {
                            ApplyInspectionResult(result);
                        }));
                    }
                }
                catch (Exception ex)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        lblDebug.Text = "검사 오류: " + ex.Message;
                    }));
                }
                finally
                {
                    System.Threading.Interlocked.Exchange(ref _isInspectionBusy, 0);
                }
            });
        }

        private void UpdateTrackedRois()
        {
            if (!_packageTracker.IsTracking)
                return;

            _packageImageRect = _packageTracker.GetPackageRect();

            if (_inspectionMgr.DateRatioRect != RectangleF.Empty)
                _dateImageRect = _inspectionMgr.GetDateRect(_packageImageRect);

            if (_inspectionMgr.BarcodeRatioRect != RectangleF.Empty)
                _barcodeImageRect = _inspectionMgr.GetBarcodeRect(_packageImageRect);

            _packageScreenRect = ImageRectToScreenRect(_packageImageRect);
            _dateScreenRect = ImageRectToScreenRect(_dateImageRect);
            _barcodeScreenRect = ImageRectToScreenRect(_barcodeImageRect);

            lblDebug.Text =
                $"P: X={_packageImageRect.X},Y={_packageImageRect.Y} | " +
                $"D: X={_dateImageRect.X},Y={_dateImageRect.Y} | " +
                $"B: X={_barcodeImageRect.X},Y={_barcodeImageRect.Y}";
        }
        //여기까지


        private async void btnStop_Click(object sender, EventArgs e)
        {
            btnRun.Enabled = true;
            btnStop.Enabled = false;

            await _cameraMgr.StopCameraAsync();

            _isAutoInspecting = false;
            _packageImageRect = Rectangle.Empty;
            _dateImageRect = Rectangle.Empty;
            _barcodeImageRect = Rectangle.Empty;

            _packageTracker.Reset();

            Image oldImage = pictureBoxFrame.Image;
            pictureBoxFrame.Image = null;
            oldImage?.Dispose();
        }

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

        private void UpdateInspectionRate()
        {
            int rate = 0;
            if (_totalInspectionCount > 0)
                rate = (int)Math.Round(
                    (_okInspectionCount / (double)_totalInspectionCount) * 100.0);

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
