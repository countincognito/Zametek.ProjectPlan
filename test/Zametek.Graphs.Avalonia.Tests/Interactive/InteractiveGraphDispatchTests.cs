using Avalonia.Headless;
using Shouldly;
using System.Reactive;
using System.Reactive.Linq;
using Xunit;

namespace Zametek.Graphs.Avalonia.Tests
{
    // How InteractiveGraphViewModel.Refresh behaves for a host with no UI thread: through the inline
    // dispatcher the graph is populated, and an error reported, on the thread that asked, before Refresh
    // returns, and once the graph is disposed a refresh still on its way builds nothing, populates
    // nothing and reports nothing. The session is here only because the view-model needs ReactiveUI,
    // which the session's app sets up when it first runs something; every test then runs on a pool
    // thread, not the session's UI thread. (That alone does not tell the two dispatchers apart for a
    // populate: in a headless session Avalonia's Invoke runs a pool thread's work where it stands. A
    // posted error report does go to the session, which is what the failing-layout test would catch.)
    // The host's rebuild notification never fires by itself, so each test decides when a refresh
    // happens, and what it runs into. A host applies a saved routing mode on the thread of its load, and
    // the tests of that hold what the graph posts in a queue, which stands in for a UI thread that is busy:
    // the configuration must have the mode at once, and the edges, which belong to the UI thread, must wait.
    [Collection("Headless rendering")]
    public class InteractiveGraphDispatchTests
    {
        private static readonly GraphEdgeRoutingMode s_PresetMode = GraphConfigurations.Vertex.EdgeRoutingMode;
        private const GraphEdgeRoutingMode c_OtherMode = GraphEdgeRoutingMode.Rectilinear;

        private static readonly DiagramGraphModel s_Diagram = new()
        {
            Nodes =
            [
                new DiagramNodeModel { Id = 1, Width = 40.0, Height = 40.0, Text = @"1" },
                new DiagramNodeModel { Id = 2, Width = 40.0, Height = 40.0, Text = @"2" },
                new DiagramNodeModel { Id = 3, Width = 40.0, Height = 40.0, Text = @"3" },
            ],
            Edges =
            [
                new DiagramEdgeModel { Id = 1, SourceId = 1, TargetId = 2, StrokeThickness = 1.0 },
                new DiagramEdgeModel { Id = 2, SourceId = 2, TargetId = 3, StrokeThickness = 1.0 },
            ],
        };

        private readonly HeadlessUnitTestSession m_Session;

        public InteractiveGraphDispatchTests(HeadlessSessionFixture fixture)
        {
            ArgumentNullException.ThrowIfNull(fixture);
            m_Session = fixture.Session;
        }

        [Fact]
        public Task Refresh_Given_AnInlineDispatcher_Then_TheGraphIsPopulatedOnTheCallingThread() =>
            OffTheUIThreadAsync(() =>
            {
                var host = new TestGraphHost();
                using InteractiveGraphViewModel graph = CreateGraph(host);
                int? refreshedOn = null;
                graph.GraphRefreshed += (_, _) => refreshedOn = Environment.CurrentManagedThreadId;

                graph.Refresh();

                host.ReportedErrors.ShouldBeEmpty();
                refreshedOn.ShouldBe(Environment.CurrentManagedThreadId);
                graph.GraphNodes.Select(x => x.Id).ShouldBe([1, 2, 3], ignoreOrder: true);
                graph.GraphEdges.Select(x => x.Id).ShouldBe([1, 2], ignoreOrder: true);
            });

        [Fact]
        public Task Refresh_Given_ALayoutThatFails_Then_TheErrorIsReportedOnTheCallingThread() =>
            OffTheUIThreadAsync(() =>
            {
                var host = new TestGraphHost { OnBuildDiagram = _ => throw new InvalidOperationException(@"no layout") };
                using InteractiveGraphViewModel graph = CreateGraph(host);

                graph.Refresh();

                host.ReportedErrors.ShouldBe([@"no layout"]);
                host.ReportedOn.ShouldBe([Environment.CurrentManagedThreadId]);
                graph.GraphNodes.ShouldBeEmpty();
            });

