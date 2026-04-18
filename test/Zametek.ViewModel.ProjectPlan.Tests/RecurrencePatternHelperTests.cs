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
                for (var i = 0; i < RecurrenceRuleTestData.InputPatterns.Count; i++)
                {
                    data.Add(RecurrenceRuleTestData.InputPatterns[i], RecurrenceRuleTestData.OutputModels[i]);
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
        public void Parse_ConvertsPatternToModel(string pattern, RecurrenceRuleModel expected)
        {
            var actual = RecurrencePatternHelper.Parse(pattern);
            CompareModels(actual, expected);
        }

        [Theory]
        [MemberData(nameof(RoundTripData))]
        public void ToPattern_ConvertsModelToPattern(string expectedPattern, RecurrenceRuleModel model)
        {
            string actual = RecurrencePatternHelper.ToPattern(model);
            actual.ShouldBe(expectedPattern);
        }

        [Theory]
        [MemberData(nameof(RoundTripData))]
        public void RoundTrip_PreservesModel(string pattern, RecurrenceRuleModel expected)
        {
            RecurrenceRuleModel parsed = RecurrencePatternHelper.Parse(pattern);
            string pattern2 = RecurrencePatternHelper.ToPattern(parsed);
            RecurrenceRuleModel parsed2 = RecurrencePatternHelper.Parse(pattern2);

            CompareModels(parsed, expected);
            CompareModels(parsed2, parsed);
        }
    }
}
