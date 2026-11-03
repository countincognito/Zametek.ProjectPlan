using Shouldly;
using System.Collections.Generic;
using System.ComponentModel;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// An item of the Show Connections list of the Gantt chart shows its label. The label follows the name of the activity, and so a
    /// rename must raise it as well, or the list keeps the old name.
    /// </summary>
    public class SelectableActivityViewModelTests
    {
        public SelectableActivityViewModelTests()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
        }

        [Fact]
        public void Label_Given_AName_Then_TheNumberADashAndTheName()
        {
            var activity = new SelectableActivityViewModel(47, @"Foundations");

            activity.Label.ShouldBe(@"47 - Foundations");
            activity.DisplayName.ShouldBe(@"Foundations");
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        public void Label_Given_NoName_Then_TheNumberTwice(string name)
        {
            var activity = new SelectableActivityViewModel(47, name);

            activity.Label.ShouldBe(@"47 - 47");
            activity.DisplayName.ShouldBe(@"47");
        }

        [Fact]
        public void Name_Given_ARename_Then_TheLabelAndTheDisplayNameFollowAndAreRaised()
        {
            var activity = new SelectableActivityViewModel(47, string.Empty);
            List<string?> raised = [];
            ((INotifyPropertyChanged)activity).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            activity.Name = @"Foundations";

            activity.Label.ShouldBe(@"47 - Foundations");
            activity.DisplayName.ShouldBe(@"Foundations");
            raised.ShouldContain(nameof(SelectableActivityViewModel.Label));
            raised.ShouldContain(nameof(SelectableActivityViewModel.DisplayName));
        }

        [Fact]
        public void Name_Given_ARenameToNoName_Then_TheLabelHasTheNumberTwice()
        {
            var activity = new SelectableActivityViewModel(47, @"Foundations");

            activity.Name = string.Empty;

            activity.Label.ShouldBe(@"47 - 47");
            activity.DisplayName.ShouldBe(@"47");
        }
    }
}