        [Fact]
        public Task Refresh_Given_ADisposedGraph_Then_NothingIsBuilt() =>
            OffTheUIThreadAsync(() =>
            {
                var host = new TestGraphHost();
                InteractiveGraphViewModel graph = CreateGraph(host);
                graph.Dispose();

                graph.Refresh();

                host.BuildDiagramCalls.ShouldBe(0);
                host.ReportedErrors.ShouldBeEmpty();
                graph.GraphNodes.ShouldBeEmpty();
            });

        [Fact]
        public Task Refresh_Given_TheGraphIsDisposedDuringTheLayout_Then_NothingIsPopulated() =>
            OffTheUIThreadAsync(() =>
            {
                var host = new TestGraphHost();
                InteractiveGraphViewModel graph = CreateGraph(host);
                host.OnBuildDiagram = diagram =>
                {
                    graph.Dispose();
                    return diagram;
                };

                graph.Refresh();

                host.BuildDiagramCalls.ShouldBe(1);
                host.ReportedErrors.ShouldBeEmpty();
                graph.GraphNodes.ShouldBeEmpty();
            });

        [Fact]
        public Task Refresh_Given_TheGraphIsDisposedBeforeTheLayoutFails_Then_NothingIsReported() =>
            OffTheUIThreadAsync(() =>
            {
                var host = new TestGraphHost();
                InteractiveGraphViewModel graph = CreateGraph(host);
                host.OnBuildDiagram = _ =>
                {
                    graph.Dispose();
                    throw new InvalidOperationException(@"no layout");
                };

                graph.Refresh();

                host.BuildDiagramCalls.ShouldBe(1);
                host.ReportedErrors.ShouldBeEmpty();
            });

        [Fact]
        public Task Refresh_Given_RoutingThatFails_Then_TheErrorIsReportedOnTheCallingThread() =>
            OffTheUIThreadAsync(() =>
            {
                var host = new TestGraphHost();
                using InteractiveGraphViewModel graph = CreateGraph(
                    host,
                    new TestEdgeRouter(() => throw new InvalidOperationException(@"no route")));

                graph.Refresh();

                host.ReportedErrors.ShouldBe([@"no route"]);
                host.ReportedOn.ShouldBe([Environment.CurrentManagedThreadId]);
            });

        [Fact]
        public Task Refresh_Given_TheGraphIsDisposedBeforeRoutingFails_Then_NothingIsReported() =>
            OffTheUIThreadAsync(() =>
            {
                var host = new TestGraphHost();
                InteractiveGraphViewModel? graph = null;
                graph = CreateGraph(
                    host,
                    new TestEdgeRouter(() =>
                    {
                        graph!.Dispose();
                        throw new InvalidOperationException(@"no route");
                    }));

                graph.Refresh();

                host.ReportedErrors.ShouldBeEmpty();
            });

        [Fact]
        public Task ApplyEdgeRoutingMode_Given_ADispatcherThatWaits_Then_TheConfigurationHasTheModeAtOnce() =>
            OffTheUIThreadAsync(() =>
            {
                var dispatcher = new QueueingGraphDispatcher();
                using InteractiveGraphViewModel graph = CreateGraph(new TestGraphHost(), new RecordingEdgeRouter(), dispatcher);

                graph.ApplyEdgeRoutingMode(c_OtherMode);

                graph.Configuration.EdgeRoutingMode.ShouldBe(c_OtherMode);
                graph.EdgeRoutingMode.ShouldBe(c_OtherMode);
            });

        [Fact]
        public Task ApplyEdgeRoutingMode_Given_ADispatcherThatWaits_Then_TheEdgesWaitForIt() =>
            OffTheUIThreadAsync(() =>
            {
                var dispatcher = new QueueingGraphDispatcher();
                using InteractiveGraphViewModel graph = CreateGraph(new TestGraphHost(), new RecordingEdgeRouter(), dispatcher);
                graph.Refresh();

                graph.ApplyEdgeRoutingMode(c_OtherMode);

                graph.GraphEdges.Select(x => x.RoutingMode).ShouldBe([s_PresetMode, s_PresetMode], @"an edge changed on the thread of the load");

                dispatcher.RunPosted();

                graph.GraphEdges.Select(x => x.RoutingMode).ShouldBe([c_OtherMode, c_OtherMode]);
            });

