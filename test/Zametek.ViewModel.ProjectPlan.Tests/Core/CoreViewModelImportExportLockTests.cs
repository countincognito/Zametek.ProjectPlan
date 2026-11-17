using Shouldly;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The importer reads a file and returns a model, and the exporter writes the models that it gets. Neither uses the state
    /// of the core, and a large workbook or an MS Project file can take seconds. So the core shows that it is busy while one
    /// of them runs, and it does not hold its lock: a setter on the UI thread, which takes that lock, does not wait for the
    /// file.
    /// </summary>
    public class CoreViewModelImportExportLockTests
    {
        private static readonly TimeSpan s_Limit = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task ImportProjectScenarioFile_Given_AnImportInProgress_Then_TheCoreIsBusyAndItsLockIsFree()
        {
            using var importer = new GatedFiles();
            (CoreViewModel core, _) = CoreViewModelFixture.CreateWithSettingService(projectScenarioFileImport: importer);

            using (core)
            {
                Task<ProjectScenarioImportModel> import = Task.Run(
                    () => core.ImportProjectScenarioFile(Stream.Null, ProjectScenarioImportFormat.Xlsx));

                try
                {
                    importer.Entered.Wait(s_Limit).ShouldBeTrue();
                    core.IsBusy.ShouldBeTrue();

                    (await TakesTheLockOfTheCoreAsync(core)).ShouldBeTrue();
                }
                finally
                {
                    importer.Release.Set();
                }

                (await import).ShouldBeSameAs(importer.Imported);
                core.IsBusy.ShouldBeFalse();
            }
        }

        [Fact]
        public async Task ExportProjectScenarioFile_Given_AnExportInProgress_Then_TheCoreIsBusyAndItsLockIsFree()
        {
            using var exporter = new GatedFiles();
            (CoreViewModel core, _) = CoreViewModelFixture.CreateWithSettingService(projectScenarioFileExport: exporter);

            using (core)
            {
                Task export = Task.Run(
                    () => core.ExportProjectScenarioFile(
                        new ProjectScenarioModel(),
                        new ResourceSeriesSetModel(),
                        new TrackingSeriesSetModel(),
                        showDates: false,
                        Stream.Null,
                        ProjectScenarioExportFormat.Xlsx));

                try
                {
                    exporter.Entered.Wait(s_Limit).ShouldBeTrue();
                    core.IsBusy.ShouldBeTrue();

                    (await TakesTheLockOfTheCoreAsync(core)).ShouldBeTrue();
                }
                finally
                {
                    exporter.Release.Set();
                }

                await export;
                core.IsBusy.ShouldBeFalse();
            }
        }

        [Fact]
        public void ImportProjectScenarioFile_Given_AnImporterThatFails_Then_TheCoreIsNotBusyAfter()
        {
            (CoreViewModel core, _) = CoreViewModelFixture.CreateWithSettingService();

            using (core)
            {
                // The importer of the fixture refuses each file.
                Should.Throw<NotSupportedException>(
                    () => core.ImportProjectScenarioFile(Stream.Null, ProjectScenarioImportFormat.Xlsx));

                core.IsBusy.ShouldBeFalse();
            }
        }

        // A setter of the core that takes its lock, on another thread, as a setter on the UI thread does. False when the
        // setter still waits after two seconds.
        private static async Task<bool> TakesTheLockOfTheCoreAsync(CoreViewModel core)
        {
            Task setter = Task.Run(() => core.SimulationSummary = new SimulationSummaryModel());
            Task first = await Task.WhenAny(setter, Task.Delay(TimeSpan.FromSeconds(2)));
            return ReferenceEquals(first, setter);
        }

        // An importer and an exporter that wait in the middle of their work until the test lets them go on.
        private sealed class GatedFiles
            : IProjectScenarioFileImport, IProjectScenarioFileExport, IDisposable
        {
            public ManualResetEventSlim Entered { get; } = new();

            public ManualResetEventSlim Release { get; } = new();

            public ProjectScenarioImportModel Imported { get; } = new();

            public ProjectScenarioImportModel ImportProjectScenarioFile(Stream stream, ProjectScenarioImportFormat format, CancellationToken cancellationToken = default)
            {
                Entered.Set();
                Release.Wait(s_Limit).ShouldBeTrue();
                return Imported;
            }

            public void ExportProjectScenarioFile(
                ProjectScenarioModel projectScenario,
                ResourceSeriesSetModel resourceSeriesSet,
                TrackingSeriesSetModel trackingSeriesSet,
                bool showDates,
                Stream stream,
                ProjectScenarioExportFormat format)
            {
                Entered.Set();
                Release.Wait(s_Limit).ShouldBeTrue();
            }

            public void Dispose()
            {
                Entered.Dispose();
                Release.Dispose();
            }
        }
    }
}
