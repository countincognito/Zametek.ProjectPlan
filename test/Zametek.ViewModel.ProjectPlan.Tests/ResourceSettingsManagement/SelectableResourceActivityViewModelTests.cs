using Shouldly;
using System.Collections.Generic;
using System.ComponentModel;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The display name of an activity of a day of a resource is the name, and the number when the name is blank: the rule that the
    /// lists of the Gantt chart and of the Effort Tracking tab use (ActivityLabelHelper).
    /// </summary>
    public class SelectableResourceActivityViewModelTests
    {
        public SelectableResourceActivityViewModelTests()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
        }

        [Fact]
        public void DisplayName_Given_AName_Then_TheName()
        {
            new SelectableResourceActivityViewModel(47, @"Foundations", 50).DisplayName.ShouldBe(@"Foundations");
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        public void DisplayName_Given_NoName_Then_TheNumber(string name)
        {
            new SelectableResourceActivityViewModel(47, name, 50).DisplayName.ShouldBe(@"47");
        }

        [Fact]
        public void Name_Given_ARename_Then_TheDisplayNameFollowsAndIsRaised()
        {
            var activity = new SelectableResourceActivityViewModel(47, string.Empty, 50);
            List<string?> raised = [];
            ((INotifyPropertyChanged)activity).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            activity.Name = @"Foundations";

            activity.DisplayName.ShouldBe(@"Foundations");
            raised.ShouldContain(nameof(SelectableResourceActivityViewModel.DisplayName));
        }
    }
}
