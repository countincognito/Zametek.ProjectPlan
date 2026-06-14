using Avalonia.Svg.Skia;
using Avalonia.Threading;
using ReactiveUI;
using SkiaSharp;
using Svg.Skia;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ArrowGraphManagerViewModel
        : ToolViewModelBase, IArrowGraphManagerViewModel
    {
        #region Fields

        private readonly Lock m_Lock;

        private static readonly IList<IFileFilter> s_ExportFileFilters =
            [
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ImageJpegFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_ImageJpegFilePattern
                    ]
                },
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
                    Name = Resource.ProjectPlan.Filters.Filter_PdfFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_PdfFilePattern
                    ]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_ImageSvgFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_ImageSvgFilePattern
                    ]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_GraphMLFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_GraphMLFilePattern
                    ]
                },
                new FileFilter
                {
                    Name = Resource.ProjectPlan.Filters.Filter_GraphVizFileType,
                    Patterns =
                    [
                        Resource.ProjectPlan.Filters.Filter_GraphVizFilePattern
                    ]
                }
            ];

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly ISettingService m_SettingService;
        private readonly IDialogService m_DialogService;
        private readonly IArrowGraphSerializer m_ArrowGraphExport;
        private readonly IGraphImageExporter m_GraphImageExporter;

        private readonly IDisposable? m_BuildArrowGraphDataSub;
        private readonly IDisposable? m_BuildArrowGraphImageSub;

        #endregion

        #region Ctors

        public ArrowGraphManagerViewModel(
            ICoreViewModel coreViewModel,
            ISettingService settingService,
            IDialogService dialogService,
            IArrowGraphSerializer arrowGraphExport,
            IGraphImageExporter graphImageExporter)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(arrowGraphExport);
            ArgumentNullException.ThrowIfNull(graphImageExporter);
            m_Lock = new();
            m_CoreViewModel = coreViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            m_ArrowGraphExport = arrowGraphExport;
            m_GraphImageExporter = graphImageExporter;

            m_ArrowGraphData = string.Empty;
            m_ArrowGraphImage = new SvgImage();

            {
                ReactiveCommand<Unit, Unit> saveArrowGraphImageFileCommand = ReactiveCommand.CreateFromTask(SaveArrowGraphImageFileAsync);
                SaveArrowGraphImageFileCommand = saveArrowGraphImageFileCommand;
            }

            m_IsBusy = this
                .WhenAnyValue(agm => agm.m_CoreViewModel.IsBusy)
                .ToProperty(this, agm => agm.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(agm => agm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, agm => agm.HasStaleOutputs);

            m_HasCompilationErrors = this
                .WhenAnyValue(agm => agm.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, agm => agm.HasCompilationErrors);

            m_ShowNames = this
                .WhenAnyValue(agm => agm.m_CoreViewModel.DisplaySettingsViewModel.ArrowGraphShowNames)
                .ToProperty(this, agm => agm.ShowNames);

            m_BaseTheme = this
                .WhenAnyValue(agm => agm.m_CoreViewModel.BaseTheme)
                .ToProperty(this, agm => agm.BaseTheme);

            m_BuildArrowGraphDataSub = this
                .WhenAnyValue(
                    agm => agm.m_CoreViewModel.ArrowGraph,
                    agm => agm.m_CoreViewModel.GraphSettings,
                    agm => agm.m_CoreViewModel.BaseTheme,
                    agm => agm.m_CoreViewModel.DisplaySettingsViewModel.ArrowGraphShowNames)
                .MuteWhile(this.WhenAnyValue(agm => agm.m_CoreViewModel.IsBulkUpdating)) // Conflate redundant notifications while a project scenario is loaded/reset.
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async _ => await BuildArrowGraphDiagramDataAsync());

            m_BuildArrowGraphImageSub = this
                .WhenAnyValue(agm => agm.ArrowGraphData)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async _ => await BuildArrowGraphDiagramAsync());

            Id = Resource.ProjectPlan.Titles.Title_ArrowGraphView;
            Title = Resource.ProjectPlan.Titles.Title_ArrowGraphView;
        }

        #endregion

        #region Properties

        private SvgImage m_ArrowGraphImage;
        public SvgImage ArrowGraphImage
        {
            get => m_ArrowGraphImage;
            private set
            {
                this.RaiseAndSetIfChanged(ref m_ArrowGraphImage, value);
            }
        }

        #endregion

        #region Private Methods

        private async Task BuildArrowGraphDiagramDataAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    BuildArrowGraphDiagramData();
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

        private async Task BuildArrowGraphDiagramAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    BuildArrowGraphDiagramImage();
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

        private async Task SaveArrowGraphImageFileAsync()
        {
            try
            {
                string title = m_SettingService.ProjectTitle;
                title = string.IsNullOrWhiteSpace(title) ? Resource.ProjectPlan.Titles.Title_UntitledProject : title;
                string graphOutputFile = $@"{title}{Resource.ProjectPlan.Suffixes.Suffix_ArrowChart}";
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(graphOutputFile, directory, s_ExportFileFilters);

                if (!string.IsNullOrWhiteSpace(filename))
                {
                    await SaveArrowGraphImageFileAsync(filename);
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

        #region IArrowGraphManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private readonly ObservableAsPropertyHelper<bool> m_ShowNames;
        public bool ShowNames
        {
            get => m_ShowNames.Value;
            set
            {
                lock (m_Lock) m_CoreViewModel.DisplaySettingsViewModel.ArrowGraphShowNames = value;
            }
        }

        private string m_ArrowGraphData;
        public string ArrowGraphData
        {
            get => m_ArrowGraphData;
            private set
            {
                lock (m_Lock)
                {
                    this.RaiseAndSetIfChanged(ref m_ArrowGraphData, value);
                }
            }
        }

        private readonly ObservableAsPropertyHelper<BaseTheme> m_BaseTheme;
        public BaseTheme BaseTheme => m_BaseTheme.Value;

        public ICommand SaveArrowGraphImageFileCommand { get; }

        public async Task SaveArrowGraphImageFileAsync(string? filename)
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
                    byte[]? data = null;
                    bool isSkiaFormat = false;

                    fileExtension.ValueSwitchOn()
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageJpegFileExtension}", _ => isSkiaFormat = true)
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension}", _ => isSkiaFormat = true)
                        .Case($".{Resource.ProjectPlan.Filters.Filter_PdfFileExtension}", _ => isSkiaFormat = true)
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageSvgFileExtension}", _ => isSkiaFormat = true)
                        .Case($".{Resource.ProjectPlan.Filters.Filter_GraphMLFileExtension}", _ =>
                        {
                            data = m_ArrowGraphExport.BuildArrowGraphMLData(GraphPresentationBuilder.ApplyPresentation(m_CoreViewModel.ArrowGraph, m_CoreViewModel.GraphSettings), m_CoreViewModel.DisplaySettingsViewModel.ArrowGraphShowNames);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_GraphVizFileExtension}", _ =>
                        {
                            data = m_ArrowGraphExport.BuildArrowGraphVizData(GraphPresentationBuilder.ApplyPresentation(m_CoreViewModel.ArrowGraph, m_CoreViewModel.GraphSettings), m_CoreViewModel.DisplaySettingsViewModel.ArrowGraphShowNames);
                        })
                        .Default(_ => throw new ArgumentOutOfRangeException(nameof(filename), @$"{Resource.ProjectPlan.Messages.Message_UnableToSaveFile} {filename}"));

                    if (isSkiaFormat && ArrowGraphImage.Source?.Picture is SKPicture picture)
                    {
                        await m_GraphImageExporter.SaveGraphImageAsync(picture, filename, scaleX: 2, scaleY: 2);
                    }

                    if (data is not null)
                    {
                        using var stream = File.OpenWrite(filename);
                        await stream.WriteAsync(data);
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
        }

        public void BuildArrowGraphDiagramData()
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(ArrowGraphManagerViewModel)}.{nameof(BuildArrowGraphDiagramData)}");
            byte[]? data = null;

            lock (m_Lock)
            {
                if (!HasCompilationErrors)
                {
                    data = m_ArrowGraphExport.BuildArrowGraphSvgData(
                        GraphPresentationBuilder.ApplyPresentation(m_CoreViewModel.ArrowGraph, m_CoreViewModel.GraphSettings),
                        m_CoreViewModel.BaseTheme,
                        m_CoreViewModel.DisplaySettingsViewModel.ArrowGraphShowNames);
                }
            }

            ArrowGraphData = data?.ByteArrayToString() ?? string.Empty;
        }

        public void BuildArrowGraphDiagramImage()
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(ArrowGraphManagerViewModel)}.{nameof(BuildArrowGraphDiagramImage)}");
            SvgSource? source = null;

            lock (m_Lock)
            {
                string arrowGraphData = ArrowGraphData;
                if (!string.IsNullOrWhiteSpace(arrowGraphData))
                {
                    source = SvgSource.LoadFromSvg(arrowGraphData);
                }
            }

            Dispatcher.UIThread.Invoke(() =>
            {
                var image = new SvgImage
                {
                    Source = source
                };
                ArrowGraphImage = image;
            });
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_BuildArrowGraphDataSub?.Dispose();
            m_BuildArrowGraphImageSub?.Dispose();
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
                m_ShowNames?.Dispose();
                m_BaseTheme?.Dispose();
            }

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
