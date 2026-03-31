using PackVisionApp.Managers;
using PackVisionApp.Vision;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace PackVisionApp.UI
{
    public partial class MainForm : Form
    {
        private Stopwatch _fpsSw = new Stopwatch();
        private PackageTracker _packageTracker = new PackageTracker();
        private CameraManager _cameraMgr = new CameraManager();
        private InspectionManager _inspectionMgr = new InspectionManager();

        private Point _startPoint;
        private Rectangle _selectionRect;
        private bool _isSelecting = false;

        // 화면에 그릴 박스 좌표
        private Rectangle _packageScreenRect;  // 초록 박스
        private Rectangle _dateScreenRect;     // 파란 박스
        private Rectangle _barcodeScreenRect;  // 노란 박스

        // 현재 어떤 ROI 드래그 중인지
        // "none" = 포장지 전체, "date" = 날짜, "barcode" = 바코드
        private string _roiMode = "none";

        private int _isTrackingBusy = 0;

        public MainForm()
        {
            InitializeComponent();
            _cameraMgr.FrameUpdated += OnFrameUpdated;
            btnDateRoi.Click += btnDateRoi_Click;
            btnBarcodeRoi.Click += btnBarcodeRoi_Click;

            pbCamera.MouseDown += pbCamera_MouseDown;
            pbCamera.MouseMove += pbCamera_MouseMove;
            pbCamera.MouseUp += pbCamera_MouseUp;
            pbCamera.Paint += pbCamera_Paint;
        }

        // 날짜 ROI 버튼
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

        // 바코드 ROI 버튼
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
                        // 화면 좌표 업데이트
                        if (_packageTracker.IsTracking)
                            _packageScreenRect = ImageRectToScreenRect(_packageTracker.GetPackageRect());

                        if (_packageTracker.IsDateRoiSet)
                            _dateScreenRect = ImageRectToScreenRect(_packageTracker.GetDateRect());

                        if (_packageTracker.IsBarcodeRoiSet)
                            _barcodeScreenRect = ImageRectToScreenRect(_packageTracker.GetBarcodeRect());

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

                        // 디버그 출력
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

                        Image oldImage = pbCamera.Image;
                        pbCamera.Image = bmpForDisplay;
                        oldImage?.Dispose();

                        pbCamera.Invalidate();
                    }));
                });
            }
            else
            {
                Image oldImage = pbCamera.Image;
                pbCamera.Image = bmpForDisplay;
                oldImage?.Dispose();
                pbCamera.Invalidate();
            }
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
                pbCamera.Invalidate();
            }
        }

        private void pbCamera_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isSelecting = false;

                if (_selectionRect.Width > 10 && _selectionRect.Height > 10)
                {
                    if (pbCamera.Image != null)
                    {
                        Rectangle imageRect = ScreenRectToImageRect(_selectionRect);

                        if (imageRect.Width > 5 && imageRect.Height > 5)
                        {
                            if (_roiMode == "date")
                            {
                                // 날짜 ROI → 비율로 저장
                                _packageTracker.SetDateRoi(imageRect);
                                _roiMode = "none";
                            }
                            else if (_roiMode == "barcode")
                            {
                                // 바코드 ROI → 비율로 저장
                                _packageTracker.SetBarcodeRoi(imageRect);
                                _roiMode = "none";
                            }
                            else
                            {
                                // 포장지 전체 드래그 → 초록 박스
                                Bitmap currentImg = (Bitmap)pbCamera.Image.Clone();
                                _packageTracker.SetTarget(currentImg, imageRect);
                                currentImg.Dispose();
                            }
                        }
                    }
                }

                _selectionRect = Rectangle.Empty;
                pbCamera.Invalidate();
            }
        }

        private void pbCamera_Paint(object sender, PaintEventArgs e)
        {
            // 드래그 중 빨간 박스
            if (_selectionRect.Width > 0 && _selectionRect.Height > 0)
            {
                using (Pen pen = new Pen(Color.Red, 2))
                    e.Graphics.DrawRectangle(pen, _selectionRect);
            }

            // 포장지 전체 — 초록 박스
            if (_packageTracker.IsTracking && _packageScreenRect != Rectangle.Empty)
            {
                using (Pen greenPen = new Pen(Color.Lime, 3))
                    e.Graphics.DrawRectangle(greenPen, _packageScreenRect);
            }

            // 날짜 ROI — 파란 박스
            if (_packageTracker.IsDateRoiSet && _dateScreenRect != Rectangle.Empty)
            {
                using (Pen bluePen = new Pen(Color.Blue, 3))
                    e.Graphics.DrawRectangle(bluePen, _dateScreenRect);
            }

            // 바코드 ROI — 노란 박스
            if (_packageTracker.IsBarcodeRoiSet && _barcodeScreenRect != Rectangle.Empty)
            {
                using (Pen yellowPen = new Pen(Color.Yellow, 3))
                    e.Graphics.DrawRectangle(yellowPen, _barcodeScreenRect);
            }
        }

        // 화면 좌표 → 이미지 좌표
        private Rectangle ScreenRectToImageRect(Rectangle screenRect)
        {
            if (pbCamera.Image == null) return Rectangle.Empty;

            float scaleX = (float)pbCamera.Image.Width / pbCamera.Width;
            float scaleY = (float)pbCamera.Image.Height / pbCamera.Height;

            int x = (int)(screenRect.X * scaleX);
            int y = (int)(screenRect.Y * scaleY);
            int w = (int)(screenRect.Width * scaleX);
            int h = (int)(screenRect.Height * scaleY);

            x = Math.Max(0, Math.Min(x, pbCamera.Image.Width - 1));
            y = Math.Max(0, Math.Min(y, pbCamera.Image.Height - 1));
            w = Math.Min(w, pbCamera.Image.Width - x);
            h = Math.Min(h, pbCamera.Image.Height - y);

            return new Rectangle(x, y, w, h);
        }

        // 이미지 좌표 → 화면 좌표
        private Rectangle ImageRectToScreenRect(Rectangle imageRect)
        {
            if (pbCamera.Image == null) return Rectangle.Empty;

            float scaleX = (float)pbCamera.Width / pbCamera.Image.Width;
            float scaleY = (float)pbCamera.Height / pbCamera.Image.Height;

            int x = (int)(imageRect.X * scaleX);
            int y = (int)(imageRect.Y * scaleY);
            int w = (int)(imageRect.Width * scaleX);
            int h = (int)(imageRect.Height * scaleY);

            return new Rectangle(x, y, w, h);
        }

        private async void btnStop_Click_1(object sender, EventArgs e)
        {
            btnStart.Enabled = true;
            btnStop.Enabled = false;

            await _cameraMgr.StopCameraAsync();

            _packageTracker.Reset();
            _packageScreenRect = Rectangle.Empty;
            _dateScreenRect = Rectangle.Empty;
            _barcodeScreenRect = Rectangle.Empty;
            _roiMode = "none";

            Image oldImage = pbCamera.Image;
            pbCamera.Image = null;
            oldImage?.Dispose();
        }

        private void btnStart_Click_1(object sender, EventArgs e)
        {
            bool success = _cameraMgr.StartCamera();
            if (success)
            {
                btnStart.Enabled = false;
                btnStop.Enabled = true;
                _fpsSw.Restart();
            }
            else
            {
                MessageBox.Show("카메라 연결에 실패했습니다.");
            }
        }
    }
}