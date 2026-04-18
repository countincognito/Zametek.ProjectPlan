using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    public class RecurrencePatternHelperTests
    {
        public static TheoryData<string, RecurrenceRuleModel> RoundTripData
        {
            get
            {
                var data = new TheoryData<string, RecurrenceRuleModel>();
                for (var i = 0; i < RecurrenceRuleFixture.InputPatterns.Count; i++)
                {
                    data.Add(RecurrenceRuleFixture.InputPatterns[i], RecurrenceRuleFixture.OutputModels[i]);
                }
                return data;
            }
        }

        private static void CompareModels(RecurrenceRuleModel actual, RecurrenceRuleModel expected)
        {
            actual.Frequency.ShouldBe(expected.Frequency);
            actual.Interval.ShouldBe(expected.Interval);
            actual.Count.ShouldBe(expected.Count);
            actual.Until.ShouldBe(expected.Until);
            actual.ByDay.ShouldBe(expected.ByDay, ignoreOrder: true);
            actual.ByMonthDay.ShouldBe(expected.ByMonthDay, ignoreOrder: true);
            actual.ByMonth.ShouldBe(expected.ByMonth, ignoreOrder: true);
            actual.BySetPos.ShouldBe(expected.BySetPos, ignoreOrder: true);
            actual.WeekStart.ShouldBe(expected.WeekStart);
        }

        [Theory]
        [MemberData(nameof(RoundTripData))]
        public void RecurrencePatternHelper_Given_InputPattern_Then_ReturnsExpectedModel(string pattern, RecurrenceRuleModel expected)
        {
            var actual = RecurrencePatternHelper.ToRule(pattern);
            CompareModels(actual, expected);
        }

        [Theory]
        [MemberData(nameof(RoundTripData))]
        public void RecurrencePatternHelper_Given_InputModel_Then_ReturnsExpectedPattern(string expectedPattern, RecurrenceRuleModel model)
        {
            string actual = RecurrencePatternHelper.ToPattern(model);
            actual.ShouldBe(expectedPattern);
        }

        [Theory]
        [MemberData(nameof(RoundTripData))]
        public void RecurrencePatternHelper_Given_InputPattern_Then_RoundTripPreservesModel(string pattern, RecurrenceRuleModel expected)
        {
            RecurrenceRuleModel parsedRule = RecurrencePatternHelper.ToRule(pattern);
            string intermediatePattern = RecurrencePatternHelper.ToPattern(parsedRule);
            RecurrenceRuleModel secondParsedRule = RecurrencePatternHelper.ToRule(intermediatePattern);

            CompareModels(parsedRule, expected);
            CompareModels(secondParsedRule, parsedRule);
        }
    }
}
