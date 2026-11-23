using Dock.Model.Core;
using Microsoft.Extensions.Logging.Abstractions;
using ReactiveUI;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Xunit;
using Zametek.Contract.ProjectPlan;
using static Zametek.ViewModel.ProjectPlan.Tests.SequencerScopes;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// When the commands of the main view model can run. One file command runs at a time: while an open, a save, an import,
    /// an export or a close runs - also while its dialog is open - no other of them can start. A command that calls an
    /// async method runs until that method ends, and so a second run cannot start during the first.
    /// </summary>
    public class MainViewModelCommandTests
    {
        public enum FileCommand
        {
            Open,
            OpenRecent,
            Save,
            SaveAs,
            Import,
            Export,
            Close,
        }

        [Theory]
        [InlineData(FileCommand.Open)]
        [InlineData(FileCommand.OpenRecent)]
        [InlineData(FileCommand.Save)]
        [InlineData(FileCommand.SaveAs)]
        [InlineData(FileCommand.Import)]
        [InlineData(FileCommand.Export)]
        [InlineData(FileCommand.Close)]
        public void FileCommands_Given_OneThatWaitsForItsDialog_Then_NoneOfThemCanStart(FileCommand running)
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            Drain(main, pool);

            // The project has changes, and so an open, an import and a close ask about them first. Each dialog stays open.
            harness.Core.IsProjectScenarioUpdated = true;
            Drain(main, pool);
            harness.Main.ProjectHasChanges.ShouldBeTrue(@"the setup must give the project a change");
            harness.Dialogs.ConfirmationGate = new TaskCompletionSource<bool>();
            harness.Dialogs.OpenFileGate = new TaskCompletionSource<string?>();
            harness.Dialogs.SaveFileGate = new TaskCompletionSource<string?>();

            foreach (FileCommand command in Enum.GetValues<FileCommand>())
            {
                harness.CanRun(command).ShouldBeTrue($@"{command} must be able to run before any command runs");
            }

            harness.Run(running);
            Drain(main, pool);

            foreach (FileCommand command in Enum.GetValues<FileCommand>())
            {
                harness.CanRun(command).ShouldBeFalse($@"{command} can start while {running} runs");
            }

            // The user cancels each dialog, and the command ends with no change.
            harness.Dialogs.ConfirmationGate.SetResult(false);
            harness.Dialogs.OpenFileGate.SetResult(null);
            harness.Dialogs.SaveFileGate.SetResult(null);
            Drain(main, pool);

            foreach (FileCommand command in Enum.GetValues<FileCommand>())
            {
                harness.CanRun(command).ShouldBeTrue($@"{command} cannot run after {running} ended");
            }
        }

        [Fact]
        public void OpenAboutCommand_Given_ADialogThatIsStillOpen_Then_TheCommandStillRuns()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            Drain(main, pool);
            harness.Dialogs.InfoGate = new TaskCompletionSource();

            harness.Main.OpenAboutCommand.Execute(null);
            Drain(main, pool);

            harness.Main.OpenAboutCommand.CanExecute(null).ShouldBeFalse(@"the command ended before its dialog closed");

            harness.Dialogs.InfoGate.SetResult();
            Drain(main, pool);

            harness.Main.OpenAboutCommand.CanExecute(null).ShouldBeTrue();
        }

        [Fact]
        public void TransitiveReductionCommand_Given_AnAsyncMethod_Then_TheCommandWaitsForItsTask()
        {
            // A command that gives the task of its method back as its result ends at once, and nothing observes the task. A
            // command that waits for the task has no result.
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();

            harness.Main.TransitiveReductionCommand.ShouldBeAssignableTo<ReactiveCommand<RxVoid, RxVoid>>();
            harness.Main.OpenAboutCommand.ShouldBeAssignableTo<ReactiveCommand<RxVoid, RxVoid>>();
        }

        [Theory]
        [InlineData(@"FREQ=YEARLY;BYMONTH=12;BYMONTHDAY=26", false)]
        [InlineData(@"FREQ=MINUTELY", true)]
        [InlineData(@"FREQ=YEARLY;UNTIL=2027-12-31", true)]
        public async Task OpenProjectFileAsync_Given_AHolidayRule_Then_TheDesktopRefusesOneThatRepeatsMoreOftenThanEachDayOrThatItCannotRead(string rule, bool refused)
        {
            // The desktop opens a project file with no check before the load. The load of a rule that repeats more often than
            // each day took minutes, and a rule that the dock of the holidays cannot read ended the application (from the
            // review of src, test and pkg). It now refuses the file with the message of HolidayRuleCheck, and loads nothing.
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            var opener = new ProjectFileOpen(new DateTimeCalculator(TimeProvider.System));
            using Harness harness = Harness.Make(opener);
            Drain(main, pool);

            Newtonsoft.Json.Linq.JObject plan = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, @"TestFiles", @"sample_v0_6_1.zpp")));
            var holidays = (Newtonsoft.Json.Linq.JArray)plan[@"Files"]![0]![@"Scenario"]![@"HolidaySettings"]![@"Holidays"]!;
            holidays.Add(new Newtonsoft.Json.Linq.JObject { [@"Id"] = 99, [@"Name"] = @"Added", [@"Notes"] = @"", [@"RecurrencePattern"] = rule });
            string path = Path.Combine(Path.GetTempPath(), $@"zpp-holiday-{Guid.NewGuid():N}.zpp");

            try
            {
                await File.WriteAllTextAsync(path, plan.ToString(), new UTF8Encoding(false));
                string? expected = HolidayRuleCheck.FindProblem(await OpenAsync(opener, path));

                Task open = harness.Main.OpenProjectFileAsync(path);

                while (!open.IsCompleted)
                {
                    Drain(main, pool);
                    await Task.Delay(10);
                }

                await open;
                Drain(main, pool);

                if (refused)
                {
                    expected.ShouldNotBeNull();
                    harness.Dialogs.Errors.ShouldHaveSingleItem().Message.ShouldBe(expected);
                    harness.Core.RawActivities.ShouldBeEmpty();
                }
                else
                {
                    expected.ShouldBeNull();
                    harness.Dialogs.Errors.ShouldBeEmpty();
                    harness.Core.RawActivities.ShouldNotBeEmpty();
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static async Task<Zametek.Common.ProjectPlan.ProjectModel> OpenAsync(ProjectFileOpen opener, string path)
        {
            await using FileStream stream = File.OpenRead(path);
            return await opener.OpenProjectFileAsync(stream);
        }

        // A main view model over a core and a project manager, with dialogs that a test can hold open.
        private sealed class Harness
            : IDisposable
        {
            private readonly ProjectScenarioManagerViewModel m_Project;

            private Harness(CoreViewModel core, ProjectScenarioManagerViewModel project, ScriptedDialogService dialogs, MainViewModel main)
            {
                Core = core;
                m_Project = project;
                Dialogs = dialogs;
                Main = main;
            }

            public CoreViewModel Core { get; }

            public ScriptedDialogService Dialogs { get; }

            public MainViewModel Main { get; }

            public static Harness Make(IProjectFileOpen? projectFileOpen = null)
            {
                (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
                var dialogs = new ScriptedDialogService();
                var project = new ProjectScenarioManagerViewModel(
                    core,
                    settings,
                    dialogs,
                    new DateTimeCalculator(TimeProvider.System),
                    CoreViewModelFixture.CreateUIDispatcher());

                var main = new MainViewModel(
                    DoNothingProxy.Of<IFactory>(),
                    DoNothingProxy.Of<IDockSerializer>(),
                    DoNothingProxy.Of<IDataGridLayoutManager>(),
                    project,
                    core,
                    projectFileOpen ?? DoNothingProxy.Of<IProjectFileOpen>(),
                    DoNothingProxy.Of<IProjectFileSave>(),
                    settings,
                    dialogs,
                    DoNothingProxy.Of<IServiceProvider>(),
                    NullLogger<MainViewModel>.Instance);

                return new Harness(core, project, dialogs, main);
            }

            public bool CanRun(FileCommand command) => Command(command).CanExecute(Parameter(command));

            public void Run(FileCommand command) => Command(command).Execute(Parameter(command));

            public void Dispose()
            {
                Main.Dispose();
                m_Project.Dispose();
                Core.Dispose();
            }

            private ICommand Command(FileCommand command) =>
                command switch
                {
                    FileCommand.Open => Main.OpenProjectFileCommand,
                    FileCommand.OpenRecent => Main.OpenRecentProjectFileCommand,
                    FileCommand.Save => Main.SaveProjectFileCommand,
                    FileCommand.SaveAs => Main.SaveAsProjectFileCommand,
                    FileCommand.Import => Main.ImportProjectScenarioFileCommand,
                    FileCommand.Export => Main.ExportProjectScenarioFileCommand,
                    FileCommand.Close => Main.CloseProjectCommand,
                    _ => throw new ArgumentOutOfRangeException(nameof(command)),
                };

            // The command that opens a recent file takes the file. Nothing reads it here: the question comes first.
            private static object? Parameter(FileCommand command) =>
                command == FileCommand.OpenRecent ? Path.Combine(Path.GetTempPath(), @"plans", @"recent.zpp") : null;
        }
    }
}
