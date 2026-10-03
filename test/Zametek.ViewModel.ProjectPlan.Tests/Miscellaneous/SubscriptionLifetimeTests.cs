using Shouldly;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The life of a view model's reactive pipelines: made, then started once, then killed, and never started after it
    /// has been killed. Every view model that starts its pipelines asks one of these whether to.
    /// </summary>
    public class SubscriptionLifetimeTests
    {
        [Fact]
        public void TryStart_Given_ALifetimeNeitherStartedNorKilled_Then_StartsItOnceOnly()
        {
            var lifetime = new SubscriptionLifetime();

            lifetime.IsStarted.ShouldBeFalse();

            lifetime.TryStart().ShouldBeTrue();
            lifetime.IsStarted.ShouldBeTrue();

            lifetime.TryStart().ShouldBeFalse();
            lifetime.IsStarted.ShouldBeTrue();
        }

        [Fact]
        public void TryStart_Given_AKilledLifetime_Then_DoesNotStartIt()
        {
            var lifetime = new SubscriptionLifetime();
            lifetime.Kill();

            lifetime.TryStart().ShouldBeFalse();
            lifetime.IsStarted.ShouldBeFalse();
        }

        [Fact]
        public void Kill_Given_AStartedLifetime_Then_EndsItForGood()
        {
            var lifetime = new SubscriptionLifetime();
            lifetime.TryStart().ShouldBeTrue();

            lifetime.Kill();

            lifetime.IsStarted.ShouldBeFalse();
            lifetime.TryStart().ShouldBeFalse();
            lifetime.IsStarted.ShouldBeFalse();
        }

        [Fact]
        public void Kill_Given_ALifetimeAlreadyKilled_Then_ChangesNothing()
        {
            var lifetime = new SubscriptionLifetime();
            lifetime.Kill();

            lifetime.Kill();

            lifetime.IsStarted.ShouldBeFalse();
            lifetime.TryStart().ShouldBeFalse();
        }
    }
}
