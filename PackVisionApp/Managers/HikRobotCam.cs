using MvCameraControl;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace PackVisionApp.Managers
{
    public class HikRobotCam : IDisposable
    {
        private IDevice _device = null;
        private bool _isGrabbing = false;

        // 이미지 버퍼 관련 (SDK에서 제공하는 버퍼 사용)
        public byte[] LatestImageBuffer { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        // 프레임 수신 시 알림 이벤트
        public event Action FrameGrabbed;

        public bool Create(string ipAddr = null)
        {
            SDKSystem.Initialize();
            try
            {
                List<IDeviceInfo> devList;
                DeviceEnumerator.EnumDevices(DeviceTLayerType.MvGigEDevice, out devList);

                if (devList.Count == 0) return false;

                // IP 조건에 맞는 장치 선택 (생략 시 첫 번째 장치)
                _device = DeviceFactory.CreateDevice(devList[0]);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Camera Create Fail: {ex.Message}");
                return false;
            }
        }

        public bool Open()
        {
            if (_device == null) return false;

            if (_device == null) return false;

            int ret = _device.Open();
            if (ret != MvError.MV_OK)
            {
                // [여기가 핵심!] 에러 코드를 메시지 박스로 띄워보세요.
                // 만약 0x80000003 이면 "이미 사용 중(Access Denied)"이라는 뜻입니다.
                System.Windows.Forms.MessageBox.Show($"카메라 열기 실패! 에러코드: 0x{ret:X8}");
                return false;
            }

            // 트리거 모드 Off (Continuous Grab)
            _device.Parameters.SetEnumValue("TriggerMode", 0);

            // 이미지 콜백 등록
            _device.StreamGrabber.FrameGrabedEvent += OnFrameGrabbed;

            // 스트리밍 시작
            ret = _device.StreamGrabber.StartGrabbing();
            _isGrabbing = (ret == MvError.MV_OK);

            UpdateResolution();
            return _isGrabbing;
        }

        private void OnFrameGrabbed(object sender, FrameGrabbedEventArgs e)
        {
            // 실시간 이미지 데이터를 LatestImageBuffer에 복사
            if (LatestImageBuffer == null || LatestImageBuffer.Length != (int)e.FrameOut.Image.ImageSize)
            {
                LatestImageBuffer = new byte[e.FrameOut.Image.ImageSize];
            }

            Marshal.Copy(e.FrameOut.Image.PixelDataPtr, LatestImageBuffer, 0, (int)e.FrameOut.Image.ImageSize);

            // 이벤트 발생 -> CameraManager가 알 수 있게 함
            FrameGrabbed?.Invoke();
        }

        private void UpdateResolution()
        {
            IIntValue w, h;
            _device.Parameters.GetIntValue("Width", out w);
            _device.Parameters.GetIntValue("Height", out h);
            Width = (int)w.CurValue;
            Height = (int)h.CurValue;
        }

        public void Close()
        {
            if (_device != null)
            {
                // [핵심] 이벤트 구독을 해제하여 콜백 실행을 중단함
                _device.StreamGrabber.FrameGrabedEvent -= OnFrameGrabbed;

                _device.StreamGrabber.StopGrabbing();
                _device.Close();

                // 장치 객체 초기화
                _device = null;
            }
        }

        public void Dispose()
        {
            Close();
            SDKSystem.Finalize();
        }
    }
}
