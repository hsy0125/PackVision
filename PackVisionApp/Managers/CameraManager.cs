using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Drawing;
using System.IO;


namespace PackVisionApp.Managers
{
    public class CameraManager
    {

        private HikRobotCam _camEngine; // 실제 카메라 엔진
        private bool _isStreaming = false;
        public event Action<Bitmap> FrameUpdated;


        

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

        private void ProcessAndPublish()
        {
            // [수정] _isStreaming이 false라면 이미지를 변환하지 않고 바로 리턴합니다.
            if (!_isStreaming || _camEngine.LatestImageBuffer == null) return;

            try
            {
                Bitmap bmp = RawToBitmap(_camEngine.LatestImageBuffer, _camEngine.Width, _camEngine.Height);

                if (bmp == null)
                    return;

                FrameUpdated?.Invoke(bmp);
            }
            catch (Exception ex)
            {
                Console.WriteLine("이미지 변환 중 오류: " + ex.Message);
            }
        }

        private Bitmap RawToBitmap(byte[] data, int width, int height)
        {
            // 데이터 크기 검증 (데이터가 부족하면 튕김 방지)
            if (data.Length < width * height) return null;

            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
            ColorPalette cp = bmp.Palette;
            for (int i = 0; i < 256; i++) cp.Entries[i] = Color.FromArgb(i, i, i);
            bmp.Palette = cp;

            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            Marshal.Copy(data, 0, bmpData.Scan0, data.Length);
            bmp.UnlockBits(bmpData);

            return bmp;
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
                    _camEngine.Height);

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
                _camEngine.Height);
        }

    }
}
