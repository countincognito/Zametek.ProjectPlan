using System.Windows.Forms;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IFileDialogService
    {
        string Filename
        {
            get;
        }

        string Directory
        {
            get;
        }

        DialogResult ShowSaveDialog(string initialDirectory, string associatedFileType, string associatedFileExtension);
        DialogResult ShowOpenDialog(string initialDirectory, string associatedFileType, string associatedFileExtension);
    }
}
