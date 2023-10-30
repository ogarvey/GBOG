using GBOG.CPU;
using GBOG.Graphics.MonoGame;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GBOG
{
  public partial class DebugView : Form
  {
    private Gameboy? _gb;

    public DebugView(Gameboy gb)
    {
      _gb = gb;
      InitializeComponent();
      InitControls();
    }

    private void InitControls()
    {
      txtAF.Text = _gb.AF.ToString("X4");
      txtPC.Text = _gb.PC.ToString("X4");
    }

    void btnStartDebug_Click(object sender, EventArgs e)
    {
      using var gbGame = new GameboyGame(_gb);
      gbGame.Run();
    }

    private void btnStep_Click(object sender, EventArgs e)
    {
      txtAF.Text = _gb.AF.ToString("X4");
    }

    private void btnQuitGame_Click(object sender, EventArgs e)
    {
      _gb?.EndGame();
      _gb = null;
      ActiveForm.Close();
    }
  }
}
