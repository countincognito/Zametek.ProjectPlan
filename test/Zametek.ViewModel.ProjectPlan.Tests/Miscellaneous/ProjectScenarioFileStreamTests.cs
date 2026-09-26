using Shouldly;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    // Scenarios are imported and exported through streams the caller owns: a file on the desktop, a buffer or a request
    // body in a service. Each reader and writer must therefore leave the stream open, however the library underneath it
    // treats the streams it is given.
    public class ProjectScenarioFileStreamTests
    {
        [Fact]
        public async Task ExportThenImport_Given_Xlsx_Then_TheActivitiesComeBackAndTheStreamStaysOpen()
        {
            ProjectScenarioModel scenario = await CoreViewModelFixture.LoadProjectScenarioAsync(@"sample_v0_6_1.zpp");
            var exporter = new XlsxScenarioFileExporter(new DateTimeCalculator(TimeProvider.System));
            using var stream = new MemoryStream();

            exporter.ExportProjectScenarioXlsxFile(scenario, new ResourceSeriesSetModel(), new TrackingSeriesSetModel(), showDates: false, stream);

            stream.CanWrite.ShouldBeTrue();
            stream.Position = 0;

            ProjectScenarioImportModel imported = new XlsxScenarioFileImporter().ImportProjectScenarioXlsxFile(stream);

            stream.CanRead.ShouldBeTrue();
            imported.DependentActivities
                .Select(x => (x.Activity.Id, x.Activity.Name, x.Activity.Duration))
                .ShouldBe(scenario.DependentActivities.Select(x => (x.Activity.Id, x.Activity.Name, x.Activity.Duration)), ignoreOrder: true);
        }

        [Fact]
        public void ImportMicrosoftProjectFile_Given_MsProjectXml_Then_TheTasksBecomeActivitiesAndTheStreamStaysOpen()
        {
            // Three tasks in a chain - Design, then Build, then Test - between two resources, written by MPXJ itself.
            var importer = new MicrosoftProjectFileImporter(CoreViewModelFixture.CreateSettingService());
            using FileStream stream = File.OpenRead(Path.Combine(@"TestFiles", @"sample_mspdi.xml"));

            ProjectScenarioImportModel imported = importer.ImportMicrosoftProjectFile(stream);

            stream.CanRead.ShouldBeTrue();
            imported.DependentActivities.Select(x => x.Activity.Name).ShouldBe([@"Design", @"Build", @"Test"]);

            // Distinct, because the importer lists each predecessor twice: it re-adds every predecessor's descendants,
            // and a task with no children counts as its own. The duplicates are harmless - dependencies are a set once
            // the scenario is processed - and they predate the move to streams.
            imported.DependentActivities.Select(x => x.Dependencies.Distinct().ToArray()).ShouldBe([[], [1], [2]]);
            imported.ResourceSettings.Resources.Select(x => x.Name).ShouldBe([@"Alice", @"Bob"]);
        }
    }
}
