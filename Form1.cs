using GBOG.CPU;
using GBOG.Graphics.UI;

namespace GBOG
{
	public partial class Form1 : Form
	{
		private static Gameboy _gb;
		private static int _cycles = 0;
		public Form1()
		{
			InitializeComponent();
		}

		private void btnLoadRom_Click(object sender, EventArgs e)
		{
			openFileDialog1 = new OpenFileDialog()
			{
				FileName = "Select a Gameboy Rom file",
				Title = "Open Gameboy Rom file"
			};
			if (openFileDialog1.ShowDialog() == DialogResult.OK)
			{
				_gb = new Gameboy();
				//_gb.LogAdded += DisplayLogData;
				_gb._memory.SerialDataReceived += DisplaySerialData;
				_gb.LoadRom(openFileDialog1.FileName);
				btnLoadRom.Enabled = false;
				btnStartGame.Enabled = true;
			}
		}

		private void DisplaySerialData(object? sender, char data)
		{
			if (InvokeRequired)
			{
				Invoke(new Action(() => DisplaySerialData(sender, data)));
			}
			else
			{
				txtSerialData.Text += data;
			}
		}
		private void DisplayLogData(object? sender, string data)
		{
			if (InvokeRequired)
			{
				// We are not on the UI thread; invoke the method on the UI thread.
				Invoke(new Action(() => DisplayLogData(sender, data)));
			}
			else
			{
				// We are on the UI thread; update the UI directly.
				// Your UI update code here.
				this.Text = (++_cycles).ToString();
				//txtLogData.Text += data + "\r\n";
			}
		}

		private void btnViewTileData_Click(object sender, EventArgs e)
		{
			var tileDataViewer = new TileDataViewer(_gb);
			tileDataViewer.Show();
		}

		private void Form1_FormClosed(object sender, FormClosedEventArgs e)
		{
			if (_gb != null)
			{
				_gb._memory.SerialDataReceived -= DisplaySerialData;
			}
		}

		private async void btnStartGame_Click(object sender, EventArgs e)
		{
			btnViewTileData.Enabled = true;
			btnQuitGame.Enabled = true;
			await _gb.RunGame();
		}

		private void btnQuitGame_Click(object sender, EventArgs e)
		{
			_gb._memory.SerialDataReceived -= DisplaySerialData;
			_gb.EndGame();
			_gb = null;
			btnViewTileData.Enabled = false;
			btnQuitGame.Enabled = false;
			btnStartGame.Enabled = false;
			btnLoadRom.Enabled = true;
		}
	}

}