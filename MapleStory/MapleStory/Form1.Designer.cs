namespace MapleStory
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnStart = new Button();
            btnStop = new Button();
            cmbTemplate = new ComboBox();
            lblStatus = new Label();
            lblWindowTitle = new Label();
            txtWindowTitle = new TextBox();
            btnCastSet3 = new Button();
            lblSet3Countdown = new Label();
            chkOverlay = new CheckBox();
            btnMoveOverlay = new Button();
            chkMpMonitor = new CheckBox();
            lblMpThreshold = new Label();
            numMpThreshold = new NumericUpDown();
            lblMpPotionKey = new Label();
            cmbMpPotionKey = new ComboBox();
            btnCalibrateMp = new Button();
            lblMpStatus = new Label();
            ((System.ComponentModel.ISupportInitialize)numMpThreshold).BeginInit();
            SuspendLayout();
            // 
            // btnStart
            // 
            btnStart.Location = new Point(615, 44);
            btnStart.Name = "btnStart";
            btnStart.Size = new Size(112, 34);
            btnStart.TabIndex = 1;
            btnStart.Text = "Start";
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // btnStop
            // 
            btnStop.Location = new Point(615, 96);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(112, 34);
            btnStop.TabIndex = 2;
            btnStop.Text = "Stop";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;
            // 
            // cmbTemplate
            // 
            cmbTemplate.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbTemplate.Location = new Point(34, 44);
            cmbTemplate.Name = "cmbTemplate";
            cmbTemplate.Size = new Size(160, 31);
            cmbTemplate.TabIndex = 0;
            cmbTemplate.SelectedIndexChanged += cmbTemplate_SelectedIndexChanged;
            //
            // lblStatus
            //
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(34, 90);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(80, 20);
            lblStatus.TabIndex = 3;
            lblStatus.Text = "狀態：待機中";
            //
            // lblWindowTitle
            //
            lblWindowTitle.AutoSize = true;
            lblWindowTitle.Location = new Point(34, 130);
            lblWindowTitle.Name = "lblWindowTitle";
            lblWindowTitle.Size = new Size(160, 20);
            lblWindowTitle.TabIndex = 4;
            lblWindowTitle.Text = "遊戲視窗標題關鍵字：";
            //
            // txtWindowTitle
            //
            txtWindowTitle.Location = new Point(34, 155);
            txtWindowTitle.Name = "txtWindowTitle";
            txtWindowTitle.Size = new Size(160, 27);
            txtWindowTitle.TabIndex = 5;
            txtWindowTitle.Text = "MapleStory";
            //
            // btnCastSet3
            //
            btnCastSet3.Location = new Point(615, 148);
            btnCastSet3.Name = "btnCastSet3";
            btnCastSet3.Size = new Size(112, 34);
            btnCastSet3.TabIndex = 6;
            btnCastSet3.Text = "施放技能";
            btnCastSet3.UseVisualStyleBackColor = true;
            btnCastSet3.Visible = false;
            btnCastSet3.Click += btnCastSet3_Click;
            //
            // lblSet3Countdown
            //
            lblSet3Countdown.AutoSize = false;
            lblSet3Countdown.Location = new Point(615, 188);
            lblSet3Countdown.Name = "lblSet3Countdown";
            lblSet3Countdown.Size = new Size(160, 24);
            lblSet3Countdown.TabIndex = 14;
            lblSet3Countdown.Text = "倒數：未開始";
            lblSet3Countdown.Visible = false;
            //
            // chkOverlay
            //
            chkOverlay.AutoSize = true;
            chkOverlay.Location = new Point(615, 218);
            chkOverlay.Name = "chkOverlay";
            chkOverlay.Size = new Size(150, 24);
            chkOverlay.TabIndex = 15;
            chkOverlay.Text = "顯示倒數浮動視窗";
            chkOverlay.UseVisualStyleBackColor = true;
            chkOverlay.Visible = false;
            chkOverlay.CheckedChanged += chkOverlay_CheckedChanged;
            //
            // btnMoveOverlay
            //
            btnMoveOverlay.Location = new Point(615, 248);
            btnMoveOverlay.Name = "btnMoveOverlay";
            btnMoveOverlay.Size = new Size(160, 32);
            btnMoveOverlay.TabIndex = 16;
            btnMoveOverlay.Text = "解鎖位置（可拖曳）";
            btnMoveOverlay.UseVisualStyleBackColor = true;
            btnMoveOverlay.Visible = false;
            btnMoveOverlay.Click += btnMoveOverlay_Click;
            //
            // chkMpMonitor
            //
            chkMpMonitor.AutoSize = true;
            chkMpMonitor.Location = new Point(34, 200);
            chkMpMonitor.Name = "chkMpMonitor";
            chkMpMonitor.Size = new Size(120, 24);
            chkMpMonitor.TabIndex = 7;
            chkMpMonitor.Text = "啟用 MP 監控";
            chkMpMonitor.UseVisualStyleBackColor = true;
            chkMpMonitor.CheckedChanged += chkMpMonitor_CheckedChanged;
            //
            // lblMpThreshold
            //
            lblMpThreshold.AutoSize = true;
            lblMpThreshold.Location = new Point(34, 234);
            lblMpThreshold.Name = "lblMpThreshold";
            lblMpThreshold.Size = new Size(100, 20);
            lblMpThreshold.TabIndex = 8;
            lblMpThreshold.Text = "MP 低於 (%)：";
            //
            // numMpThreshold
            //
            numMpThreshold.Location = new Point(34, 259);
            numMpThreshold.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            numMpThreshold.Name = "numMpThreshold";
            numMpThreshold.Size = new Size(80, 27);
            numMpThreshold.TabIndex = 9;
            numMpThreshold.Value = new decimal(new int[] { 30, 0, 0, 0 });
            //
            // lblMpPotionKey
            //
            lblMpPotionKey.AutoSize = true;
            lblMpPotionKey.Location = new Point(34, 296);
            lblMpPotionKey.Name = "lblMpPotionKey";
            lblMpPotionKey.Size = new Size(90, 20);
            lblMpPotionKey.TabIndex = 10;
            lblMpPotionKey.Text = "補 MP 按鍵：";
            //
            // cmbMpPotionKey
            //
            cmbMpPotionKey.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMpPotionKey.Location = new Point(34, 321);
            cmbMpPotionKey.Name = "cmbMpPotionKey";
            cmbMpPotionKey.Size = new Size(160, 31);
            cmbMpPotionKey.TabIndex = 11;
            //
            // btnCalibrateMp
            //
            btnCalibrateMp.Location = new Point(34, 361);
            btnCalibrateMp.Name = "btnCalibrateMp";
            btnCalibrateMp.Size = new Size(160, 34);
            btnCalibrateMp.TabIndex = 12;
            btnCalibrateMp.Text = "校準 MP 範圍";
            btnCalibrateMp.UseVisualStyleBackColor = true;
            btnCalibrateMp.Click += btnCalibrateMp_Click;
            //
            // lblMpStatus
            //
            lblMpStatus.AutoSize = false;
            lblMpStatus.Location = new Point(34, 400);
            lblMpStatus.Name = "lblMpStatus";
            lblMpStatus.Size = new Size(700, 24);
            lblMpStatus.TabIndex = 13;
            lblMpStatus.Text = "MP 範圍尚未校準";
            //
            // Form1
            //
            AutoScaleDimensions = new SizeF(11F, 23F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(lblMpStatus);
            Controls.Add(btnCalibrateMp);
            Controls.Add(cmbMpPotionKey);
            Controls.Add(lblMpPotionKey);
            Controls.Add(numMpThreshold);
            Controls.Add(lblMpThreshold);
            Controls.Add(chkMpMonitor);
            Controls.Add(btnMoveOverlay);
            Controls.Add(chkOverlay);
            Controls.Add(lblSet3Countdown);
            Controls.Add(btnCastSet3);
            Controls.Add(txtWindowTitle);
            Controls.Add(lblWindowTitle);
            Controls.Add(lblStatus);
            Controls.Add(cmbTemplate);
            Controls.Add(btnStop);
            Controls.Add(btnStart);
            Name = "Form1";
            Text = "Form1";
            Load += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)numMpThreshold).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnStart;
        private Button btnStop;
        private ComboBox cmbTemplate;
        private Label lblStatus;
        private Label lblWindowTitle;
        private TextBox txtWindowTitle;
        private Button btnCastSet3;
        private Label lblSet3Countdown;
        private CheckBox chkOverlay;
        private Button btnMoveOverlay;
        private CheckBox chkMpMonitor;
        private Label lblMpThreshold;
        private NumericUpDown numMpThreshold;
        private Label lblMpPotionKey;
        private ComboBox cmbMpPotionKey;
        private Button btnCalibrateMp;
        private Label lblMpStatus;
    }
}
