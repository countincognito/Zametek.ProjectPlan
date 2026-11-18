using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Globalization;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Keeps the recovery copy of the open project (see <see cref="IProjectRecovery"/>). At an interval, it writes a copy
    /// when the plan has changes that the last copy does not have. It removes the copy as soon as the plan has no changes:
    /// a copy of a plan that is saved is a copy that is older than the file of the user, or the same.
    /// </summary>
    /// <remarks>
    /// The two counters of the save tell a change of the plan (ARCHITECTURE section 9). The marks of a change do not: a plan
    /// that has its mark keeps it at the next change. A copy reads the counters before it takes its copy of the project, as
    /// a save does, and so a change during the write is a change that the next interval sees.
    /// </remarks>
    public sealed class ProjectRecovery
        : IProjectRecovery
    {
        /// <summary>
        /// The time between two copies.
        /// </summary>
        public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

        // The places of the choices of the question about a copy that a run left.
        private const int c_Open = 0;
        private const int c_Delete = 2;

        private readonly IMainViewModel m_MainViewModel;
        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IProjectScenarioManagerViewModel m_ProjectScenarioManagerViewModel;
        private readonly IProjectFileSave m_ProjectFileSave;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;
        private readonly IRecoveryStore m_Store;
        private readonly TimeProvider m_TimeProvider;
        private readonly ILogger<ProjectRecovery> m_Logger;

        // One write of a copy at a time.
        private readonly SemaphoreSlim m_Gate = new(1, 1);
        private readonly Lock m_Lock = new();
        private (int Scenario, int Project)? m_CopiedRevisions;
        private ITimer? m_Timer;
        private bool m_HasStarted;
        private bool m_HasEnded;

        public ProjectRecovery(
            IMainViewModel mainViewModel,
            ICoreViewModel coreViewModel,
            IProjectScenarioManagerViewModel projectScenarioManagerViewModel,
            IProjectFileSave projectFileSave,
            ISettingService settingService,
            IDialogService dialogService,
            IRecoveryStore store,
            TimeProvider timeProvider,
            ILogger<ProjectRecovery> logger)
        {
            ArgumentNullException.ThrowIfNull(mainViewModel);
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(projectScenarioManagerViewModel);
            ArgumentNullException.ThrowIfNull(projectFileSave);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(logger);
            m_MainViewModel = mainViewModel;
            m_CoreViewModel = coreViewModel;
            m_ProjectScenarioManagerViewModel = projectScenarioManagerViewModel;
            m_ProjectFileSave = projectFileSave;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            m_Store = store;
            m_TimeProvider = timeProvider;
            m_Logger = logger;
        }

        #region IProjectRecovery Members

        public void Start()
        {
            lock (m_Lock)
            {
                if (m_HasStarted
                    || m_HasEnded)
                {
                    return;
                }

                m_HasStarted = true;
            }

            try
            {
                m_Store.BeginSession();
            }
            catch (Exception ex) when (IsOfTheFiles(ex))
            {
                // The application runs without recovery copies. A copy that is asked for later says why it fails.
                m_Logger.LogError(ex, "The session of the recovery copies did not start");
            }

            if (m_MainViewModel is INotifyPropertyChanged notifier)
            {
                notifier.PropertyChanged += OnMainPropertyChanged;
            }

            m_Timer = m_TimeProvider.CreateTimer(_ => _ = WriteCopyIfChangedAsync(), state: null, Interval, Interval);
        }

        public async Task<bool> WriteCopyIfChangedAsync()
        {
            if (HasEnded)
            {
                return false;
            }

            // An interval that comes while a copy is written has nothing to add: the next one sees what is left.
            if (!await m_Gate.WaitAsync(TimeSpan.Zero))
            {
                return false;
            }

            try
            {
                if (HasEnded
                    || !m_SettingService.KeepRecoveryCopy
                    || !m_MainViewModel.ProjectHasChanges
                    || CurrentRevisions() == CopiedRevisions)
                {
                    return false;
                }

                return await WriteAsync(RecoveryCopyReasons.Interval) is not null;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                m_Logger.LogError(ex, "The recovery copy at an interval failed");
                return false;
            }
            finally
            {
                m_Gate.Release();
            }
        }

        public async Task<RecoveryCopyModel?> WriteCopyAsync(string reason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            await m_Gate.WaitAsync();

            try
            {
                return await WriteAsync(reason);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                m_Logger.LogError(ex, "The recovery copy failed (reason: {Reason})", reason);
                return null;
            }
            finally
            {
                m_Gate.Release();
            }
        }

        public async Task<bool> OfferLeftCopiesAsync()
        {
            IReadOnlyList<RecoveryCopyModel> copies;

            try
            {
                copies = m_Store.FindLeftCopies();
            }
            catch (Exception ex) when (IsOfTheFiles(ex))
            {
                m_Logger.LogError(ex, "The look for recovery copies failed");
                return false;
            }

            string[] choices =
            [
                Resource.ProjectPlan.Labels.Label_RecoveryCopyOpen,
                Resource.ProjectPlan.Labels.Label_RecoveryCopyNotNow,
                Resource.ProjectPlan.Labels.Label_RecoveryCopyDelete,
            ];

            foreach (RecoveryCopyModel copy in copies)
            {
                m_Logger.LogInformation("Found the recovery copy {RecoveryFile} of {ProjectFile}, written at {WrittenAt}", copy.Filename, copy.ProjectFile, copy.WrittenAt);
                int choice = await m_DialogService.ShowChoiceAsync(
                    Resource.ProjectPlan.Titles.Title_RecoveryCopy,
                    string.Empty,
                    Describe(copy),
                    choices);

                switch (choice)
                {
                    case c_Open:
                        if (await m_MainViewModel.OpenRecoveryCopyAsync(copy))
                        {
                            // This run keeps the project as its own copy from now on. The copy that the other run left goes
                            // only when this run has its own: a run that ends between the two leaves one of them.
                            if (await WriteCopyAsync(RecoveryCopyReasons.Opened) is not null)
                            {
                                Remove(copy);
                            }

                            return true;
                        }

                        break;
                    case c_Delete:
                        m_Logger.LogInformation("The user deleted the recovery copy {RecoveryFile}", copy.Filename);
                        Remove(copy);
                        break;
                    default:
                        // Not now, or a dialog that the user closed: the copy stays, and the next start asks again.
                        break;
                }
            }

            return false;
        }

        public void End(bool keepCopy)
        {
            lock (m_Lock)
            {
                if (m_HasEnded)
                {
                    return;
                }

                m_HasEnded = true;
            }

            m_Timer?.Dispose();

            if (m_MainViewModel is INotifyPropertyChanged notifier)
            {
                notifier.PropertyChanged -= OnMainPropertyChanged;
            }

            if (!keepCopy)
            {
                DeleteCopy();
            }

            m_Store.Dispose();
        }

        #endregion

        #region Private Members

        private bool HasEnded
        {
            get
            {
                lock (m_Lock)
                {
                    return m_HasEnded;
                }
            }
        }

        private (int Scenario, int Project)? CopiedRevisions
        {
            get
            {
                lock (m_Lock)
                {
                    return m_CopiedRevisions;
                }
            }
            set
            {
                lock (m_Lock)
                {
                    m_CopiedRevisions = value;
                }
            }
        }

        private (int Scenario, int Project)? CurrentRevisions() =>
            (m_CoreViewModel.ProjectScenarioUpdateRevision, m_ProjectScenarioManagerViewModel.ProjectUpdateRevision);

        // The copy, with the gate held. The counters are read before the copy of the project is taken, as a save reads them.
        private async Task<RecoveryCopyModel?> WriteAsync(string reason)
        {
            (int Scenario, int Project)? revisions = CurrentRevisions();
            string title = m_SettingService.ProjectTitle;
            var copy = new RecoveryCopyModel
            {
                ProjectFile = ProjectFile(title),
                Title = title,
                WrittenAt = m_TimeProvider.GetLocalNow(),
                Reason = reason,
            };

            ProjectModel projectModel = await m_MainViewModel.BuildProjectAsync();
            RecoveryCopyModel written = await m_Store.WriteCopyAsync(copy, stream => m_ProjectFileSave.SaveProjectFileAsync(projectModel, stream));
            CopiedRevisions = revisions;
            m_Logger.LogInformation("Wrote a recovery copy of {ProjectTitle} to {RecoveryFile} (reason: {Reason})", title, written.Filename, reason);

            // A save that ended during the write removed the copy before this one, and not this one. An error keeps its
            // copy, whatever the marks of the plan say.
            if (reason == RecoveryCopyReasons.Interval
                && !m_MainViewModel.ProjectHasChanges)
            {
                DeleteCopy();
                return null;
            }

            return written;
        }

        // The file of the project, when it has one: a project that the user opened or saved has the name of its file as its
        // title, in the folder of the project.
        private string ProjectFile(string title)
        {
            if (string.IsNullOrWhiteSpace(title)
                || !m_SettingService.IsTitleBoundToFilename)
            {
                return string.Empty;
            }

            return Path.Combine(m_SettingService.ProjectDirectory, $@"{title}.{Resource.ProjectPlan.Filters.Filter_ProjectFileExtension}");
        }

        // The mark of a change is raised where the plan changes: on the UI thread after a save, on a pool thread after a load.
        // A failure here must not reach the save or the load that cleared the mark.
        private void OnMainPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IMainViewModel.ProjectHasChanges)
                && !m_MainViewModel.ProjectHasChanges)
            {
                DeleteCopy();
            }
        }

        private void DeleteCopy()
        {
            try
            {
                m_Store.DeleteCopy();
                CopiedRevisions = null;
            }
            catch (Exception ex) when (IsOfTheFiles(ex))
            {
                m_Logger.LogError(ex, "The recovery copy could not be removed");
            }
        }

        // What the question says of a copy: the file that it is a copy of, or the title of a project with no file, and
        // the time of the copy as the user writes a time.
        private string Describe(RecoveryCopyModel copy)
        {
            string project = !string.IsNullOrWhiteSpace(copy.ProjectFile)
                ? copy.ProjectFile
                : !string.IsNullOrWhiteSpace(copy.Title)
                    ? copy.Title
                    : Resource.ProjectPlan.Messages.Message_RecoveryCopyOfNoName;
            DateTimeOffset writtenAt = TimeZoneInfo.ConvertTime(copy.WrittenAt, m_TimeProvider.LocalTimeZone);

            return string.Format(
                CultureInfo.CurrentCulture,
                Resource.ProjectPlan.Messages.Message_RecoveryCopyFound,
                project,
                writtenAt.ToString(@"g", CultureInfo.CurrentCulture),
                Resource.ProjectPlan.Labels.Label_RecoveryCopyNotNow);
        }

        private void Remove(RecoveryCopyModel copy)
        {
            try
            {
                m_Store.Remove(copy);
            }
            catch (Exception ex) when (IsOfTheFiles(ex))
            {
                m_Logger.LogError(ex, "The recovery copy {RecoveryFile} could not be removed", copy.Filename);
            }
        }

        private static bool IsOfTheFiles(Exception ex) => ex is IOException or UnauthorizedAccessException;

        #endregion

        #region IDisposable Members

        // The gate is not disposed: an interval that is on its way when the run ends may still ask for it, and a gate that
        // nobody waits on by a handle holds nothing to free.
        public void Dispose()
        {
            End(keepCopy: true);
        }

        #endregion
    }
}
