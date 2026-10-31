using Shouldly;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using static Zametek.ViewModel.ProjectPlan.Tests.SequencerScopes;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Moves a revision counter near the end of its range, for a test of what happens there: no test can make two billion
    /// increments. A test sets the field, and the real event does the rest (ARCHITECTURE section 9).
    /// </summary>
    /// <remarks>
    /// The field changes without a raise, and so a chain that watches the counter still holds the value from before. A test
    /// that compares the counter with a copy has to let a raise through first.
    /// </remarks>
    internal static class RevisionCounters
    {
        public static void SetProjectScenarioRevision(CoreViewModel core, int value) =>
            Field<CoreViewModel>(@"m_ProjectScenarioRevision").SetValue(core, value);

        public static void SetCompilationOutputRevision(CoreViewModel core, int value) =>
            Field<CoreViewModel>(@"m_CompilationOutputRevision").SetValue(core, value);

        public static void SetCompilationRequestRevision(CoreViewModel core, int value) =>
            Field<CoreViewModel>(@"m_CompilationRequestRevision").SetValue(core, value);

        public static void SetTitleRevision(ProjectScenarioManagerViewModel project, int value) =>
            Field<ProjectScenarioManagerViewModel>(@"m_TitleRevision").SetValue(project, value);

        public static void SetInputRevision(CompilationScheduler scheduler, int value) =>
            Field<CompilationScheduler>(@"m_InputRevision").SetValue(scheduler, value);

        public static int InputRevisionOf(CompilationScheduler scheduler) =>
            (int)Field<CompilationScheduler>(@"m_InputRevision").GetValue(scheduler)!;

        public static Lock LockOf(CoreViewModel core) =>
            (Lock)Field<CoreViewModel>(@"m_Lock").GetValue(core)!;

        public static Lock LockOf(ProjectScenarioManagerViewModel project) =>
            (Lock)Field<ProjectScenarioManagerViewModel>(@"m_Lock").GetValue(project)!;

        private static FieldInfo Field<TOwner>(string name) =>
            typeof(TOwner).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($@"{typeof(TOwner).Name} has no field {name}");
    }

    /// <summary>
    /// The revision counters keep the form that ARCHITECTURE section 9 gives: each event moves the value on by one, the
    /// value rolls over at the end of its range and does not wrap anywhere else, one event gives one raise, and the getter
    /// has the new value when the raise occurs. A consumer reads the getter at the raise, and so a raise that comes before the
    /// increment looks like no change at all.
    /// </summary>
    public class RevisionCounterTests
    {
        private static readonly TimeSpan s_Patience = TimeSpan.FromSeconds(10);

        private const int c_ActivityCount = 5;

        // Reads the getter on another thread while this thread holds the lock of its owner. A chain reads a getter again on the
        // thread that raises the change, and a getter that waits for a lock can deadlock with it (ARCHITECTURE section 7 rule 9).
        private static void ShouldNotWaitForTheLock(Lock ownerLock, Func<int> getter)
        {
            var reader = new Thread(() => getter()) { IsBackground = true };

            lock (ownerLock)
            {
                reader.Start();
                reader.Join(s_Patience).ShouldBeTrue("the getter waited for the lock of its owner");
            }
        }

        // What the getter gives at each raise of the property.
        private static List<int> RecordRaises(INotifyPropertyChanged owner, string propertyName, Func<int> getter)
        {
            var raises = new List<int>();

            owner.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == propertyName)
                {
                    raises.Add(getter());
                }
            };

            return raises;
        }

        private static (List<IDependentActivity>, Dictionary<int, IManagedActivityViewModel>) NoCopies() =>
            (new List<IDependentActivity>(), new Dictionary<int, IManagedActivityViewModel>());

        [Theory]
        [InlineData(int.MaxValue, int.MinValue)]
        [InlineData(int.MinValue, int.MinValue + 1)]
        [InlineData(999_999, 1_000_000)]
        public void CompilationOutputRevision_Given_ACascadeThatPublishes_Then_ItMovesOnByOneAndRaisesOnce(int before, int after)
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using CoreViewModel core = CoreViewModelFixture.Create();
            core.StartSubscriptions();
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");
            Drain(main, pool);

            RevisionCounters.SetCompilationOutputRevision(core, before);
            List<int> raises = RecordRaises(core, nameof(ICoreViewModel.CompilationOutputRevision), () => core.CompilationOutputRevision);

            core.RunCompile();
            Drain(main, pool);

            raises.ShouldHaveSingleItem().ShouldBe(after);
            core.CompilationOutputRevision.ShouldBe(after);
        }

        [Theory]
        [InlineData(int.MaxValue, int.MinValue)]
        [InlineData(int.MinValue, int.MinValue + 1)]
        public void ProjectScenarioRevision_Given_AReset_Then_ItMovesOnByOneAndRaisesOnce(int before, int after)
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            RevisionCounters.SetProjectScenarioRevision(core, before);
            List<int> raises = RecordRaises(core, nameof(ICoreViewModel.ProjectScenarioRevision), () => core.ProjectScenarioRevision);

            core.ResetProjectScenario();

            raises.ShouldHaveSingleItem().ShouldBe(after);
            core.ProjectScenarioRevision.ShouldBe(after);
        }

        [Theory]
        [InlineData(int.MaxValue, int.MinValue)]
        [InlineData(int.MinValue, int.MinValue + 1)]
        public void CompilationRequestRevision_Given_ARequest_Then_ItMovesOnByOneAndRaisesOnce(int before, int after)
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            RevisionCounters.SetCompilationRequestRevision(core, before);
            List<int> raises = RecordRaises(core, nameof(CoreViewModel.CompilationRequestRevision), () => core.CompilationRequestRevision);

            core.ProjectStart = core.ProjectStart.AddDays(1);

            raises.ShouldHaveSingleItem().ShouldBe(after);
            core.CompilationRequestRevision.ShouldBe(after);
        }

        [Theory]
        [InlineData(nameof(ICoreViewModel.ProjectStart))]
        [InlineData(nameof(ICoreViewModel.GraphSettings))]
        [InlineData(nameof(ICoreViewModel.ResourceSettings))]
        [InlineData(nameof(ICoreViewModel.WorkStreamSettings))]
        [InlineData(nameof(ICoreViewModel.HolidaySettings))]
        [InlineData(nameof(IProjectScenarioDisplaySettingsViewModel.NonWorkingDayMode))]
        public void CompilationRequestRevision_Given_AChangeOfWhatACompilationReads_Then_ItMoves(string setting)
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            int revision = core.CompilationRequestRevision;

            switch (setting)
            {
                case nameof(ICoreViewModel.ProjectStart):
                    core.ProjectStart = core.ProjectStart.AddDays(1);
                    break;
                case nameof(ICoreViewModel.GraphSettings):
                    core.GraphSettings = CoreViewModelFixture.DefaultGraphSettings;
                    break;
                case nameof(ICoreViewModel.ResourceSettings):
                    core.ResourceSettings = new ResourceSettingsModel();
                    break;
                case nameof(ICoreViewModel.WorkStreamSettings):
                    core.WorkStreamSettings = new WorkStreamSettingsModel();
                    break;
                case nameof(ICoreViewModel.HolidaySettings):
                    core.HolidaySettings = new HolidaySettingsModel();
                    break;
                case nameof(IProjectScenarioDisplaySettingsViewModel.NonWorkingDayMode):
                    core.DisplaySettingsViewModel.NonWorkingDayMode = NonWorkingDayMode.Weekends;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(setting));
            }

            core.CompilationRequestRevision.ShouldNotBe(revision, $"a change of {setting} did not ask for a compilation");
        }

        [Fact]
        public void CompilationRequestRevision_Given_ALoad_Then_ItDoesNotMove()
        {
            // A load changes all that a compilation reads, and it compiles by itself. A request would only compile it again.
            using CoreViewModel core = CoreViewModelFixture.Create();
            List<int> raises = RecordRaises(core, nameof(CoreViewModel.CompilationRequestRevision), () => core.CompilationRequestRevision);

            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(c_ActivityCount), Guid.NewGuid(), @"Test");

            raises.ShouldBeEmpty();
        }

        [Theory]
        [InlineData(int.MaxValue, int.MinValue)]
        [InlineData(int.MinValue, int.MinValue + 1)]
        public void TitleRevision_Given_AnArm_Then_ItMovesOnByOneAndRaisesOnce(int before, int after)
        {
            (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
            using CoreViewModel coreOwner = core;
            using var project = new ProjectScenarioManagerViewModel(
                core,
                settings,
                CoreViewModelFixture.CreateDialogService(),
                new DateTimeCalculator(TimeProvider.System),
                CoreViewModelFixture.CreateUIDispatcher());
            RevisionCounters.SetTitleRevision(project, before);
            List<int> raises = RecordRaises(project, nameof(IProjectScenarioManagerViewModel.TitleRevision), () => project.TitleRevision);

            project.IsReadyToReviseTitle = ReadyToRevise.Yes;

            raises.ShouldHaveSingleItem().ShouldBe(after);
            project.TitleRevision.ShouldBe(after);
        }

        [Fact]
        public void Getters_Given_TheLockOfTheCoreHeld_Then_TheyDoNotWaitForIt()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            Lock coreLock = RevisionCounters.LockOf(core);

            ShouldNotWaitForTheLock(coreLock, () => core.CompilationOutputRevision);
            ShouldNotWaitForTheLock(coreLock, () => core.CompilationRequestRevision);
            ShouldNotWaitForTheLock(coreLock, () => core.ProjectScenarioRevision);
        }

        [Fact]
        public void TitleRevision_Given_TheLockOfTheManagerHeld_Then_ItDoesNotWaitForIt()
        {
            (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
            using CoreViewModel coreOwner = core;
            using var project = new ProjectScenarioManagerViewModel(
                core,
                settings,
                CoreViewModelFixture.CreateDialogService(),
                new DateTimeCalculator(TimeProvider.System),
                CoreViewModelFixture.CreateUIDispatcher());

            ShouldNotWaitForTheLock(RevisionCounters.LockOf(project), () => project.TitleRevision);
        }

        [Theory]
        [InlineData(int.MaxValue, int.MinValue)]
        [InlineData(int.MinValue, int.MinValue + 1)]
        public void NoteInputsChanged_Given_ASnapshot_Then_ItMovesOnByOneAndTheSnapshotIsOutOfDate(int before, int after)
        {
            var scheduler = new CompilationScheduler();
            RevisionCounters.SetInputRevision(scheduler, before);

            CompilationSnapshot snapshot = scheduler.TakeSnapshot([], [], 0, NoCopies);

            snapshot.InputRevision.ShouldBe(before);
            scheduler.HasMovedOnSince(snapshot).ShouldBeFalse();
            scheduler.HasUnabsorbedInputs().ShouldBeFalse();

            scheduler.NoteInputsChanged();

            RevisionCounters.InputRevisionOf(scheduler).ShouldBe(after);
            scheduler.HasMovedOnSince(snapshot).ShouldBeTrue("a snapshot from before the rollover looked current");
            scheduler.HasUnabsorbedInputs().ShouldBeTrue("a change across the rollover looked absorbed");

            scheduler.AbsorbPendingInputs();

            scheduler.HasUnabsorbedInputs().ShouldBeFalse();
        }
    }
}
