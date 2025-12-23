using Hexa.NET.ImGui.Widgets.Dialogs;

namespace GBOG
{
    public class FileOpenDialog : OpenFileDialog
    {
        public string DialogName { get; set; }

        public FileOpenDialog() : base()
        {
            DialogName = "FileOpenDialog";
        }

        public FileOpenDialog(string name) : base()
        {
            DialogName = name;
        }
    }
}
