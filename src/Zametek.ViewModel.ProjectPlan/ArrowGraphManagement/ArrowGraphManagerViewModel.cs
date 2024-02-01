using Avalonia.Svg.Skia;
using ReactiveUI;
using SkiaSharp;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ArrowGraphManagerViewModel
        : ToolViewModelBase, IArrowGraphManagerViewModel, IDisposable
    {
        #region Fields

        private readonly object m_Lock;

        private static readonly IList<IFileFilter> s_ExportFileFilters =
            new List<IFileFilter>
            {
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
            };

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

            m_BuildArrowGraphDataSub = this
                .WhenAnyValue(agm => agm.m_CoreViewModel.ArrowGraph, agm => agm.m_CoreViewModel.ArrowGraphSettings)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async result => ArrowGraphData = await BuildArrowGraphDiagramDataAsync(result.Item1, result.Item2));

            m_BuildArrowGraphImageSub = this
                .WhenAnyValue(agm => agm.ArrowGraphData)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(async data => ArrowGraphImage = await BuildArrowGraphDiagramImageAsync(data));

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

        private async Task<string> BuildArrowGraphDiagramDataAsync(
            ArrowGraphModel arrowGraphModel,
            ArrowGraphSettingsModel arrowGraphSettingsModel)
        {
            try
            {
                lock (m_Lock)
                {
                    byte[] data = m_ArrowGraphExport.BuildArrowGraphSvgData(arrowGraphModel, arrowGraphSettingsModel);
                    return data.ByteArrayToString();
                }
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    ex.Message);
            }

            return string.Empty;
        }

        private async Task<SvgImage> BuildArrowGraphDiagramImageAsync(string arrowGraphData)
        {
            if (string.IsNullOrWhiteSpace(arrowGraphData))
            {
                return new SvgImage();
            }

            try
            {
                lock (m_Lock)
                {
                    var source = SvgSource.LoadFromSvg(arrowGraphData);
                    return new SvgImage
                    {
                        Source = source
                    };
                }
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    ex.Message);
            }

            return new SvgImage();
        }

        private async Task SaveArrowGraphImageFileInternalAsync(string? filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    Resource.ProjectPlan.Messages.Message_EmptyFilename);
            }
            else
            {
                string fileExtension = Path.GetExtension(filename);
                byte[]? data = null;

                fileExtension.ValueSwitchOn()
                    .Case($".{Resource.ProjectPlan.Filters.Filter_ImageJpegFileExtension}", _ =>
                    {
                        ArrowGraphImage.Source?.Save(filename, SKColors.White, SKEncodedImageFormat.Jpeg, scaleX: 2, scaleY: 2);
                    })
                    .Case($".{Resource.ProjectPlan.Filters.Filter_ImagePngFileExtension}", _ =>
                    {
                        ArrowGraphImage.Source?.Save(filename, SKColors.White, SKEncodedImageFormat.Png, scaleX: 2, scaleY: 2);
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
                    await SaveArrowGraphImageFileInternalAsync(filename);
                }
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
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

        public ICommand SaveArrowGraphImageFileCommand { get; }

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
                m_BuildArrowGraphDataSub?.Dispose();
                m_BuildArrowGraphImageSub?.Dispose();
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
