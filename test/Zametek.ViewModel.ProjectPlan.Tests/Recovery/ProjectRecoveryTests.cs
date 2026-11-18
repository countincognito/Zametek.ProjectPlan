using Dock.Model.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The recovery copy of the open project. The application writes one at an interval while the plan has changes that
    /// the last copy does not have, and it removes the copy when the plan has no changes: after a save, or after a close
    /// that the user agreed to. A copy changes nothing of the project: the marks of a change stay.
    /// </summary>
    public class ProjectRecoveryTests
    {
        private static readonly DateTimeOffset s_Now = new(2026, 11, 18, 9, 30, 0, TimeSpan.Zero);

        #region The copy at an interval

        [Fact]
        public async Task WriteCopyIfChangedAsync_Given_APlanWithNoChanges_Then_NoCopy()
        {
            using Harness harness = Harness.Make();

            (await harness.Recovery.WriteCopyIfChangedAsync()).ShouldBeFalse();

            harness.Store.Copy.ShouldBeNull();
            harness.Saver.Writes.ShouldBe(0);
        }

        [Fact]
        public async Task WriteCopyIfChangedAsync_Given_APlanWithChanges_Then_ACopyThatSaysWhatItIsOf()
        {
            using Harness harness = Harness.Make();
            harness.Core.IsProjectScenarioUpdated = true;

            (await harness.Recovery.WriteCopyIfChangedAsync()).ShouldBeTrue();

            RecoveryCopyModel copy = harness.Store.Copy.ShouldNotBeNull();
            copy.ProjectFile.ShouldBe(harness.Filename);
            copy.Title.ShouldBe(@"plan");
            copy.WrittenAt.ShouldBe(s_Now);
            copy.Reason.ShouldBe(RecoveryCopyReasons.Interval);
            File.ReadAllText(copy.Filename).ShouldBe(ScriptedProjectFileSave.Content);

            // The copy is not a save: the title still shows the change, and the close still asks.
            harness.Core.IsProjectScenarioUpdated.ShouldBeTrue();
            harness.Main.ProjectHasChanges.ShouldBeTrue();
            File.Exists(harness.Filename).ShouldBeFalse(@"the copy wrote the file of the user");
        }

        [Fact]
        public async Task WriteCopyIfChangedAsync_Given_AProjectWithNoFile_Then_TheCopyNamesNoFile()
        {
            using Harness harness = Harness.Make(hasFile: false);
            harness.Project.IsProjectUpdated = true;

            (await harness.Recovery.WriteCopyIfChangedAsync()).ShouldBeTrue();

            RecoveryCopyModel copy = harness.Store.Copy.ShouldNotBeNull();
            copy.ProjectFile.ShouldBeEmpty();
            copy.Title.ShouldBeEmpty();
        }

        [Fact]
        public async Task WriteCopyIfChangedAsync_Given_NoChangeSinceTheLastCopy_Then_NoSecondCopy()
        {
            using Harness harness = Harness.Make();
            harness.Core.IsProjectScenarioUpdated = true;
            await harness.Recovery.WriteCopyIfChangedAsync();

            (await harness.Recovery.WriteCopyIfChangedAsync()).ShouldBeFalse();

            harness.Saver.Writes.ShouldBe(1);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task WriteCopyIfChangedAsync_Given_AChangeSinceTheLastCopy_Then_ASecondCopy(bool ofTheScenario)
        {
            using Harness harness = Harness.Make();
            harness.Core.IsProjectScenarioUpdated = true;
            await harness.Recovery.WriteCopyIfChangedAsync();

            // The plan has its mark already. The change moves a counter, and the mark stays as it is.
            if (ofTheScenario)
            {
                harness.Core.IsProjectScenarioUpdated = true;
            }
            else
            {
                harness.Project.IsProjectUpdated = true;
            }

            (await harness.Recovery.WriteCopyIfChangedAsync()).ShouldBeTrue();

            harness.Saver.Writes.ShouldBe(2);
        }

        [Fact]
        public async Task WriteCopyIfChangedAsync_Given_TheSettingIsOff_Then_NoCopy()
        {
            using Harness harness = Harness.Make();
            harness.Settings.KeepRecoveryCopy = false;
            harness.Core.IsProjectScenarioUpdated = true;

            (await harness.Recovery.WriteCopyIfChangedAsync()).ShouldBeFalse();

            harness.Store.Copy.ShouldBeNull();
        }

        // The copy is written in the background, at a time that the user did not choose. A failure goes to the log, and the
        // next interval tries again.
        [Fact]
        public async Task WriteCopyIfChangedAsync_Given_AWriteThatFails_Then_NoExceptionAndTheNextOneTriesAgain()
        {
            using Harness harness = Harness.Make();
            harness.Core.IsProjectScenarioUpdated = true;
            harness.Saver.Failure = new IOException(@"The disk is full.");

            (await harness.Recovery.WriteCopyIfChangedAsync()).ShouldBeFalse();
            harness.Store.Copy.ShouldBeNull();

            harness.Saver.Failure = null;

            (await harness.Recovery.WriteCopyIfChangedAsync()).ShouldBeTrue();
        }

        #endregion

        #region The setting

        [Fact]
        public void ToggleKeepRecoveryCopyCommand_Given_TheSettingOn_Then_ItIsOffAndTheMenuHearsOfIt()
        {
            using Harness harness = Harness.Make();
            var heard = new System.Collections.Generic.List<string?>();
            harness.Main.PropertyChanged += (_, e) => heard.Add(e.PropertyName);
            harness.Main.KeepRecoveryCopy.ShouldBeTrue();

            harness.Main.ToggleKeepRecoveryCopyCommand.Execute(null);

            harness.Settings.KeepRecoveryCopy.ShouldBeFalse();
            harness.Main.KeepRecoveryCopy.ShouldBeFalse();
            heard.ShouldContain(nameof(IMainViewModel.KeepRecoveryCopy));

            harness.Main.ToggleKeepRecoveryCopyCommand.Execute(null);

            harness.Settings.KeepRecoveryCopy.ShouldBeTrue();
        }

        #endregion

        #region The copy now

        // For an error that ends the application: a copy whatever the setting says, and whether or not the last copy has
        // the changes.
        [Fact]
        public async Task WriteCopyAsync_Given_TheSettingIsOffAndNoChangeSinceTheLastCopy_Then_ACopyWithItsReason()
        {
            using Harness harness = Harness.Make();
            harness.Core.IsProjectScenarioUpdated = true;
            await harness.Recovery.WriteCopyIfChangedAsync();
            harness.Settings.KeepRecoveryCopy = false;

            RecoveryCopyModel? copy = await harness.Recovery.WriteCopyAsync(RecoveryCopyReasons.Error);

            copy.ShouldNotBeNull().Reason.ShouldBe(RecoveryCopyReasons.Error);
            harness.Store.Copy.ShouldBe(copy);
            harness.Saver.Writes.ShouldBe(2);
        }

        [Fact]
        public async Task WriteCopyAsync_Given_AWriteThatFails_Then_NullAndNoException()
        {
            using Harness harness = Harness.Make();
            harness.Saver.Failure = new IOException(@"The disk is full.");

            (await harness.Recovery.WriteCopyAsync(RecoveryCopyReasons.Error)).ShouldBeNull();
        }

        #endregion

        #region The copy goes when the plan has no changes

        [Fact]
        public async Task Save_Given_ACopy_Then_TheCopyGoes()
        {
            using Harness harness = Harness.Make();
            harness.Core.IsProjectScenarioUpdated = true;
            await harness.Recovery.WriteCopyIfChangedAsync();
            string copyFilename = harness.Store.Copy.ShouldNotBeNull().Filename;

            await harness.Main.SaveProjectFileAsync();

            harness.Dialogs.Errors.ShouldBeEmpty();
            harness.Main.ProjectHasChanges.ShouldBeFalse();
            harness.Store.Copy.ShouldBeNull();
            File.Exists(copyFilename).ShouldBeFalse();
        }

        // After a save, the first change is a change that no copy has.
        [Fact]
        public async Task WriteCopyIfChangedAsync_Given_AChangeAfterASave_Then_ACopy()
        {
            using Harness harness = Harness.Make();
            harness.Core.IsProjectScenarioUpdated = true;
            await harness.Recovery.WriteCopyIfChangedAsync();
            await harness.Main.SaveProjectFileAsync();

            harness.Core.IsProjectScenarioUpdated = true;

            (await harness.Recovery.WriteCopyIfChangedAsync()).ShouldBeTrue();
            harness.Store.Copy.ShouldNotBeNull();
        }

        // A save ends while a copy is written: the copy that comes after the save is a copy of a plan with no changes.
        [Fact]
        public async Task WriteCopyIfChangedAsync_Given_ThePlanLosesItsChangesDuringTheWrite_Then_NoCopyStays()
        {
            using Harness harness = Harness.Make();
            harness.Core.IsProjectScenarioUpdated = true;
            harness.Saver.DuringTheWrite = () => harness.Core.IsProjectScenarioUpdated = false;

            await harness.Recovery.WriteCopyIfChangedAsync();

            harness.Main.ProjectHasChanges.ShouldBeFalse();
            harness.Store.Copy.ShouldBeNull();
        }

        #endregion

        #region The end of the run

        // The user closed the application, and saved or agreed to lose the changes.
        [Fact]
        public async Task End_Given_ARunThatEndsWell_Then_TheCopyGoesAndTheNextRunFindsNone()
        {
            using Harness harness = Harness.Make();
            harness.Core.IsProjectScenarioUpdated = true;
            await harness.Recovery.WriteCopyIfChangedAsync();

            harness.Recovery.End(keepCopy: false);

            using var next = new RecoveryStore(harness.RecoveryFolder);
            next.FindLeftCopies().ShouldBeEmpty();
        }

        // The application ends after an error: the copy is for the next run.
        [Fact]
        public async Task End_Given_ARunThatEndsAfterAnError_Then_TheNextRunFindsTheCopy()
        {
            using Harness harness = Harness.Make();
            harness.Core.IsProjectScenarioUpdated = true;
            RecoveryCopyModel? copy = await harness.Recovery.WriteCopyAsync(RecoveryCopyReasons.Error);

            harness.Recovery.End(keepCopy: true);

            using var next = new RecoveryStore(harness.RecoveryFolder);
            next.FindLeftCopies().ShouldBe([copy.ShouldNotBeNull()]);
        }

        // After the end, a change of the plan (the close of the project, for one) reaches no copy.
        [Fact]
        public async Task End_Given_AChangeOfThePlanAfterTheEnd_Then_TheCopyStays()
        {
            using Harness harness = Harness.Make();
            harness.Core.IsProjectScenarioUpdated = true;
            RecoveryCopyModel? copy = await harness.Recovery.WriteCopyAsync(RecoveryCopyReasons.Error);
            harness.Recovery.End(keepCopy: true);

            harness.Core.IsProjectScenarioUpdated = false;

            File.Exists(copy.ShouldNotBeNull().Filename).ShouldBeTrue();
        }

        #endregion

        // Writes a line in place of the project, counts the writes, and lets a test fail one or change the plan during one.
        private sealed class ScriptedProjectFileSave
            : IProjectFileSave
        {
            public const string Content = @"The scripted save writes this line.";

            public int Writes { get; private set; }

            public Exception? Failure { get; set; }

            public Action? DuringTheWrite { get; set; }

            public async Task SaveProjectFileAsync(ProjectModel project, Stream stream)
            {
                Writes++;
                DuringTheWrite?.Invoke();

                if (Failure is not null)
                {
                    throw Failure;
                }

                await using var writer = new StreamWriter(stream, leaveOpen: true);
                await writer.WriteAsync(Content);
            }
        }

        // A main view model, and the recovery of its project with a store in a folder of the test.
        private sealed class Harness
            : IDisposable
        {
            private readonly string m_Directory;

            private Harness(
                CoreViewModel core,
                ISettingService settings,
                ProjectScenarioManagerViewModel project,
                ScriptedDialogService dialogs,
                ScriptedProjectFileSave saver,
                MainViewModel main,
                RecoveryStore store,
                ProjectRecovery recovery,
                string directory)
            {
                Core = core;
                Settings = settings;
                Project = project;
                Dialogs = dialogs;
                Saver = saver;
                Main = main;
                Store = store;
                Recovery = recovery;
                m_Directory = directory;
                Filename = Path.Combine(directory, $@"plan.{Resource.ProjectPlan.Filters.Filter_ProjectFileExtension}");
                RecoveryFolder = Path.Combine(directory, @"recovery");
            }

            public CoreViewModel Core { get; }

            public ISettingService Settings { get; }

            public ProjectScenarioManagerViewModel Project { get; }

            public ScriptedDialogService Dialogs { get; }

            public ScriptedProjectFileSave Saver { get; }

            public MainViewModel Main { get; }

            public RecoveryStore Store { get; }

            public ProjectRecovery Recovery { get; }

            public string Filename { get; }

            public string RecoveryFolder { get; }

            public static Harness Make(bool hasFile = true)
            {
                (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
                var dialogs = new ScriptedDialogService();
                var saver = new ScriptedProjectFileSave();
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
                    DoNothingProxy.Of<IProjectFileOpen>(),
                    saver,
                    settings,
                    dialogs,
                    DoNothingProxy.Of<IServiceProvider>(),
                    NullLogger<MainViewModel>.Instance);

                string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"zpp-recovery-{Guid.NewGuid():N}")).FullName;
                var store = new RecoveryStore(Path.Combine(directory, @"recovery"));
                var recovery = new ProjectRecovery(
                    main,
                    core,
                    project,
                    saver,
                    settings,
                    store,
                    new FixedTimeProvider(s_Now, TimeZoneInfo.Utc),
                    NullLogger<ProjectRecovery>.Instance);
                var harness = new Harness(core, settings, project, dialogs, saver, main, store, recovery, directory);
                settings.KeepRecoveryCopy = true;

                if (hasFile)
                {
                    settings.SetProjectFilePath(harness.Filename, bindTitleToFilename: true);
                }

                recovery.Start();
                return harness;
            }

            public void Dispose()
            {
                Recovery.Dispose();
                Main.Dispose();
                Project.Dispose();
                Core.Dispose();

                try
                {
                    Directory.Delete(m_Directory, recursive: true);
                }
                catch (IOException)
                {
                    // Best-effort cleanup only.
                }
            }
        }
    }
}
