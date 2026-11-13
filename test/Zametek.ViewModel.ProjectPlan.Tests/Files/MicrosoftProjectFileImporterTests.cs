using Shouldly;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    // A plan of MS Project gives each duration in a unit of its own: minutes, hours, days, weeks or months. A duration of zpp
    // is a number of working days. The sample has three tasks in days: Design 3, Build 5 and Test 2. It has 8 hours to a day,
    // 40 hours to a week and 20 days to a month.
    public class MicrosoftProjectFileImporterTests
    {
        private static readonly XNamespace s_Project = @"http://schemas.microsoft.com/project";

        // The units of a duration in a file of MS Project (DurationFormat).
        private const int c_Minutes = 3;
        private const int c_Hours = 5;
        private const int c_Days = 7;
        private const int c_Weeks = 9;
        private const int c_Months = 11;

        private static ProjectScenarioImportModel Import(Action<XDocument>? change = null)
        {
            XDocument document = XDocument.Load(Path.Combine(@"TestFiles", @"sample_mspdi.xml"));
            change?.Invoke(document);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(document.ToString()));
            var importer = new MicrosoftProjectFileImporter(
                CoreViewModelFixture.CreateSettingService(),
                new DateTimeCalculator(TimeProvider.System));
            return importer.ImportMicrosoftProjectFile(stream);
        }

        // Gives the task a duration as MS Project writes it: the working time in hours, and the unit in which the task shows
        // it.
        private static Action<XDocument> WithDuration(string task, string duration, int unit) => document =>
        {
            XElement element = document.Descendants(s_Project + @"Task").Single(x => (string?)x.Element(s_Project + @"Name") == task);
            element.Element(s_Project + @"Duration")!.Value = duration;
            element.Element(s_Project + @"DurationFormat")!.Value = unit.ToString(CultureInfo.InvariantCulture);
        };

        private static int[] Durations(ProjectScenarioImportModel imported) =>
            [.. imported.DependentActivities.Select(x => x.Activity.Duration)];

        [Fact]
        public void ImportMicrosoftProjectFile_Given_DurationsInDays_Then_TheDays()
        {
            Durations(Import()).ShouldBe([3, 5, 2]);
        }

        // Build takes 40 hours: 2,400 minutes, 5 days, 1 week or a quarter of a month. The importer took the number of the
        // unit as a number of days, so 40 hours became 40 days, and 1 week became 1 day.
        [Theory]
        [InlineData(c_Minutes)]
        [InlineData(c_Hours)]
        [InlineData(c_Days)]
        [InlineData(c_Weeks)]
        [InlineData(c_Months)]
        public void ImportMicrosoftProjectFile_Given_ADurationInAUnit_Then_ItsWorkingDays(int unit)
        {
            Durations(Import(WithDuration(@"Build", @"PT40H0M0S", unit))).ShouldBe([3, 5, 2]);
        }

        // A task with work takes at least the day of that work: a part of a day counts as a whole day.
        [Theory]
        [InlineData(@"PT4H0M0S", c_Hours, 1)]
        [InlineData(@"PT1H0M0S", c_Minutes, 1)]
        [InlineData(@"PT20H0M0S", c_Days, 3)]
        [InlineData(@"PT44H0M0S", c_Weeks, 6)]
        public void ImportMicrosoftProjectFile_Given_APartOfADay_Then_AWholeDay(string duration, int unit, int days)
        {
            Durations(Import(WithDuration(@"Build", duration, unit))).ShouldBe([3, days, 2]);
        }

        // A milestone has no work, in any unit.
        [Theory]
        [InlineData(c_Hours)]
        [InlineData(c_Days)]
        public void ImportMicrosoftProjectFile_Given_NoWork_Then_NoDays(int unit)
        {
            Durations(Import(WithDuration(@"Build", @"PT0H0M0S", unit))).ShouldBe([3, 0, 2]);
        }
    }
}
