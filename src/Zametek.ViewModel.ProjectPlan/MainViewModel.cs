using Dock.Model.Controls;
using Dock.Model.Core;
using ReactiveUI;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class MainViewModel
        : ViewModelBase, IMainViewModel
    {
        #region Fields

        private readonly object m_Lock;

        private static readonly IList<IFileFilter> s_ProjectPlanFileFilters =
            [
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ProjectPlanFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_ProjectPlanFilePattern
                    ]
                }
            ];

        private static readonly IList<IFileFilter> s_ImportFileFilters =
            [
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_MicrosoftProjectFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_MicrosoftProjectMppFilePattern,
                        Resource.ProjectPlan.Filters.Filter_MicrosoftProjectXmlFilePattern
                    ]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ProjectXlsxFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_ProjectXlsxFilePattern
                    ]
                }
            ];

        private static readonly IList<IFileFilter> s_ExportFileFilters =
            [
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ExcelFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_ExcelXlsxFilePattern
                    ]
                }
            ];

        private readonly IFactory m_DockFactory;
        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IProjectFileImport m_ProjectFileImport;
        private readonly IProjectFileExport m_ProjectFileExport;
        private readonly IProjectFileOpen m_ProjectFileOpen;
        private readonly IProjectFileSave m_ProjectFileSave;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;

        #endregion

        #region Ctors

        public MainViewModel(
            IFactory dockFactory,
            ICoreViewModel coreViewModel,
            IProjectFileImport projectFileImport,
            IProjectFileExport projectFileExport,
            IProjectFileOpen projectFileOpen,
            IProjectFileSave projectFileSave,
            ISettingService settingService,
            IDialogService dialogService)
        {
            ArgumentNullException.ThrowIfNull(dockFactory);
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(projectFileImport);
            ArgumentNullException.ThrowIfNull(projectFileExport);
            ArgumentNullException.ThrowIfNull(projectFileOpen);
            ArgumentNullException.ThrowIfNull(projectFileSave);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            m_Lock = new object();
            m_DockFactory = dockFactory;
            m_CoreViewModel = coreViewModel;
            m_ProjectFileImport = projectFileImport;
            m_ProjectFileExport = projectFileExport;
            m_ProjectFileOpen = projectFileOpen;
            m_ProjectFileSave = projectFileSave;
            m_SettingService = settingService;
            m_DialogService = dialogService;

            {
                ReactiveCommand<Unit, Unit> openProjectPlanFileCommand = ReactiveCommand.CreateFromTask(OpenProjectPlanFileAsync);
                openProjectPlanFileCommand.IsExecuting.ToProperty(this, main => main.IsOpening, out m_IsOpening);
                OpenProjectPlanFileCommand = openProjectPlanFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> saveProjectPlanFileCommand = ReactiveCommand.CreateFromTask(SaveProjectPlanFileAsync);
                saveProjectPlanFileCommand.IsExecuting.ToProperty(this, main => main.IsSaving, out m_IsSaving);
                SaveProjectPlanFileCommand = saveProjectPlanFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> saveAsProjectPlanFileCommand = ReactiveCommand.CreateFromTask(SaveAsProjectPlanFileAsync);
                saveAsProjectPlanFileCommand.IsExecuting.ToProperty(this, main => main.IsSavingAs, out m_IsSavingAs);
                SaveAsProjectPlanFileCommand = saveAsProjectPlanFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> importProjectFileCommand = ReactiveCommand.CreateFromTask(ImportProjectFileAsync);
                importProjectFileCommand.IsExecuting.ToProperty(this, main => main.IsImporting, out m_IsImporting);
                ImportProjectFileCommand = importProjectFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> exportProjectFileCommand = ReactiveCommand.CreateFromTask(ExportProjectFileAsync);
                exportProjectFileCommand.IsExecuting.ToProperty(this, main => main.IsExporting, out m_IsExporting);
                ExportProjectFileCommand = exportProjectFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> closeProjectPlanCommand = ReactiveCommand.CreateFromTask(CloseProjectPlanAsync);
                closeProjectPlanCommand.IsExecuting.ToProperty(this, main => main.IsClosing, out m_IsClosing);
                CloseProjectPlanCommand = closeProjectPlanCommand;
            }

            ToggleShowDatesCommand = ReactiveCommand.Create(ToggleShowDates);
            ToggleUseClassicDatesCommand = ReactiveCommand.Create(ToggleUseClassicDates);
            ToggleUseBusinessDaysCommand = ReactiveCommand.Create(ToggleUseBusinessDays);
            ChangeThemeCommand = ReactiveCommand.CreateFromTask<string>(ChangeThemeAsync);

            CompileCommand = ReactiveCommand.CreateFromTask(ForceCompileAsync);
            ToggleAutoCompileCommand = ReactiveCommand.Create(ToggleAutoCompile);
            TransitiveReductionCommand = ReactiveCommand.Create(RunTransitiveReductionAsync);

            OpenHyperLinkCommand = ReactiveCommand.CreateFromTask<string>(OpenHyperLinkAsync);
            OpenAboutCommand = ReactiveCommand.Create(OpenAboutAsync);

            m_ProjectTitle = this
                .WhenAnyValue(
                    main => main.m_CoreViewModel.ProjectTitle,
                    main => main.m_CoreViewModel.IsProjectUpdated,
                    (title, isProjectUpdate) => $@"{(isProjectUpdate ? "*" : "")}{(string.IsNullOrWhiteSpace(title) ? Resource.ProjectPlan.Titles.Title_UntitledProject : title)} - {Resource.ProjectPlan.Titles.Title_ProjectPlan} {Resource.ProjectPlan.Labels.Label_AppVersion}")
                .ToProperty(this, main => main.ProjectTitle);

            m_IsBusy = this
                .WhenAnyValue(main => main.m_CoreViewModel.IsBusy)
                .ToProperty(this, x => x.IsBusy);

            m_IsProjectUpdated = this
                .WhenAnyValue(main => main.m_CoreViewModel.IsProjectUpdated)
                .ToProperty(this, main => main.IsProjectUpdated);

            m_ProjectStart = this
                .WhenAnyValue(main => main.m_CoreViewModel.ProjectStart)
                .ToProperty(this, main => main.ProjectStart);

            m_ProjectStartDateTime = this
                .WhenAnyValue(main => main.m_CoreViewModel.ProjectStartDateTime)
                .ToProperty(this, main => main.ProjectStartDateTime);

            m_HasStaleOutputs = this
                .WhenAnyValue(main => main.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, main => main.HasStaleOutputs);

            m_HasCompilationErrors = this
                .WhenAnyValue(main => main.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, main => main.HasCompilationErrors);

            m_ShowDates = this
                .WhenAnyValue(main => main.m_CoreViewModel.ShowDates)
                .ToProperty(this, main => main.ShowDates);

            m_UseClassicDates = this
                .WhenAnyValue(main => main.m_CoreViewModel.UseClassicDates)
                .ToProperty(this, main => main.UseClassicDates);

            m_UseBusinessDays = this
                .WhenAnyValue(main => main.m_CoreViewModel.UseBusinessDays)
                .ToProperty(this, main => main.UseBusinessDays);

            m_AutoCompile = this
                .WhenAnyValue(main => main.m_CoreViewModel.AutoCompile)
                .ToProperty(this, main => main.AutoCompile);

            m_SelectedTheme = this
                .WhenAnyValue(main => main.m_CoreViewModel.SelectedTheme)
                .ToProperty(this, main => main.SelectedTheme);

            m_BaseTheme = this
                .WhenAnyValue(main => main.m_CoreViewModel.BaseTheme)
                .ToProperty(this, main => main.BaseTheme);

            m_CoreViewModel.AutoCompile = true;
            m_CoreViewModel.ViewEarnedValueProjections = false;
            m_CoreViewModel.GanttChartAnnotationStyle = default;
            m_CoreViewModel.GanttChartGroupByMode = default;
            m_CoreViewModel.ViewGanttChartGroupLabels = false;
            m_CoreViewModel.ViewGanttChartProjectFinish = false;
            m_CoreViewModel.ViewGanttChartTracking = false;
            m_CoreViewModel.IsProjectUpdated = false;

#if DEBUG
            DebugFactoryEvents(m_DockFactory);
#endif

            m_Layout = m_DockFactory.CreateLayout();
            if (m_Layout is not null)
            {
                m_DockFactory.InitLayout(m_Layout);
            }
        }

        #endregion

        #region Properties

        private IRootDock? m_Layout;
        public IRootDock? Layout
        {
            get => m_Layout;
            set => this.RaiseAndSetIfChanged(ref m_Layout, value);
        }

        #endregion

        #region Private Methods

        private static void DebugFactoryEvents(IFactory factory)
        {
            factory.ActiveDockableChanged += (_, args) =>
            {
                Debug.WriteLine($"[ActiveDockableChanged] Title='{args.Dockable?.Title}'");
            };

            factory.FocusedDockableChanged += (_, args) =>
            {
                Debug.WriteLine($"[FocusedDockableChanged] Title='{args.Dockable?.Title}'");
            };

            factory.DockableAdded += (_, args) =>
            {
                Debug.WriteLine($"[DockableAdded] Title='{args.Dockable?.Title}'");
            };

            factory.DockableRemoved += (_, args) =>
            {
                Debug.WriteLine($"[DockableRemoved] Title='{args.Dockable?.Title}'");
            };

            factory.DockableClosed += (_, args) =>
            {
                Debug.WriteLine($"[DockableClosed] Title='{args.Dockable?.Title}'");
            };

            factory.DockableMoved += (_, args) =>
            {
                Debug.WriteLine($"[DockableMoved] Title='{args.Dockable?.Title}'");
            };

            factory.DockableSwapped += (_, args) =>
            {
                Debug.WriteLine($"[DockableSwapped] Title='{args.Dockable?.Title}'");
            };

            factory.DockablePinned += (_, args) =>
            {
                Debug.WriteLine($"[DockablePinned] Title='{args.Dockable?.Title}'");
            };

            factory.DockableUnpinned += (_, args) =>
            {
                Debug.WriteLine($"[DockableUnpinned] Title='{args.Dockable?.Title}'");
            };

            factory.WindowOpened += (_, args) =>
            {
                Debug.WriteLine($"[WindowOpened] Title='{args.Window?.Title}'");
            };

            factory.WindowClosed += (_, args) =>
            {
                Debug.WriteLine($"[WindowClosed] Title='{args.Window?.Title}'");
            };

            factory.WindowClosing += (_, args) =>
            {
                // NOTE: Set to True to cancel window closing.
#if false
                args.Cancel = true;
#endif
                Debug.WriteLine($"[WindowClosing] Title='{args.Window?.Title}', Cancel={args.Cancel}");
            };

            factory.WindowAdded += (_, args) =>
            {
                Debug.WriteLine($"[WindowAdded] Title='{args.Window?.Title}'");
            };

            factory.WindowRemoved += (_, args) =>
            {
                Debug.WriteLine($"[WindowRemoved] Title='{args.Window?.Title}'");
            };

            factory.WindowMoveDragBegin += (_, args) =>
            {
                // NOTE: Set to True to cancel window dragging.
#if false
                args.Cancel = true;
#endif
                Debug.WriteLine($"[WindowMoveDragBegin] Title='{args.Window?.Title}', Cancel={args.Cancel}, X='{args.Window?.X}', Y='{args.Window?.Y}'");
            };

            factory.WindowMoveDrag += (_, args) =>
            {
                Debug.WriteLine($"[WindowMoveDrag] Title='{args.Window?.Title}', X='{args.Window?.X}', Y='{args.Window?.Y}");
            };

            factory.WindowMoveDragEnd += (_, args) =>
            {
                Debug.WriteLine($"[WindowMoveDragEnd] Title='{args.Window?.Title}', X='{args.Window?.X}', Y='{args.Window?.Y}");
            };
        }

        private void ToggleShowDates() => ShowDates = !ShowDates;

        private void ToggleUseClassicDates() => UseClassicDates = !UseClassicDates;

        private void ToggleUseBusinessDays() => UseBusinessDays = !UseBusinessDays;

        private void ToggleAutoCompile() => AutoCompile = !AutoCompile;

        private void ProcessProjectImport(ProjectImportModel importModel) => m_CoreViewModel.ProcessProjectImport(importModel);

        private void ProcessProjectPlan(ProjectPlanModel planModel) => m_CoreViewModel.ProcessProjectPlan(planModel);

        private async Task<ProjectPlanModel> BuildProjectPlanAsync() => await Task.Run(m_CoreViewModel.BuildProjectPlan);

        private async Task ForceCompileAsync() => await Task.Run(async () =>
        {
            m_CoreViewModel.IsReadyToReviseTrackers = ReadyToRevise.Yes;
            await RunCompileAsync(); // Need to force a compilation here.
        });

        private async Task RunCompileAsync() => await Task.Run(m_CoreViewModel.RunCompile);

        private async Task RunAutoCompileAsync() => await Task.Run(m_CoreViewModel.RunAutoCompile);

        private async Task RunTransitiveReductionAsync() => await Task.Run(m_CoreViewModel.RunTransitiveReduction);

        private void ResetProject() => m_CoreViewModel.ResetProject();

        private async Task OpenProjectPlanFileInternalAsync(string? filename)
        {
            if (!string.IsNullOrWhiteSpace(filename))
            {
                ProjectPlanModel planModel = await m_ProjectFileOpen.OpenProjectPlanFileAsync(filename);
                ProcessProjectPlan(planModel);
                m_SettingService.SetProjectFilePath(filename, bindTitleToFilename: true);
                //await RunAutoCompileAsync();
            }
        }

        private async Task SaveProjectPlanFileInternalAsync(string? filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    Resource.ProjectPlan.Messages.Message_EmptyFilename);
            }
            else
            {
                ProjectPlanModel projectPlan = await BuildProjectPlanAsync();
                await m_ProjectFileSave.SaveProjectPlanFileAsync(projectPlan, filename);
                m_CoreViewModel.IsProjectUpdated = false;
                m_SettingService.SetProjectFilePath(filename, bindTitleToFilename: true);
            }
        }

        private async Task ChangeThemeAsync(string theme)
        {
            try
            {
                SelectedTheme = theme ?? Resource.ProjectPlan.Themes.Theme_Default;
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        #endregion

        #region IMainViewModel Members

        private readonly ObservableAsPropertyHelper<string> m_ProjectTitle;
        public string ProjectTitle
        {
            get => m_ProjectTitle.Value;
        }

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsOpening;
        public bool IsOpening => m_IsOpening.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsSaving;
        public bool IsSaving => m_IsSaving.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsSavingAs;
        public bool IsSavingAs => m_IsSavingAs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsImporting;
        public bool IsImporting => m_IsImporting.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsExporting;
        public bool IsExporting => m_IsExporting.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsClosing;
        public bool IsClosing => m_IsClosing.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsProjectUpdated;
        public bool IsProjectUpdated => m_IsProjectUpdated.Value;

        private readonly ObservableAsPropertyHelper<DateTimeOffset> m_ProjectStart;
        public DateTimeOffset ProjectStart
        {
            get => m_ProjectStart.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.ProjectStart = value;
            }
        }

        private readonly ObservableAsPropertyHelper<DateTime> m_ProjectStartDateTime;
        public DateTime ProjectStartDateTime
        {
            get => m_ProjectStartDateTime.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.ProjectStartDateTime = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private readonly ObservableAsPropertyHelper<bool> m_ShowDates;
        public bool ShowDates
        {
            get => m_ShowDates.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.ShowDates = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_UseClassicDates;
        public bool UseClassicDates
        {
            get => m_UseClassicDates.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.UseClassicDates = value;
            }
        }
        private readonly ObservableAsPropertyHelper<bool> m_UseBusinessDays;
        public bool UseBusinessDays
        {
            get => m_UseBusinessDays.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.UseBusinessDays = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_AutoCompile;
        public bool AutoCompile
        {
            get => m_AutoCompile.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.AutoCompile = value;
            }
        }

        private readonly ObservableAsPropertyHelper<string> m_SelectedTheme;
        public string SelectedTheme
        {
            get => m_SelectedTheme.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.SelectedTheme = value;
            }
        }

        private readonly ObservableAsPropertyHelper<BaseTheme> m_BaseTheme;
        public BaseTheme BaseTheme
        {
            get => m_BaseTheme.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.BaseTheme = value;
            }
        }

        public ICommand OpenProjectPlanFileCommand { get; }

        public ICommand SaveProjectPlanFileCommand { get; }

        public ICommand SaveAsProjectPlanFileCommand { get; }

        public ICommand ImportProjectFileCommand { get; }

        public ICommand ExportProjectFileCommand { get; }

        public ICommand CloseProjectPlanCommand { get; }

        public ICommand ToggleShowDatesCommand { get; }

        public ICommand ToggleUseClassicDatesCommand { get; }

        public ICommand ToggleUseBusinessDaysCommand { get; }

        public ICommand ChangeThemeCommand { get; }

        public ICommand CompileCommand { get; }

        public ICommand ToggleAutoCompileCommand { get; }

        public ICommand TransitiveReductionCommand { get; }

        public ICommand OpenHyperLinkCommand { get; }

        public ICommand OpenAboutCommand { get; }

        public void CloseLayout()
        {
            if (Layout is IDock dock)
            {
                if (dock.Close.CanExecute(null))
                {
                    dock.Close.Execute(null);
                }
            }
        }

        public void ResetLayout()
        {
            if (Layout is not null)
            {
                if (Layout.Close.CanExecute(null))
                {
                    Layout.Close.Execute(null);
                }
            }

            IRootDock? layout = m_DockFactory.CreateLayout();
            if (layout is not null)
            {
                Layout = layout;
                m_DockFactory.InitLayout(layout);
            }
        }

        public async Task OpenProjectPlanFileAsync()
        {
            try
            {
                if (IsProjectUpdated)
                {
                    bool confirmation = await m_DialogService.ShowConfirmationAsync(
                        Resource.ProjectPlan.Titles.Title_UnsavedChanges,
                        string.Empty,
                        Resource.ProjectPlan.Messages.Message_UnsavedChanges);

                    if (!confirmation)
                    {
                        return;
                    }
                }
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowOpenFileDialogAsync(directory, s_ProjectPlanFileFilters);
                await OpenProjectPlanFileInternalAsync(filename);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
                ResetProject();
            }
        }

        public async Task OpenProjectPlanFileAsync(string? filename)
        {
            try
            {
                if (IsProjectUpdated)
                {
                    bool confirmation = await m_DialogService.ShowConfirmationAsync(
                        Resource.ProjectPlan.Titles.Title_UnsavedChanges,
                        string.Empty,
                        Resource.ProjectPlan.Messages.Message_UnsavedChanges);

                    if (!confirmation)
                    {
                        return;
                    }
                }

                await OpenProjectPlanFileInternalAsync(filename);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
                ResetProject();
            }
        }

        public async Task SaveProjectPlanFileAsync()
        {
            try
            {
                string projectTitle = m_SettingService.ProjectTitle;

                if (string.IsNullOrWhiteSpace(projectTitle)
                    || !m_SettingService.IsTitleBoundToFilename)
                {
                    await SaveAsProjectPlanFileAsync();
                    return;
                }

                string directory = m_SettingService.ProjectDirectory;
                string filename = Path.Combine(directory, projectTitle);
                filename = $@"{filename}.{Resource.ProjectPlan.Filters.Filter_ProjectPlanFileExtension}";
                await SaveProjectPlanFileInternalAsync(filename);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        public async Task SaveAsProjectPlanFileAsync()
        {
            try
            {
                string directory = m_SettingService.ProjectDirectory;
                string projectTitle = m_SettingService.ProjectTitle;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(projectTitle, directory, s_ProjectPlanFileFilters);

                if (string.IsNullOrWhiteSpace(filename))
                {
                    return;
                }

                await SaveProjectPlanFileInternalAsync(filename);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        public async Task ImportProjectFileAsync()
        {
            try
            {
                if (IsProjectUpdated)
                {
                    bool confirmation = await m_DialogService.ShowConfirmationAsync(
                        Resource.ProjectPlan.Titles.Title_UnsavedChanges,
                        string.Empty,
                        Resource.ProjectPlan.Messages.Message_UnsavedChanges);

                    if (!confirmation)
                    {
                        return;
                    }
                }
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowOpenFileDialogAsync(directory, s_ImportFileFilters);

                if (!string.IsNullOrWhiteSpace(filename))
                {
                    ProjectImportModel importModel = await m_ProjectFileImport.ImportProjectFileAsync(filename);
                    ProcessProjectImport(importModel);
                    m_SettingService.SetProjectFilePath(filename, bindTitleToFilename: false);
                    await RunAutoCompileAsync();
                }
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
                ResetProject();
            }
        }

        public async Task ExportProjectFileAsync()
        {
            try
            {
                string projectTitle = m_SettingService.ProjectTitle;
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(projectTitle, directory, s_ExportFileFilters);

                if (!string.IsNullOrWhiteSpace(filename))
                {
                    ProjectPlanModel projectPlan = await BuildProjectPlanAsync();
                    await m_ProjectFileExport.ExportProjectFileAsync(
                        projectPlan,
                        m_CoreViewModel.ResourceSeriesSet,
                        m_CoreViewModel.TrackingSeriesSet,
                        ShowDates,
                        filename);
                }
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        public async Task CloseProjectPlanAsync()
        {
            try
            {
                if (IsProjectUpdated)
                {
                    bool confirmation = await m_DialogService.ShowConfirmationAsync(
                        Resource.ProjectPlan.Titles.Title_UnsavedChanges,
                        string.Empty,
                        Resource.ProjectPlan.Messages.Message_UnsavedChanges);

                    if (!confirmation)
                    {
                        return;
                    }
                }
                ResetProject();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
                ResetProject();
            }
        }

        public async Task OpenHyperLinkAsync(string hyperlink)
        {
            try
            {
                var uri = new Uri(hyperlink);
                Process.Start(new ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        public async Task OpenAboutAsync()
        {
            try
            {
                var about = new StringBuilder();
                about.AppendLine($"{Resource.ProjectPlan.Labels.Label_Version} {Resource.ProjectPlan.Labels.Label_AppVersion}");
                about.AppendLine();
                about.AppendLine($"{Resource.ProjectPlan.Labels.Label_Copyright}, {Resource.ProjectPlan.Labels.Label_Author}");

                await m_DialogService.ShowInfoAsync(
                    Resource.ProjectPlan.Titles.Title_ProjectPlan,
                    Resource.ProjectPlan.Titles.Title_ProjectPlan,
                    about.ToString());
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        #endregion

        #region IDisposable Members

        private bool m_Disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
            }

            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
                m_ProjectTitle?.Dispose();
                m_IsBusy?.Dispose();
                m_IsProjectUpdated?.Dispose();
                m_ProjectStart?.Dispose();
                m_ProjectStartDateTime?.Dispose();
                m_HasStaleOutputs?.Dispose();
                m_HasCompilationErrors?.Dispose();
                m_ShowDates?.Dispose();
                m_UseClassicDates?.Dispose();
                m_UseBusinessDays?.Dispose();
                m_AutoCompile?.Dispose();
                m_SelectedTheme?.Dispose();
                m_BaseTheme?.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

            m_Disposed = true;
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
