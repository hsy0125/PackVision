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
            pbCamera = new PictureBox();
            btnStart = new Button();
            btnStop = new Button();
            btnDateRoi = new Button();
            btnBarcodeRoi = new Button();
            pictureBox2 = new PictureBox();
            lblDebug = new Label();
            ((System.ComponentModel.ISupportInitialize)pbCamera).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox2).BeginInit();
            SuspendLayout();
            // 
            // pbCamera
            // 
            pbCamera.Location = new Point(12, 12);
            pbCamera.Name = "pbCamera";
            pbCamera.Size = new Size(768, 596);
            pbCamera.SizeMode = PictureBoxSizeMode.StretchImage;
            pbCamera.TabIndex = 0;
            pbCamera.TabStop = false;
            pbCamera.Paint += pbCamera_Paint;
            pbCamera.MouseDown += pbCamera_MouseDown;
            pbCamera.MouseMove += pbCamera_MouseMove;
            pbCamera.MouseUp += pbCamera_MouseUp;
            // 
            // btnStart
            // 
            btnStart.Location = new Point(786, 75);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(120, 56);
            btnStart.TabIndex = 1;
            btnStart.Text = "시작버튼";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click_1;
            // 
            // btnStop
            // 
            btnStop.Location = new Point(786, 161);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(120, 56);
            btnStop.TabIndex = 2;
            btnStop.Text = "중지버튼";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click_1;
            // 
            // btnDateRoi
            // 
            btnDateRoi.Location = new Point(786, 243);
            btnDateRoi.Name = "btnDateRoi";
            btnDateRoi.Size = new Size(213, 56);
            btnDateRoi.TabIndex = 3;
            btnDateRoi.Text = "날짜 ROI test";
            btnDateRoi.UseVisualStyleBackColor = true;
            // 
            // btnBarcodeRoi
            // 
            btnBarcodeRoi.Location = new Point(786, 322);
            btnBarcodeRoi.Name = "btnBarcodeRoi";
            btnBarcodeRoi.Size = new Size(204, 56);
            btnBarcodeRoi.TabIndex = 4;
            btnBarcodeRoi.Text = "바코드 ROI test";
            btnBarcodeRoi.UseVisualStyleBackColor = true;
            // 
            // pictureBox2
            // 
            pictureBox2.Location = new Point(743, 54);
            pictureBox2.Name = "pictureBox2";
            pictureBox2.Size = new Size(8, 8);
            pictureBox2.TabIndex = 5;
            pictureBox2.TabStop = false;
            // 
            // lblDebug
            // 
            lblDebug.AutoSize = true;
            lblDebug.Location = new Point(820, 498);
            lblDebug.Name = "lblDebug";
            lblDebug.Size = new Size(86, 25);
            lblDebug.TabIndex = 6;
            lblDebug.Text = "lblDebug";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1111, 620);
            Controls.Add(lblDebug);
            Controls.Add(pictureBox2);
            Controls.Add(btnBarcodeRoi);
            Controls.Add(btnDateRoi);
            Controls.Add(btnStop);
            Controls.Add(btnStart);
            Controls.Add(pbCamera);
            Name = "MainForm";
            Text = "MainForm";
            ((System.ComponentModel.ISupportInitialize)pbCamera).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureBox2).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private PictureBox pbCamera;
        private Button btnDateRoi;
        private Button btnBarcodeRoi;
        private Button btnStop;
        private Button btnStop_Click;
        private Button btnStart;
        private Button btnStart_Click;
        private Label lblDebug;
        private PictureBox pictureBox2;
    }
}