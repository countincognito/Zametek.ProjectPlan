using Avalonia.Svg.Skia;
using Avalonia.Threading;
using PlantUml.Net;
using ReactiveUI;
using SkiaSharp;
using Svg.Skia;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class DependencyGraphManagerViewModel
        : ToolViewModelBase, IDependencyGraphManagerViewModel
    {
        #region Fields

        private readonly Lock m_Lock;

        private static readonly IList<IFileFilter> s_ExportFileFilters =
            [
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ImagePngFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_ImagePngFilePattern
                    ]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_PlantUmlFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_PlantUmlFilePattern
                    ]
                }
            ];

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;

        private readonly IDisposable? m_BuildDependencyGraphDataSub;
        private readonly IDisposable? m_BuildDependencyGraphImageSub;

        #endregion

        #region Ctors

        public DependencyGraphManagerViewModel(
            ICoreViewModel coreViewModel,
            ISettingService settingService,
            IDialogService dialogService)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            m_Lock = new();
            m_CoreViewModel = coreViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;

            m_DependencyGraphData = string.Empty;
            m_DependencyGraphImage = new SvgImage();

            {
                ReactiveCommand<Unit, Unit> saveDependencyGraphImageFileCommand = ReactiveCommand.CreateFromTask(SaveDependencyGraphImageFileAsync);
                SaveDependencyGraphImageFileCommand = saveDependencyGraphImageFileCommand;
            }

            m_IsBusy = this
                .WhenAnyValue(dgm => dgm.m_CoreViewModel.IsBusy)
                .ToProperty(this, dgm => dgm.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(dgm => dgm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, dgm => dgm.HasStaleOutputs);

            m_HasCompilationErrors = this
                .WhenAnyValue(dgm => dgm.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, dgm => dgm.HasCompilationErrors);

            m_BaseTheme = this
                .WhenAnyValue(dgm => dgm.m_CoreViewModel.BaseTheme)
                .ToProperty(this, dgm => dgm.BaseTheme);

            m_BuildDependencyGraphDataSub = this
                .WhenAnyValue(
                    dgm => dgm.m_CoreViewModel.ArrowGraph,
                    dgm => dgm.m_CoreViewModel.BaseTheme)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async _ => await BuildDependencyGraphDiagramDataAsync());

            m_BuildDependencyGraphImageSub = this
                .WhenAnyValue(dgm => dgm.DependencyGraphData)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async _ => await BuildDependencyGraphDiagramAsync());

            Id = Resource.ProjectPlan.Titles.Title_DependencyGraphView;
            Title = Resource.ProjectPlan.Titles.Title_DependencyGraphView;
        }

        #endregion

        #region Properties

        private SvgImage m_DependencyGraphImage;
        public SvgImage DependencyGraphImage
        {
            get => m_DependencyGraphImage;
            private set
            {
                this.RaiseAndSetIfChanged(ref m_DependencyGraphImage, value);
            }
        }

        #endregion

        #region Private Methods

        private async Task BuildDependencyGraphDiagramDataAsync()
        {
            try
            {
                await Task.Run(async () =>
                {
                    string puml;
                    lock (m_Lock)
                    {
                        ProjectScenarioModel projectScenarioModel = m_CoreViewModel.BuildProjectScenario();
                        puml = PlantUmlExportService.GeneratePlantUml(
                            projectScenarioModel.DependentActivities,
                            projectScenarioModel.WorkStreamSettings);
                    }

                    IPlantUmlRenderer renderer = new RendererFactory().CreateRenderer();
                    byte[] svgBytes = await renderer.RenderAsync(puml, OutputFormat.Svg, CancellationToken.None);
                    string svgText = Encoding.UTF8.GetString(svgBytes);

                    DependencyGraphData = svgText;
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

        private async Task BuildDependencyGraphDiagramAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    BuildDependencyGraphDiagramImage();
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

        private async Task SaveDependencyGraphImageFileAsync()
        {
            try
            {
                string title = m_SettingService.ProjectTitle;
                title = string.IsNullOrWhiteSpace(title) ? Resource.ProjectPlan.Titles.Title_UntitledProject : title;
                string graphOutputFile = $@"{title}{Resource.ProjectPlan.Suffixes.Suffix_DependencyChart}";
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(graphOutputFile, directory, s_ExportFileFilters);

                if (!string.IsNullOrWhiteSpace(filename))
                {
                    await SaveDependencyGraphImageFileAsync(filename);
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

        #endregion

        #region IDependencyGraphManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private string m_DependencyGraphData;
        public string DependencyGraphData
        {
            get => m_DependencyGraphData;
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_DependencyGraphData, value);
                }
            }
        }

        private readonly ObservableAsPropertyHelper<BaseTheme> m_BaseTheme;
        public BaseTheme BaseTheme => m_BaseTheme.Value;

        public ICommand SaveDependencyGraphImageFileCommand { get; }

        public async Task SaveDependencyGraphImageFileAsync(string? filename)
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
                try
                {
                    string fileExtension = Path.GetExtension(filename);

                    fileExtension.ValueSwitchOn()
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension}", _ =>
                        {
                            using var stream = File.OpenWrite(filename);
                            DependencyGraphImage.Source?.Picture?.ToImage(
                                stream, SKColors.White, SKEncodedImageFormat.Png, quality: 100, scaleX: 2, scaleY: 2,
                                skColorType: SKColorType.Argb4444, skAlphaType: SKAlphaType.Premul, skColorSpace: SKColorSpace.CreateSrgb());
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_PlantUmlFileExtension}", _ =>
                        {
                            lock (m_Lock)
                            {
                                ProjectScenarioModel projectScenarioModel = m_CoreViewModel.BuildProjectScenario();
                                string puml = PlantUmlExportService.GeneratePlantUml(
                                    projectScenarioModel.DependentActivities,
                                    projectScenarioModel.WorkStreamSettings);
                                File.WriteAllText(filename, puml);
                            }
                        })
                        .Default(_ => throw new ArgumentOutOfRangeException(nameof(filename), @$"{Resource.ProjectPlan.Messages.Message_UnableToSaveFile} {filename}"));
                }
                catch (Exception ex)
                {
                    await m_DialogService.ShowErrorAsync(
                        Resource.ProjectPlan.Titles.Title_Error,
                        string.Empty,
                        ex.Message);
                }
            }
        }

        public void BuildDependencyGraphDiagramData()
        {
            // Data is built asynchronously via BuildDependencyGraphDiagramDataAsync
            // This synchronous method triggers the async pipeline
            _ = BuildDependencyGraphDiagramDataAsync();
        }

        public void BuildDependencyGraphDiagramImage()
        {
            SvgSource? source = null;

            lock (m_Lock)
            {
                string dependencyGraphData = DependencyGraphData;
                if (!string.IsNullOrWhiteSpace(dependencyGraphData))
                {
                    source = SvgSource.LoadFromSvg(dependencyGraphData);
                }
            }

            Dispatcher.UIThread.Invoke(() =>
            {
                var image = new SvgImage
                {
                    Source = source
                };
                DependencyGraphImage = image;
            });
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_BuildDependencyGraphDataSub?.Dispose();
            m_BuildDependencyGraphImageSub?.Dispose();
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
                KillSubscriptions();
                m_IsBusy?.Dispose();
                m_HasStaleOutputs?.Dispose();
                m_HasCompilationErrors?.Dispose();
                m_BaseTheme?.Dispose();
            }

            m_Disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
