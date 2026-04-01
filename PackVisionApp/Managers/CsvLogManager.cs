using System;
using System.IO;
using System.Text;
using PackVisionApp.Models;
using System;
using System.IO;

namespace PackVisionApp.Managers
{
	/*
     * CsvLogManager
     * 
     * 역할:
     * - 검사 결과를 CSV 파일에 한 줄씩 즉시 저장
     * - 1000개 단위로 파일 자동 분할
     */
	public class CsvLogManager
	{
		private readonly string _folderPath;
		private int _currentCount = 0;

		public CsvLogManager()
		{
			string basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			_folderPath = Path.Combine(basePath, "PackVision", "Logs");

			if (!Directory.Exists(_folderPath))
			{
				Directory.CreateDirectory(_folderPath);
			}

			_currentCount = GetExistingLogCount();
		}

		public void SaveLog(InspectionResult result)
		{
			if (result == null)
				return;

			_currentCount++;

			int fileIndex = ((_currentCount - 1) / 1000) + 1;
			string fileName = $"inspection_{fileIndex:D3}.csv";
			string filePath = Path.Combine(_folderPath, fileName);

			// 파일 없으면 헤더 생성
			if (!File.Exists(filePath))
			{
				string header = "InspectTime,Result,FailReason,ExpectedDate,ActualDate,ExpectedBarcode,ActualBarcode";
				File.WriteAllText(filePath, header + Environment.NewLine, Encoding.UTF8);
			}

			string line =
				Escape(result.InspectTime.ToString("yyyy-MM-dd HH:mm:ss")) + "," +
				Escape(result.ResultText) + "," +
				Escape(result.FailReasonText) + "," +
				Escape(result.ExpectedDate) + "," +
				Escape(result.ActualDate) + "," +
				Escape(result.ExpectedBarcode) + "," +
				Escape(result.ActualBarcode);

			File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
		}

		private int GetExistingLogCount()
		{
			try
			{
				int total = 0;
				var files = Directory.GetFiles(_folderPath, "inspection_*.csv");

				foreach (var file in files)
				{
					var lines = File.ReadAllLines(file);
					total += Math.Max(0, lines.Length - 1);
				}

				return total;
			}
			catch
			{
				return 0;
			}
		}

		private string Escape(string value)
		{
			if (string.IsNullOrEmpty(value))
				return "\"\"";

			value = value.Replace("\"", "\"\"");
			return "\"" + value + "\"";
		}
	}
}