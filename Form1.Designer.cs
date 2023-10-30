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
      btnViewTileData = new Button();
      tableLayoutPanel1 = new TableLayoutPanel();
      tableLayoutPanel2 = new TableLayoutPanel();
      btnQuitGame = new Button();
      btnStartGame = new Button();
      btnDebug = new Button();
      tableLayoutPanel1.SuspendLayout();
      tableLayoutPanel2.SuspendLayout();
      SuspendLayout();
      // 
      // btnLoadRom
      // 
      btnLoadRom.Location = new Point(3, 48);
      btnLoadRom.Name = "btnLoadRom";
      btnLoadRom.Size = new Size(104, 36);
      btnLoadRom.TabIndex = 0;
      btnLoadRom.Text = "Load ROM";
      btnLoadRom.UseVisualStyleBackColor = true;
      btnLoadRom.Click += btnLoadRom_Click;
      // 
      // openFileDialog1
      // 
      openFileDialog1.FileName = "openFileDialog1";
      // 
      // txtSerialData
      // 
      txtSerialData.Dock = DockStyle.Fill;
      txtSerialData.Location = new Point(3, 385);
      txtSerialData.Multiline = true;
      txtSerialData.Name = "txtSerialData";
      txtSerialData.Size = new Size(1158, 228);
      txtSerialData.TabIndex = 1;
      // 
      // btnViewTileData
      // 
      btnViewTileData.Enabled = false;
      btnViewTileData.Location = new Point(3, 3);
      btnViewTileData.Name = "btnViewTileData";
      btnViewTileData.Size = new Size(104, 36);
      btnViewTileData.TabIndex = 3;
      btnViewTileData.Text = "View TileData";
      btnViewTileData.UseVisualStyleBackColor = true;
      btnViewTileData.Click += btnViewTileData_Click;
      // 
      // tableLayoutPanel1
      // 
      tableLayoutPanel1.ColumnCount = 2;
      tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 90.86651F));
      tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 9.13349F));
      tableLayoutPanel1.Controls.Add(txtSerialData, 0, 1);
      tableLayoutPanel1.Controls.Add(tableLayoutPanel2, 1, 1);
      tableLayoutPanel1.Dock = DockStyle.Fill;
      tableLayoutPanel1.Location = new Point(0, 0);
      tableLayoutPanel1.Name = "tableLayoutPanel1";
      tableLayoutPanel1.RowCount = 2;
      tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 62.0129852F));
      tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 37.9870148F));
      tableLayoutPanel1.Size = new Size(1281, 616);
      tableLayoutPanel1.TabIndex = 4;
      // 
      // tableLayoutPanel2
      // 
      tableLayoutPanel2.ColumnCount = 1;
      tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
      tableLayoutPanel2.Controls.Add(btnViewTileData, 0, 0);
      tableLayoutPanel2.Controls.Add(btnLoadRom, 0, 1);
      tableLayoutPanel2.Controls.Add(btnQuitGame, 0, 4);
      tableLayoutPanel2.Controls.Add(btnStartGame, 0, 2);
      tableLayoutPanel2.Controls.Add(btnDebug, 0, 3);
      tableLayoutPanel2.Dock = DockStyle.Fill;
      tableLayoutPanel2.Location = new Point(1167, 385);
      tableLayoutPanel2.Name = "tableLayoutPanel2";
      tableLayoutPanel2.RowCount = 5;
      tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
      tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
      tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
      tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
      tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
      tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
      tableLayoutPanel2.Size = new Size(111, 228);
      tableLayoutPanel2.TabIndex = 2;
      // 
      // btnQuitGame
      // 
      btnQuitGame.Enabled = false;
      btnQuitGame.Location = new Point(3, 183);
      btnQuitGame.Name = "btnQuitGame";
      btnQuitGame.Size = new Size(104, 36);
      btnQuitGame.TabIndex = 5;
      btnQuitGame.Text = "End Game";
      btnQuitGame.UseVisualStyleBackColor = true;
      btnQuitGame.Click += btnQuitGame_Click;
      // 
      // btnStartGame
      // 
      btnStartGame.Enabled = false;
      btnStartGame.Location = new Point(3, 93);
      btnStartGame.Name = "btnStartGame";
      btnStartGame.Size = new Size(104, 36);
      btnStartGame.TabIndex = 4;
      btnStartGame.Text = "Start ROM";
      btnStartGame.UseVisualStyleBackColor = true;
      btnStartGame.Click += btnStartGame_Click;
      // 
      // btnDebug
      // 
      btnDebug.Dock = DockStyle.Fill;
      btnDebug.Location = new Point(3, 138);
      btnDebug.Name = "btnDebug";
      btnDebug.Size = new Size(105, 39);
      btnDebug.TabIndex = 6;
      btnDebug.Text = "Debug ROM";
      btnDebug.UseVisualStyleBackColor = true;
      btnDebug.Click += btnDebug_Click;
      // 
      // Form1
      // 
      AutoScaleDimensions = new SizeF(7F, 15F);
      AutoScaleMode = AutoScaleMode.Font;
      AutoSizeMode = AutoSizeMode.GrowAndShrink;
      ClientSize = new Size(1281, 616);
      Controls.Add(tableLayoutPanel1);
      Name = "Form1";
      Text = "Form1";
      FormClosed += Form1_FormClosed;
      tableLayoutPanel1.ResumeLayout(false);
      tableLayoutPanel1.PerformLayout();
      tableLayoutPanel2.ResumeLayout(false);
      ResumeLayout(false);
    }

    #endregion

    private Button btnLoadRom;
    private OpenFileDialog openFileDialog1;
    private TextBox txtSerialData;
    private TextBox txtLogData;
    private Button btnViewTileData;
    private TableLayoutPanel tableLayoutPanel1;
    private TableLayoutPanel tableLayoutPanel2;
    private PictureBox pbTileData;
    private Button btnStartGame;
    private Button btnQuitGame;
    private Button btnDebug;
  }
}