using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Graphs.Avalonia;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// How the two graph managers keep the routing mode of a graph and the display setting of the scenario in step. A load
    /// changes the setting on its own thread, and what the graph then posts waits in a queue here, as it does for a UI thread
    /// that is busy. What waits must not write a mode back to the setting, and it must not mark the scenario as updated. Only a
    /// choice of the user does that.
    /// </summary>
    /// <remarks>
    /// The managers are not started: the setting and the graph are in step from the constructor of a manager.
    /// </remarks>
    public class GraphManagerEdgeRoutingModeTests
    {
        public enum Graph
        {
            Arrow,
            Vertex,
        }

        private static ProjectScenarioModel Scenario(EdgeRoutingMode mode) =>
            CoreViewModelFixture.CreateProjectScenario(3) with
            {
                DisplaySettings = new ProjectScenarioDisplaySettingsModel
                {
                    ArrowGraphEdgeRoutingMode = mode,
                    VertexGraphEdgeRoutingMode = mode,
                },
            };

        [Theory]
        [InlineData(Graph.Arrow)]
        [InlineData(Graph.Vertex)]
        public void Load_Then_TheGraphHasTheModeOfTheScenarioBeforeTheQueueRuns(Graph graph)
        {
            using Harness harness = Harness.Make(graph);

            harness.Core.ProcessProjectScenario(Scenario(EdgeRoutingMode.Rectilinear), Guid.NewGuid(), @"Test");

            harness.Interactive.Configuration.EdgeRoutingMode.ShouldBe(GraphEdgeRoutingMode.Rectilinear);
        }

        [Theory]
        [InlineData(Graph.Arrow)]
        [InlineData(Graph.Vertex)]
        public void Load_When_TheQueueRunsAfterIt_Then_TheSettingKeepsTheModeAndTheScenarioIsNotUpdated(Graph graph)
        {
            using Harness harness = Harness.Make(graph);

            harness.Core.ProcessProjectScenario(Scenario(EdgeRoutingMode.Rectilinear), Guid.NewGuid(), @"Test");
            harness.Dispatcher.RunPosted();

            harness.Setting.ShouldBe(EdgeRoutingMode.Rectilinear);
            harness.Interactive.EdgeRoutingMode.ShouldBe(GraphEdgeRoutingMode.Rectilinear);
            harness.Core.IsProjectScenarioUpdated.ShouldBeFalse(@"a load is not an edit");
        }

        [Theory]
        [InlineData(Graph.Arrow)]
        [InlineData(Graph.Vertex)]
        public void TwoLoads_When_TheQueueRunsAfterThem_Then_TheSettingKeepsTheLastMode(Graph graph)
        {
            using Harness harness = Harness.Make(graph);

            harness.Core.ProcessProjectScenario(Scenario(EdgeRoutingMode.Rectilinear), Guid.NewGuid(), @"First");
            harness.Core.ProcessProjectScenario(Scenario(EdgeRoutingMode.StraightLine), Guid.NewGuid(), @"Second");
            harness.Dispatcher.RunPosted();

            harness.Setting.ShouldBe(EdgeRoutingMode.StraightLine, @"what waited wrote an older mode back");
            harness.Interactive.EdgeRoutingMode.ShouldBe(GraphEdgeRoutingMode.StraightLine);
            harness.Core.IsProjectScenarioUpdated.ShouldBeFalse(@"a load is not an edit");
        }

        [Theory]
        [InlineData(Graph.Arrow)]
        [InlineData(Graph.Vertex)]
        public void ChangeEdgeRoutingModeCommand_Then_TheSettingHasTheModeAndTheScenarioIsUpdated(Graph graph)
        {
            using Harness harness = Harness.Make(graph);
            harness.Core.ProcessProjectScenario(Scenario(EdgeRoutingMode.Rectilinear), Guid.NewGuid(), @"Test");
            harness.Dispatcher.RunPosted();

            harness.Interactive.ChangeEdgeRoutingModeCommand.Execute(GraphEdgeRoutingMode.StraightLine);

            harness.Setting.ShouldBe(EdgeRoutingMode.StraightLine);
            harness.Core.IsProjectScenarioUpdated.ShouldBeTrue(@"a choice of the user is an edit");
        }

        [Theory]
        [InlineData(Graph.Arrow)]
        [InlineData(Graph.Vertex)]
        public void Load_Given_AChoiceOfTheUserBefore_When_TheQueueRunsAfterIt_Then_TheSettingKeepsTheModeOfTheScenario(Graph graph)
        {
            using Harness harness = Harness.Make(graph);
            harness.Core.ProcessProjectScenario(Scenario(EdgeRoutingMode.Rectilinear), Guid.NewGuid(), @"First");
            harness.Dispatcher.RunPosted();
            harness.Interactive.ChangeEdgeRoutingModeCommand.Execute(GraphEdgeRoutingMode.StraightLine);

            harness.Core.ProcessProjectScenario(Scenario(EdgeRoutingMode.Spline), Guid.NewGuid(), @"Second");
            harness.Dispatcher.RunPosted();

            harness.Setting.ShouldBe(EdgeRoutingMode.Spline);
            harness.Interactive.EdgeRoutingMode.ShouldBe(GraphEdgeRoutingMode.Spline);
            harness.Core.IsProjectScenarioUpdated.ShouldBeFalse(@"a load is not an edit");
        }

        [Theory]
        [InlineData(Graph.Arrow)]
        [InlineData(Graph.Vertex)]
        public void ChangeEdgeRoutingModeCommand_Given_AManagerThatIsDisposed_Then_TheSettingStays(Graph graph)
        {
            using Harness harness = Harness.Make(graph);
            harness.Core.ProcessProjectScenario(Scenario(EdgeRoutingMode.Rectilinear), Guid.NewGuid(), @"Test");
            harness.Dispatcher.RunPosted();

            harness.DisposeManager();
            harness.Interactive.ChangeEdgeRoutingModeCommand.Execute(GraphEdgeRoutingMode.StraightLine);

            harness.Setting.ShouldBe(EdgeRoutingMode.Rectilinear);
        }

        // One graph manager over a core, and a queue in the place of the UI thread.
        private sealed class Harness
            : IDisposable
        {
            private readonly IDisposable m_Manager;
            private readonly Func<EdgeRoutingMode> m_Setting;

            private Harness(
                CoreViewModel core,
                QueueingGraphDispatcher dispatcher,
                IDisposable manager,
                InteractiveGraphViewModel interactive,
                Func<EdgeRoutingMode> setting)
            {
                Core = core;
                Dispatcher = dispatcher;
                m_Manager = manager;
                Interactive = interactive;
                m_Setting = setting;
            }

            public CoreViewModel Core { get; }

            public QueueingGraphDispatcher Dispatcher { get; }

            public InteractiveGraphViewModel Interactive { get; }

            public EdgeRoutingMode Setting => m_Setting();

            public static Harness Make(Graph graph)
            {
                (CoreViewModel core, ISettingService settings) = CoreViewModelFixture.CreateWithSettingService();
                IProjectScenarioDisplaySettingsViewModel display = core.DisplaySettingsViewModel;
                var dispatcher = new QueueingGraphDispatcher();

                if (graph == Graph.Arrow)
                {
                    var manager = new ArrowGraphManagerViewModel(core, settings, CoreViewModelFixture.CreateDialogService(), new MsaglGraphLayoutEngine(), dispatcher);
                    return new Harness(core, dispatcher, manager, (InteractiveGraphViewModel)manager.Interactive, () => display.ArrowGraphEdgeRoutingMode);
                }
                else
                {
                    var manager = new VertexGraphManagerViewModel(core, settings, CoreViewModelFixture.CreateDialogService(), new MsaglGraphLayoutEngine(), dispatcher);
                    return new Harness(core, dispatcher, manager, (InteractiveGraphViewModel)manager.Interactive, () => display.VertexGraphEdgeRoutingMode);
                }
            }

            public void DisposeManager() => m_Manager.Dispose();

            public void Dispose()
            {
                m_Manager.Dispose();
                Core.Dispose();
            }
        }

        // What is posted waits until the test runs it, as it does for a UI thread that is busy.
        private sealed class QueueingGraphDispatcher
            : IGraphDispatcher
        {
            private readonly Queue<Action> m_Queue = new();

            public void Invoke(Action action) => action();

            public void Post(Action action) => m_Queue.Enqueue(action);

            public void RunPosted()
            {
                while (m_Queue.TryDequeue(out Action? action))
                {
                    action();
                }
            }
        }
    }
}
