using System.Drawing;
using System.IO;
using System.Text.Json;

namespace PackVisionApp.Services
{
	/// <summary>포장지·날짜·바코드 ROI 티칭 결과를 디스크에 보관 (재실행 시 복원).</summary>
	public static class RoiTeachPersistence
	{
		private const int FileVersion = 1;

		public sealed class Snapshot
		{
			public int Version { get; set; } = FileVersion;
			public float PackageX { get; set; }
			public float PackageY { get; set; }
			public float PackageW { get; set; }
			public float PackageH { get; set; }
			public float DateX { get; set; }
			public float DateY { get; set; }
			public float DateW { get; set; }
			public float DateH { get; set; }
			public float BarcodeX { get; set; }
			public float BarcodeY { get; set; }
			public float BarcodeW { get; set; }
			public float BarcodeH { get; set; }
		}

		public static string GetDefaultPath()
		{
			string dir = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
				"PackVision");
			Directory.CreateDirectory(dir);
			return Path.Combine(dir, "roi_teaching.json");
		}

		public static void Save(
			Rectangle packageRect,
			RectangleF dateRatio,
			RectangleF barcodeRatio,
			int imageWidth,
			int imageHeight)
		{
			if (imageWidth <= 0 || imageHeight <= 0) return;
			if (packageRect == Rectangle.Empty) return;

			var snap = new Snapshot
			{
				Version = FileVersion,
				PackageX = (float)packageRect.X / imageWidth,
				PackageY = (float)packageRect.Y / imageHeight,
				PackageW = (float)packageRect.Width / imageWidth,
				PackageH = (float)packageRect.Height / imageHeight,
				DateX = dateRatio.X,
				DateY = dateRatio.Y,
				DateW = dateRatio.Width,
				DateH = dateRatio.Height,
				BarcodeX = barcodeRatio.X,
				BarcodeY = barcodeRatio.Y,
				BarcodeW = barcodeRatio.Width,
				BarcodeH = barcodeRatio.Height,
			};

			string path = GetDefaultPath();
			var options = new JsonSerializerOptions { WriteIndented = true };
			File.WriteAllText(path, JsonSerializer.Serialize(snap, options));
		}

		public static bool TryLoad(out Snapshot? snapshot)
		{
			snapshot = null;
			string path = GetDefaultPath();
			if (!File.Exists(path)) return false;

			try
			{
				string json = File.ReadAllText(path);
				var s = JsonSerializer.Deserialize<Snapshot>(json);
				if (s == null || s.Version < 1) return false;
				if (s.PackageW <= 0 || s.PackageH <= 0) return false;
				if (s.DateW <= 0 || s.DateH <= 0 || s.BarcodeW <= 0 || s.BarcodeH <= 0) return false;
				snapshot = s;
				return true;
			}
			catch
			{
				return false;
			}
		}

		public static Rectangle DenormalizePackage(Snapshot s, int imageWidth, int imageHeight)
		{
			if (imageWidth <= 0 || imageHeight <= 0) return Rectangle.Empty;

			int x = (int)Math.Round(s.PackageX * imageWidth);
			int y = (int)Math.Round(s.PackageY * imageHeight);
			int w = (int)Math.Round(s.PackageW * imageWidth);
			int h = (int)Math.Round(s.PackageH * imageHeight);
			var frame = new Rectangle(0, 0, imageWidth, imageHeight);
			return Rectangle.Intersect(new Rectangle(x, y, w, h), frame);
		}

		public static RectangleF DateRatio(Snapshot s) =>
			new RectangleF(s.DateX, s.DateY, s.DateW, s.DateH);

		public static RectangleF BarcodeRatio(Snapshot s) =>
			new RectangleF(s.BarcodeX, s.BarcodeY, s.BarcodeW, s.BarcodeH);
	}
}
