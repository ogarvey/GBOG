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
			SuspendLayout();
			// 
			// btnLoadRom
			// 
			btnLoadRom.Location = new Point(684, 402);
			btnLoadRom.Name = "btnLoadRom";
			btnLoadRom.Size = new Size(104, 36);
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
			txtSerialData.Location = new Point(12, 355);
			txtSerialData.Multiline = true;
			txtSerialData.Name = "txtSerialData";
			txtSerialData.Size = new Size(666, 83);
			txtSerialData.TabIndex = 1;
			// 
			// txtLogData
			// 
			txtLogData.Location = new Point(12, 12);
			txtLogData.Multiline = true;
			txtLogData.Name = "txtLogData";
			txtLogData.ScrollBars = ScrollBars.Vertical;
			txtLogData.Size = new Size(666, 337);
			txtLogData.TabIndex = 2;
			// 
			// btnCompareLog
			// 
			btnCompareLog.Location = new Point(684, 355);
			btnCompareLog.Name = "btnCompareLog";
			btnCompareLog.Size = new Size(104, 36);
			btnCompareLog.TabIndex = 3;
			btnCompareLog.Text = "Compare Log";
			btnCompareLog.UseVisualStyleBackColor = true;
			btnCompareLog.Click += btnCompareLog_Click;
			// 
			// Form1
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(800, 450);
			Controls.Add(btnCompareLog);
			Controls.Add(txtLogData);
			Controls.Add(txtSerialData);
			Controls.Add(btnLoadRom);
			Name = "Form1";
			Text = "Form1";
			ResumeLayout(false);
			PerformLayout();
		}

		#endregion

		private Button btnLoadRom;
		private OpenFileDialog openFileDialog1;
		private TextBox txtSerialData;
		private TextBox txtLogData;
		private Button btnCompareLog;
	}
}