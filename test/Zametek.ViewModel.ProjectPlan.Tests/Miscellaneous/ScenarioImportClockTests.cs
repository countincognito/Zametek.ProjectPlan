using NPOI.XSSF.UserModel;
using Shouldly;
using System;
using System.IO;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    // An import takes the day from the importer's clock, never the wall clock: the day a Microsoft Project plan is
    // imported on, and the day an Excel workbook that does not say otherwise starts on.
    public class ScenarioImportClockTests
    {
        // 20:00 UTC on 27 September is already 01:30 on the 28th at +05:30, so a day taken from the machine's clock or
        // from UTC would be the wrong one.
        private static readonly FixedTimeProvider s_Clock = new(
            new DateTimeOffset(2026, 9, 27, 20, 0, 0, TimeSpan.Zero),
            TimeZoneInfo.CreateCustomTimeZone(@"Test +05:30", TimeSpan.FromMinutes(330), @"Test +05:30", @"Test +05:30"));

        private static readonly (DateTime, TimeSpan) s_Today = (new DateTime(2026, 9, 28), TimeSpan.FromMinutes(330));

        [Fact]
        public void ImportMicrosoftProjectFile_Given_AClock_Then_TodayIsTheClocksDay()
        {
            var importer = new MicrosoftProjectFileImporter(
                CoreViewModelFixture.CreateSettingService(),
                new DateTimeCalculator(s_Clock));
            using FileStream stream = File.OpenRead(Path.Combine(@"TestFiles", @"sample_mspdi.xml"));

            ProjectScenarioImportModel imported = importer.ImportMicrosoftProjectFile(stream);

            (imported.Today.DateTime, imported.Today.Offset).ShouldBe(s_Today);
        }

        [Fact]
        public void ImportProjectScenarioXlsxFile_Given_AWorkbookWithoutDates_Then_ItStartsOnTheClocksDay()
        {
            // No General sheet, so the workbook says neither when the plan starts nor what day it is.
            using var workbook = new MemoryStream();
            new XSSFWorkbook().Write(workbook, leaveOpen: true);
            workbook.Position = 0;

            ProjectScenarioImportModel imported = new XlsxScenarioFileImporter(new DateTimeCalculator(s_Clock))
                .ImportProjectScenarioXlsxFile(workbook);

            (imported.ProjectStart.DateTime, imported.ProjectStart.Offset).ShouldBe(s_Today);
            (imported.Today.DateTime, imported.Today.Offset).ShouldBe(s_Today);
        }
    }
}
