namespace Zametek.Contract.ProjectPlan
{
    public interface IOutputManagerViewModel
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        string CompilationOutput { get; }
    }
}
