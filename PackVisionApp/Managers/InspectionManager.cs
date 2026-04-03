using System;
using System.Drawing;
using System.Linq;
using PackVisionApp.Models;
using PackVisionApp.Vision;

namespace PackVisionApp.Managers
{
	/// <summary>
	/// InspectionManager
	/// 
	/// 역할: 판정 담당 클래스
	/// - 기준값(날짜, 바코드)과 실제 검사값을 비교하여 검사 결과를 생성
	/// - 카메라 실시간 검사 (Bitmap + ROI 기반)
	/// - 더미값/문자열 직접 비교 기반 검사
	/// </summary>
	public class InspectionManager
	{
		// ── 판정 완화: 0이면 완전 일치만 OK. 1이면 편집거리 1 이하까지 OK (OCR 1글자 튐 허용).
		private const int LooseBarcodeMaxEdits = 1;
		private const int LooseDateMaxEdits = 1;

		private readonly BarcodeReader _barcodeReader;
		private readonly DateReader _dateReader;
		private readonly RoiMapper _roiMapper;

		public RectangleF DateRatioRect { get; private set; } = RectangleF.Empty;
		public RectangleF BarcodeRatioRect { get; private set; } = RectangleF.Empty;

		public InspectionManager()
		{
			_barcodeReader = new BarcodeReader();
			_dateReader = new DateReader();
			_roiMapper = new RoiMapper();
		}

		// ═══════════════════════════════════════════════════════
		// ROI 비율 저장 / 계산
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// 티칭한 package/date/barcode 기준으로 상대 비율 저장
		/// 반드시 package 내부로 잘라서 저장
		/// </summary>
		public void SetRoiRatios(
			Rectangle packageRect,
			Rectangle dateRect,
			Rectangle barcodeRect)
		{
			if (packageRect == Rectangle.Empty ||
				dateRect == Rectangle.Empty ||
				barcodeRect == Rectangle.Empty)
				return;

			Rectangle validDateRect = Rectangle.Intersect(dateRect, packageRect);
			Rectangle validBarcodeRect = Rectangle.Intersect(barcodeRect, packageRect);

			if (validDateRect == Rectangle.Empty || validBarcodeRect == Rectangle.Empty)
				return;

			DateRatioRect = ClampRatioRect(_roiMapper.RectToRatio(validDateRect, packageRect));
			BarcodeRatioRect = ClampRatioRect(_roiMapper.RectToRatio(validBarcodeRect, packageRect));
		}

		public Rectangle GetDateRect(Rectangle packageRect)
		{
			if (packageRect == Rectangle.Empty || DateRatioRect == RectangleF.Empty)
				return Rectangle.Empty;

			Rectangle rect = _roiMapper.RatioToRect(DateRatioRect, packageRect);
			return Rectangle.Intersect(rect, packageRect);
		}

		public Rectangle GetBarcodeRect(Rectangle packageRect)
		{
			if (packageRect == Rectangle.Empty || BarcodeRatioRect == RectangleF.Empty)
				return Rectangle.Empty;

			Rectangle rect = _roiMapper.RatioToRect(BarcodeRatioRect, packageRect);
			return Rectangle.Intersect(rect, packageRect);
		}

		private RectangleF ClampRatioRect(RectangleF ratio)
		{
			float x = Clamp01(ratio.X);
			float y = Clamp01(ratio.Y);
			float w = Clamp01(ratio.Width);
			float h = Clamp01(ratio.Height);

			if (x + w > 1f)
				w = 1f - x;

			if (y + h > 1f)
				h = 1f - y;

			if (w < 0f) w = 0f;
			if (h < 0f) h = 0f;

			return new RectangleF(x, y, w, h);
		}

		private float Clamp01(float value)
		{
			if (value < 0f) return 0f;
			if (value > 1f) return 1f;
			return value;
		}

		// ═══════════════════════════════════════════════════════
		// 카메라 프레임 기반 실시간 검사
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// Bitmap 프레임과 packageRect를 받아 실제 OCR/바코드 읽기 후 판정
		/// </summary>
		public InspectionResult Inspect(
			Bitmap frame,
			Rectangle packageRect,
			string expectedBarcode,
			string expectedDate)
		{
			if (frame == null || packageRect == Rectangle.Empty)
				return BuildResult(expectedBarcode, "", expectedDate, "", false, false, true);

			Rectangle dateRect = GetDateRect(packageRect);
			Rectangle barcodeRect = GetBarcodeRect(packageRect);

			BarcodeResult barcodeResult = _barcodeReader.ReadBarcode(frame, barcodeRect);
			DateResult dateResult = _dateReader.ReadDate(frame, dateRect);

			string actualBarcode = barcodeResult.Success ? barcodeResult.Value : string.Empty;
			string actualDate = dateResult.Success ? dateResult.Value : string.Empty;

			bool isBarcodeOk = barcodeResult.Success &&
							   BarcodeMatches(expectedBarcode, actualBarcode);

			bool isDateOk = dateResult.Success &&
							DateMatches(expectedDate, actualDate);

			return BuildResult(expectedBarcode, actualBarcode,
							   expectedDate, actualDate,
							   isBarcodeOk, isDateOk, true);
		}