        [Fact]
        public Task ApplyEdgeRoutingMode_Given_ADispatcherThatWaits_Then_TheChangeIsRaisedWhenItRuns() =>
            OffTheUIThreadAsync(() =>
            {
                var dispatcher = new QueueingGraphDispatcher();
                using InteractiveGraphViewModel graph = CreateGraph(new TestGraphHost(), new RecordingEdgeRouter(), dispatcher);
                int raises = 0;
                graph.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(InteractiveGraphViewModel.EdgeRoutingMode))
                    {
                        raises++;
                    }
                };

                graph.ApplyEdgeRoutingMode(c_OtherMode);

                raises.ShouldBe(0, @"the change was raised on the thread of the load");

                dispatcher.RunPosted();

                raises.ShouldBe(1);
            });

        [Fact]
        public Task ApplyEdgeRoutingMode_Given_ADispatcherThatWaits_Then_TheEdgesAreRoutedWhenItRuns() =>
            OffTheUIThreadAsync(() =>
            {
                var dispatcher = new QueueingGraphDispatcher();
                var router = new RecordingEdgeRouter();
                using InteractiveGraphViewModel graph = CreateGraph(new TestGraphHost(), router, dispatcher);
                graph.Refresh();
                router.Requests.Clear();

                graph.ApplyEdgeRoutingMode(c_OtherMode);

                router.Requests.ShouldBeEmpty(@"the edges were read on the thread of the load");

                dispatcher.RunPosted();

                router.Requests.ShouldHaveSingleItem().Mode.ShouldBe(c_OtherMode);
            });

        [Fact]
        public Task ApplyEdgeRoutingMode_Given_TwoModesBeforeItRuns_Then_TheLastWins() =>
            OffTheUIThreadAsync(() =>
            {
                var dispatcher = new QueueingGraphDispatcher();
                var router = new RecordingEdgeRouter();
                using InteractiveGraphViewModel graph = CreateGraph(new TestGraphHost(), router, dispatcher);
                graph.Refresh();
                router.Requests.Clear();

                graph.ApplyEdgeRoutingMode(GraphEdgeRoutingMode.None);
                graph.ApplyEdgeRoutingMode(c_OtherMode);

                dispatcher.RunPosted();

                graph.EdgeRoutingMode.ShouldBe(c_OtherMode);
                graph.GraphEdges.Select(x => x.RoutingMode).ShouldBe([c_OtherMode, c_OtherMode]);
                router.Requests.Select(x => x.Mode).ShouldAllBe(x => x == c_OtherMode, @"a posted change must read the mode when it runs");
            });

        [Fact]
        public Task ApplyEdgeRoutingMode_Given_TheModeItHas_Then_NothingIsPosted() =>
            OffTheUIThreadAsync(() =>
            {
                var dispatcher = new QueueingGraphDispatcher();
                using InteractiveGraphViewModel graph = CreateGraph(new TestGraphHost(), new RecordingEdgeRouter(), dispatcher);

                graph.ApplyEdgeRoutingMode(s_PresetMode);

                dispatcher.Posted.ShouldBe(0);
            });

        [Fact]
        public Task ApplyEdgeRoutingMode_Given_TheGraphIsDisposedBeforeItRuns_Then_NothingHappens() =>
            OffTheUIThreadAsync(() =>
            {
                var dispatcher = new QueueingGraphDispatcher();
                var router = new RecordingEdgeRouter();
                InteractiveGraphViewModel graph = CreateGraph(new TestGraphHost(), router, dispatcher);
                graph.Refresh();
                router.Requests.Clear();

                graph.ApplyEdgeRoutingMode(c_OtherMode);
                graph.Dispose();
                dispatcher.RunPosted();

                graph.GraphEdges.Select(x => x.RoutingMode).ShouldBe([s_PresetMode, s_PresetMode]);
                router.Requests.ShouldBeEmpty();
            });

        [Fact]
        public Task ApplyEdgeRoutingMode_Given_AnInlineDispatcher_Then_TheEdgesHaveTheModeWhenItReturns() =>
            OffTheUIThreadAsync(() =>
            {
                using InteractiveGraphViewModel graph = CreateGraph(new TestGraphHost());
                graph.Refresh();

                graph.ApplyEdgeRoutingMode(c_OtherMode);

                graph.GraphEdges.Select(x => x.RoutingMode).ShouldBe([c_OtherMode, c_OtherMode]);
            });

        [Fact]
        public Task Refresh_Given_AModeThatIsPostedStill_Then_TheLayoutAndTheNewEdgesHaveIt() =>
            OffTheUIThreadAsync(() =>
            {
                var dispatcher = new QueueingGraphDispatcher();
                var engine = new RecordingLayoutEngine();
                using var graph = new InteractiveGraphViewModel(
                    new TestGraphHost(),
                    engine,
                    new GraphSerializer(),
                    GraphConfigurations.Vertex,
                    new RecordingEdgeRouter(),
                    dispatcher: dispatcher);

                graph.ApplyEdgeRoutingMode(c_OtherMode);
                graph.Refresh();

                engine.Modes.ShouldHaveSingleItem().ShouldBe(c_OtherMode);
                graph.GraphEdges.Select(x => x.RoutingMode).ShouldBe([c_OtherMode, c_OtherMode]);
            });

        [Fact]
        public Task ApplyEdgeRoutingMode_Then_TheEventOfTheUserIsNotRaised() =>
            OffTheUIThreadAsync(() =>
            {
                var dispatcher = new QueueingGraphDispatcher();
                using InteractiveGraphViewModel graph = CreateGraph(new TestGraphHost(), new RecordingEdgeRouter(), dispatcher);
                int raises = 0;
                graph.EdgeRoutingModeChanged += (_, _) => raises++;

                graph.ApplyEdgeRoutingMode(c_OtherMode);
                dispatcher.RunPosted();

                raises.ShouldBe(0, @"a saved mode must not go back to the host as a choice of the user");
            });

        [Fact]
        public Task ChangeEdgeRoutingModeCommand_Then_TheEdgesHaveTheModeAndTheEventIsRaisedOnce() =>
            OffTheUIThreadAsync(() =>
            {
                var dispatcher = new QueueingGraphDispatcher();
                var router = new RecordingEdgeRouter();
                using InteractiveGraphViewModel graph = CreateGraph(new TestGraphHost(), router, dispatcher);
                graph.Refresh();
                router.Requests.Clear();
                List<GraphEdgeRoutingMode> raised = [];
                graph.EdgeRoutingModeChanged += (_, _) => raised.Add(graph.EdgeRoutingMode);

                // A click: the UI thread itself, and so nothing waits.
                graph.ChangeEdgeRoutingModeCommand.Execute(c_OtherMode);

                dispatcher.Posted.ShouldBe(0);
                graph.GraphEdges.Select(x => x.RoutingMode).ShouldBe([c_OtherMode, c_OtherMode]);
                router.Requests.ShouldHaveSingleItem().Mode.ShouldBe(c_OtherMode);
                raised.ShouldBe([c_OtherMode]);
            });

        [Fact]
        public Task ChangeEdgeRoutingModeCommand_Given_TheModeItHas_Then_TheEventIsNotRaised() =>
            OffTheUIThreadAsync(() =>
            {
                using InteractiveGraphViewModel graph = CreateGraph(new TestGraphHost(), new RecordingEdgeRouter(), new QueueingGraphDispatcher());
                int raises = 0;
                graph.EdgeRoutingModeChanged += (_, _) => raises++;

                graph.ChangeEdgeRoutingModeCommand.Execute(s_PresetMode);

                raises.ShouldBe(0);
            });

        private static InteractiveGraphViewModel CreateGraph(TestGraphHost host) =>
            CreateGraph(host, edgeRouter: null);

        private static InteractiveGraphViewModel CreateGraph(
            TestGraphHost host,
            IInteractiveEdgeRouter? edgeRouter,
            IGraphDispatcher? dispatcher = null) =>
            new(
                host,
                new MsaglGraphLayoutEngine(),
                new GraphSerializer(),
                GraphConfigurations.Vertex,
                edgeRouter,
                dispatcher: dispatcher ?? new InlineGraphDispatcher());

        // Starts the session's app first - which is what sets ReactiveUI up - then runs the test on a pool
        // thread, which is not the session's UI thread.
        private async Task OffTheUIThreadAsync(Action test)
        {
            int uiThread = await m_Session.Dispatch(() => Environment.CurrentManagedThreadId, CancellationToken.None);
            await Task.Run(() =>
            {
                Environment.CurrentManagedThreadId.ShouldNotBe(uiThread);
                test();
            });
        }

        // Routes nothing: fails the way it is told to, at once, so the failure reaches the view-model
        // before Refresh returns.
        private sealed class TestEdgeRouter(Func<IReadOnlyList<RoutedEdge>> route)
            : IInteractiveEdgeRouter
        {
            public Task<IReadOnlyList<RoutedEdge>> RouteAsync(EdgeRoutingRequest request, CancellationToken cancellationToken)
            {
                try
                {
                    return Task.FromResult(route());
                }
                catch (Exception ex)
                {
                    return Task.FromException<IReadOnlyList<RoutedEdge>>(ex);
                }
            }
        }

        // Routes nothing, and remembers what it was asked to route, so a test can see when the graph read
        // its edges for a route.
        private sealed class RecordingEdgeRouter
            : IInteractiveEdgeRouter
        {
            public List<EdgeRoutingRequest> Requests { get; } = [];

            public Task<IReadOnlyList<RoutedEdge>> RouteAsync(EdgeRoutingRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult<IReadOnlyList<RoutedEdge>>([]);
            }
        }

        // The real layout, and a note of the mode that it was given.
        private sealed class RecordingLayoutEngine
            : IGraphLayoutEngine
        {
            private readonly MsaglGraphLayoutEngine m_Engine = new();

            public List<GraphEdgeRoutingMode> Modes { get; } = [];

            public GraphLayoutModel BuildLayout(DiagramGraphModel diagramGraph, GraphConfiguration configuration, GraphTheme theme)
            {
                Modes.Add(configuration.EdgeRoutingMode);
                return m_Engine.BuildLayout(diagramGraph, configuration, theme);
            }

            public byte[] RenderSvg(DiagramGraphModel diagramGraph, GraphConfiguration configuration, GraphTheme theme) =>
                m_Engine.RenderSvg(diagramGraph, configuration, theme);
        }

        // What is posted waits until the test runs it, as it does for a UI thread that is busy. Invoke runs
        // at once, so that a refresh has populated the graph when it returns.
        private sealed class QueueingGraphDispatcher
            : IGraphDispatcher
        {
            private readonly Queue<Action> m_Queue = new();

            public int Posted => m_Queue.Count;

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

        private sealed class TestGraphHost
            : IGraphHost
        {
            private int m_BuildDiagramCalls;

            public Func<DiagramGraphModel, DiagramGraphModel> OnBuildDiagram { get; set; } = x => x;

            public int BuildDiagramCalls => m_BuildDiagramCalls;

            public List<string> ReportedErrors { get; } = [];

            public List<int> ReportedOn { get; } = [];

            public GraphTheme Theme => GraphTheme.Light;

            public bool ShowNames { get; set; }

            public bool HasCompilationErrors => false;

            public DiagramGraphModel BuildDiagram(bool multiLineEdgeLabels)
            {
                Interlocked.Increment(ref m_BuildDiagramCalls);
                return OnBuildDiagram(s_Diagram);
            }

            public IObservable<Unit> RebuildRequested => Observable.Never<Unit>();

            public Task<string?> PickSaveFileAsync() => Task.FromResult<string?>(null);

            public Task ReportErrorAsync(string message)
            {
                ReportedErrors.Add(message);
                ReportedOn.Add(Environment.CurrentManagedThreadId);
                return Task.CompletedTask;
            }
        }
    }
}
