using System;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using PackVisionApp.Models;
using PackVisionApp.Vision;

namespace PackVisionApp.Managers
{
	/// <summary>
	/// InspectionManager
	/// 
	/// 역할:
	/// - 기준값(날짜, 바코드)과 실제 검사값을 비교하여 결과 생성
	/// - 날짜 검사는 "날짜 부분"과 "뒤 문자열(A1 F1 같은 코드 부분)"을 따로 비교
	/// - 하지만 최종 로그/결과 표시는 하나로 합쳐서 D 로 처리
	/// </summary>
	public class InspectionManager
	{
		// OCR이 1글자 정도 틀리는 경우를 약하게 허용
		private const int LooseBarcodeMaxEdits = 0;
		private const int LooseDateMaxEdits = 0;
		private const int LooseSuffixMaxEdits = 0;

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

			// 날짜 부분 + 뒤 코드 부분을 각각 비교한 뒤 최종 하나로 합침
			bool isDateOk = dateResult.Success &&
							DateMatches(expectedDate, actualDate);

			return BuildResult(expectedBarcode, actualBarcode,
							   expectedDate, actualDate,
							   isBarcodeOk, isDateOk, true);
		}

		// ═══════════════════════════════════════════════════════
		// 문자열 직접 비교 검사
		// ═══════════════════════════════════════════════════════

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
		// 결과 생성
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

				// 화면/로그에 보여줄 값은 보기 좋게 정리해서 넣음
				ExpectedDate = NormalizeDateDisplay(expectedDate),
				ActualDate = NormalizeDateDisplay(actualDate),

