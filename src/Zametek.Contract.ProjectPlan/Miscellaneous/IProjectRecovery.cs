using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    /// <summary>
    /// The recovery copy of the open project: a project file that the application keeps apart from the files of the user,
    /// for a run that ends before the user saves. A copy is not a save. It changes nothing of the project.
    /// </summary>
    public interface IProjectRecovery
        : IDisposable
    {
        /// <summary>
        /// Starts the session of this run, and the copies at an interval.
        /// </summary>
        void Start();

        /// <summary>
        /// Writes a copy when the setting says so and the plan has changes that the last copy does not have. This is what
        /// the interval does. A failure goes to the log.
        /// </summary>
        /// <returns>Whether it wrote a copy.</returns>
        Task<bool> WriteCopyIfChangedAsync();

        /// <summary>
        /// Writes a copy now, whatever the setting says. A failure goes to the log.
        /// </summary>
        /// <param name="reason">One of <see cref="RecoveryCopyReasons"/>.</param>
        /// <returns>The copy, or null when it could not be written.</returns>
        Task<RecoveryCopyModel?> WriteCopyAsync(string reason);

        /// <summary>
        /// Asks the user about each copy that an earlier run left, the newest first: to open it, to keep it for the next
        /// start, or to delete it. A copy that the user opens is the project of this run, and the question about the other
        /// copies waits for the next start.
        /// </summary>
        /// <returns>Whether the user opened a copy.</returns>
        Task<bool> OfferLeftCopiesAsync();

        /// <summary>
        /// Ends the session. A run that ends well removes its copy: the user saved, or agreed to lose the changes. A run
        /// that ends after an error keeps it for the next run.
        /// </summary>
        void End(bool keepCopy);
    }
}
