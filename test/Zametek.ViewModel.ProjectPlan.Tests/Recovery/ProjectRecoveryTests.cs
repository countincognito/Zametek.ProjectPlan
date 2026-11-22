using Dock.Model.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
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

        #region A copy changes nothing of the project

        // A save puts the time of the save into the scenario on show. A copy is written while the user works, and the
        // list of the scenarios must not change under the user.
        [Fact]
        public async Task WriteCopyIfChangedAsync_Given_APlanWithChanges_Then_TheScenarioKeepsItsTimeOfChange()
        {
            using Harness harness = Harness.Make();
            IManagedNodeViewModel node = harness.Project.GetNode(harness.Settings.ScenarioId).ShouldNotBeNull();
            DateTimeOffset modifiedOn = node.ModifiedOn;
            var heard = new List<string?>();
            ((INotifyPropertyChanged)node).PropertyChanged += (_, e) => heard.Add(e.PropertyName);
            harness.Core.IsProjectScenarioUpdated = true;

            (await harness.Recovery.WriteCopyIfChangedAsync()).ShouldBeTrue();

            node.ModifiedOn.ShouldBe(modifiedOn);
            heard.ShouldNotContain(nameof(IManagedNodeViewModel.ModifiedOn));
        }

        // The main window shows its wait overlay for the busy state of the scenario manager with no delay, and for the
        // busy state of the core after a delay.
        [Fact]
        public async Task WriteCopyIfChangedAsync_Given_APlanWithChanges_Then_NoBusyState()
        {
            using Harness harness = Harness.Make();
            var heard = new List<string>();
            harness.Project.PropertyChanged += (_, e) => heard.Add($@"scenario manager: {e.PropertyName}");
            harness.Core.PropertyChanged += (_, e) => heard.Add($@"core: {e.PropertyName}");
            harness.Core.IsProjectScenarioUpdated = true;
            heard.Clear();

            (await harness.Recovery.WriteCopyIfChangedAsync()).ShouldBeTrue();

            heard.ShouldNotContain($@"scenario manager: {nameof(IProjectScenarioManagerViewModel.IsBusy)}");
            heard.ShouldNotContain($@"core: {nameof(ICoreViewModel.IsBusy)}");
        }

        // The copy holds what a save writes at the same moment. Only the time of the scenario on show is the time of each
        // build.
        [Fact]
        public async Task WriteCopyIfChangedAsync_Given_APlanWithActivities_Then_TheCopyIsTheProjectThatASaveBuilds()
        {
            using Harness harness = Harness.Make();
            harness.Core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(3), harness.Settings.ScenarioId, harness.Settings.ScenarioTitle);
            harness.Core.IsProjectScenarioUpdated = true;

            (await harness.Recovery.WriteCopyIfChangedAsync()).ShouldBeTrue();

            ProjectModel copied = harness.Saver.LastProject.ShouldNotBeNull();
            ProjectModel saved = await harness.Main.BuildProjectAsync();
            copied.Current.ShouldBe(harness.Settings.ScenarioId);
            copied.Files.Single(x => x.NodeId == copied.Current).Scenario.DependentActivities.Count.ShouldBe(3);
            WithNoTimeOfChange(copied).ShouldBeEquivalentTo(WithNoTimeOfChange(saved));
        }

        // The nodes and the files in one order, and no time of a change.
        private static ProjectModel WithNoTimeOfChange(ProjectModel project) =>
            project with
            {
                Nodes = [.. project.Nodes.OrderBy(x => x.Id).Select(x => x with { ModifiedOn = default })],
                Files = [.. project.Files.OrderBy(x => x.NodeId)],
            };

        #endregion

        #region The setting

        [Fact]
        public void ToggleKeepRecoveryCopyCommand_Given_TheSettingOn_Then_ItIsOffAndTheMenuHearsOfIt()
        {
            using Harness harness = Harness.Make();
            var heard = new List<string?>();
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

        #region The copies that a run left

        private const int c_Open = 0;
        private const int c_NotNow = 1;
        private const int c_Delete = 2;

        private static readonly string[] s_Choices = [@"Open", @"Not Now", @"Delete"];
        private static readonly DateTimeOffset s_March = new(2027, 3, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset s_June = new(2027, 6, 1, 0, 0, 0, TimeSpan.Zero);

        // A run that ended after an error: it had a project with changes, and it left its copy.
        private static async Task<RecoveryCopyModel> LeaveCopyAsync(Harness run, DateTimeOffset projectStart)
        {
            run.Core.RequestedProjectStart = projectStart;
            run.Main.ProjectHasChanges.ShouldBeTrue();
            RecoveryCopyModel copy = (await run.Recovery.WriteCopyAsync(RecoveryCopyReasons.Error)).ShouldNotBeNull();
            run.Recovery.End(keepCopy: true);
            return copy;
        }

        [Fact]
        public async Task OfferLeftCopiesAsync_Given_NoCopy_Then_NoQuestion()
        {
            using Harness harness = Harness.Make(realFiles: true);

            (await harness.Recovery.OfferLeftCopiesAsync()).ShouldBeFalse();

            harness.Dialogs.Choices.ShouldBeEmpty();
        }

        [Fact]
        public async Task OfferLeftCopiesAsync_Given_ACopy_Then_TheQuestionNamesTheFileAndTheThreeChoices()
        {
            using Harness first = Harness.Make(realFiles: true);
            await LeaveCopyAsync(first, s_March);
            using Harness second = Harness.Make(hasFile: false, directory: first.Folder, realFiles: true);

            await second.Recovery.OfferLeftCopiesAsync();

            (string title, string message, IReadOnlyList<string> choices) = second.Dialogs.Choices.ShouldHaveSingleItem();
            title.ShouldBe(@"Recovery Copy");
            choices.ShouldBe(s_Choices);
            message.ShouldBe(
                $@"The application ended before it saved the changes to {first.Filename}, and it kept a copy from {s_Now.ToString(@"g", CultureInfo.CurrentCulture)}. "
                + @"Open the copy to go on with it: no file changes until you save. ""Not Now"" keeps the copy, and the application asks again at its next start.");
        }

        [Fact]
        public async Task OfferLeftCopiesAsync_Given_TheUserOpensTheCopy_Then_ItIsTheProjectWithChangesAndNoFileOfTheUserChanged()
        {
            using Harness first = Harness.Make(realFiles: true);
            RecoveryCopyModel left = await LeaveCopyAsync(first, s_March);
            using Harness second = Harness.Make(hasFile: false, directory: first.Folder, realFiles: true);
            second.Dialogs.ChoiceAnswers.Enqueue(c_Open);

            (await second.Recovery.OfferLeftCopiesAsync()).ShouldBeTrue();

            second.Dialogs.Errors.ShouldBeEmpty();
            second.Core.RequestedProjectStart.Date.ShouldBe(s_March.Date);
            second.Main.ProjectHasChanges.ShouldBeTrue();

            // The project has the name and the folder of the file, and it is not that file.
            second.Settings.ProjectTitle.ShouldBe(@"plan");
            second.Settings.ProjectDirectory.ShouldBe(first.Folder);
            second.Settings.IsTitleBoundToFilename.ShouldBeFalse();
            File.Exists(first.Filename).ShouldBeFalse(@"the open of the copy wrote the file of the user");

            // This run has the project as its own copy, and the copy that the first run left is gone.
            RecoveryCopyModel own = second.Store.Copy.ShouldNotBeNull();
            own.Reason.ShouldBe(RecoveryCopyReasons.Opened);
            own.Title.ShouldBe(@"plan");
            File.Exists(left.Filename).ShouldBeFalse();
        }

        // A project can have a title and no file: a copy that the user opened and did not save, for one. Its copy has the
        // title, and the project that opens from the copy has it too.
        [Fact]
        public async Task OfferLeftCopiesAsync_Given_ACopyOfAProjectWithATitleAndNoFile_Then_TheProjectHasTheTitle()
        {
            using Harness first = Harness.Make(hasFile: false, realFiles: true);
            first.Settings.SetProjectFilePath(Path.Combine(first.Folder, @"the wall v1.2.zpp"), bindTitleToFilename: false);
            RecoveryCopyModel left = await LeaveCopyAsync(first, s_March);
            left.ProjectFile.ShouldBeEmpty();
            left.Title.ShouldBe(@"the wall v1.2");
            using Harness second = Harness.Make(hasFile: false, directory: first.Folder, realFiles: true);
            second.Dialogs.ChoiceAnswers.Enqueue(c_Open);

            (await second.Recovery.OfferLeftCopiesAsync()).ShouldBeTrue();

            second.Dialogs.Choices.ShouldHaveSingleItem().Message.ShouldStartWith(@"The application ended before it saved the changes to the wall v1.2, and");
            second.Settings.ProjectTitle.ShouldBe(@"the wall v1.2");
            second.Settings.IsTitleBoundToFilename.ShouldBeFalse();
            second.Store.Copy.ShouldNotBeNull().Title.ShouldBe(@"the wall v1.2");
        }

        // A copy of a project with no title is a project with no title.
        [Fact]
        public async Task OfferLeftCopiesAsync_Given_ACopyOfAProjectWithNoTitle_Then_TheQuestionSaysSoAndTheProjectHasNone()
        {
            using Harness first = Harness.Make(hasFile: false, realFiles: true);
            await LeaveCopyAsync(first, s_March);
            using Harness second = Harness.Make(hasFile: false, directory: first.Folder, realFiles: true);
            second.Dialogs.ChoiceAnswers.Enqueue(c_Open);

            (await second.Recovery.OfferLeftCopiesAsync()).ShouldBeTrue();

            second.Dialogs.Choices.ShouldHaveSingleItem().Message.ShouldStartWith(@"The application ended before it saved the changes to a project with no name, and");
            second.Settings.ProjectTitle.ShouldBeEmpty();
            second.Main.ProjectHasChanges.ShouldBeTrue();
        }

        // The file of the user changes only when the user says where to save.
        [Fact]
        public async Task SaveProjectFileAsync_Given_AnOpenedCopy_Then_TheSaveAsksWhereToWrite()
        {
            using Harness first = Harness.Make(realFiles: true);
            await LeaveCopyAsync(first, s_March);
            using Harness second = Harness.Make(hasFile: false, directory: first.Folder, realFiles: true);
            second.Dialogs.ChoiceAnswers.Enqueue(c_Open);
            await second.Recovery.OfferLeftCopiesAsync();

            await second.Main.SaveProjectFileAsync();

            (string name, string directory, _) = second.Dialogs.SaveDialogs.ShouldHaveSingleItem();
            name.ShouldBe(@"plan");
            directory.ShouldBe(first.Folder);
            File.Exists(first.Filename).ShouldBeFalse(@"the save wrote with no answer of the user");

            // The user gives the name, and the save writes there. The plan has no changes then, and the copy goes.
            second.Dialogs.SaveFilename = first.Filename;

            await second.Main.SaveProjectFileAsync();

            second.Dialogs.Errors.ShouldBeEmpty();
            File.Exists(first.Filename).ShouldBeTrue();
            second.Main.ProjectHasChanges.ShouldBeFalse();
            second.Store.Copy.ShouldBeNull();
        }

        [Theory]
        [InlineData(c_NotNow)]
        [InlineData(-1)]
        public async Task OfferLeftCopiesAsync_Given_NotNowOrAClosedDialog_Then_TheCopyStaysForTheNextStart(int answer)
        {
            using Harness first = Harness.Make(realFiles: true);
            RecoveryCopyModel left = await LeaveCopyAsync(first, s_March);
            using Harness second = Harness.Make(hasFile: false, directory: first.Folder, realFiles: true);
            second.Dialogs.ChoiceAnswers.Enqueue(answer);

            (await second.Recovery.OfferLeftCopiesAsync()).ShouldBeFalse();

            second.Main.ProjectHasChanges.ShouldBeFalse();
            second.Store.FindLeftCopies().ShouldBe([left]);
        }

        [Fact]
        public async Task OfferLeftCopiesAsync_Given_Delete_Then_TheCopyGoes()
        {
            using Harness first = Harness.Make(realFiles: true);
            RecoveryCopyModel left = await LeaveCopyAsync(first, s_March);
            using Harness second = Harness.Make(hasFile: false, directory: first.Folder, realFiles: true);
            second.Dialogs.ChoiceAnswers.Enqueue(c_Delete);

            (await second.Recovery.OfferLeftCopiesAsync()).ShouldBeFalse();

            second.Main.ProjectHasChanges.ShouldBeFalse();
            second.Store.FindLeftCopies().ShouldBeEmpty();
            File.Exists(left.Filename).ShouldBeFalse();
        }

        // One project is open at a time. After the user opens a copy, the question about the others waits.
        [Fact]
        public async Task OfferLeftCopiesAsync_Given_TwoCopies_Then_TheNewestFirstAndNoQuestionAfterAnOpen()
        {
            using Harness older = Harness.Make(realFiles: true);
            RecoveryCopyModel olderCopy = await LeaveCopyAsync(older, s_March);
            using Harness newer = Harness.Make(directory: older.Folder, realFiles: true, now: s_Now.AddHours(1));
            RecoveryCopyModel newerCopy = await LeaveCopyAsync(newer, s_June);
            using Harness third = Harness.Make(hasFile: false, directory: older.Folder, realFiles: true);
            third.Dialogs.ChoiceAnswers.Enqueue(c_Open);

            (await third.Recovery.OfferLeftCopiesAsync()).ShouldBeTrue();

            third.Dialogs.Choices.Count.ShouldBe(1);
            third.Core.RequestedProjectStart.Date.ShouldBe(s_June.Date);
            File.Exists(newerCopy.Filename).ShouldBeFalse();
            third.Store.FindLeftCopies().ShouldBe([olderCopy]);
        }

        [Fact]
        public async Task OfferLeftCopiesAsync_Given_TwoCopiesAndNotNowForTheFirst_Then_TheQuestionAboutTheSecond()
        {
            using Harness older = Harness.Make(realFiles: true);
            RecoveryCopyModel olderCopy = await LeaveCopyAsync(older, s_March);
            using Harness newer = Harness.Make(directory: older.Folder, realFiles: true, now: s_Now.AddHours(1));
            RecoveryCopyModel newerCopy = await LeaveCopyAsync(newer, s_June);
            using Harness third = Harness.Make(hasFile: false, directory: older.Folder, realFiles: true);
            third.Dialogs.ChoiceAnswers.Enqueue(c_NotNow);
            third.Dialogs.ChoiceAnswers.Enqueue(c_Delete);

            (await third.Recovery.OfferLeftCopiesAsync()).ShouldBeFalse();

            third.Dialogs.Choices.Count.ShouldBe(2);
            third.Store.FindLeftCopies().ShouldBe([newerCopy]);
            File.Exists(olderCopy.Filename).ShouldBeFalse();
        }

        // A copy that cannot be read: the user hears of it, the copy stays, and the application has an empty project.
        [Fact]
        public async Task OfferLeftCopiesAsync_Given_ACopyThatCannotBeOpened_Then_AnErrorAndTheCopyStays()
        {
            using Harness first = Harness.Make(realFiles: true);
            RecoveryCopyModel left = await LeaveCopyAsync(first, s_March);
            File.WriteAllText(left.Filename, @"This is not a project file.");
            using Harness second = Harness.Make(hasFile: false, directory: first.Folder, realFiles: true);
            second.Dialogs.ChoiceAnswers.Enqueue(c_Open);

            (await second.Recovery.OfferLeftCopiesAsync()).ShouldBeFalse();

            second.Dialogs.Errors.Count.ShouldBe(1);
            second.Main.ProjectHasChanges.ShouldBeFalse();
            second.Store.Copy.ShouldBeNull();
            File.Exists(left.Filename).ShouldBeTrue();
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

        // A copy at an interval is on its way when the user closes the application and agrees to lose the changes. The end
        // waits for the write, and then it removes the copy. Thus, the next start has no copy of those changes.
        [Fact]
        public async Task End_Given_AWriteInProgress_Then_TheEndWaitsForItAndNoCopyStays()
        {
            using Harness harness = Harness.Make();
            harness.Core.IsProjectScenarioUpdated = true;
            using var started = new ManualResetEventSlim();
            using var goOn = new ManualResetEventSlim();
            harness.Saver.DuringTheWrite = () =>
            {
                started.Set();
                goOn.Wait(s_TestLimit);
            };
            Task<bool> write = Task.Run(harness.Recovery.WriteCopyIfChangedAsync);
            started.Wait(s_TestLimit).ShouldBeTrue();

            Task end = Task.Run(() => harness.Recovery.End(keepCopy: false));

            (await Task.WhenAny(end, Task.Delay(TimeSpan.FromMilliseconds(250)))).ShouldNotBe(end, @"the end did not wait for the write");
            goOn.Set();
            await end.WaitAsync(s_TestLimit);
            await write.WaitAsync(s_TestLimit);

            harness.Saver.Writes.ShouldBe(1);
            harness.Store.Copy.ShouldBeNull();
            FilesOf(harness.RecoveryFolder).ShouldBeEmpty();
        }

        // The wait of the end has a limit. A write that ends after the limit removes its own copy.
        [Fact]
        public async Task End_Given_AWriteThatEndsAfterTheWait_Then_ThatWriteRemovesItsCopy()
        {
            using Harness harness = Harness.Make(endWait: TimeSpan.FromMilliseconds(50));
            harness.Core.IsProjectScenarioUpdated = true;

            // The end comes on the thread of the write, and so its wait cannot end before its limit.
            harness.Saver.DuringTheWrite = () => harness.Recovery.End(keepCopy: false);

            (await harness.Recovery.WriteCopyIfChangedAsync()).ShouldBeFalse();

            harness.Saver.Writes.ShouldBe(1);
            harness.Store.Copy.ShouldBeNull();
            FilesOf(harness.RecoveryFolder).ShouldBeEmpty();
        }

        // The end after an error keeps the copy, and so it keeps a copy that is on its way too.
        [Fact]
        public async Task End_Given_AWriteInProgressAndAnEndAfterAnError_Then_TheNextRunFindsTheCopy()
        {
            using Harness harness = Harness.Make();
            harness.Core.IsProjectScenarioUpdated = true;
            harness.Saver.DuringTheWrite = () => harness.Recovery.End(keepCopy: true);

            RecoveryCopyModel? copy = await harness.Recovery.WriteCopyAsync(RecoveryCopyReasons.Error);

            using var next = new RecoveryStore(harness.RecoveryFolder);
            next.FindLeftCopies().ShouldBe([copy.ShouldNotBeNull()]);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task WriteCopyAsync_Given_ARunThatEnded_Then_NoCopy(bool keepCopy)
        {
            using Harness harness = Harness.Make();
            harness.Core.IsProjectScenarioUpdated = true;
            harness.Recovery.End(keepCopy);

            (await harness.Recovery.WriteCopyAsync(RecoveryCopyReasons.Error)).ShouldBeNull();
            (await harness.Recovery.WriteCopyIfChangedAsync()).ShouldBeFalse();

            harness.Saver.Writes.ShouldBe(0);
            FilesOf(harness.RecoveryFolder).ShouldBeEmpty();
        }

        private static readonly TimeSpan s_TestLimit = TimeSpan.FromSeconds(30);

        private static string[] FilesOf(string folder) =>
            Directory.Exists(folder) ? [.. Directory.GetFiles(folder).Select(Path.GetFileName).OfType<string>()] : [];

        #endregion

        // Writes a line in place of the project, counts the writes, and lets a test fail one or change the plan during one.
        private sealed class ScriptedProjectFileSave
            : IProjectFileSave
        {
            public const string Content = @"The scripted save writes this line.";

            public int Writes { get; private set; }

            public Exception? Failure { get; set; }

            public Action? DuringTheWrite { get; set; }

            public ProjectModel? LastProject { get; private set; }

            public async Task SaveProjectFileAsync(ProjectModel project, Stream stream)
            {
                Writes++;
                LastProject = project;
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
            private readonly bool m_OwnsDirectory;

            private Harness(
                CoreViewModel core,
                ISettingService settings,
                ProjectScenarioManagerViewModel project,
                ScriptedDialogService dialogs,
                ScriptedProjectFileSave saver,
                MainViewModel main,
                RecoveryStore store,
                ProjectRecovery recovery,
                string directory,
                bool ownsDirectory)
            {
                m_OwnsDirectory = ownsDirectory;
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

            public string Folder => m_Directory;

            public string Filename { get; }

            public string RecoveryFolder { get; }

            // A run of the application. With a directory, it is a later run in the folders of an earlier one. With real
            // files, the project files are those that the application writes and reads.
            public static Harness Make(
                bool hasFile = true,
                string? directory = null,
                bool realFiles = false,
                DateTimeOffset? now = null,
                TimeSpan? endWait = null)
            {
                (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
                var dialogs = new ScriptedDialogService();
                var saver = new ScriptedProjectFileSave();
                var calculator = new DateTimeCalculator(TimeProvider.System);
                IProjectFileSave projectFileSave = realFiles ? new ProjectFileSave() : saver;
                IProjectFileOpen projectFileOpen = realFiles ? new ProjectFileOpen(calculator) : DoNothingProxy.Of<IProjectFileOpen>();
                var project = new ProjectScenarioManagerViewModel(
                    core,
                    settings,
                    dialogs,
                    calculator,
                    CoreViewModelFixture.CreateUIDispatcher());

                var main = new MainViewModel(
                    DoNothingProxy.Of<IFactory>(),
                    DoNothingProxy.Of<IDockSerializer>(),
                    DoNothingProxy.Of<IDataGridLayoutManager>(),
                    project,
                    core,
                    projectFileOpen,
                    projectFileSave,
                    settings,
                    dialogs,
                    DoNothingProxy.Of<IServiceProvider>(),
                    NullLogger<MainViewModel>.Instance);

                bool ownsDirectory = directory is null;
                directory ??= Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"zpp-recovery-{Guid.NewGuid():N}")).FullName;
                var store = new RecoveryStore(Path.Combine(directory, @"recovery"));
                var recovery = new ProjectRecovery(
                    main,
                    core,
                    project,
                    projectFileSave,
                    settings,
                    dialogs,
                    store,
                    new FixedTimeProvider(now ?? s_Now, TimeZoneInfo.Utc),
                    NullLogger<ProjectRecovery>.Instance);

                if (endWait is TimeSpan wait)
                {
                    recovery.EndWait = wait;
                }

                var harness = new Harness(core, settings, project, dialogs, saver, main, store, recovery, directory, ownsDirectory);
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

                // The first run of a folder removes it, after the later runs.
                if (!m_OwnsDirectory)
                {
                    return;
                }

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
