using Shouldly;
using System.Collections.Generic;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    // The text of the dependencies of an activity, as the user types it in the grid. The pattern of the rule accepts a
    // number of any length, and the rule then called int.Parse, which threw OverflowException for a number of more than
    // 10 digits, in the setter of DependenciesString (from the review of src, test and pkg).
    public class DependenciesStringValidationRuleTests
    {
        [Theory]
        [InlineData(@"3, 1,2 , 3", 5, new[] { 1, 2, 3 })]
        [InlineData(@"", 5, new int[0])]
        [InlineData(@"2147483647", 5, new[] { 2147483647 })]
        public void Validate_Given_IdsThatAnIntHolds_Then_TheIdsInOrderOnce(string text, int id, int[] expected)
        {
            (IEnumerable<int>? output, string? errorMessage) = DependenciesStringValidationRule.Validate(text, id);

            errorMessage.ShouldBeNull();
            output.ShouldNotBeNull().ShouldBe(expected);
        }

        [Theory]
        [InlineData(@"2147483648")]
        [InlineData(@"1,99999999999")]
        [InlineData(@"123456789012345678901234567890")]
        public void Validate_Given_AnIdThatAnIntDoesNotHold_Then_AnInvalidFormatAndNoThrow(string text)
        {
            (IEnumerable<int>? output, string? errorMessage) = DependenciesStringValidationRule.Validate(text, 5);

            output.ShouldBeNull();
            errorMessage.ShouldBe(Resource.ProjectPlan.Labels.Label_InvalidFormat);
        }

        [Theory]
        [InlineData(@"1;2")]
        [InlineData(@"-1")]
        [InlineData(@"1,,2")]
        public void Validate_Given_ATextThatThePatternRefuses_Then_AnInvalidFormat(string text)
        {
            DependenciesStringValidationRule.Validate(text, 5).errorMessage.ShouldBe(Resource.ProjectPlan.Labels.Label_InvalidFormat);
        }

        [Fact]
        public void Validate_Given_TheIdOfTheActivityItself_Then_ASelfDependency()
        {
            DependenciesStringValidationRule.Validate(@"1,5", 5).errorMessage.ShouldBe(Resource.ProjectPlan.Labels.Label_SelfDependency);
        }
    }
}
