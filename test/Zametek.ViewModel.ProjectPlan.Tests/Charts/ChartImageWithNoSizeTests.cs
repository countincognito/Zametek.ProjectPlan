using Microsoft.Extensions.Logging.Abstractions;
using ReactiveUI;
using Shouldly;
using System;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Xunit;
using Zametek.Contract.ProjectPlan;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The size of a chart on the screen is the size of its image (ImageBounds). A chart that the screen has not shown has no
    /// size. The save of its image then wrote no file and showed no message (from the review of src, test and pkg). It now shows
    /// the error, and it still writes no file.
    /// </summary>
    public sealed class ChartImageWithNoSizeTests
        : IDisposable
    {
        private readonly string m_Folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $@"zpp-chart-size-{Guid.NewGuid():N}")).FullName;

        public void Dispose()
        {
            Directory.Delete(m_Folder, recursive: true);
        }

        [Theory]
        [InlineData(@"gantt")]
        [InlineData(@"earned value")]
        [InlineData(@"resource")]
        [InlineData(@"scenario")]
        [InlineData(@"simulation")]
        public async Task SaveImage_Given_AChartWithNoSize_Then_AnErrorAndNoFile(string chart)
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
            using CoreViewModel _ = core;
            string filename = Path.Combine(m_Folder, @"chart.png");
            var dialogs = new ScriptedDialogService { SaveFilename = filename };
            var calculator = new DateTimeCalculator(TimeProvider.System);
            var exporter = new ScottPlotImageExporter();
            IUIDispatcher dispatcher = CoreViewModelFixture.CreateUIDispatcher();
            using var manager = new ProjectScenarioManagerViewModel(core, settings, CoreViewModelFixture.CreateDialogService(), calculator, dispatcher);

            (IDisposable panel, ICommand save) = chart switch
            {
                @"gantt" => Make(new GanttChartManagerViewModel(core, settings, dialogs, calculator, exporter, dispatcher), x => x.SaveGanttChartImageFileCommand),
                @"earned value" => Make(new EarnedValueChartManagerViewModel(core, settings, dialogs, calculator, exporter, new ResourceSchedulingService(new ProjectPlanMapper()), dispatcher), x => x.SaveEarnedValueChartImageFileCommand),
                @"resource" => Make(new ResourceChartManagerViewModel(core, settings, dialogs, calculator, exporter, dispatcher), x => x.SaveResourceChartImageFileCommand),
                @"scenario" => Make(new ScenarioChartManagerViewModel(core, manager, settings, dialogs, calculator, exporter, dispatcher), x => x.SaveScenarioChartImageFileCommand),
                @"simulation" => Make(new SimulationChartManagerViewModel(core, settings, dialogs, calculator, exporter, dispatcher, NullLogger<SimulationChartManagerViewModel>.Instance), x => x.SaveSimulationChartImageFileCommand),
                _ => throw new ArgumentOutOfRangeException(nameof(chart)),
            };

            using (panel)
            {
                await ((ReactiveCommand<RxVoid, RxVoid>)save).Execute();
            }

            dialogs.SaveDialogs.Count.ShouldBe(1);
            dialogs.Errors.ShouldHaveSingleItem().Message.ShouldBe(Resource.ProjectPlan.Messages.Message_ChartHasNoSize);
            File.Exists(filename).ShouldBeFalse();
        }

        private static (IDisposable Panel, ICommand Save) Make<T>(T panel, Func<T, ICommand> save)
            where T : IDisposable
        {
            return (panel, save(panel));
        }
    }
}
