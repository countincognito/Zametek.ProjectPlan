using Zametek.Contract.ProjectPlan;

namespace Zametek.Engine.ProjectPlan
{
    /// <summary>
    /// The engine's <see cref="IUIDispatcher"/>: a job has no UI thread, so the work runs on the
    /// thread that asked for it, before the call returns. Nothing a job does is bound to anything,
    /// so there is no thread it has to run on - and running it here is what keeps the work, and
    /// everything it holds, inside the job that wanted it.
    /// </summary>
    public sealed class InlineUIDispatcher
        : IUIDispatcher
    {
        public Task InvokeAsync(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);

            try
            {
                action();
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                // A dispatcher hands the failure back through the task it returned, and the
                // callers await that inside their own error handling, so it goes back the same way.
                return Task.FromException(ex);
            }
        }

        public void Defer(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);

            // There is nothing already waiting to run, and nothing that will run this later, so
            // "after the work already waiting" is now.
            action();
        }
    }
}
