using GBOG.CPU;
using GBOG.Graphics.UI;
using GBOG.Utils;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace GBOG
{
	public partial class Form1 : Form
	{
		private static Gameboy _gb;
		private static int _cycles = 0;
		public Form1()
		{
			_gb = new Gameboy();
			InitializeComponent();
		}

		private async void btnLoadRom_Click(object sender, EventArgs e)
		{
			openFileDialog1 = new OpenFileDialog()
			{
				FileName = "Select a Gameboy Rom file",
				Title = "Open Gameboy Rom file"
			};
			if (openFileDialog1.ShowDialog() == DialogResult.OK)
			{
				//_gb.LogAdded += DisplayLogData;
				_gb._memory.SerialDataReceived += DisplaySerialData;
				// call _gb.LoadRom on a separate thread from the UI
				await Task.Run(() =>
				{
					_gb.LoadRom(openFileDialog1.FileName);
				});
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

		private void btnCompareLog_Click(object sender, EventArgs e)
		{

		}

		private void btnViewTileData_Click(object sender, EventArgs e)
		{
			var tileDataViewer = new TileDataViewer(_gb);
			tileDataViewer.Show();
		}
	}

}