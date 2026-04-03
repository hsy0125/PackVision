using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using PackVisionApp.Managers;

namespace PackVisionApp.Core
{
	/// <summary>
	/// [1] 카메라에서 프레임 수신(Transfer 완료) 이벤트가 들어올 때마다
	///     검사를 "한 번에 한 건씩"만 처리하도록 제어하는 클래스입니다.
	///
	/// [2] 검사가 끝나기 전에 새 프레임이 들어오면 그 프레임은 버립니다.
	///     즉, "촬영 → 검사 → 다음 촬영 → 다음 검사" 순서를 유지하려는 목적입니다.
	///
	/// [3] 검사 중에는 라이브 화면용 프레임 전달과 검사 프레임 전달을 분리하여,
	///     검사 타이밍이 꼬이거나 UI 표시와 검사 흐름이 섞이지 않도록 설계되었습니다.
	///
	/// [4] 검사 구간: 카메라를 소프트웨어 트리거 모드로 두고 TriggerSoftware 당 이미지 1장만 받습니다.
	///     검사 워커가 끝난 뒤 다음 TriggerSoftware를 발행해 촬영→검사→촬영→검사를 맞춥니다.
	///     ROI 티칭 등 라이브는 연속(FreeRun) 모드입니다.
	/// </summary>
	public sealed class InspectStage : IDisposable
	{
		private readonly CameraManager _camera;
		private int _transferBusy;
		private int _stallRecoverBusy;

		/// <summary>검사에 넘겨 처리한 프레임 수(드롭 제외).</summary>
		public long AcceptedForInspectCount => System.Threading.Volatile.Read(ref _acceptedForInspect);

		/// <summary>이전 검사가 끝나기 전에 들어와 버려진 프레임 수.</summary>
		public long DroppedWhileBusyCount => System.Threading.Volatile.Read(ref _droppedWhileBusy);

		private long _acceptedForInspect;
		private long _droppedWhileBusy;

		public InspectStage(CameraManager camera)
		{
			_camera = camera ?? throw new ArgumentNullException(nameof(camera));
			_camera.InspectPipelineStalled += OnInspectPipelineStalled;
		}

		public bool IsInspectCycleActive { get; private set; }

		/// <summary>SDK/Grab 스레드가 아닌 ThreadPool 워커에서 동기 호출됩니다. 내부에서 UI는 Invoke로만 접근하세요.</summary>
		public Action<Bitmap>? RunInspectSync { get; set; }

		public void StartInspectCycle()
		{
			if (IsInspectCycleActive)
				return;

			if (!_camera.EnterInspectSingleCaptureMode())
			{
				InspectFlowLog.Write("CYCLE_START_FAIL", "EnterInspectSingleCaptureMode (SW trigger)");
				return;
			}

			Interlocked.Exchange(ref _acceptedForInspect, 0);
			Interlocked.Exchange(ref _droppedWhileBusy, 0);
			IsInspectCycleActive = true;
			_camera.UseInspectTransferPath = true;
			_camera.InspectTransferCompleted += OnInspectTransferCompleted;

			if (!_camera.FireSoftwareTriggerForNextFrame())
				InspectFlowLog.Write("TRIGGER_FAIL", "first frame");

			InspectFlowLog.Write("CYCLE_START",
				"SW trigger ON → 이미지 1장 수신 → 검사 → 다시 TriggerSoftware");
		}

		private void OnInspectPipelineStalled()
		{
			ScheduleArmNextTrigger("pipeline_stalled");
		}

		private void ScheduleArmNextTrigger(string reason)
		{
			if (!IsInspectCycleActive)
				return;

			if (Interlocked.CompareExchange(ref _stallRecoverBusy, 1, 0) != 0)
				return;

			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					InspectFlowLog.Write("STALL_RECOVER_START", reason);
					for (int attempt = 0; attempt < 20 && IsInspectCycleActive; attempt++)
					{
						if (_camera.FireSoftwareTriggerForNextFrame(retries: 1, delayMs: 0))
						{
							InspectFlowLog.Write("STALL_RECOVER_OK", $"{reason} attempt={attempt}");
							return;
						}
						Thread.Sleep(6);
					}
					InspectFlowLog.Write("STALL_RECOVER_FAIL", reason);
				}
				finally
				{
					Interlocked.Exchange(ref _stallRecoverBusy, 0);
				}
			});
		}

		public void StopInspectCycle()
		{
			if (!IsInspectCycleActive)
				return;

			IsInspectCycleActive = false;
			_camera.InspectTransferCompleted -= OnInspectTransferCompleted;
			_camera.UseInspectTransferPath = false;
			_camera.ExitInspectSingleCaptureMode();
			InspectFlowLog.Write("CYCLE_STOP",
				$"accepted={Volatile.Read(ref _acceptedForInspect)} droppedBusy={Volatile.Read(ref _droppedWhileBusy)}");

			// UI 스레드에서 워커의 Invoke(ApplyUi)와 동시에 대기하면 데드락이 납니다. 대기하지 않습니다.
		}

		private void OnInspectTransferCompleted(Bitmap bmp)
		{
			if (!IsInspectCycleActive)
			{
				InspectFlowLog.Write("TRANSFER_IGNORED", "cycle_off");
				bmp.Dispose();
				return;
			}

			if (Interlocked.CompareExchange(ref _transferBusy, 1, 0) != 0)
			{
				Interlocked.Increment(ref _droppedWhileBusy);
				InspectFlowLog.Write("FRAME_DROP_BUSY", $"{bmp.Width}x{bmp.Height}");
				bmp.Dispose();
				ScheduleArmNextTrigger("drop_busy");
				return;
			}

			Interlocked.Increment(ref _acceptedForInspect);
			InspectFlowLog.Write("FRAME_ACCEPT", $"{bmp.Width}x{bmp.Height} → queue worker");

			ThreadPool.QueueUserWorkItem(_ =>
			{
				InspectFlowLog.Write("WORKER_RUN_INSPECT_START", $"{bmp.Width}x{bmp.Height}");
				try
				{
					RunInspectSync?.Invoke(bmp);
				}
				catch (Exception ex)
				{
					InspectFlowLog.Write("WORKER_RUN_INSPECT_ERR", ex.Message);
					Debug.WriteLine("InspectStage RunInspectSync: " + ex);
				}
				finally
				{
					InspectFlowLog.Write("WORKER_RUN_INSPECT_END", "dispose frame");
					bmp.Dispose();
					Interlocked.Exchange(ref _transferBusy, 0);
					InspectFlowLog.Write("WORKER_BUSY_CLEAR", "SW trigger next frame");

					if (IsInspectCycleActive)
					{
						if (_camera.FireSoftwareTriggerForNextFrame())
							InspectFlowLog.Write("TRIGGER_ARM_NEXT", "after inspect");
						else
						{
							InspectFlowLog.Write("TRIGGER_ARM_NEXT_FAIL", "");
							ScheduleArmNextTrigger("after_inspect_trigger_fail");
						}
					}
				}
			});
		}

		public void Dispose()
		{
			StopInspectCycle();
		}
	}
}
