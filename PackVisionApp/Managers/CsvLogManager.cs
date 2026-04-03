using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PackVisionApp.Models;

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

		/// <summary>화면 로그 리스트(한 세션)를 리셋 전에 별도 CSV로 저장합니다. 행은 화면 표시 순서(최신이 위) 그대로 저장합니다.</summary>
		public string? SaveUiLogSnapshot(IReadOnlyList<string?[]> rows)
		{
			if (rows == null || rows.Count == 0)
				return null;

			string name = $"ui_session_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
			string filePath = Path.Combine(_folderPath, name);

			var sb = new StringBuilder();
			sb.AppendLine("Result,Time,Reason,Date,Barcode");
			foreach (var cols in rows)
			{
				if (cols == null || cols.Length < 5)
					continue;
				sb.AppendLine(string.Join(",",
					Enumerable.Range(0, 5).Select(i => Escape(cols[i] ?? string.Empty))));
			}

			File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			return filePath;
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