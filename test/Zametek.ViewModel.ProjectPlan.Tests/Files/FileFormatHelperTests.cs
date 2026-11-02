using Shouldly;
using System;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    // The desktop's dialogs and zpp's options start from a file name, and the file layer takes a format. These pin which
    // extension names which format, and that any other extension is refused with the message the file layer gave when
    // it was handed file names.
    public class FileFormatHelperTests
    {
        [Theory]
        [InlineData(@"plan.mpp", ProjectScenarioImportFormat.MicrosoftProject)]
        [InlineData(@"plan.xml", ProjectScenarioImportFormat.MicrosoftProject)]
        [InlineData(@"plan.xlsx", ProjectScenarioImportFormat.Xlsx)]
        public void GetProjectScenarioImportFormat_Given_KnownExtension_Then_NamesItsFormat(string filename, ProjectScenarioImportFormat expected)
        {
            FileFormatHelper.GetProjectScenarioImportFormat(filename).ShouldBe(expected);
        }

        [Fact]
        public void GetProjectScenarioImportFormat_Given_UnknownExtension_Then_RefusesIt()
        {
            ArgumentOutOfRangeException exception = Should.Throw<ArgumentOutOfRangeException>(
                () => FileFormatHelper.GetProjectScenarioImportFormat(@"plan.txt"));

            exception.Message.ShouldStartWith($@"{Resource.ProjectPlan.Messages.Message_UnableToImportFile} plan.txt");
        }

        [Fact]
        public void GetProjectScenarioExportFormat_Given_Xlsx_Then_NamesXlsx()
        {
            FileFormatHelper.GetProjectScenarioExportFormat(@"plan.xlsx").ShouldBe(ProjectScenarioExportFormat.Xlsx);
        }

        [Fact]
        public void GetProjectScenarioExportFormat_Given_UnknownExtension_Then_RefusesIt()
        {
            ArgumentOutOfRangeException exception = Should.Throw<ArgumentOutOfRangeException>(
                () => FileFormatHelper.GetProjectScenarioExportFormat(@"plan.mpp"));

            exception.Message.ShouldStartWith($@"{Resource.ProjectPlan.Messages.Message_UnableToExportFile} plan.mpp");
        }

        [Theory]
        [InlineData(@"chart.jpeg", ChartImageFormat.Jpeg)]
        [InlineData(@"chart.png", ChartImageFormat.Png)]
        [InlineData(@"chart.bmp", ChartImageFormat.Bmp)]
        [InlineData(@"chart.webp", ChartImageFormat.Webp)]
        [InlineData(@"chart.svg", ChartImageFormat.Svg)]
        [InlineData(@"chart.pdf", ChartImageFormat.Pdf)]
        public void GetChartImageFormat_Given_KnownExtension_Then_NamesItsFormat(string filename, ChartImageFormat expected)
        {
            FileFormatHelper.GetChartImageFormat(filename).ShouldBe(expected);
        }

        [Fact]
        public void GetChartImageFormat_Given_UnknownExtension_Then_RefusesIt()
        {
            ArgumentOutOfRangeException exception = Should.Throw<ArgumentOutOfRangeException>(
                () => FileFormatHelper.GetChartImageFormat(@"chart.gif"));

            exception.Message.ShouldStartWith($@"{Resource.ProjectPlan.Messages.Message_UnableToSaveFile} chart.gif");
        }
    }
}
