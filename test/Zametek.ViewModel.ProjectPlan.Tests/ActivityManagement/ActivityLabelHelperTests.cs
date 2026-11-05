using Shouldly;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for ActivityLabelHelper. The Show Connections list of the Gantt chart and the lists of the Effort Tracking tab show an
    /// activity with these two rules, and so they show it in the same way. An activity with no name shows its number in the place
    /// of the name.
    /// </summary>
    public class ActivityLabelHelperTests
    {
        [Fact]
        public void BuildDisplayName_Given_AName_Then_TheName()
        {
            ActivityLabelHelper.BuildDisplayName(12, @"Backend").ShouldBe(@"Backend");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\t \r\n")]
        public void BuildDisplayName_Given_NoName_Then_TheNumber(string? name)
        {
            ActivityLabelHelper.BuildDisplayName(12, name).ShouldBe(@"12");
        }

        [Fact]
        public void BuildLabel_Given_AName_Then_TheNumberADashAndTheName()
        {
            ActivityLabelHelper.BuildLabel(12, @"Backend").ShouldBe(@"12 - Backend");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\t \r\n")]
        public void BuildLabel_Given_NoName_Then_TheNumberTwice(string? name)
        {
            ActivityLabelHelper.BuildLabel(12, name).ShouldBe(@"12 - 12");
        }

        [Fact]
        public void BuildLabel_Given_ANameWithSpaces_Then_TheNameAsItIs()
        {
            ActivityLabelHelper.BuildLabel(12, @" Backend ").ShouldBe(@"12 -  Backend ");
        }
    }
}
