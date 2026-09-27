using Shouldly;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    // A workbook takes its times from the exporter's clock, never the wall clock: the time it was created, and the
    // time each part of its package was last written. So exporting a scenario at a given time gives the same workbook
    // however often, and wherever, it is done.
    public class XlsxScenarioFileExporterTests
    {
        private static readonly XNamespace s_DcTerms = @"http://purl.org/dc/terms/";

        [Fact]
        public async Task Export_Given_AClock_Then_TheWorkbookIsStampedWithItsTime()
        {
            ProjectScenarioModel scenario = await CoreViewModelFixture.LoadProjectScenarioAsync(@"sample_v0_6_1.zpp");

            // Long past, so the wall clock could never pass for it.
            byte[] workbook = Export(scenario, new FixedTimeProvider(new DateTimeOffset(2001, 2, 3, 4, 5, 6, TimeSpan.Zero), TimeZoneInfo.Utc));

            using var archive = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
            archive.Entries.Select(x => x.LastWriteTime.DateTime).Distinct().ShouldBe([new DateTime(2001, 2, 3, 4, 5, 6)]);
            ReadCreated(archive).ShouldBe(@"2001-02-03T04:05:06Z");
        }

        [Fact]
        public async Task Export_Given_AClockInAnotherTimeZone_Then_ThePartsHoldItsLocalTimeAndTheWorkbookItsUtcTime()
        {
            ProjectScenarioModel scenario = await CoreViewModelFixture.LoadProjectScenarioAsync(@"sample_v0_6_1.zpp");
            TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone(@"Test +05:30", TimeSpan.FromMinutes(330), @"Test +05:30", @"Test +05:30");

            byte[] workbook = Export(scenario, new FixedTimeProvider(new DateTimeOffset(2001, 2, 3, 4, 5, 6, TimeSpan.Zero), zone));

            // A zip holds the local clock time; the workbook's properties hold UTC.
            using var archive = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
            archive.Entries.Select(x => x.LastWriteTime.DateTime).Distinct().ShouldBe([new DateTime(2001, 2, 3, 9, 35, 6)]);
            ReadCreated(archive).ShouldBe(@"2001-02-03T04:05:06Z");
        }

        [Fact]
        public async Task Export_Given_TheSameTime_Then_TheWorkbooksAreIdentical()
        {
            ProjectScenarioModel scenario = await CoreViewModelFixture.LoadProjectScenarioAsync(@"sample_v0_6_1.zpp");
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.FromHours(1)), TimeZoneInfo.Utc);

            Export(scenario, clock).ShouldBe(Export(scenario, clock));
        }

        [Theory]
        [InlineData(1970, 1, 1, 1980, 1, 1, 0, 0, 0)]
        [InlineData(2200, 6, 1, 2107, 12, 31, 23, 59, 58)]
        public async Task Export_Given_ATimeAZipCannotHold_Then_ThePartsHoldTheNearestItCan(
            int year, int month, int day,
            int expectedYear, int expectedMonth, int expectedDay, int expectedHour, int expectedMinute, int expectedSecond)
        {
            ProjectScenarioModel scenario = await CoreViewModelFixture.LoadProjectScenarioAsync(@"sample_v0_6_1.zpp");

            byte[] workbook = Export(scenario, new FixedTimeProvider(new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc));

            using var archive = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read);
            archive.Entries.Select(x => x.LastWriteTime.DateTime).Distinct().ShouldBe(
                [new DateTime(expectedYear, expectedMonth, expectedDay, expectedHour, expectedMinute, expectedSecond)]);
            ReadCreated(archive).ShouldBe($@"{year:D4}-{month:D2}-{day:D2}T00:00:00Z");
        }

        private static byte[] Export(ProjectScenarioModel scenario, TimeProvider clock)
        {
            var exporter = new XlsxScenarioFileExporter(new DateTimeCalculator(clock));
            using var stream = new MemoryStream();
            exporter.ExportProjectScenarioXlsxFile(scenario, new ResourceSeriesSetModel(), new TrackingSeriesSetModel(), showDates: false, stream);
            return stream.ToArray();
        }

        private static string ReadCreated(ZipArchive archive)
        {
            using Stream core = archive.GetEntry(@"docProps/core.xml")!.Open();
            return XDocument.Load(core).Descendants(s_DcTerms + @"created").Single().Value;
        }
    }
}
