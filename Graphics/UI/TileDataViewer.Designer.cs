﻿namespace GBOG.Graphics.UI
{
	partial class TileDataViewer
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
			pbTileData = new PictureBox();
			tableLayoutPanel1 = new TableLayoutPanel();
			tableLayoutPanel2 = new TableLayoutPanel();
			btnRefreshData = new Button();
			btnDisplayScreenData = new Button();
			((System.ComponentModel.ISupportInitialize)pbTileData).BeginInit();
			tableLayoutPanel1.SuspendLayout();
			tableLayoutPanel2.SuspendLayout();
			SuspendLayout();
			// 
			// pbTileData
			// 
			pbTileData.Dock = DockStyle.Fill;
			pbTileData.Location = new Point(3, 59);
			pbTileData.Name = "pbTileData";
			pbTileData.Size = new Size(944, 557);
			pbTileData.TabIndex = 0;
			pbTileData.TabStop = false;
			// 
			// tableLayoutPanel1
			// 
			tableLayoutPanel1.ColumnCount = 1;
			tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
			tableLayoutPanel1.Controls.Add(pbTileData, 0, 1);
			tableLayoutPanel1.Controls.Add(tableLayoutPanel2, 0, 0);
			tableLayoutPanel1.Dock = DockStyle.Fill;
			tableLayoutPanel1.Location = new Point(0, 0);
			tableLayoutPanel1.Name = "tableLayoutPanel1";
			tableLayoutPanel1.RowCount = 2;
			tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 9.111111F));
			tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 90.8888855F));
			tableLayoutPanel1.Size = new Size(950, 619);
			tableLayoutPanel1.TabIndex = 1;
			// 
			// tableLayoutPanel2
			// 
			tableLayoutPanel2.ColumnCount = 4;
			tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
			tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
			tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
			tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
			tableLayoutPanel2.Controls.Add(btnRefreshData, 0, 0);
			tableLayoutPanel2.Controls.Add(btnDisplayScreenData, 1, 0);
			tableLayoutPanel2.Dock = DockStyle.Fill;
			tableLayoutPanel2.Location = new Point(3, 3);
			tableLayoutPanel2.Name = "tableLayoutPanel2";
			tableLayoutPanel2.RowCount = 1;
			tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
			tableLayoutPanel2.Size = new Size(944, 50);
			tableLayoutPanel2.TabIndex = 1;
			// 
			// btnRefreshData
			// 
			btnRefreshData.Location = new Point(3, 3);
			btnRefreshData.Name = "btnRefreshData";
			btnRefreshData.Size = new Size(106, 29);
			btnRefreshData.TabIndex = 1;
			btnRefreshData.Text = "Refresh Data";
			btnRefreshData.UseVisualStyleBackColor = true;
			btnRefreshData.Click += btnRefreshData_Click;
			// 
			// btnDisplayScreenData
			// 
			btnDisplayScreenData.Location = new Point(239, 3);
			btnDisplayScreenData.Name = "btnDisplayScreenData";
			btnDisplayScreenData.Size = new Size(138, 29);
			btnDisplayScreenData.TabIndex = 2;
			btnDisplayScreenData.Text = "Display Screen Data";
			btnDisplayScreenData.UseVisualStyleBackColor = true;
			btnDisplayScreenData.Click += btnDisplayScreenData_Click;
			// 
			// TileDataViewer
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(950, 619);
			Controls.Add(tableLayoutPanel1);
			Name = "TileDataViewer";
			Text = "TileDataViewer";
			((System.ComponentModel.ISupportInitialize)pbTileData).EndInit();
			tableLayoutPanel1.ResumeLayout(false);
			tableLayoutPanel2.ResumeLayout(false);
			ResumeLayout(false);
		}

		#endregion

		private PictureBox pbTileData;
		private TableLayoutPanel tableLayoutPanel1;
		private Button btnRefreshData;
		private TableLayoutPanel tableLayoutPanel2;
		private Button btnDisplayScreenData;
	}
}