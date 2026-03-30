using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace PackVisionApp.UI
{
	public partial class MainForm : Form
	{
		// 사용자가 제출한 기준 날짜
		private string _expectedDate = "";

		// 사용자가 제출한 기준 바코드
		private string _expectedBarcode = "";

		// 총 검사 횟수
		private int _totalInspectionCount = 0;

		// OK 판정 횟수
		private int _okInspectionCount = 0;

		public object Global { get; private set; }

		public MainForm()
		{
			InitializeComponent();

			// 처음 폼이 열릴 때 검사율 표시 초기화
			UpdateInspectionRate();
		}

		private void _pictureBoxFrame_Click(object sender, EventArgs e)
		{
		}

		private void imageOpenToolStripMenuItem_Click(object sender, EventArgs e)
		{
		}

		/// <summary>
		/// 메뉴에서 이미지 열기 클릭 시 실행
		/// 선택한 이미지를 pictureBoxFrame에 표시
		/// </summary>
		private void imageToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (OpenFileDialog ofd = new OpenFileDialog())
			{
				ofd.Title = "이미지 선택";
				ofd.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";

				if (ofd.ShowDialog() == DialogResult.OK)
				{
					try
					{
						// 기존 이미지가 있으면 메모리 해제
						pictureBoxFrame.Image?.Dispose();

						Bitmap bmp = new Bitmap(ofd.FileName);
						pictureBoxFrame.Image = bmp;
					}
					catch (Exception ex)
					{
						MessageBox.Show("이미지 로드 실패: " + ex.Message);
					}
				}
			}
		}

		/// <summary>
		/// 현재 PictureBox에 보이는 이미지 저장
		/// </summary>
		private void imageSaveToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (pictureBoxFrame.Image == null)
			{
				MessageBox.Show("저장할 이미지가 없습니다.");
				return;
			}

			using (SaveFileDialog sfd = new SaveFileDialog())
			{
				sfd.Title = "이미지 저장";
				sfd.Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap Image|*.bmp";
				sfd.DefaultExt = "png";
				sfd.FileName = "saved_image";

				if (sfd.ShowDialog() == DialogResult.OK)
				{
					try
					{
						pictureBoxFrame.Image.Save(sfd.FileName);
						MessageBox.Show("이미지 저장 완료");
					}
					catch (Exception ex)
					{
						MessageBox.Show("이미지 저장 실패: " + ex.Message);
					}
				}
			}
		}

		/// <summary>
		/// 기준 날짜 제출 버튼
		/// TextBox에 입력된 값을 _expectedDate 에 저장
		/// </summary>
		private void btnDate_Click(object sender, EventArgs e)
		{
			_expectedDate = txtDate.Text.Trim();

			if (string.IsNullOrWhiteSpace(_expectedDate))
			{
				MessageBox.Show("날짜를 입력하세요.");
				return;
			}

			MessageBox.Show("기준 날짜 저장 완료: " + _expectedDate);
		}

		/// <summary>
		/// 기준 바코드 제출 버튼
		/// TextBox에 입력된 값을 _expectedBarcode 에 저장
		/// </summary>
		private void btnBarcode_Click(object sender, EventArgs e)
		{
			_expectedBarcode = txtBarcode.Text.Trim();

			if (string.IsNullOrWhiteSpace(_expectedBarcode))
			{
				MessageBox.Show("바코드를 입력하세요.");
				return;
			}

			MessageBox.Show("기준 바코드 저장 완료: " + _expectedBarcode);
		}

		/// <summary>
		/// RUN 버튼 클릭 시 실행
		/// 현재는 더미값으로 판정하고, 나중에 실제 ZXing/OCR 결과로 교체하면 됨
		/// </summary>
		private void btnRun_Click(object sender, EventArgs e)
		{
			// 이미지가 없는 경우 검사 불가
			if (pictureBoxFrame.Image == null)
			{
				MessageBox.Show("먼저 이미지를 불러오세요.");
				return;
			}

			// 기준값이 없는 경우 검사 불가
			if (string.IsNullOrWhiteSpace(_expectedDate) || string.IsNullOrWhiteSpace(_expectedBarcode))
			{
				MessageBox.Show("먼저 기준 날짜와 기준 바코드를 제출하세요.");
				return;
			}

			// TODO:
			// 나중에 실제 바코드 인식 결과 / 날짜 인식 결과로 교체
			string readBarcode = "880106262476";
			string readDate = "2026-09-21";

			bool isBarcodeOk = readBarcode == _expectedBarcode;
			bool isDateOk = readDate == _expectedDate;

			// 총 검사 횟수 증가
			_totalInspectionCount++;

			if (isBarcodeOk && isDateOk)
			{
				// OK 횟수 증가
				_okInspectionCount++;

				// 화면에 OK 표시
				lblResult.Text = "OK";
				lblResult.ForeColor = Color.LimeGreen;

				// ListView에 OK 로그 추가
				AddLogItem("OK", "-", readDate, readBarcode, Color.Green);
			}
			else
			{
				// 화면에 NOK 표시
				lblResult.Text = "NOK";
				lblResult.ForeColor = Color.Red;

				string failReason;

				// 실패 원인 분기
				if (!isBarcodeOk && !isDateOk)
					failReason = "BD";   // Barcode + Date 둘 다 실패
				else if (!isBarcodeOk)
					failReason = "B";    // Barcode 실패
				else
					failReason = "D";    // Date 실패

				// ListView에 NOK 로그 추가
				AddLogItem("NOK", failReason, readDate, readBarcode, Color.Red);
			}

			// 검사율 업데이트
			UpdateInspectionRate();
		}

		/// <summary>
		/// 검사율과 총 검사 수 화면 갱신
		/// </summary>
		private void UpdateInspectionRate()
		{
			int rate = 0;

			if (_totalInspectionCount > 0)
			{
				rate = (int)Math.Round((_okInspectionCount / (double)_totalInspectionCount) * 100.0);
			}

			lblInspectionRate.Text = rate + "%";
			lblInspectionCount.Text = "총 검사 개수";
			lblInspectionSummary.Text = $"{_okInspectionCount}/{_totalInspectionCount}";
		}

		/// <summary>
		/// ListView에 로그 한 줄 추가
		/// Result / Time / Reason / Date / Barcode 컬럼에 각각 값 넣음
		/// </summary>
		private void AddLogItem(string result, string reason, string date, string barcode, Color color)
		{
			// 첫 번째 컬럼: Result
			ListViewItem item = new ListViewItem(result);

			// 두 번째 컬럼: 현재 시간
			item.SubItems.Add(DateTime.Now.ToString("HH:mm:ss"));

			// 세 번째 컬럼: 실패 원인
			item.SubItems.Add(reason);

			// 네 번째 컬럼: 읽은 날짜
			item.SubItems.Add(date);

			// 다섯 번째 컬럼: 읽은 바코드
			item.SubItems.Add(barcode);

			// 줄 전체 글자색 지정
			item.ForeColor = color;

			// 가장 위에 삽입
			lvLogs.Items.Insert(0, item);
		}

		/// <summary>
		/// 폼이 처음 열릴 때 ListView 컬럼 설정
		/// </summary>
		private void MainForm_Load(object sender, EventArgs e)
		{
			// 컬럼 중복 추가 방지
			lvLogs.Columns.Clear();

			lvLogs.View = View.Details;
			lvLogs.FullRowSelect = true;
			lvLogs.GridLines = true;

			lvLogs.Columns.Add("Result", 80);
			lvLogs.Columns.Add("Time", 100);
			lvLogs.Columns.Add("Reason", 100);
			lvLogs.Columns.Add("Date", 120);
			lvLogs.Columns.Add("Barcode", 180);
		}
	}
}