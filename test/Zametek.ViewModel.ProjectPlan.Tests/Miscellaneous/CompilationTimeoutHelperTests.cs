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

        #region Run

        [Theory]
        [InlineData(0)]
        [InlineData(60_000)]
        public void Run_Given_ACompilationThatFinishes_Then_GivesItsResult(int timeoutMilliseconds)
        {
            int result = CompilationTimeoutHelper.Run(timeoutMilliseconds, _ => 42);

            result.ShouldBe(42);
        }

        [Fact]
        public void Run_Given_NoBudget_Then_TheCompilationGetsATokenThatCannotBeCancelled()
        {
            CancellationToken given = CompilationTimeoutHelper.Run(0, token => token);

            given.CanBeCanceled.ShouldBeFalse();
        }

        [Fact]
        public void Run_Given_ABudget_Then_TheCompilationGetsATokenThatIsNotCancelledYet()
        {
            (bool canBeCancelled, bool isCancelled) = CompilationTimeoutHelper.Run(60_000, token => (token.CanBeCanceled, token.IsCancellationRequested));

            canBeCancelled.ShouldBeTrue();
            isCancelled.ShouldBeFalse();
        }

        [Fact]
        public void Run_Given_ACompilationThatOutlastsTheBudget_Then_ThrowsTheTimeoutExceptionOfThatBudget()
        {
            // The compilation waits for the watchdog, as a compilation that has stopped making progress does, and ends as a
            // cancelled compilation does. The wait is a ceiling: the watchdog ends it after the budget.
            var cancellation = new OperationCanceledException();

            GraphCompilationTimeoutException ex = Should.Throw<GraphCompilationTimeoutException>(() => CompilationTimeoutHelper.Run(
                50,
                token =>
                {
                    token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                    if (token.IsCancellationRequested)
                    {
                        throw cancellation;
                    }

                    return 0;
                }));

            ex.Timeout.ShouldBe(TimeSpan.FromMilliseconds(50));
            ex.InnerException.ShouldBeSameAs(cancellation);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(60_000)]
        public void Run_Given_ACancellationThatIsNotTheWatchdog_Then_ItIsNotTurnedIntoATimeout(int timeoutMilliseconds)
        {
            var cancellation = new OperationCanceledException();

            OperationCanceledException ex = Should.Throw<OperationCanceledException>(
                () => CompilationTimeoutHelper.Run<int>(timeoutMilliseconds, _ => throw cancellation));

            ex.ShouldBeSameAs(cancellation);
        }

        [Fact]
        public void Run_Given_AnExceptionThatIsNotACancellation_Then_ItPropagates()
        {
            var failure = new InvalidOperationException();

            InvalidOperationException ex = Should.Throw<InvalidOperationException>(
                () => CompilationTimeoutHelper.Run<int>(60_000, _ => throw failure));

            ex.ShouldBeSameAs(failure);
        }

        [Fact]
        public void Run_Given_ACompilationThatEnds_Then_TheWatchdogIsDisposed()
        {
            // A token whose source is disposed gives no wait handle.
            CancellationToken ended = CompilationTimeoutHelper.Run(60_000, token => token);
            Should.Throw<ObjectDisposedException>(() => { _ = ended.WaitHandle; });

            CancellationToken failed = default;
            Should.Throw<InvalidOperationException>(() => CompilationTimeoutHelper.Run<int>(
                60_000,
                token =>
                {
                    failed = token;
                    throw new InvalidOperationException();
                }));
            Should.Throw<ObjectDisposedException>(() => { _ = failed.WaitHandle; });
        }

        [Fact]
        public void Run_Given_NoCompilation_Then_ItThrows()
        {
            Should.Throw<ArgumentNullException>(() => CompilationTimeoutHelper.Run<int>(0, null!))
                .ParamName.ShouldBe(@"compile");
        }

        #endregion
    }
}
