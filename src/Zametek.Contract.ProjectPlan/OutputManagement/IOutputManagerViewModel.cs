namespace Zametek.Contract.ProjectPlan
{
    public interface IOutputManagerViewModel
        : IKillSubscriptions
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        string CompilationOutput { get; }

        void BuildCompilationOutput();
    }
}
