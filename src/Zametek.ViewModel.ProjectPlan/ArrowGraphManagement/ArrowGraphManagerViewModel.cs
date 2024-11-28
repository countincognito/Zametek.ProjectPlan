using Avalonia.Svg.Skia;
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

        private readonly object m_Lock;

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

        private readonly IDisposable? m_BuildArrowGraphDataSub;
        private readonly IDisposable? m_BuildArrowGraphImageSub;

        #endregion

        #region Ctors

        public ArrowGraphManagerViewModel(
            ICoreViewModel coreViewModel,
            ISettingService settingService,
            IDialogService dialogService,
            IArrowGraphSerializer arrowGraphExport)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(settingService);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(arrowGraphExport);
            m_Lock = new object();
            m_CoreViewModel = coreViewModel;
            m_SettingService = settingService;
            m_DialogService = dialogService;
            m_ArrowGraphExport = arrowGraphExport;

            m_ArrowGraphData = string.Empty;
            m_ArrowGraphImage = new SvgImage();

            {
                ReactiveCommand<Unit, Unit> saveArrowGraphImageFileCommand = ReactiveCommand.CreateFromTask(SaveArrowGraphImageFileAsync);
                SaveArrowGraphImageFileCommand = saveArrowGraphImageFileCommand;
            }

            m_IsBusy = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.IsBusy)
                .ToProperty(this, mm => mm.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, mm => mm.HasStaleOutputs);

            m_HasCompilationErrors = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, mm => mm.HasCompilationErrors);

            m_BaseTheme = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.BaseTheme)
                .ToProperty(this, mm => mm.BaseTheme);

            m_BuildArrowGraphDataSub = this
                .WhenAnyValue(
                    agm => agm.m_CoreViewModel.ArrowGraph,
                    agm => agm.m_CoreViewModel.ArrowGraphSettings,
                    agm => agm.m_CoreViewModel.BaseTheme)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async _ => await BuildArrowGraphDiagramDataAsync());

            m_BuildArrowGraphImageSub = this
                .ObservableForProperty(agm => agm.ArrowGraphData)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(async _ => await BuildArrowGraphDiagramImageAsync());

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
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_ArrowGraphImage, value);
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

        private async Task BuildArrowGraphDiagramImageAsync()
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
                string projectTitle = m_SettingService.ProjectTitle;
                string directory = m_SettingService.ProjectDirectory;
                string? filename = await m_DialogService.ShowSaveFileDialogAsync(projectTitle, directory, s_ExportFileFilters);

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

        private string m_ArrowGraphData;
        public string ArrowGraphData
        {
            get => m_ArrowGraphData;
            private set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_ArrowGraphData, value);
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

                    fileExtension.ValueSwitchOn()
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageJpegFileExtension}", _ =>
                        {
                            using var stream = File.OpenWrite(filename);
                            ArrowGraphImage.Source?.Picture?.ToImage(
                                stream, SKColors.White, SKEncodedImageFormat.Jpeg, quality: 100, scaleX: 2, scaleY: 2,
                                skColorType: SKColorType.Argb4444, skAlphaType: SKAlphaType.Premul, skColorSpace: SKColorSpace.CreateSrgb());
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension}", _ =>
                        {
                            using var stream = File.OpenWrite(filename);
                            ArrowGraphImage.Source?.Picture?.ToImage(
                                stream, SKColors.White, SKEncodedImageFormat.Png, quality: 100, scaleX: 2, scaleY: 2,
                                skColorType: SKColorType.Argb4444, skAlphaType: SKAlphaType.Premul, skColorSpace: SKColorSpace.CreateSrgb());
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_PdfFileExtension}", _ =>
                        {
                            ArrowGraphImage.Source?.Picture?.ToPdf(filename, SKColors.White, scaleX: 2, scaleY: 2);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_ImageSvgFileExtension}", _ =>
                        {
                            ArrowGraphImage.Source?.Picture?.ToSvg(filename, SKColors.White, scaleX: 2, scaleY: 2);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_GraphMLFileExtension}", _ =>
                        {
                            data = m_ArrowGraphExport.BuildArrowGraphMLData(m_CoreViewModel.ArrowGraph, m_CoreViewModel.ArrowGraphSettings);
                        })
                        .Case($".{Resource.ProjectPlan.Filters.Filter_GraphVizFileExtension}", _ =>
                        {
                            data = m_ArrowGraphExport.BuildArrowGraphVizData(m_CoreViewModel.ArrowGraph, m_CoreViewModel.ArrowGraphSettings);
                        })
                        .Default(_ => throw new ArgumentOutOfRangeException(nameof(filename), @$"{Resource.ProjectPlan.Messages.Message_UnableToSaveFile} {filename}"));

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
            byte[]? data = null;

            lock (m_Lock)
            {
                data = m_ArrowGraphExport.BuildArrowGraphSvgData(
                    m_CoreViewModel.ArrowGraph,
                    m_CoreViewModel.ArrowGraphSettings,
                    m_CoreViewModel.BaseTheme);
            }

            ArrowGraphData = data?.ByteArrayToString() ?? string.Empty;
        }

        public void BuildArrowGraphDiagramImage()
        {
            SvgImage? image = null;

            lock (m_Lock)
            {
                string arrowGraphData = ArrowGraphData;
                if (!string.IsNullOrWhiteSpace(arrowGraphData))
                {
                    SvgSource? source = SvgSource.LoadFromSvg(arrowGraphData);
                    image = new SvgImage
                    {
                        Source = source
                    };
                }
            }

            ArrowGraphImage = image ?? new SvgImage();
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
                // TODO: dispose managed state (managed objects).
                KillSubscriptions();
                m_IsBusy?.Dispose();
                m_HasStaleOutputs?.Dispose();
                m_HasCompilationErrors?.Dispose();
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
