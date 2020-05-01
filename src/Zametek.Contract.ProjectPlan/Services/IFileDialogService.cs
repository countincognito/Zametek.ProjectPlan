using System.Linq;
using System.Text;

namespace Zametek.Contract.ProjectPlan
{
    public interface IFileDialogService
    {
        string Filename { get; }

        string Directory { get; }

        bool ShowSaveDialog(string initialDirectory, FileDialogFileTypeFilter filter);

        bool ShowOpenDialog(string initialDirectory, FileDialogFileTypeFilter filter);
    }
}
