using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// What the application does with an exception that nothing handled: it writes the exception to the log, keeps a
    /// recovery copy of a project with changes, tells the user, and ends. It does this one time, whatever comes while it
    /// does it, and it always ends.
    /// </summary>
    public class UnhandledErrorHandlerTests
    {
        private static readonly RecoveryCopyModel s_Copy = new() { Filename = @"C:\recovery\copy.zpp", Reason = RecoveryCopyReasons.Error };

        // The application around the handler: what it was asked to do, in the order in which it was asked.
        private sealed class Host
        {
            public List<string> Calls { get; } = [];

            public List<string> Messages { get; } = [];

            public bool HasChanges { get; set; } = true;

            public Func<Task<RecoveryCopyModel?>> Copy { get; set; } = () => Task.FromResult<RecoveryCopyModel?>(s_Copy);

            public Func<Task> Dialog { get; set; } = () => Task.CompletedTask;

            public UnhandledErrorHandler Make(TimeSpan? copyTimeLimit = null) =>
                new(
                    hasChanges: () => HasChanges,
                    writeCopyAsync: () =>
                    {
                        Calls.Add(@"copy");
                        return Copy();
                    },
                    showErrorAsync: message =>
                    {
                        Calls.Add(@"dialog");
                        Messages.Add(message);
                        return Dialog();
                    },
                    end: exitCode => Calls.Add($@"end {exitCode}"),
                    log: (exception, text) => Calls.Add($@"log {exception.Message}"),
                    copyTimeLimit ?? TimeSpan.FromSeconds(30));
        }

        [Fact]
        public async Task Handle_Given_AnExceptionAndAProjectWithChanges_Then_TheLogTheCopyTheMessageAndTheEndInThatOrder()
        {
            var host = new Host();
            UnhandledErrorHandler handler = host.Make();

            handler.Handle(new InvalidOperationException(@"The grid lost its row.")).ShouldBeTrue();
            await handler.Completion;

            host.Calls.ShouldBe([@"log The grid lost its row.", @"copy", @"dialog", @"end 1"]);
            host.Messages.ShouldHaveSingleItem().ShouldBe(
                @"The application met an error, and it must close. It kept a copy of the open project, and its next start offers that copy. The error: The grid lost its row.");
            handler.IsEnding.ShouldBeTrue();
        }

        [Fact]
        public async Task Handle_Given_AProjectWithNoChanges_Then_NoCopyAndTheMessageSaysThatNothingIsLost()
        {
            var host = new Host { HasChanges = false };
            UnhandledErrorHandler handler = host.Make();

            handler.Handle(new InvalidOperationException(@"The grid lost its row."));
            await handler.Completion;

            host.Calls.ShouldBe([@"log The grid lost its row.", @"dialog", @"end 1"]);
            host.Messages.ShouldHaveSingleItem().ShouldBe(
                @"The application met an error, and it must close. The open project had no changes that were not saved. The error: The grid lost its row.");
        }

        [Fact]
        public async Task Handle_Given_ACopyThatFails_Then_TheMessageSaysSoAndTheApplicationEnds()
        {
            var host = new Host { Copy = () => Task.FromResult<RecoveryCopyModel?>(null) };
            UnhandledErrorHandler handler = host.Make();

            handler.Handle(new InvalidOperationException(@"The grid lost its row."));
            await handler.Completion;

            host.Calls.ShouldBe([@"log The grid lost its row.", @"copy", @"dialog", @"end 1"]);
            host.Messages.ShouldHaveSingleItem().ShouldBe(
                @"The application met an error, and it must close. It could not keep a copy of the open project. The error: The grid lost its row.");
        }

        [Fact]
        public async Task Handle_Given_ACopyThatThrows_Then_TheMessageSaysSoAndTheApplicationEnds()
        {
            var host = new Host { Copy = () => throw new InvalidOperationException(@"The project cannot be built.") };
            UnhandledErrorHandler handler = host.Make();

            handler.Handle(new InvalidOperationException(@"The grid lost its row."));
            await handler.Completion;

            host.Calls.ShouldBe([@"log The grid lost its row.", @"copy", @"log The project cannot be built.", @"dialog", @"end 1"]);
            host.Messages.ShouldHaveSingleItem().ShouldContain(@"It could not keep a copy of the open project.");
        }

        // The state that broke the application can also hold the lock that the copy needs. The application does not wait
        // for the copy for ever.
        [Fact]
        public async Task Handle_Given_ACopyThatDoesNotEnd_Then_TheApplicationGoesOnAfterTheTimeLimit()
        {
            var never = new TaskCompletionSource<RecoveryCopyModel?>();
            var host = new Host { Copy = () => never.Task };
            UnhandledErrorHandler handler = host.Make(copyTimeLimit: TimeSpan.FromMilliseconds(50));

            handler.Handle(new InvalidOperationException(@"The grid lost its row."));
            await handler.Completion.WaitAsync(TimeSpan.FromSeconds(30));

            host.Calls.ShouldBe([@"log The grid lost its row.", @"copy", @"dialog", @"end 1"]);
            host.Messages.ShouldHaveSingleItem().ShouldContain(@"It could not keep a copy of the open project.");
        }

        [Fact]
        public async Task Handle_Given_ADialogThatFails_Then_TheApplicationStillEnds()
        {
            var host = new Host { Dialog = () => throw new InvalidOperationException(@"No window.") };
            UnhandledErrorHandler handler = host.Make();

            handler.Handle(new InvalidOperationException(@"The grid lost its row."));
            await handler.Completion;

            host.Calls.ShouldBe([@"log The grid lost its row.", @"copy", @"dialog", @"log No window.", @"end 1"]);
        }

        // A second exception while the message is on the screen: it goes to the log, and nothing starts a second time.
        [Fact]
        public async Task Handle_Given_ASecondExceptionWhileTheMessageShows_Then_ItIsLoggedAndNothingElse()
        {
            var dialog = new TaskCompletionSource();
            var host = new Host { Dialog = () => dialog.Task };
            UnhandledErrorHandler handler = host.Make();
            handler.Handle(new InvalidOperationException(@"first"));

            handler.Handle(new InvalidOperationException(@"second")).ShouldBeTrue();

            host.Calls.ShouldBe([@"log first", @"copy", @"dialog", @"log second"]);

            dialog.SetResult();
            await handler.Completion;

            host.Calls.ShouldBe([@"log first", @"copy", @"dialog", @"log second", @"end 1"]);
        }

        // Exceptions that keep coming (a view that fails at each frame, for one) can keep the user from the message.
        // After some of them, the application ends with no more wait, and it ends one time.
        [Fact]
        public async Task Handle_Given_ExceptionsThatKeepComing_Then_TheApplicationEndsWithoutTheAnswerOfTheUser()
        {
            var dialog = new TaskCompletionSource();
            var host = new Host { Dialog = () => dialog.Task };
            UnhandledErrorHandler handler = host.Make();
            handler.Handle(new InvalidOperationException(@"first"));

            for (int i = 2; i <= UnhandledErrorHandler.MostExceptions; i++)
            {
                handler.Handle(new InvalidOperationException($@"number {i}"));
            }

            host.Calls.ShouldContain(@"end 1");
            host.Calls.FindAll(x => x == @"copy").Count.ShouldBe(1);
            host.Calls.FindAll(x => x == @"dialog").Count.ShouldBe(1);

            // The dialog closes with the application. Nothing ends the application a second time.
            dialog.SetResult();
            await handler.Completion;
            handler.Handle(new InvalidOperationException(@"late"));

            host.Calls.FindAll(x => x == @"end 1").Count.ShouldBe(1);
        }

        [Fact]
        public void IsEnding_Given_NoException_Then_False()
        {
            new Host().Make().IsEnding.ShouldBeFalse();
        }
    }
}
