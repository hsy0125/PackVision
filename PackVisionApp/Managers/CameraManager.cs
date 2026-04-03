using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Drawing;
using System.IO;
using System.Threading;


namespace PackVisionApp.Managers
{
    public class CameraManager
    {

        private HikRobotCam _camEngine; // 실제 카메라 엔진
        private bool _isStreaming = false;
        public bool IsStreaming => _isStreaming;
        public event Action<Bitmap> FrameUpdated;

        /// <summary>true이면 라이브(FrameUpdated) 대신 검사용 콜백으로만 프레임을 넘깁니다(검사 중 라이브 정지).</summary>
        public bool UseInspectTransferPath { get; set; }

        /// <summary>프레임 1장(클론). 수신 측에서 Dispose 해야 합니다.</summary>
        public event Action<Bitmap>? InspectTransferCompleted;

        /// <summary>
        /// 검사 경로에서 비트맵 생성 실패·예외·구독자 예외 등으로 다음 트리거가 걸리지 않을 때.
        /// InspectStage가 다음 TriggerSoftware를 다시 걸도록 합니다.
        /// </summary>
        public event Action? InspectPipelineStalled;


        

        public CameraManager()
        {
            _camEngine = new HikRobotCam();
            _camEngine.FrameGrabbed += ProcessAndPublish;
        }

        public bool StartCamera()
        {
            if (_camEngine.Create() && _camEngine.Open())
            {
                _isStreaming = true; // [추가] 시작 시 true
                return true;
            }
            return false;
        }

        public async Task StopCameraAsync()
        {
            _isStreaming = false; // [추가] 멈추자마자 플래그를 꺼서 프레임 전달 차단

            await Task.Run(() => {
                _camEngine?.Close();
            });
        }

        /// <summary>검사 전용: 소프트웨어 트리거 모드(장당 1프레임). ROI 라이브는 연속 모드로 복구할 것.</summary>
        public bool EnterInspectSingleCaptureMode()
        {
            return _camEngine != null && _camEngine.ApplySingleFrameSoftwareTriggerMode();
        }

        /// <summary>검사 종료 후 연속 라이브(FreeRun)로 복귀.</summary>
        public void ExitInspectSingleCaptureMode()
        {
            _camEngine?.ApplyFreeRunAndRestartGrab();
        }

        /// <summary>다음 검사용 이미지 1장 촬영 요청(검사 완료 후 호출).</summary>
        public bool FireSoftwareTriggerForNextFrame(int retries = 8, int delayMs = 5)
        {
            if (_camEngine == null) return false;
            for (int i = 0; i < retries; i++)
            {
                if (_camEngine.SendSoftwareTrigger())
                    return true;
                Thread.Sleep(delayMs);
            }
            return false;
        }

        private void RaiseInspectPipelineStalled()
        {
            if (!UseInspectTransferPath || !_isStreaming)
                return;
            try
            {
                InspectPipelineStalled?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine("InspectPipelineStalled: " + ex.Message);
            }
        }

        private void ProcessAndPublish()
        {
            // [수정] _isStreaming이 false라면 이미지를 변환하지 않고 바로 리턴합니다.
            if (!_isStreaming || _camEngine.LatestImageBuffer == null)
                return;

            try
            {
                Bitmap bmp = RawToBitmap(
                    _camEngine.LatestImageBuffer,
                    _camEngine.Width,
                    _camEngine.Height,
                    _camEngine.BytesPerPixel);

                if (bmp == null)
                {
                    RaiseInspectPipelineStalled();
                    return;
                }

                if (UseInspectTransferPath && InspectTransferCompleted != null)
                {
                    Bitmap clone = (Bitmap)bmp.Clone();
                    bmp.Dispose();
                    try
                    {
                        InspectTransferCompleted.Invoke(clone);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("InspectTransferCompleted: " + ex.Message);
                        try { clone.Dispose(); } catch { /* ignore */ }
                        RaiseInspectPipelineStalled();
                    }
                    return;
                }

                FrameUpdated?.Invoke(bmp);
            }
            catch (Exception ex)
            {
                Console.WriteLine("이미지 변환 중 오류: " + ex.Message);
                RaiseInspectPipelineStalled();
            }
        }

        /// <summary>
        /// bytesPerPixel 1: 그레이(8bpp). 3: 카메라에서 BGR8 Packed로 받은 컬러 → UI용 24bpp RGB 비트맵.
        /// </summary>
        private Bitmap RawToBitmap(byte[] data, int width, int height, int bytesPerPixel)
        {
            if (data == null || width <= 0 || height <= 0)
                return null;

            if (bytesPerPixel == 3)
            {
                int need = width * height * 3;
                if (data.Length < need)
                    return null;

                Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                BitmapData bmpData = bmp.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format24bppRgb);
                try
                {
                    int dstStride = bmpData.Stride;
                    int srcStride = width * 3;
                    for (int y = 0; y < height; y++)
                    {
                        Marshal.Copy(data, y * srcStride, IntPtr.Add(bmpData.Scan0, y * dstStride), srcStride);
                    }
                }
                finally
                {
                    bmp.UnlockBits(bmpData);
                }

                return bmp;
            }

            // 그레이스케일
            if (data.Length < width * height)
                return null;

            Bitmap grayBmp = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
            ColorPalette cp = grayBmp.Palette;
            for (int i = 0; i < 256; i++) cp.Entries[i] = Color.FromArgb(i, i, i);
            grayBmp.Palette = cp;

            BitmapData grayData = grayBmp.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format8bppIndexed);
            Marshal.Copy(data, 0, grayData.Scan0, width * height);
            grayBmp.UnlockBits(grayData);

            return grayBmp;
        }

        public void SaveCurrentFrame()
        {
            if (_camEngine.LatestImageBuffer == null)
            {
                Console.WriteLine("[경고] 저장할 프레임이 없습니다.");
                return;
            }

            try
            {
                string saveFolder = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "001");

                if (!Directory.Exists(saveFolder))
                    Directory.CreateDirectory(saveFolder);

                string fileName = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".bmp";
                string fullPath = Path.Combine(saveFolder, fileName);

                Bitmap bmp = RawToBitmap(
                    _camEngine.LatestImageBuffer,
                    _camEngine.Width,
                    _camEngine.Height,
                    _camEngine.BytesPerPixel);

                bmp?.Save(fullPath);
                bmp?.Dispose();

                Console.WriteLine($"[저장 완료] {fullPath}");

                // [추가] 저장 완료 메시지 + 폴더 위치 알려주기
                System.Windows.Forms.MessageBox.Show(
                    $"저장 완료!\n\n저장 위치:\n{fullPath}",
                    "프레임 저장");

                // [추가] 저장된 폴더를 탐색기로 바로 열기
                System.Diagnostics.Process.Start("explorer.exe", saveFolder);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[저장 실패] {ex.Message}");
                System.Windows.Forms.MessageBox.Show($"저장 실패!\n{ex.Message}");
            }
        }

        public Bitmap GetCurrentFrame()
        {
            // 카메라가 꺼져있거나 버퍼가 비어있으면 null 반환
            if (!_isStreaming || _camEngine.LatestImageBuffer == null)
            {
                Console.WriteLine("[경고] GetCurrentFrame: 카메라가 꺼져있습니다.");
                return null;
            }

            // 현재 버퍼에서 Bitmap 하나 만들어서 반환
            return RawToBitmap(
                _camEngine.LatestImageBuffer,
                _camEngine.Width,
                _camEngine.Height,
                _camEngine.BytesPerPixel);
        }

    }
}
