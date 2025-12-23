using System.Numerics;

namespace GBOG.Controls.ImGuiHexEditor;

public struct HexEditorHighlightRange
{
    public int From;
    public int To;
    public uint Color;
    public uint BorderColor;
    public HexEditorHighlightFlags Flags;
}
