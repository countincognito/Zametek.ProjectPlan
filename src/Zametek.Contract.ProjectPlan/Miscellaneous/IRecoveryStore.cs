using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    /// <summary>
    /// The place of the recovery copies. Each run of the application (a session) has one copy at most, which each new copy
    /// replaces. A copy that a run left when it ended is there for the next run to find.
    /// </summary>
    public interface IRecoveryStore
        : IDisposable
    {
        /// <summary>
        /// Starts the session of this run. From now on, another run of the application knows that this run is alive, and
        /// it does not take the copy of this run for a copy that a run left.
        /// </summary>
        void BeginSession();

        /// <summary>
        /// The copy of this session, or null when it has none.
        /// </summary>
        RecoveryCopyModel? Copy { get; }

        /// <summary>
        /// Writes the copy of this session, in the place of the copy before it. A write that fails leaves the copy before it.
        /// </summary>
        /// <param name="copy">What the copy is of. The store gives the file.</param>
        /// <param name="write">Writes the project file to the stream.</param>
        Task<RecoveryCopyModel> WriteCopyAsync(RecoveryCopyModel copy, Func<Stream, Task> write);

        /// <summary>
        /// Removes the copy of this session, if it has one.
        /// </summary>
        void DeleteCopy();

        /// <summary>
        /// The copies that earlier runs left, the newest first. The copy of a run that is alive is not one of them.
        /// </summary>
        IReadOnlyList<RecoveryCopyModel> FindLeftCopies();

        /// <summary>
        /// Removes a copy that an earlier run left.
        /// </summary>
        void Remove(RecoveryCopyModel copy);
    }
}