		// ═══════════════════════════════════════════════════════
		// 문자열 직접 비교 검사
		// ═══════════════════════════════════════════════════════

		/// <summary>
		/// 문자열로 직접 값을 받아 판정 (카메라 없이 이미지 파일 기반)
		/// </summary>
		public InspectionResult Inspect(
			string expectedBarcode,
			string actualBarcode,
			string expectedDate,
			string actualDate,
			bool isPrintOk)
		{
			bool isBarcodeOk = BarcodeMatches(expectedBarcode, actualBarcode);
			bool isDateOk = DateMatches(expectedDate, actualDate);

			return BuildResult(expectedBarcode, actualBarcode,
							   expectedDate, actualDate,
							   isBarcodeOk, isDateOk, isPrintOk);
		}

		// ═══════════════════════════════════════════════════════
		// 공통 — 결과 생성
		// ═══════════════════════════════════════════════════════

		private InspectionResult BuildResult(
			string expectedBarcode,
			string actualBarcode,
			string expectedDate,
			string actualDate,
			bool isBarcodeOk,
			bool isDateOk,
			bool isPrintOk)
		{
			InspectionResult result = new InspectionResult
			{
				ExpectedBarcode = expectedBarcode ?? string.Empty,
				ActualBarcode = actualBarcode ?? string.Empty,
				ExpectedDate = NormalizeDate(expectedDate),
				ActualDate = NormalizeDate(actualDate),
				IsBarcodeOk = isBarcodeOk,
				IsDateOk = isDateOk,
				IsPrintOk = isPrintOk
			};

			if (!isBarcodeOk)
				result.FailReasons.Add("바코드 오류");

			if (!isDateOk)
				result.FailReasons.Add("날짜 오류");

			if (!isPrintOk)
				result.FailReasons.Add("프린트 오류");

			result.UpdateOverallResult();
			return result;
		}

		// ═══════════════════════════════════════════════════════
		// 공통 — 정규화
		// ═══════════════════════════════════════════════════════

		private bool BarcodeMatches(string expected, string actual)
		{
			string e = NormalizeBarcode(expected);
			string a = NormalizeBarcode(actual);
			if (string.IsNullOrEmpty(a))
				return false;
			if (e == a)
				return true;
			// Max가 0이면 아래 조건은 절대 참이 안 됨 → 사실상 완전 일치만 OK
			return Levenshtein(e, a) <= LooseBarcodeMaxEdits;
		}

		private bool DateMatches(string expected, string actual)
		{
			string e = NormalizeDate(expected);
			string a = NormalizeDate(actual);
			if (string.IsNullOrEmpty(a))
				return false;
			if (e == a)
				return true;

			string ed = new string(e.Where(char.IsDigit).ToArray());
			string ad = new string(a.Where(char.IsDigit).ToArray());
			if (ed.Length >= 6 && ad.Length >= 6)
				return Levenshtein(ed, ad) <= LooseDateMaxEdits;

			return Levenshtein(e, a) <= LooseDateMaxEdits;
		}

		/// <summary>편집 거리(삽입/삭제/치환 1회 = 1). CS 알고리즘 수업에서 나오는 표준 DP.</summary>
		private static int Levenshtein(string s, string t)
		{
			if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
			if (string.IsNullOrEmpty(t)) return s.Length;

			int n = s.Length;
			int m = t.Length;
			var row = new int[m + 1];
			for (int j = 0; j <= m; j++) row[j] = j;

			for (int i = 1; i <= n; i++)
			{
				int prev = row[0];
				row[0] = i;
				for (int j = 1; j <= m; j++)
				{
					int tmp = row[j];
					int cost = s[i - 1] == t[j - 1] ? 0 : 1;
					row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), prev + cost);
					prev = tmp;
				}
			}

			return row[m];
		}

		private string NormalizeBarcode(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw))
				return string.Empty;

			return new string(raw.Where(char.IsLetterOrDigit).ToArray());
		}

		private string NormalizeDate(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw))
				return string.Empty;

			string digits = new string(raw.Where(char.IsDigit).ToArray());

			if (digits.Length == 8)
				return $"{digits.Substring(0, 4)}-{digits.Substring(4, 2)}-{digits.Substring(6, 2)}";

			if (digits.Length == 6)
				return $"20{digits.Substring(0, 2)}-{digits.Substring(2, 2)}-{digits.Substring(4, 2)}";

			return raw.Trim();
		}
	}
}