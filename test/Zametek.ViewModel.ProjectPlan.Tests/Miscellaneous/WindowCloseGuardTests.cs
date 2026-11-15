using Shouldly;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The main window asks before it closes a project that has changes. A handler of the Closing event cannot wait for the
    /// answer, so the guard cancels each request to close until the answer is yes, and then it closes the window itself.
    /// The handlers that it replaces took the cancelling handler away while the question was open. A second request in that
    /// time (the end of the Windows session, for example) then closed the window without the answer. They also did not wait
    /// for a save in progress, and an exception from the dialog ended the process.
    /// </summary>
    public class WindowCloseGuardTests
    {
        // What the guard works with: a view model that can be in the middle of a write, a question that the test answers,
        // and a window that asks the guard about each request to close, as the Closing event does.
        private sealed class Window
            : INotifyPropertyChanged
        {
            private bool m_IsWriting;

            public Window()
            {
                Guard = new WindowCloseGuard(
                    () => IsWriting,
                    this,
                    () => HasChanges,
                    () =>
                    {
                        Questions++;
                        Answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        return FailTheQuestion is null ? Answer.Task : throw FailTheQuestion;
                    },
                    () =>
                    {
                        if (FailTheClose is not null)
                        {
                            throw FailTheClose;
                        }

                        // The window raises Closing again, and it closes when nothing cancels the event.
                        if (!Guard!.ShouldCancel())
                        {
                            IsClosed = true;
                        }
                    },
                    Failures.Add);
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            public WindowCloseGuard Guard { get; }

            public bool HasChanges { get; set; }

            public bool IsWriting
            {
                get => m_IsWriting;
                set
                {
                    m_IsWriting = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsWriting)));
                }
            }

            public int Questions { get; private set; }

            public TaskCompletionSource<bool>? Answer { get; private set; }

            public Exception? FailTheQuestion { get; set; }

            public Exception? FailTheClose { get; set; }

            public List<Exception> Failures { get; } = [];

            public bool IsClosed { get; private set; }

            // A request to close, as a person or the system makes it. True when the guard cancelled it.
            public bool RequestClose() => Guard.ShouldCancel();

            public async Task AnswerAsync(bool wishToClose)
            {
                Answer!.SetResult(wishToClose);
                await Guard.LastDecision;
            }
        }

        [Fact]
        public async Task ShouldCancel_Given_NoChanges_Then_TheWindowClosesWithNoQuestion()
        {
            var window = new Window();

            window.RequestClose().ShouldBeTrue();
            await window.Guard.LastDecision;

            window.Questions.ShouldBe(0);
            window.IsClosed.ShouldBeTrue();
            window.Failures.ShouldBeEmpty();
        }

        [Fact]
        public async Task ShouldCancel_Given_ChangesAndTheAnswerYes_Then_TheWindowClosesAfterTheAnswer()
        {
            var window = new Window { HasChanges = true };

            window.RequestClose().ShouldBeTrue();
            window.Questions.ShouldBe(1);
            window.IsClosed.ShouldBeFalse();

            await window.AnswerAsync(wishToClose: true);

            window.IsClosed.ShouldBeTrue();
            window.Questions.ShouldBe(1);
        }

        [Fact]
        public async Task ShouldCancel_Given_ChangesAndTheAnswerNo_Then_TheWindowStaysOpenAndTheNextRequestAsksAgain()
        {
            var window = new Window { HasChanges = true };

            window.RequestClose().ShouldBeTrue();
            await window.AnswerAsync(wishToClose: false);

            window.IsClosed.ShouldBeFalse();

            window.RequestClose().ShouldBeTrue();
            window.Questions.ShouldBe(2);
            await window.AnswerAsync(wishToClose: true);

            window.IsClosed.ShouldBeTrue();
        }

        [Fact]
        public async Task ShouldCancel_Given_ASecondRequestWhileTheQuestionIsOpen_Then_ItIsCancelledAndNothingAsksAgain()
        {
            var window = new Window { HasChanges = true };
            window.RequestClose().ShouldBeTrue();

            // The fault of the old handlers: nothing cancelled this request, and the window closed without the answer.
            window.RequestClose().ShouldBeTrue();
            window.RequestClose().ShouldBeTrue();

            window.IsClosed.ShouldBeFalse();
            window.Questions.ShouldBe(1);

            await window.AnswerAsync(wishToClose: false);

            window.IsClosed.ShouldBeFalse();
            window.Questions.ShouldBe(1);
        }

        [Fact]
        public async Task ShouldCancel_Given_AQuestionThatFails_Then_TheWindowStaysOpenAndTheFailureIsReported()
        {
            var failure = new InvalidOperationException(@"The dialog has no window.");
            var window = new Window { HasChanges = true, FailTheQuestion = failure };

            window.RequestClose().ShouldBeTrue();
            await window.Guard.LastDecision;

            window.IsClosed.ShouldBeFalse();
            window.Failures.ShouldBe(new Exception[] { failure });

            // The next request asks again.
            window.FailTheQuestion = null;
            window.RequestClose().ShouldBeTrue();
            window.Questions.ShouldBe(2);
            await window.AnswerAsync(wishToClose: true);

            window.IsClosed.ShouldBeTrue();
        }

        [Fact]
        public async Task ShouldCancel_Given_ACloseThatFails_Then_TheNextRequestAsksAgain()
        {
            var failure = new InvalidOperationException(@"The layout did not close.");
            var window = new Window { HasChanges = true, FailTheClose = failure };

            window.RequestClose().ShouldBeTrue();
            await window.AnswerAsync(wishToClose: true);

            window.IsClosed.ShouldBeFalse();
            window.Failures.ShouldBe(new Exception[] { failure });

            // The yes was for a close that did not happen: it does not let a later request through.
            window.FailTheClose = null;
            window.RequestClose().ShouldBeTrue();
            window.Questions.ShouldBe(2);
            await window.AnswerAsync(wishToClose: false);

            window.IsClosed.ShouldBeFalse();
        }

        [Fact]
        public async Task ShouldCancel_Given_AWriteInProgress_Then_NothingHappensUntilTheWriteEnds()
        {
            var window = new Window { HasChanges = true, IsWriting = true };

            window.RequestClose().ShouldBeTrue();
            window.RequestClose().ShouldBeTrue();

            window.Questions.ShouldBe(0);
            window.IsClosed.ShouldBeFalse();

            // The save ends, and it leaves no changes: the window closes with no question.
            window.HasChanges = false;
            window.IsWriting = false;
            await window.Guard.LastDecision;

            window.Questions.ShouldBe(0);
            window.IsClosed.ShouldBeTrue();
        }

        [Fact]
        public async Task ShouldCancel_Given_AWriteThatLeavesChanges_Then_TheQuestionComesAfterTheWrite()
        {
            var window = new Window { HasChanges = true, IsWriting = true };
            window.RequestClose().ShouldBeTrue();
            window.Questions.ShouldBe(0);

            // An export ends, for example: the project still has its changes.
            window.IsWriting = false;
            await WaitForAsync(() => window.Questions == 1);
            window.IsClosed.ShouldBeFalse();

            await window.AnswerAsync(wishToClose: true);

            window.IsClosed.ShouldBeTrue();
        }

        [Fact]
        public async Task ShouldCancel_Given_AClosedWindow_Then_ALaterRequestIsNotCancelled()
        {
            var window = new Window();
            window.RequestClose().ShouldBeTrue();
            await window.Guard.LastDecision;

            window.RequestClose().ShouldBeFalse();
        }

        private static async Task WaitForAsync(Func<bool> condition)
        {
            DateTime limit = DateTime.UtcNow.AddSeconds(10);

            while (!condition())
            {
                (DateTime.UtcNow < limit).ShouldBeTrue();
                await Task.Delay(10);
            }
        }
    }
}
