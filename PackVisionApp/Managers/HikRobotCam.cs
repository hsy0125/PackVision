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
        private bool _singleFrameSoftwareTriggerMode = false;

        // 이미지 버퍼 관련 (Mono8: 1바이트/픽셀, 컬러 변환 후: BGR8 Packed = 3바이트/픽셀)
        public byte[] LatestImageBuffer { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        /// <summary>마지막으로 채운 버퍼의 픽셀당 바이트 (1=그레이, 3=BGR 컬러).</summary>
        public int BytesPerPixel { get; private set; } = 1;

        /// <summary>연속 FreeRun이 아니라 소프트웨어 트리거(장당 1프레임) 모드인지.</summary>
        public bool IsSingleFrameSoftwareTriggerMode => _singleFrameSoftwareTriggerMode;

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

            // 티칭/라이브: 연속(Trigger Off). 검사 구간에서만 소프트웨어 트리거로 전환.
            _singleFrameSoftwareTriggerMode = false;
            ApplyFreeRunContinuousGrab();

            // 이미지 콜백 등록
            _device.StreamGrabber.FrameGrabedEvent += OnFrameGrabbed;

            // 스트리밍 시작
            ret = _device.StreamGrabber.StartGrabbing();
            _isGrabbing = (ret == MvError.MV_OK);
            TryExpandStreamBuffers();

            UpdateResolution();
            return _isGrabbing;
        }

        /// <summary>Live/ROI 티칭용: Trigger Off → 들어오는 프레임마다 콜백(동영상 스트림).</summary>
        private void ApplyFreeRunContinuousGrab()
        {
            if (_device == null) return;
            _device.Parameters.SetEnumValue("TriggerMode", 0);
        }

        /// <summary>검사용: Trigger On + Software → TriggerSoftware 명령당 이미지 1장.</summary>
        public bool ApplySingleFrameSoftwareTriggerMode()
        {
            if (_device == null || !_isGrabbing)
                return false;

            try
            {
                _device.StreamGrabber.StopGrabbing();
                _singleFrameSoftwareTriggerMode = true;

                TrySetEnumByString("TriggerMode", "On");
                TrySetEnumByString("TriggerSource", "Software");

                int ret = _device.StreamGrabber.StartGrabbing();
                _isGrabbing = (ret == MvError.MV_OK);
                TryExpandStreamBuffers();
                UpdateResolution();
                return _isGrabbing;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ApplySingleFrameSoftwareTriggerMode: " + ex.Message);
                return false;
            }
        }

        /// <summary>장치별 노드명이 다를 수 있어 무시해도 됨. 버퍼 부족 시 트리거 누락 완화.</summary>
        private void TryExpandStreamBuffers()
        {
            if (_device == null) return;
            try
            {
                _device.Parameters.SetIntValue("StreamBufferCount", 64);
            }
            catch
            {
                try { _device.Parameters.SetIntValue("StreamBufferNumber", 64); } catch { }
            }
        }

        /// <summary>검사 종료 후 라이브 복구.</summary>
        public bool ApplyFreeRunAndRestartGrab()
        {
            if (_device == null)
                return false;

            try
            {
                _device.StreamGrabber.StopGrabbing();
                _singleFrameSoftwareTriggerMode = false;
                ApplyFreeRunContinuousGrab();
                int ret = _device.StreamGrabber.StartGrabbing();
                _isGrabbing = (ret == MvError.MV_OK);
                return _isGrabbing;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ApplyFreeRunAndRestartGrab: " + ex.Message);
                return false;
            }
        }

        /// <summary>소프트웨어 트리거 1회 → 다음 수신 프레임이 1장 촬영분.</summary>
        public bool SendSoftwareTrigger()
        {
            if (_device == null || !_isGrabbing || !_singleFrameSoftwareTriggerMode)
                return false;

            try
            {
                int ret = _device.Parameters.SetCommandValue("TriggerSoftware");
                return ret == MvError.MV_OK;
            }
            catch (Exception ex)
            {
                Console.WriteLine("SendSoftwareTrigger: " + ex.Message);
                return false;
            }
        }

        private void TrySetEnumByString(string name, string value)
        {
            try
            {
                _device.Parameters.SetEnumValueByString(name, value);
            }
            catch (Exception)
            {
                if (string.Equals(name, "TriggerMode", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(value, "On", StringComparison.OrdinalIgnoreCase))
                    _device.Parameters.SetEnumValue("TriggerMode", 1u);
                else if (string.Equals(name, "TriggerMode", StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(value, "Off", StringComparison.OrdinalIgnoreCase))
                    _device.Parameters.SetEnumValue("TriggerMode", 0u);
                else if (string.Equals(name, "TriggerSource", StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(value, "Software", StringComparison.OrdinalIgnoreCase))
                    _device.Parameters.SetEnumValue("TriggerSource", 7u);
                else
                    throw;
            }
        }

        private void OnFrameGrabbed(object sender, FrameGrabbedEventArgs e)
        {
            if (_device == null)
                return;

            IFrameOut frameOut = e.FrameOut;
            IImage srcImage = frameOut.Image;

            try
            {
                if (srcImage.PixelType == MvGvspPixelType.PixelType_Gvsp_Mono8)
                {
                    int size = (int)srcImage.ImageSize;
                    EnsureBuffer(size);
                    Marshal.Copy(srcImage.PixelDataPtr, LatestImageBuffer, 0, size);
                    BytesPerPixel = 1;
                }
                else
                {
                    IImage outImage = null;
                    int conv = _device.PixelTypeConverter.ConvertPixelType(
                        srcImage,
                        out outImage,
                        MvGvspPixelType.PixelType_Gvsp_BGR8_Packed);

                    if (conv != MvError.MV_OK || outImage == null)
                    {
                        Console.WriteLine($"[HikRobotCam] PixelTypeConverter 실패: 0x{conv:X8}");
                        return;
                    }

                    try
                    {
                        int size = (int)outImage.ImageSize;
                        EnsureBuffer(size);
                        Marshal.Copy(outImage.PixelDataPtr, LatestImageBuffer, 0, size);
                        BytesPerPixel = 3;
                    }
                    finally
                    {
                        TryDisposeImage(outImage);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[HikRobotCam] OnFrameGrabbed: " + ex.Message);
                return;
            }

            FrameGrabbed?.Invoke();
        }

        private void EnsureBuffer(int byteCount)
        {
            if (LatestImageBuffer == null || LatestImageBuffer.Length != byteCount)
                LatestImageBuffer = new byte[byteCount];
        }

        private static void TryDisposeImage(IImage image)
        {
            if (image is IDisposable d)
                d.Dispose();
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
