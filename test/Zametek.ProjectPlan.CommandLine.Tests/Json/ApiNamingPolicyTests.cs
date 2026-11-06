using Shouldly;
using System.Text.RegularExpressions;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for how zpp serve's API names what it names: camelCase, with graphml - the name of a file format - a word.
    /// </summary>
    public class ApiNamingPolicyTests
    {
        private static readonly Regex s_LowerCamelCase = new(@"^[a-z][a-zA-Z0-9]*$");

        [Theory]
        [InlineData(@"GanttChart", @"ganttChart")]
        [InlineData(@"EarnedValueChart", @"earnedValueChart")]
        [InlineData(@"ErrorLine", @"errorLine")]
        [InlineData(@"ActivityRiskWithStandardDeviationCorrection", @"activityRiskWithStandardDeviationCorrection")]
        [InlineData(@"Jpeg", @"jpeg")]
        [InlineData(@"Dot", @"dot")]
        [InlineData(@"GraphML", @"graphml")]
        [InlineData(@"Graphml", @"graphml")]
        public void ConvertName_Given_AName_Then_CamelCase(string name, string expected)
        {
            ApiNamingPolicy.Instance.ConvertName(name).ShouldBe(expected);
        }

        [Fact]
        public void ConvertName_Given_EveryValueOfEveryEnumTheApiNames_Then_ALowerCamelCaseWord()
        {
            string[] names =
            [
                .. Enum.GetNames<BaseTheme>(),
                .. Enum.GetNames<MetricsExport>(),
                .. Enum.GetNames<PlotExport>(),
                .. Enum.GetNames<GraphExport>(),
                .. Enum.GetNames<JobOutput>(),
                .. Enum.GetNames<JobTranscriptKind>(),
            ];

            foreach (string name in names)
            {
                s_LowerCamelCase.IsMatch(ApiNamingPolicy.Instance.ConvertName(name)).ShouldBeTrue(name);
            }
        }

        [Fact]
        public void ConvertName_Given_TheValuesOfOneEnum_Then_NoTwoAreTheSame()
        {
            foreach (Type type in new[] { typeof(BaseTheme), typeof(MetricsExport), typeof(PlotExport), typeof(GraphExport), typeof(JobOutput), typeof(JobTranscriptKind) })
            {
                string[] names = [.. Enum.GetNames(type).Select(ApiNamingPolicy.Instance.ConvertName)];

                names.Distinct().Count().ShouldBe(names.Length, type.Name);
            }
        }
    }
}
