using Dock.Model.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System;
using System.IO;
using System.Reflection;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using static Zametek.ViewModel.ProjectPlan.Tests.SequencerScopes;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// The window title follows the edits made to the project only once the main view model has been started (see
    /// IStartSubscriptions), and not from its constructor.
    /// </summary>
    /// <remarks>
    /// The pumps stand in for the thread pool and the main thread, so each test says when a delivery runs. The main view
    /// model also makes helpers for its busy flag that deliver on the main thread from the constructor, as properties of
    /// nothing but the window do, so what is looked at here is the title and not what is waiting.
    /// </remarks>
    public class MainViewModelStartTests
    {
        [Fact]
        public void StartSubscriptions_Given_AStartedMainViewModel_Then_TheTitleMarksTheEditsOfTheProject()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Main.StartSubscriptions();
            Drain(main, pool);

            harness.Core.IsProjectScenarioUpdated = true;
            Drain(main, pool);

            harness.Main.ProjectTitle.ShouldStartWith("*");
        }

        [Fact]
        public void StartSubscriptions_Given_AMainViewModelNotStarted_Then_TheTitleDoesNotFollowTheProject()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();

            harness.Core.IsProjectScenarioUpdated = true;
            Drain(main, pool);

            harness.Main.ProjectTitle.ShouldBeEmpty();
        }

        [Fact]
        public void StartSubscriptions_Given_AKilledMainViewModel_Then_StartsNothing()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Main.KillSubscriptions();

            harness.Main.StartSubscriptions();
            harness.Core.IsProjectScenarioUpdated = true;
            Drain(main, pool);

            harness.Main.ProjectTitle.ShouldBeEmpty();
        }

        [Fact]
        public void KillSubscriptions_Given_AStartedMainViewModel_Then_TheTitleFollowsTheProjectNoMore()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Main.StartSubscriptions();
            Drain(main, pool);
            harness.Core.IsProjectScenarioUpdated = true;
            Drain(main, pool);
            harness.Main.ProjectTitle.StartsWith('*').ShouldBeTrue("the title was not following the project to begin with");

            harness.Main.KillSubscriptions();
            harness.Core.IsProjectScenarioUpdated = false;
            Drain(main, pool);

            harness.Main.ProjectTitle.ShouldStartWith("*");
        }

        [Fact]
        public void Title_Given_AProjectThatIsClosedWithNoEdit_Then_TheTitleIsTheTitleOfNoProject()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Main.StartSubscriptions();
            Drain(main, pool);
            harness.Settings.SetProjectFilePath(PlanFile(@"plan"), bindTitleToFilename: true);
            harness.Project.IsReadyToReviseTitle = ReadyToRevise.Yes;
            Drain(main, pool);
            harness.Main.ProjectTitle.ShouldStartWith(@"plan - ");

            // No edit has been made, and so the project does not change: the title follows the close all the same.
            harness.Project.ResetProject();
            Drain(main, pool);

            harness.Main.ProjectTitle.ShouldStartWith($@"{Resource.ProjectPlan.Titles.Title_UntitledProject} - ");
        }

        [Fact]
        public void Title_Given_AScenarioThatIsLoadedWithNoEdit_Then_TheTitleNamesTheScenario()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Main.StartSubscriptions();
            Drain(main, pool);
            harness.Settings.SetProjectFilePath(PlanFile(@"plan"), bindTitleToFilename: true);
            harness.Settings.SetProjectScenarioTitle(@"Alpha");
            harness.Project.IsReadyToReviseTitle = ReadyToRevise.Yes;
            Drain(main, pool);
            harness.Main.ProjectTitle.ShouldContain(@" - Alpha - ");

            harness.Settings.SetProjectScenarioTitle(@"Beta");
            harness.Project.IsReadyToReviseTitle = ReadyToRevise.Yes;
            Drain(main, pool);

            harness.Main.ProjectTitle.ShouldContain(@" - Beta - ");
            harness.Main.ProjectTitle.ShouldNotStartWith(@"*");
        }

        [Fact]
        public void Title_Given_AProjectThatIsLoadedAfterOneWithEdits_Then_TheStarGoesAndTheTitleIsTheNewOne()
        {
            CoreViewModelFixture.EnsureReactiveUIInitialized();
            using var main = new MainThreadSequencerScope();
            using var pool = new TaskpoolSequencerScope();
            using Harness harness = Harness.Make();
            harness.Main.StartSubscriptions();
            Drain(main, pool);
            harness.Settings.SetProjectFilePath(PlanFile(@"first"), bindTitleToFilename: true);
            harness.Core.IsProjectScenarioUpdated = true;
            Drain(main, pool);
            harness.Main.ProjectTitle.ShouldStartWith(@"*first - ");

            // A load clears the edits and names the new project, in that order: the title is made of both.
            harness.Settings.SetProjectFilePath(PlanFile(@"second"), bindTitleToFilename: true);
            harness.Core.IsProjectScenarioUpdated = false;
            harness.Project.IsReadyToReviseTitle = ReadyToRevise.Yes;
            Drain(main, pool);

            harness.Main.ProjectTitle.ShouldStartWith(@"second - ");
        }

        // The path of a project file in a folder, made with the separator of the platform that runs the test. The title is the
        // name of the file without its folder, and on Linux a backslash does not end a folder: a path written as C:\plans\plan.zpp
        // would be one long file name there. Nothing is read from or written to the path.
        private static string PlanFile(string name) => Path.Combine(Path.GetTempPath(), @"plans", $"{name}.zpp");

        // A main view model over a core and a project manager, with the dock, the files and the services a test never
        // reaches stood in for by objects that do nothing.
        private sealed class Harness
            : IDisposable
        {
            private readonly ProjectScenarioManagerViewModel m_Project;

            private Harness(CoreViewModel core, ISettingService settings, ProjectScenarioManagerViewModel project, MainViewModel main)
            {
                Core = core;
                Settings = settings;
                m_Project = project;
                Main = main;
            }

            public CoreViewModel Core { get; }

            public ISettingService Settings { get; }

            public ProjectScenarioManagerViewModel Project => m_Project;

            public MainViewModel Main { get; }

            public static Harness Make()
            {
                (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
                IDialogService dialogs = CoreViewModelFixture.CreateDialogService();
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
                    DoNothingProxy.Of<IProjectFileSave>(),
                    settings,
                    dialogs,
                    DoNothingProxy.Of<IServiceProvider>(),
                    NullLogger<MainViewModel>.Instance);

                return new Harness(core, settings, project, main);
            }

            public void Dispose()
            {
                m_Project.Dispose();
                Core.Dispose();
            }
        }
    }

    /// <summary>
    /// Stands in for an interface a test never reaches: every member does nothing, and gives back the default of its type.
    /// </summary>
    public class DoNothingProxy
        : DispatchProxy
    {
        public static T Of<T>()
            where T : class
        {
            return Create<T, DoNothingProxy>();
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Type returnType = targetMethod!.ReturnType;

            return returnType != typeof(void) && returnType.IsValueType
                ? Activator.CreateInstance(returnType)
                : null;
        }
    }
}
