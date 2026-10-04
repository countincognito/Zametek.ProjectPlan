using Shouldly;
using System.Reflection;
using Xunit;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    /// <summary>
    /// Tests for a compiled project's metrics as zpp serve's API gives them: every metric the engine has that is a value
    /// and not the text zpp prints, each under its name in words - the one it has in the engine, but for what it
    /// abbreviates - with the value it has there.
    /// </summary>
    public class MetricsResponseTests
    {
        // The metrics of the engine that are text zpp prints, which the API does not carry.
        private static readonly string[] s_Displays =
        [
            nameof(JobMetrics.ProjectFinish),
            nameof(JobMetrics.DisplayDirectMargin),
            nameof(JobMetrics.DisplayIndirectMargin),
            nameof(JobMetrics.DisplayOtherMargin),
            nameof(JobMetrics.DisplayTotalMargin),
        ];

        // The metrics whose names the API gives in words.
        private static readonly Dictionary<string, string> s_Renamed = new()
        {
            [nameof(JobMetrics.ActivityRiskWithStdDevCorrection)] = nameof(MetricsResponse.ActivityRiskWithStandardDeviationCorrection),
        };

        private static string ApiName(string engineName)
        {
            return s_Renamed.TryGetValue(engineName, out string? renamed) ? renamed : engineName;
        }

        [Fact]
        public void From_Given_EveryMetric_Then_EachInItsMemberWithItsValue()
        {
            // Each metric a value of its own, so that one carried to another's member shows.
            var metrics = new JobMetrics();
            int next = 1;

            foreach (PropertyInfo property in typeof(JobMetrics).GetProperties().Where(x => !s_Displays.Contains(x.Name)))
            {
                Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                object value = type == typeof(int)
                    ? next
                    : type == typeof(DateOnly)
                        ? new DateOnly(2024, 1, next)
                        : (object)(next + 0.5);
                property.SetValue(metrics, value);
                next++;
            }

            MetricsResponse response = MetricsResponse.From(metrics);

            foreach (PropertyInfo property in typeof(JobMetrics).GetProperties().Where(x => !s_Displays.Contains(x.Name)))
            {
                PropertyInfo member = typeof(MetricsResponse).GetProperty(ApiName(property.Name)).ShouldNotBeNull(property.Name);

                member.GetValue(response).ShouldBe(property.GetValue(metrics), property.Name);
            }
        }

        [Fact]
        public void From_Given_EveryMemberOfTheResponse_Then_AMetricOfTheEngineAndNothingElse()
        {
            string[] engine = [.. typeof(JobMetrics).GetProperties().Where(x => !s_Displays.Contains(x.Name)).Select(x => ApiName(x.Name))];
            string[] api = [.. typeof(MetricsResponse).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(x => x.Name)];

            api.ShouldBe(engine, ignoreOrder: true);
        }

        [Fact]
        public void From_Given_NoValues_Then_NoneAreInTheResponse()
        {
            MetricsResponse.From(new JobMetrics()).ShouldBe(new MetricsResponse());
        }

        [Fact]
        public void From_Given_NoMetrics_Then_Throws()
        {
            Should.Throw<ArgumentNullException>(() => MetricsResponse.From(null!));
        }

        [Fact]
        public void MetricsResponse_Given_EveryMember_Then_AValueThatIsNotTextAndMayBeNone()
        {
            typeof(MetricsResponse).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ShouldAllBe(x => Nullable.GetUnderlyingType(x.PropertyType) != null);
        }
    }
}
