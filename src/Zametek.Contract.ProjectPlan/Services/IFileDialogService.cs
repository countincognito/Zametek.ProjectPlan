namespace Zametek.Contract.ProjectPlan
{
    public interface IFileDialogService
    {
        string Filename { get; }

        string Directory { get; }

        bool ShowSaveDialog(string initialDirectory, IFileDialogFileTypeFilter filter);

        bool ShowOpenDialog(string initialDirectory, IFileDialogFileTypeFilter filter);
    }
}
