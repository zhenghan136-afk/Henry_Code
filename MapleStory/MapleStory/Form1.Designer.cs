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
            // Form1
            //
            AutoScaleDimensions = new SizeF(11F, 23F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(txtWindowTitle);
            Controls.Add(lblWindowTitle);
            Controls.Add(lblStatus);
            Controls.Add(cmbTemplate);
            Controls.Add(btnStop);
            Controls.Add(btnStart);
            Name = "Form1";
            Text = "Form1";
            Load += Form1_Load;
            ResumeLayout(false);
        }

        #endregion

        private Button btnStart;
        private Button btnStop;
        private ComboBox cmbTemplate;
        private Label lblStatus;
        private Label lblWindowTitle;
        private TextBox txtWindowTitle;
    }
}
