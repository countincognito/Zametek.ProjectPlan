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

        private static readonly IList<IFileFilter> s_ProjectFileFilters =
            [
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ProjectFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_ProjectFilePattern
                    ]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_AllFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_AllFilePattern
                    ]
                }
            ];

        private static readonly IList<IFileFilter> s_ImportFileFilters =
            [
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ProjectXlsxFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_ProjectXlsxFilePattern
                    ]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_MicrosoftProjectFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_MicrosoftProjectMppFilePattern,
                        Resource.ProjectPlan.Filters.Filter_MicrosoftProjectXmlFilePattern
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
        private readonly IProjectPlanManagerViewModel m_ProjectPlanManagerViewModel;
        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IProjectFileOpen m_ProjectFileOpen;
        private readonly IProjectFileSave m_ProjectFileSave;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;

        #endregion

        #region Ctors

        public MainViewModel(
            IFactory dockFactory,
            IProjectPlanManagerViewModel projectPlanManagerViewModel,
            ICoreViewModel coreViewModel,
            IProjectFileOpen projectFileOpen,
            IProjectFileSave projectFileSave,
            ISettingService settingService,
            IDialogService dialogService)
        {
            ArgumentNullException.ThrowIfNull(dockFactory);
            ArgumentNullException.ThrowIfNull(projectPlanManagerViewModel);
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(projectFileOpen);
            ArgumentNullException.ThrowIfNull(projectFileSave);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            m_Lock = new object();
            m_DockFactory = dockFactory;
            m_ProjectPlanManagerViewModel = projectPlanManagerViewModel;
            m_CoreViewModel = coreViewModel;
            m_ProjectFileOpen = projectFileOpen;
            m_ProjectFileSave = projectFileSave;
            m_SettingService = settingService;
            m_DialogService = dialogService;

            {
                ReactiveCommand<Unit, Unit> openProjectFileCommand = ReactiveCommand.CreateFromTask(OpenProjectFileAsync);
                openProjectFileCommand.IsExecuting.ToProperty(this, main => main.IsOpening, out m_IsOpening);
                OpenProjectFileCommand = openProjectFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> saveProjectFileCommand = ReactiveCommand.CreateFromTask(SaveProjectFileAsync);
                saveProjectFileCommand.IsExecuting.ToProperty(this, main => main.IsSaving, out m_IsSaving);
                SaveProjectFileCommand = saveProjectFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> saveAsProjectFileCommand = ReactiveCommand.CreateFromTask(SaveAsProjectFileAsync);
                saveAsProjectFileCommand.IsExecuting.ToProperty(this, main => main.IsSavingAs, out m_IsSavingAs);
                SaveAsProjectFileCommand = saveAsProjectFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> importProjectPlanFileCommand = ReactiveCommand.CreateFromTask(ImportProjectPlanFileAsync);
                importProjectPlanFileCommand.IsExecuting.ToProperty(this, main => main.IsImporting, out m_IsImporting);
                ImportProjectPlanFileCommand = importProjectPlanFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> exportProjectPlanFileCommand = ReactiveCommand.CreateFromTask(ExportProjectPlanFileAsync);
                exportProjectPlanFileCommand.IsExecuting.ToProperty(this, main => main.IsExporting, out m_IsExporting);
                ExportProjectPlanFileCommand = exportProjectPlanFileCommand;
            }
            {
                ReactiveCommand<Unit, Unit> closeProjectCommand = ReactiveCommand.CreateFromTask(CloseProjectAsync);
                closeProjectCommand.IsExecuting.ToProperty(this, main => main.IsClosing, out m_IsClosing);
                CloseProjectCommand = closeProjectCommand;
            }

            ToggleShowDatesCommand = ReactiveCommand.Create(ToggleShowDates);
            ToggleUseClassicDatesCommand = ReactiveCommand.Create(ToggleUseClassicDates);
            ToggleUseBusinessDaysCommand = ReactiveCommand.Create(ToggleUseBusinessDays);
            ToggleHideCostCommand = ReactiveCommand.Create(ToggleHideCost);
            ToggleHideBillingCommand = ReactiveCommand.Create(ToggleHideBilling);

            ToggleDefaultShowDatesCommand = ReactiveCommand.Create(ToggleDefaultShowDates);
            ToggleDefaultUseClassicDatesCommand = ReactiveCommand.Create(ToggleDefaultUseClassicDates);
            ToggleDefaultUseBusinessDaysCommand = ReactiveCommand.Create(ToggleDefaultUseBusinessDays);
            ToggleDefaultHideCostCommand = ReactiveCommand.Create(ToggleDefaultHideCost);
            ToggleDefaultHideBillingCommand = ReactiveCommand.Create(ToggleDefaultHideBilling);

            ChangeThemeCommand = ReactiveCommand.CreateFromTask<string>(ChangeThemeAsync);

            CompileCommand = ReactiveCommand.CreateFromTask(ForceCompileAsync);
            ToggleAutoCompileCommand = ReactiveCommand.Create(ToggleAutoCompile);
            TransitiveReductionCommand = ReactiveCommand.Create(RunTransitiveReductionAsync);

            OpenDocumentationCommand = ReactiveCommand.CreateFromTask(OpenDocumentationAsync);
            OpenDonateCommand = ReactiveCommand.CreateFromTask(OpenDonateAsync);
            OpenMainPageCommand = ReactiveCommand.CreateFromTask(OpenMainPageAsync);
            OpenReportIssueCommand = ReactiveCommand.CreateFromTask(OpenReportIssueAsync);
            OpenViewLicenseCommand = ReactiveCommand.CreateFromTask(OpenViewLicenseAsync);
            OpenAboutCommand = ReactiveCommand.Create(OpenAboutAsync);

            m_ProjectTitle = this
                .WhenAnyValue(
                    main => main.m_SettingService.ProjectTitle,
                    main => main.m_SettingService.PlanTitle,
                    main => main.m_SettingService.PlanId,
                    main => main.m_ProjectPlanManagerViewModel.ProjectHasChanges,
                    (projectTitle, planTitle, projectPlanId, projectHasChanges) =>
                    {
                        string plan = projectPlanId.ToShortString();
                        if (!string.IsNullOrWhiteSpace(planTitle))
                        {
                            plan = planTitle;
                        }
                        return $@"{(projectHasChanges ? "*" : string.Empty)}{(string.IsNullOrWhiteSpace(projectTitle) ? Resource.ProjectPlan.Titles.Title_UntitledProject : projectTitle)} - {plan} - {Resource.ProjectPlan.Titles.Title_ProjectPlan} {Resource.ProjectPlan.Labels.Label_AppVersion}";
                    })
                .ToProperty(this, main => main.ProjectTitle);

            m_IsBusy = this
                .WhenAnyValue(
                    main => main.m_CoreViewModel.IsBusy,
                    main => main.m_ProjectPlanManagerViewModel.IsBusy,
                    (isCoreBusy, isProjectBusy) => isCoreBusy || isProjectBusy)
                .ToProperty(this, main => main.IsBusy);

            m_IsProjectUpdated = this
                .WhenAnyValue(pm => pm.m_ProjectPlanManagerViewModel.IsProjectUpdated)
                .ToProperty(this, pm => pm.IsProjectUpdated);

            m_IsProjectPlanUpdated = this
                .WhenAnyValue(pm => pm.m_CoreViewModel.IsProjectPlanUpdated)
                .ToProperty(this, pm => pm.IsProjectPlanUpdated);

            m_ProjectHasChanges = this
                .WhenAnyValue(
                    pm => pm.IsProjectUpdated,
                    pm => pm.IsProjectPlanUpdated,
                    (isProjectUpdated, isProjectPlanUpdated) => isProjectUpdated || isProjectPlanUpdated)
                .ToProperty(this, pm => pm.ProjectHasChanges);

            m_ProjectStart = this
                .WhenAnyValue(main => main.m_CoreViewModel.ProjectStart)
                .ToProperty(this, main => main.ProjectStart);

            m_Today = this
                .WhenAnyValue(main => main.m_CoreViewModel.Today)
                .ToProperty(this, main => main.Today);

            m_HasStaleOutputs = this
                .WhenAnyValue(main => main.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, main => main.HasStaleOutputs);

            m_HasCompilationErrors = this
                .WhenAnyValue(main => main.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, main => main.HasCompilationErrors);

            m_ShowDates = this
                .WhenAnyValue(main => main.m_CoreViewModel.DisplaySettingsViewModel.ShowDates)
                .ToProperty(this, main => main.ShowDates);

            m_UseClassicDates = this
                .WhenAnyValue(main => main.m_CoreViewModel.DisplaySettingsViewModel.UseClassicDates)
                .ToProperty(this, main => main.UseClassicDates);

            m_UseBusinessDays = this
                .WhenAnyValue(main => main.m_CoreViewModel.DisplaySettingsViewModel.UseBusinessDays)
                .ToProperty(this, main => main.UseBusinessDays);

            m_HideCost = this
                .WhenAnyValue(main => main.m_CoreViewModel.DisplaySettingsViewModel.HideCost)
                .ToProperty(this, main => main.HideCost);

            m_HideBilling = this
                .WhenAnyValue(main => main.m_CoreViewModel.DisplaySettingsViewModel.HideBilling)
                .ToProperty(this, main => main.HideBilling);

            m_DefaultShowDates = this
                .WhenAnyValue(main => main.m_CoreViewModel.DefaultShowDates)
                .ToProperty(this, main => main.DefaultShowDates);

            m_DefaultUseClassicDates = this
                .WhenAnyValue(main => main.m_CoreViewModel.DefaultUseClassicDates)
                .ToProperty(this, main => main.DefaultUseClassicDates);

            m_DefaultUseBusinessDays = this
                .WhenAnyValue(main => main.m_CoreViewModel.DefaultUseBusinessDays)
                .ToProperty(this, main => main.DefaultUseBusinessDays);

            m_DefaultHideCost = this
                .WhenAnyValue(main => main.m_CoreViewModel.DefaultHideCost)
                .ToProperty(this, main => main.DefaultHideCost);

            m_DefaultHideBilling = this
                .WhenAnyValue(main => main.m_CoreViewModel.DefaultHideBilling)
                .ToProperty(this, main => main.DefaultHideBilling);

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

            ResetProject();

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

        private void ToggleHideCost() => HideCost = !HideCost;

        private void ToggleHideBilling() => HideBilling = !HideBilling;

        private void ToggleDefaultShowDates() => DefaultShowDates = !DefaultShowDates;

        private void ToggleDefaultUseClassicDates() => DefaultUseClassicDates = !DefaultUseClassicDates;

        private void ToggleDefaultUseBusinessDays() => DefaultUseBusinessDays = !DefaultUseBusinessDays;

        private void ToggleDefaultHideCost() => DefaultHideCost = !DefaultHideCost;

        private void ToggleDefaultHideBilling() => DefaultHideBilling = !DefaultHideBilling;

        private void ToggleAutoCompile() => AutoCompile = !AutoCompile;

        private async Task ProjectPlanImportAsync(string filename) =>
            await Task.Run(() => ProjectPlanImport(filename));

        private void ProjectPlanImport(string filename)
        {
            lock (m_Lock)
            {
                Guid projectPlanId = m_SettingService.PlanId;
                ProjectPlanImportModel importModel = m_CoreViewModel.ImportProjectPlanFile(filename);
                m_CoreViewModel.ProcessProjectPlanImport(importModel, projectPlanId);
            }
        }

        private async Task ProjectPlanExportAsync(string filename) =>
            await Task.Run(() => ProjectPlanExport(filename));

        private void ProjectPlanExport(string filename)
        {
            lock (m_Lock)
            {
                ProjectPlanModel projectPlanModel = BuildProjectPlan();
                m_CoreViewModel.ExportProjectPlanFile(
                    projectPlanModel,
                    m_CoreViewModel.ResourceSeriesSet,
                    m_CoreViewModel.TrackingSeriesSet,
                    ShowDates,
                    filename);
            }
        }

        private async Task<ProjectModel> BuildProjectAsync() => await Task.Run(BuildProject);

        private ProjectModel BuildProject()
        {
            lock (m_Lock)
            {
                return m_ProjectPlanManagerViewModel.BuildProject();
            }
        }

        private async Task<ProjectPlanModel> BuildProjectPlanAsync() => await Task.Run(BuildProjectPlan);

        private ProjectPlanModel BuildProjectPlan()
        {
            lock (m_Lock)
            {
                return m_CoreViewModel.BuildProjectPlan();
            }
        }

        private async Task ForceCompileAsync() => await Task.Run(async () =>
        {
            // We set this flag to force revision of trackers in case there
            // changes to activities or resources have occurred since the last compile.
            m_CoreViewModel.IsReadyToReviseTrackers = ReadyToRevise.Yes;

            await RunCompileAsync(); // Need to force a compilation here.
        });

        private async Task RunCompileAsync() => await Task.Run(m_CoreViewModel.RunCompile);

        private async Task RunAutoCompileAsync() => await Task.Run(m_CoreViewModel.RunAutoCompile);

        private async Task RunTransitiveReductionAsync() => await Task.Run(m_CoreViewModel.RunTransitiveReduction);

        private void ResetProject()
        {
            lock (m_Lock)
            {
                m_ProjectPlanManagerViewModel.ResetProject();
            }
        }

        private async Task OpenProjectFileInternalAsync(string? filename)
        {
            if (!string.IsNullOrWhiteSpace(filename))
            {
                ProjectModel projectModel = await m_ProjectFileOpen.OpenProjectFileAsync(filename);

                // First process the project.
                m_ProjectPlanManagerViewModel.ProcessProject(projectModel);

                // Now bind the project title to the filename.
                m_SettingService.SetProjectFilePath(filename, bindTitleToFilename: true);
            }
        }

        private async Task SaveProjectFileInternalAsync(string? filename)
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
                ProjectModel projectModel = await BuildProjectAsync();
                await m_ProjectFileSave.SaveProjectFileAsync(projectModel, filename);
                m_CoreViewModel.IsProjectPlanUpdated = false;
                m_ProjectPlanManagerViewModel.IsProjectUpdated = false;
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

        private readonly ObservableAsPropertyHelper<bool> m_IsProjectPlanUpdated;
        public bool IsProjectPlanUpdated => m_IsProjectPlanUpdated.Value;

        private readonly ObservableAsPropertyHelper<bool> m_ProjectHasChanges;
        public bool ProjectHasChanges => m_ProjectHasChanges.Value;

        private readonly ObservableAsPropertyHelper<DateTimeOffset> m_ProjectStart;
        public DateTimeOffset ProjectStart
        {
            get => m_ProjectStart.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.ProjectStart = value;
            }
        }

        private readonly ObservableAsPropertyHelper<DateTimeOffset> m_Today;
        public DateTimeOffset Today
        {
            get => m_Today.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.Today = value;
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
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.ShowDates = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_UseClassicDates;
        public bool UseClassicDates
        {
            get => m_UseClassicDates.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.UseClassicDates = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_UseBusinessDays;
        public bool UseBusinessDays
        {
            get => m_UseBusinessDays.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.UseBusinessDays = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_HideCost;
        public bool HideCost
        {
            get => m_HideCost.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.HideCost = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_HideBilling;
        public bool HideBilling
        {
            get => m_HideBilling.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.HideBilling = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_DefaultShowDates;
        public bool DefaultShowDates
        {
            get => m_DefaultShowDates.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DefaultShowDates = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_DefaultUseClassicDates;
        public bool DefaultUseClassicDates
        {
            get => m_DefaultUseClassicDates.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DefaultUseClassicDates = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_DefaultUseBusinessDays;
        public bool DefaultUseBusinessDays
        {
            get => m_DefaultUseBusinessDays.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DefaultUseBusinessDays = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_DefaultHideCost;
        public bool DefaultHideCost
        {
            get => m_DefaultHideCost.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DefaultHideCost = value;
            }
        }

        private readonly ObservableAsPropertyHelper<bool> m_DefaultHideBilling;
        public bool DefaultHideBilling
        {
            get => m_DefaultHideBilling.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DefaultHideBilling = value;
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

        public ICommand OpenProjectFileCommand { get; }

        public ICommand SaveProjectFileCommand { get; }

        public ICommand SaveAsProjectFileCommand { get; }

        public ICommand ImportProjectPlanFileCommand { get; }

        public ICommand ExportProjectPlanFileCommand { get; }

        public ICommand CloseProjectCommand { get; }

        public ICommand ToggleShowDatesCommand { get; }

        public ICommand ToggleUseClassicDatesCommand { get; }

        public ICommand ToggleUseBusinessDaysCommand { get; }

        public ICommand ToggleHideCostCommand { get; }

        public ICommand ToggleHideBillingCommand { get; }

        public ICommand ToggleDefaultShowDatesCommand { get; }

        public ICommand ToggleDefaultUseClassicDatesCommand { get; }

        public ICommand ToggleDefaultUseBusinessDaysCommand { get; }

        public ICommand ToggleDefaultHideCostCommand { get; }

        public ICommand ToggleDefaultHideBillingCommand { get; }

        public ICommand ChangeThemeCommand { get; }

        public ICommand CompileCommand { get; }

        public ICommand ToggleAutoCompileCommand { get; }

        public ICommand TransitiveReductionCommand { get; }

        public ICommand OpenDocumentationCommand { get; }

        public ICommand OpenDonateCommand { get; }

        public ICommand OpenMainPageCommand { get; }

        public ICommand OpenReportIssueCommand { get; }

        public ICommand OpenViewLicenseCommand { get; }

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

        public async Task OpenProjectFileAsync()
        {
            try
            {
                if (ProjectHasChanges)
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
                string? filename = await m_DialogService.ShowOpenFileDialogAsync(directory, s_ProjectFileFilters);
                await OpenProjectFileInternalAsync(filename);
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

        public async Task OpenProjectFileAsync(string? filename)
        {
            try
            {
                if (ProjectHasChanges)
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

                await OpenProjectFileInternalAsync(filename);
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

        public async Task SaveProjectFileAsync()
        {
            try
            {
                string projectTitle = m_SettingService.ProjectTitle;

                if (string.IsNullOrWhiteSpace(projectTitle)
                    || !m_SettingService.IsTitleBoundToFilename)
                {
                    await SaveAsProjectFileAsync();
                    return;
                }

                string directory = m_SettingService.ProjectDirectory;
                string filename = Path.Combine(directory, projectTitle);
                filename = $@"{filename}.{Resource.ProjectPlan.Filters.Filter_ProjectFileExtension}";
                await SaveProjectFileInternalAsync(filename);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        public async Task SaveAsProjectFileAsync()
        {
            try
            {
                string directory = m_SettingService.ProjectDirectory;
                string projectTitle = m_SettingService.ProjectTitle;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(projectTitle, directory, s_ProjectFileFilters);

                if (string.IsNullOrWhiteSpace(filename))
                {
                    return;
                }

                await SaveProjectFileInternalAsync(filename);
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        public async Task ImportProjectPlanFileAsync()
        {
            try
            {
                if (IsProjectPlanUpdated)
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
                    await ProjectPlanImportAsync(filename);
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

        public async Task ExportProjectPlanFileAsync()
        {
            try
            {
                string title = m_SettingService.ProjectTitle;
                title = string.IsNullOrWhiteSpace(title) ? Resource.ProjectPlan.Titles.Title_UntitledProject : title;

                Guid projectPlanId = m_SettingService.PlanId;
                IManagedNodeViewModel? managedNode = m_ProjectPlanManagerViewModel.GetNode(projectPlanId);

                if (managedNode is not null)
                {
                    title = $@"{title}-{managedNode.Name}";
                }

                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(title, directory, s_ExportFileFilters);

                if (!string.IsNullOrWhiteSpace(filename))
                {
                    await ProjectPlanExportAsync(filename);
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

        public async Task CloseProjectAsync()
        {
            try
            {
                if (ProjectHasChanges)
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

        public async Task OpenDocumentationAsync()
        {
            try
            {
                UriHelper.OpenDocumentation();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        public async Task OpenDonateAsync()
        {
            try
            {
                UriHelper.OpenDonate();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        public async Task OpenMainPageAsync()
        {
            try
            {
                UriHelper.OpenMainPage();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        public async Task OpenReportIssueAsync()
        {
            try
            {
                UriHelper.OpenReportIssue();
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        public async Task OpenViewLicenseAsync()
        {
            try
            {
                UriHelper.OpenViewLicense();
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
                    about.ToString(),
                    showMainPageLink: true);
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
                m_IsProjectPlanUpdated?.Dispose();
                m_ProjectHasChanges?.Dispose();
                m_ProjectStart?.Dispose();
                m_Today?.Dispose();
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
