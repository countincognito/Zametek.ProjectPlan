namespace Zametek.Contract.ProjectPlan
{
    public interface IOutputManagerViewModel
        : IStartSubscriptions, IKillSubscriptions, IDisposable
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        string CompilationOutput { get; }

        void BuildCompilationOutput();
    }
}
