using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace PackVisionApp.Core
{
	/// <summary>
	/// 촬영(Transfer) → 검사 워커 → UI 반영 순서를 파일로 추적합니다.
	/// 바탕화면의 inspect_flow_trace.txt (디버그 시 tail / 메모장으로 확인)
	/// </summary>
	public static class InspectFlowLog
	{
		private static readonly object FileLock = new object();
		private static long _seq;

		/// <summary>false이면 기록하지 않음(릴리스 전 끄기)</summary>
		public static bool Enabled { get; set; } = true;

		public static string TraceFilePath =>
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "inspect_flow_trace.txt");

		public static void Write(string stage, string detail = "")
		{
			if (!Enabled)
				return;

			long n = Interlocked.Increment(ref _seq);
			string line =
				$"{DateTime.Now:HH:mm:ss.fff}\t#{n}\tT{Thread.CurrentThread.ManagedThreadId}\t{stage}\t{detail}\r\n";

			lock (FileLock)
			{
				try
				{
					File.AppendAllText(TraceFilePath, line);
				}
				catch (Exception ex)
				{
					Debug.WriteLine("InspectFlowLog: " + ex.Message);
				}
			}
		}
	}
}
