namespace Zametek.Contract.ProjectPlan
{
    public interface IFileDialogFileTypeFilter
    {
        string DefaultExtension { get; }

        string ToFileDialogFilterString();
    }
}