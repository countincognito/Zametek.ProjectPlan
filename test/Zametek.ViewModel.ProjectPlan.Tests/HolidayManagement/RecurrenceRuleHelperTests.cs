using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    public class RecurrenceRuleHelperTests
    {
        public static TheoryData<RecurrenceRuleModel, string> PhraseData
        {
            get
            {
                var data = new TheoryData<RecurrenceRuleModel, string>();
                for (var i = 0; i < RecurrenceRuleFixture.OutputModels.Count; i++)
                {
                    data.Add(RecurrenceRuleFixture.OutputModels[i], RecurrenceRuleFixture.ExpectedEnglishPhrases[i]);
                }
                return data;
            }
        }

        [Theory]
        [MemberData(nameof(PhraseData))]
        public void RecurrenceRuleHelper_Given_InputModel_Then_ReturnsExpectedPhrase(RecurrenceRuleModel model, string expected)
        {
            string actual = RecurrenceRuleHelper.ToPhrase(model);
            Assert.Equal(expected, actual);
        }
    }
}