				IsBarcodeOk = isBarcodeOk,
				IsDateOk = isDateOk,
				IsPrintOk = isPrintOk
			};

			if (!isBarcodeOk)
				result.FailReasons.Add("B");

			// 날짜 + 뒤 코드(A1 F1)를 따로 비교하더라도
			// 결과 로그에는 하나로 합쳐서 D 만 넣음
			if (!isDateOk)
				result.FailReasons.Add("D");

			if (!isPrintOk)
				result.FailReasons.Add("P");

			result.UpdateOverallResult();
			return result;
		}

		// ═══════════════════════════════════════════════════════
		// 비교
		// ═══════════════════════════════════════════════════════

		private bool BarcodeMatches(string expected, string actual)
		{
			string e = NormalizeBarcode(expected);
			string a = NormalizeBarcode(actual);

			if (string.IsNullOrEmpty(a))
				return false;

			if (e == a)
				return true;

			return Levenshtein(e, a) <= LooseBarcodeMaxEdits;
		}

		/// <summary>
		/// 날짜 검사는 2개로 나눠서 비교
		/// 1) 날짜 부분
		/// 2) 뒤 코드 부분 (예: A1 F1)
		/// 
		/// 둘 다 맞아야 true
		/// </summary>
		private bool DateMatches(string expected, string actual)
		{
			string expectedDateOnly = NormalizeDateOnly(expected);
			string actualDateOnly = NormalizeDateOnly(actual);

			string expectedSuffix = NormalizeDateSuffix(expected);
			string actualSuffix = NormalizeDateSuffix(actual);

			// 실제 OCR값이 비어있으면 실패
			if (string.IsNullOrEmpty(actualDateOnly) && string.IsNullOrEmpty(actualSuffix))
				return false;

			bool isDatePartOk = CompareDatePart(expectedDateOnly, actualDateOnly);
			bool isSuffixPartOk = CompareSuffixPart(expectedSuffix, actualSuffix);

			return isDatePartOk && isSuffixPartOk;
		}

		private bool CompareDatePart(string expected, string actual)
		{
			if (string.IsNullOrWhiteSpace(actual))
				return false;

			string e = expected.Trim().ToUpper();
			string a = actual.Trim().ToUpper();

			if (e == a)
				return true;

			return Levenshtein(e, a) <= LooseDateMaxEdits;
		}

		private bool CompareSuffixPart(string expected, string actual)
		{
			// 기준값에 뒤 코드가 없으면 뒤 코드는 검사 안 함
			if (string.IsNullOrWhiteSpace(expected))
				return true;

			if (string.IsNullOrWhiteSpace(actual))
				return false;

			string e = Regex.Replace(expected.ToUpper().Trim(), @"\s+", " ");
			string a = Regex.Replace(actual.ToUpper().Trim(), @"\s+", " ");

			if (e == a)
				return true;

			return Levenshtein(e, a) <= LooseSuffixMaxEdits;
		}

		// ═══════════════════════════════════════════════════════
		// 정규화
		// ═══════════════════════════════════════════════════════

		private string NormalizeBarcode(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw))
				return string.Empty;

			return new string(raw.Where(char.IsLetterOrDigit).ToArray());
		}

		/// <summary>
		/// 표시용 날짜 문자열 정리
		/// 예:
		/// 27.01.28 A1 F1 -> 2027-01-28 A1 F1
		/// </summary>
		private string NormalizeDateDisplay(string raw)
		{
			string dateOnly = NormalizeDateOnly(raw);
			string suffix = NormalizeDateSuffix(raw);

			if (string.IsNullOrEmpty(dateOnly) && string.IsNullOrEmpty(suffix))
				return string.Empty;

			if (string.IsNullOrEmpty(suffix))
				return dateOnly;

			if (string.IsNullOrEmpty(dateOnly))
				return suffix;

			return $"{dateOnly} {suffix}";
		}

		/// <summary>
		/// 날짜 부분만 추출해서 YYYY-MM-DD로 통일
		/// 입력 예:
		/// 27-01-28 A1 F1
		/// 27.01.28 A1 F1
		/// 2027/01/28 A1 F1
		/// </summary>
		private string NormalizeDateOnly(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw))
				return string.Empty;

			string text = raw.Trim().ToUpper();

			Match match = Regex.Match(text, @"(?<date>\d{2,4}[.\-/]\d{2}[.\-/]\d{2})");
			if (!match.Success)
				return string.Empty;

			string datePart = match.Groups["date"].Value;
			string digits = Regex.Replace(datePart, @"[^0-9]", "");

			if (digits.Length == 8)
				return $"{digits.Substring(0, 4)}-{digits.Substring(4, 2)}-{digits.Substring(6, 2)}";

			if (digits.Length == 6)
				return $"20{digits.Substring(0, 2)}-{digits.Substring(2, 2)}-{digits.Substring(4, 2)}";

			return string.Empty;
		}

		/// <summary>
		/// 날짜 뒤의 코드 부분만 추출
		/// 예:
		/// 27-01-28 A1 F1 -> A1 F1
		/// 27.01.28 A1 F1 -> A1 F1
		/// </summary>
		private string NormalizeDateSuffix(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw))
				return string.Empty;

			string text = raw.Trim().ToUpper();
			text = Regex.Replace(text, @"\s+", " ");

			Match match = Regex.Match(text, @"\d{2,4}[.\-/]\d{2}[.\-/]\d{2}\s*(?<suffix>.*)");
			if (!match.Success)
				return string.Empty;

			string suffix = match.Groups["suffix"].Value.Trim();

			if (string.IsNullOrWhiteSpace(suffix))
				return string.Empty;

			// 알파벳/숫자/공백만 남김
			suffix = Regex.Replace(suffix, @"[^A-Z0-9 ]", "");
			suffix = Regex.Replace(suffix, @"\s+", " ").Trim();

			return suffix;
		}

		// ═══════════════════════════════════════════════════════
		// 공통 알고리즘
		// ═══════════════════════════════════════════════════════

		private static int Levenshtein(string s, string t)
		{
			if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
			if (string.IsNullOrEmpty(t)) return s.Length;

			int n = s.Length;
			int m = t.Length;
			var row = new int[m + 1];

			for (int j = 0; j <= m; j++)
				row[j] = j;

			for (int i = 1; i <= n; i++)
			{
				int prev = row[0];
				row[0] = i;

				for (int j = 1; j <= m; j++)
				{
					int tmp = row[j];
					int cost = s[i - 1] == t[j - 1] ? 0 : 1;
					row[j] = Math.Min(
						Math.Min(row[j] + 1, row[j - 1] + 1),
						prev + cost);
					prev = tmp;
				}
			}

			return row[m];
		}
	}
}