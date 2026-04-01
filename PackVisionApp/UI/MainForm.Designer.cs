namespace PackVisionApp.UI
{
	partial class MainForm
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			btnRun = new Button();
			btnStop = new Button();
			_imagePanel = new Panel();
			lblResult = new Label();
			pictureBoxFrame = new PictureBox();
			txtDate = new TextBox();
			btnDate = new Button();
			panel1 = new Panel();
			label1 = new Label();
			label2 = new Label();
			btnBarcode = new Button();
			txtBarcode = new TextBox();
			panelBottom = new Panel();
			panelLog = new Panel();
			lvLogs = new ListView();
			panelStatus = new Panel();
			lblInspectionSummary = new Label();
			lblInspectionCount = new Label();
			lblInspectionRate = new Label();
			menuStrip1 = new MenuStrip();
			imageOpenToolStripMenuItem = new ToolStripMenuItem();
			imageToolStripMenuItem = new ToolStripMenuItem();
			imageSaveToolStripMenuItem = new ToolStripMenuItem();
			btnTestCrop = new Button();
			_imagePanel.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)pictureBoxFrame).BeginInit();
			panel1.SuspendLayout();
			panelBottom.SuspendLayout();
			panelLog.SuspendLayout();
			panelStatus.SuspendLayout();
			menuStrip1.SuspendLayout();
			SuspendLayout();
			// 
			// btnRun
			// 
			btnRun.Location = new Point(926, 14);
			btnRun.Name = "btnRun";
			btnRun.Size = new Size(169, 55);
			btnRun.TabIndex = 0;
			btnRun.Text = "RUN";
			btnRun.UseVisualStyleBackColor = true;
			btnRun.Click += btnRun_Click;
			// 
			// btnStop
			// 
			btnStop.Location = new Point(1101, 14);
			btnStop.Name = "btnStop";
			btnStop.Size = new Size(169, 55);
			btnStop.TabIndex = 1;
			btnStop.Text = "STOP";
			btnStop.UseVisualStyleBackColor = true;
			// 
			// _imagePanel
			// 
			_imagePanel.AllowDrop = true;
			_imagePanel.Controls.Add(btnTestCrop);
			_imagePanel.Controls.Add(lblResult);
			_imagePanel.Controls.Add(pictureBoxFrame);
			_imagePanel.Location = new Point(12, 115);
			_imagePanel.Name = "_imagePanel";
			_imagePanel.Size = new Size(1304, 449);
			_imagePanel.TabIndex = 2;
			// 
			// lblResult
			// 
			lblResult.AutoSize = true;
			lblResult.BackColor = Color.Transparent;
			lblResult.Font = new Font("맑은 고딕", 36F, FontStyle.Bold, GraphicsUnit.Point, 129);
			lblResult.Location = new Point(3, 12);
			lblResult.Name = "lblResult";
			lblResult.Size = new Size(337, 96);
			lblResult.TabIndex = 3;
			lblResult.Text = "lblResult";
			// 
			// pictureBoxFrame
			// 
			pictureBoxFrame.Dock = DockStyle.Fill;
			pictureBoxFrame.Location = new Point(0, 0);
			pictureBoxFrame.Name = "pictureBoxFrame";
			pictureBoxFrame.Size = new Size(1304, 449);
			pictureBoxFrame.SizeMode = PictureBoxSizeMode.Zoom;
			pictureBoxFrame.TabIndex = 0;
			pictureBoxFrame.TabStop = false;
			pictureBoxFrame.Click += _pictureBoxFrame_Click;
			// 
			// txtDate
			// 
			txtDate.Location = new Point(11, 35);
			txtDate.Name = "txtDate";
			txtDate.Size = new Size(309, 31);
			txtDate.TabIndex = 3;
			// 
			// btnDate
			// 
			btnDate.Location = new Point(326, 35);
			btnDate.Name = "btnDate";
			btnDate.Size = new Size(112, 34);
			btnDate.TabIndex = 4;
			btnDate.Text = "제출";
			btnDate.UseVisualStyleBackColor = true;
			btnDate.Click += btnDate_Click;
			// 
			// panel1
			// 
			panel1.Controls.Add(label1);
			panel1.Controls.Add(label2);
			panel1.Controls.Add(btnBarcode);
			panel1.Controls.Add(btnDate);
			panel1.Controls.Add(txtBarcode);
			panel1.Controls.Add(btnRun);
			panel1.Controls.Add(txtDate);
			panel1.Controls.Add(btnStop);
			panel1.Location = new Point(11, 27);
			panel1.Name = "panel1";
			panel1.Size = new Size(1305, 82);
			panel1.TabIndex = 5;
			// 
			// label1
			// 
			label1.AutoSize = true;
			label1.Location = new Point(446, 7);
			label1.Name = "label1";
			label1.Size = new Size(79, 25);
			label1.TabIndex = 8;
			label1.Text = "Barcode";
			// 
			// label2
			// 
			label2.AutoSize = true;
			label2.Location = new Point(11, 3);
			label2.Name = "label2";
			label2.Size = new Size(98, 25);
			label2.TabIndex = 7;
			label2.Text = "yy-mm-dd";
			// 
			// btnBarcode
			// 
			btnBarcode.Location = new Point(759, 35);
			btnBarcode.Name = "btnBarcode";
			btnBarcode.Size = new Size(112, 34);
			btnBarcode.TabIndex = 5;
			btnBarcode.Text = "제출";
			btnBarcode.UseVisualStyleBackColor = true;
			btnBarcode.Click += btnBarcode_Click;
			// 
			// txtBarcode
			// 
			txtBarcode.Location = new Point(444, 35);
			txtBarcode.Name = "txtBarcode";
			txtBarcode.Size = new Size(309, 31);
			txtBarcode.TabIndex = 4;
			// 
			// panelBottom
			// 
			panelBottom.Controls.Add(panelLog);
			panelBottom.Controls.Add(panelStatus);
			panelBottom.Location = new Point(11, 570);
			panelBottom.Name = "panelBottom";
			panelBottom.Size = new Size(1305, 334);
			panelBottom.TabIndex = 6;
			// 
			// panelLog
			// 
			panelLog.Controls.Add(lvLogs);
			panelLog.Location = new Point(261, 17);
			panelLog.Name = "panelLog";
			panelLog.Size = new Size(1031, 301);
			panelLog.TabIndex = 1;
			// 
			// lvLogs
			// 
			lvLogs.FullRowSelect = true;
			lvLogs.GridLines = true;
			lvLogs.Location = new Point(35, 29);
			lvLogs.Name = "lvLogs";
			lvLogs.Size = new Size(974, 250);
			lvLogs.TabIndex = 1;
			lvLogs.UseCompatibleStateImageBehavior = false;
			lvLogs.View = View.Details;
			// 
			// panelStatus
			// 
			panelStatus.Controls.Add(lblInspectionSummary);
			panelStatus.Controls.Add(lblInspectionCount);
			panelStatus.Controls.Add(lblInspectionRate);
			panelStatus.Location = new Point(10, 15);
			panelStatus.Name = "panelStatus";
			panelStatus.Size = new Size(233, 303);
			panelStatus.TabIndex = 0;
			// 
			// lblInspectionSummary
			// 
			lblInspectionSummary.AutoSize = true;
			lblInspectionSummary.Location = new Point(28, 206);
			lblInspectionSummary.Name = "lblInspectionSummary";
			lblInspectionSummary.Size = new Size(59, 25);
			lblInspectionSummary.TabIndex = 2;
			lblInspectionSummary.Text = "0/nnn";
			// 
			// lblInspectionCount
			// 
			lblInspectionCount.AutoSize = true;
			lblInspectionCount.Location = new Point(28, 145);
			lblInspectionCount.Name = "lblInspectionCount";
			lblInspectionCount.Size = new Size(114, 25);
			lblInspectionCount.TabIndex = 1;
			lblInspectionCount.Text = "총 검사 개수";
			// 
			// lblInspectionRate
			// 
			lblInspectionRate.AutoSize = true;
			lblInspectionRate.Font = new Font("맑은 고딕", 14F, FontStyle.Bold, GraphicsUnit.Point, 129);
			lblInspectionRate.Location = new Point(28, 31);
			lblInspectionRate.Name = "lblInspectionRate";
			lblInspectionRate.Size = new Size(42, 38);
			lblInspectionRate.TabIndex = 0;
			lblInspectionRate.Text = "%";
			// 
			// menuStrip1
			// 
			menuStrip1.ImageScalingSize = new Size(24, 24);
			menuStrip1.Items.AddRange(new ToolStripItem[] { imageOpenToolStripMenuItem });
			menuStrip1.Location = new Point(0, 0);
			menuStrip1.Name = "menuStrip1";
			menuStrip1.Size = new Size(1337, 33);
			menuStrip1.TabIndex = 7;
			menuStrip1.Text = "menuStrip1";
			// 
			// imageOpenToolStripMenuItem
			// 
			imageOpenToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { imageToolStripMenuItem, imageSaveToolStripMenuItem });
			imageOpenToolStripMenuItem.Name = "imageOpenToolStripMenuItem";
			imageOpenToolStripMenuItem.Size = new Size(55, 29);
			imageOpenToolStripMenuItem.Text = "File";
			imageOpenToolStripMenuItem.Click += imageOpenToolStripMenuItem_Click;
			// 
			// imageToolStripMenuItem
			// 
			imageToolStripMenuItem.Name = "imageToolStripMenuItem";
			imageToolStripMenuItem.Size = new Size(210, 34);
			imageToolStripMenuItem.Text = "ImageOpen";
			imageToolStripMenuItem.Click += imageToolStripMenuItem_Click;
			// 
			// imageSaveToolStripMenuItem
			// 
			imageSaveToolStripMenuItem.Name = "imageSaveToolStripMenuItem";
			imageSaveToolStripMenuItem.Size = new Size(210, 34);
			imageSaveToolStripMenuItem.Text = "ImageSave";
			imageSaveToolStripMenuItem.Click += imageSaveToolStripMenuItem_Click;
			// 
			// btnTestCrop
			// 
			btnTestCrop.Location = new Point(1151, 67);
			btnTestCrop.Name = "btnTestCrop";
			btnTestCrop.Size = new Size(140, 34);
			btnTestCrop.TabIndex = 4;
			btnTestCrop.Text = "크롭 테스트";
			btnTestCrop.UseVisualStyleBackColor = true;
			btnTestCrop.Click += btnTestCrop_Click;
			// 
			// MainForm
			// 
			AutoScaleDimensions = new SizeF(10F, 25F);
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(1337, 916);
			Controls.Add(panelBottom);
			Controls.Add(_imagePanel);
			Controls.Add(panel1);
			Controls.Add(menuStrip1);
			MainMenuStrip = menuStrip1;
			Name = "MainForm";
			Text = "MainForm";
			Load += MainForm_Load;
			_imagePanel.ResumeLayout(false);
			_imagePanel.PerformLayout();
			((System.ComponentModel.ISupportInitialize)pictureBoxFrame).EndInit();
			panel1.ResumeLayout(false);
			panel1.PerformLayout();
			panelBottom.ResumeLayout(false);
			panelLog.ResumeLayout(false);
			panelStatus.ResumeLayout(false);
			panelStatus.PerformLayout();
			menuStrip1.ResumeLayout(false);
			menuStrip1.PerformLayout();
			ResumeLayout(false);
			PerformLayout();
		}

		#endregion

		private Button btnRun;
		private Button btnStop;
		private Panel _imagePanel;
		private PictureBox pictureBoxFrame;
		private TextBox txtDate;
		private PictureBox _pictureBoxFrame;
		private TextBox textBox1;
		private Button btnDate;
		private Panel panel1;
		private Button btnBarcode;
		private TextBox txtBarcode;
		private Panel panelBottom;
		private Panel panelStatus;
		private Panel panelLog;
		private MenuStrip menuStrip1;
		private ToolStripMenuItem imageOpenToolStripMenuItem;
		private ToolStripMenuItem imageToolStripMenuItem;
		private ToolStripMenuItem imageSaveToolStripMenuItem;
		private Label lblInspectionCount;
		private Label lblInspectionRate;
		private Label lblInspectionSummary;
		private Label lblResult;
		private ListView lvLogs;
		private Label label1;
		private Label label2;
		private Button btnTestCrop;
	}
}