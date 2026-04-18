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
                for (var i = 0; i < RecurrenceRuleTestData.OutputModels.Count; i++)
                {
                    data.Add(RecurrenceRuleTestData.OutputModels[i], RecurrenceRuleTestData.ExpectedEnglishPhrases[i]);
                }
                return data;
            }
        }

        [Theory]
        [MemberData(nameof(PhraseData))]
        public void ToPhrase_ConvertsModelToText(RecurrenceRuleModel model, string expected)
        {
            var actual = RecurrenceRuleHelper.ToPhrase(model);
            Assert.Equal(expected, actual);
        }
    }
}
