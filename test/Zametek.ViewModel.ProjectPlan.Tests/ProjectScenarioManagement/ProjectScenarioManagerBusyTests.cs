using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the busy signal of the project scenario manager. The sections of the manager nest, and the signal changes
    /// only when the outermost one starts and when it ends.
    /// </summary>
    /// <remarks>
    /// The manager is not started: the signal does not depend on its pipelines.
    /// </remarks>
    public class ProjectScenarioManagerBusyTests
    {
        [Fact]
        public void Construction_Then_TheManagerIsNotBusy()
        {
            using Harness harness = Harness.Make();

            harness.Manager.IsBusy.ShouldBeFalse();
        }

        [Fact]
        public void ResetProject_Then_IsBusyChangesOnceOnAndOnceOff()
        {
            using Harness harness = Harness.Make();
            List<bool> busy = Watch(harness.Manager);

            // The reset resets the root node, which is a busy section inside its own.
            harness.Manager.ResetProject();

            busy.ShouldBe(new[] { true, false });
            harness.Manager.IsBusy.ShouldBeFalse();
        }

        [Fact]
        public void ResetProject_Given_TwoCalls_Then_EachChangesTheSignalOnAndOff()
        {
            using Harness harness = Harness.Make();
            List<bool> busy = Watch(harness.Manager);

            harness.Manager.ResetProject();
            harness.Manager.ResetProject();

            busy.ShouldBe(new[] { true, false, true, false });
        }

        [Fact]
        public void ResetProject_Then_TheManagerAndTheCoreCountTheirSectionsApart()
        {
            using Harness harness = Harness.Make();
            List<bool> coreBusy = [];
            harness.Core.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ICoreViewModel.IsBusy))
                {
                    coreBusy.Add(harness.Core.IsBusy);
                }
            };
            List<bool> busy = Watch(harness.Manager);

            harness.Manager.ResetProject();

            // The manager resets the core and then builds the scenario of the core, inside one section of its own. Each of
            // the two calls is a section of the core. The manager changes its signal once on and once off around both, and
            // the core changes its signal for each of them.
            busy.ShouldBe(new[] { true, false });
            coreBusy.ShouldBe(new[] { true, false, true, false });
        }

        // The value that the manager gives each time that it raises the change of IsBusy.
        private static List<bool> Watch(ProjectScenarioManagerViewModel manager)
        {
            List<bool> busy = [];

            manager.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(IProjectScenarioManagerViewModel.IsBusy))
                {
                    busy.Add(manager.IsBusy);
                }
            };

            return busy;
        }

        // A manager over a core, with the services a test never reaches stood in for.
        private sealed class Harness
            : IDisposable
        {
            private Harness(CoreViewModel core, ProjectScenarioManagerViewModel manager)
            {
                Core = core;
                Manager = manager;
            }

            public CoreViewModel Core { get; }

            public ProjectScenarioManagerViewModel Manager { get; }

            public static Harness Make()
            {
                CoreViewModelFixture.EnsureReactiveUIInitialized();

                (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
                var manager = new ProjectScenarioManagerViewModel(
                    core,
                    settings,
                    CoreViewModelFixture.CreateDialogService(),
                    new DateTimeCalculator(TimeProvider.System),
                    CoreViewModelFixture.CreateUIDispatcher());

                return new Harness(core, manager);
            }

            public void Dispose()
            {
                Manager.Dispose();
                Core.Dispose();
            }
        }
    }
}
