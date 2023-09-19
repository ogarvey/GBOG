namespace GBOG
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
			btnLoadRom = new Button();
			openFileDialog1 = new OpenFileDialog();
			txtSerialData = new TextBox();
			txtLogData = new TextBox();
			btnCompareLog = new Button();
			pictureBox1 = new PictureBox();
			((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
			SuspendLayout();
			// 
			// btnLoadRom
			// 
			btnLoadRom.Location = new Point(782, 536);
			btnLoadRom.Margin = new Padding(3, 4, 3, 4);
			btnLoadRom.Name = "btnLoadRom";
			btnLoadRom.Size = new Size(119, 48);
			btnLoadRom.TabIndex = 0;
			btnLoadRom.Text = "Load Rom File";
			btnLoadRom.UseVisualStyleBackColor = true;
			btnLoadRom.Click += btnLoadRom_Click;
			// 
			// openFileDialog1
			// 
			openFileDialog1.FileName = "openFileDialog1";
			// 
			// txtSerialData
			// 
			txtSerialData.Location = new Point(14, 473);
			txtSerialData.Margin = new Padding(3, 4, 3, 4);
			txtSerialData.Multiline = true;
			txtSerialData.Name = "txtSerialData";
			txtSerialData.Size = new Size(761, 109);
			txtSerialData.TabIndex = 1;
			// 
			// txtLogData
			// 
			txtLogData.Location = new Point(14, 16);
			txtLogData.Margin = new Padding(3, 4, 3, 4);
			txtLogData.Multiline = true;
			txtLogData.Name = "txtLogData";
			txtLogData.ScrollBars = ScrollBars.Vertical;
			txtLogData.Size = new Size(761, 448);
			txtLogData.TabIndex = 2;
			// 
			// btnCompareLog
			// 
			btnCompareLog.Location = new Point(782, 473);
			btnCompareLog.Margin = new Padding(3, 4, 3, 4);
			btnCompareLog.Name = "btnCompareLog";
			btnCompareLog.Size = new Size(119, 48);
			btnCompareLog.TabIndex = 3;
			btnCompareLog.Text = "Compare Log";
			btnCompareLog.UseVisualStyleBackColor = true;
			btnCompareLog.Click += btnCompareLog_Click;
			// 
			// pictureBox1
			// 
			pictureBox1.Location = new Point(784, 16);
			pictureBox1.Name = "pictureBox1";
			pictureBox1.Size = new Size(666, 447);
			pictureBox1.TabIndex = 4;
			pictureBox1.TabStop = false;
			// 
			// Form1
			// 
			AutoScaleDimensions = new SizeF(8F, 20F);
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(1464, 600);
			Controls.Add(pictureBox1);
			Controls.Add(btnCompareLog);
			Controls.Add(txtLogData);
			Controls.Add(txtSerialData);
			Controls.Add(btnLoadRom);
			Margin = new Padding(3, 4, 3, 4);
			Name = "Form1";
			Text = "Form1";
			((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
			ResumeLayout(false);
			PerformLayout();
		}

		#endregion

		private Button btnLoadRom;
		private OpenFileDialog openFileDialog1;
		private TextBox txtSerialData;
		private TextBox txtLogData;
		private Button btnCompareLog;
		private PictureBox pictureBox1;
	}
}