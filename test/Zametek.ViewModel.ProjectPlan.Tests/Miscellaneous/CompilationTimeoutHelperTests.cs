using Shouldly;
using System;
using System.Threading;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for CompilationTimeoutHelper. These pin the budget contract that the
    /// application settings and the CLI's --compile-timeout option both expose:
    /// the value is in milliseconds, and zero or less means no limit at all rather
    /// than an instant cancellation.
    /// </summary>
    public class CompilationTimeoutHelperTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void CreateTimeoutSource_Given_ZeroOrLess_Then_NoLimit(int timeoutMilliseconds)
        {
            using CancellationTokenSource? source = CompilationTimeoutHelper.CreateTimeoutSource(timeoutMilliseconds);

            source.ShouldBeNull();
            CompilationTimeoutHelper.TokenOrNone(source).ShouldBe(CancellationToken.None);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(AppSettingsModel.DefaultCompilationTimeoutMilliseconds)]
        [InlineData(int.MaxValue)]
        public void CreateTimeoutSource_Given_PositiveValue_Then_Cancellable(int timeoutMilliseconds)
        {
            // int.MaxValue is included because the largest value the setting can hold
            // must still produce a source rather than throw out of the constructor.
            using CancellationTokenSource? source = CompilationTimeoutHelper.CreateTimeoutSource(timeoutMilliseconds);

            source.ShouldNotBeNull();
            CompilationTimeoutHelper.TokenOrNone(source).CanBeCanceled.ShouldBeTrue();
        }

        [Fact]
        public void CreateTimeoutSource_Given_ElapsedBudget_Then_TokenCancels()
        {
            using CancellationTokenSource? source = CompilationTimeoutHelper.CreateTimeoutSource(1);

            source.ShouldNotBeNull();

            // The timer only guarantees to fire at or after the delay, so the wait is
            // generous; what is being pinned is that the source cancels itself at all.
            source.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            source.IsCancellationRequested.ShouldBeTrue();
        }

        [Fact]
        public void TimedOut_Then_CarriesBudgetAndInnerException()
        {
            var inner = new OperationCanceledException();

            GraphCompilationTimeoutException ex = CompilationTimeoutHelper.TimedOut(2_500, inner);

            ex.Timeout.ShouldBe(TimeSpan.FromMilliseconds(2_500));
            ex.InnerException.ShouldBeSameAs(inner);
            ex.Message.ShouldContain(@"2500");
        }
    }
}
