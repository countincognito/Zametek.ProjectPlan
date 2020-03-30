namespace Zametek.Contract.ProjectPlan
{
    public interface IFileDialogService
    {
        string Filename { get; }

        string Directory { get; }

        bool ShowSaveDialog(string initialDirectory, string associatedFileType, string associatedFileExtension);

        bool ShowOpenDialog(string initialDirectory, string associatedFileType, string associatedFileExtension);
    }
}
