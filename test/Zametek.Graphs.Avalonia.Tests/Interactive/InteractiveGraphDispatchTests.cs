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
    // happens, and what it runs into.
    [Collection("Headless rendering")]
    public class InteractiveGraphDispatchTests
    {
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

        private static InteractiveGraphViewModel CreateGraph(TestGraphHost host) =>
            CreateGraph(host, edgeRouter: null);

        private static InteractiveGraphViewModel CreateGraph(TestGraphHost host, IInteractiveEdgeRouter? edgeRouter) =>
            new(
                host,
                new MsaglGraphLayoutEngine(),
                new GraphSerializer(),
                GraphConfigurations.Vertex,
                edgeRouter,
                dispatcher: new InlineGraphDispatcher());

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
