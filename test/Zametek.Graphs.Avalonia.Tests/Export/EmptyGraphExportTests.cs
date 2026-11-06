using Avalonia.Controls;
using Avalonia.Headless;
using Shouldly;
using System.Reactive;
using System.Reactive.Linq;
using Xunit;

namespace Zametek.Graphs.Avalonia.Tests.Export
{
    // A graph with no nodes has no image. Before, Save As wrote an empty file for it, and Copy Image did nothing and said
    // nothing. Now the context menu offers neither while the graph has no nodes, and the Save commands cannot run. An export
    // of the canvas of such a graph fails with a message, and writes nothing. The message of a copy that failed names the
    // menu item that saves the image.
    [Collection("Headless rendering")]
    public class EmptyGraphExportTests
    {
        private static readonly DiagramGraphModel s_NoNodes = new();

        private static readonly DiagramGraphModel s_TwoNodes = new()
        {
            Nodes =
            [
                new DiagramNodeModel { Id = 1, Width = 40.0, Height = 40.0, Text = @"1" },
                new DiagramNodeModel { Id = 2, Width = 40.0, Height = 40.0, Text = @"2" },
            ],
            Edges =
            [
                new DiagramEdgeModel { Id = 1, SourceId = 1, TargetId = 2, StrokeThickness = 1.0 },
            ],
        };

        private readonly HeadlessUnitTestSession m_Session;

        public EmptyGraphExportTests(HeadlessSessionFixture fixture)
        {
            ArgumentNullException.ThrowIfNull(fixture);
            m_Session = fixture.Session;
        }

        [Fact]
        public void Message_ClipboardCopyFailed_Then_NamesTheMenuItemThatSavesTheImage()
        {
            // The menu item is "Save _As...": an access key, and an ellipsis that says it asks for more.
            string saveAs = Graphs_Menus.Menu_SaveAs.Replace(@"_", string.Empty, StringComparison.Ordinal).TrimEnd('.');

            Graphs_Messages.Message_ClipboardCopyFailed.ShouldContain($@"Use {saveAs} instead");
        }

        [Theory]
        [InlineData(GraphFileFormat.Png)]
        [InlineData(GraphFileFormat.Jpeg)]
        [InlineData(GraphFileFormat.Pdf)]
        [InlineData(GraphFileFormat.Svg)]
        public Task WriteImageAsync_Given_AGraphWithNoNodes_Then_FailsAndWritesNothing(GraphFileFormat format) =>
            OnTheUIThreadAsync(async () =>
            {
                var host = new TestGraphHost(s_NoNodes);
                using InteractiveGraphViewModel graph = CreateGraph(host);
                graph.Refresh();
                host.ReportedErrors.ShouldBeEmpty();
                graph.GraphNodes.ShouldBeEmpty();
                using var stream = new MemoryStream();

                InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
                    () => graph.WriteImageAsync(stream, format, GraphImageSource.InteractiveCanvas, FixedLayoutGraphType.None));

                ex.Message.ShouldBe(Graphs_Messages.Message_GraphHasNoImage);
                stream.Length.ShouldBe(0);
            });

        [Fact]
        public Task WriteImageAsync_Given_AGraphWithNodes_Then_WritesTheImage() =>
            OnTheUIThreadAsync(async () =>
            {
                var host = new TestGraphHost(s_TwoNodes);
                using InteractiveGraphViewModel graph = CreateGraph(host);
                graph.Refresh();
                host.ReportedErrors.ShouldBeEmpty();
                graph.GraphNodes.Count.ShouldBe(2);
                using var stream = new MemoryStream();

                await graph.WriteImageAsync(stream, GraphFileFormat.Svg, GraphImageSource.InteractiveCanvas, FixedLayoutGraphType.None);

                stream.Length.ShouldBeGreaterThan(0);
            });

        [Fact]
        public Task HasNodes_Given_AGraphWithNoNodes_Then_IsFalseAndTheSaveCommandsCannotRun() =>
            OnTheUIThreadAsync(() =>
            {
                var host = new TestGraphHost(s_NoNodes);
                using InteractiveGraphViewModel graph = CreateGraph(host);

                graph.Refresh();

                host.ReportedErrors.ShouldBeEmpty();
                graph.HasNodes.ShouldBeFalse();
                graph.SaveGraphImageFileCommand.CanExecute(null).ShouldBeFalse();
                graph.SaveGraphImageWithModeCommand.CanExecute(GraphExportMode.Vector).ShouldBeFalse();
                graph.SaveGraphImageWithModeCommand.CanExecute(GraphExportMode.Raster).ShouldBeFalse();
                return Task.CompletedTask;
            });

        [Fact]
        public Task HasNodes_Given_AGraphWithNodes_Then_IsTrueAndTheSaveCommandsCanRun() =>
            OnTheUIThreadAsync(() =>
            {
                var host = new TestGraphHost(s_TwoNodes);
                using InteractiveGraphViewModel graph = CreateGraph(host);

                graph.Refresh();

                host.ReportedErrors.ShouldBeEmpty();
                graph.HasNodes.ShouldBeTrue();
                graph.SaveGraphImageFileCommand.CanExecute(null).ShouldBeTrue();
                graph.SaveGraphImageWithModeCommand.CanExecute(GraphExportMode.Vector).ShouldBeTrue();
                graph.SaveGraphImageWithModeCommand.CanExecute(GraphExportMode.Raster).ShouldBeTrue();
                return Task.CompletedTask;
            });

        [Fact]
        public Task HasNodes_Given_AGraphThatLosesItsNodes_Then_IsFalseAgainAndSaysSo() =>
            OnTheUIThreadAsync(() =>
            {
                // A plan that stops compiling, for example: its graph is drawn again with no nodes.
                var host = new TestGraphHost(s_TwoNodes);
                using InteractiveGraphViewModel graph = CreateGraph(host);
                graph.Refresh();
                List<bool> changes = [];
                graph.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(InteractiveGraphViewModel.HasNodes))
                    {
                        changes.Add(graph.HasNodes);
                    }
                };

                host.Diagram = s_NoNodes;
                graph.Refresh();

                host.ReportedErrors.ShouldBeEmpty();
                graph.HasNodes.ShouldBeFalse();
                changes.ShouldBe([false]);
                graph.SaveGraphImageFileCommand.CanExecute(null).ShouldBeFalse();
                return Task.CompletedTask;
            });

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task ContextMenu_Given_AGraph_Then_OffersCopyImageAndSaveAsOnlyWhileItHasNodes(bool hasNodes) =>
            OnTheUIThreadAsync(() =>
            {
                var host = new TestGraphHost(hasNodes ? s_TwoNodes : s_NoNodes);
                using InteractiveGraphViewModel graph = CreateGraph(host);
                graph.Refresh();
                var view = new InteractiveGraphView { DataContext = graph };

                // The menu takes the data context of the control when it opens. Here, it takes it at once.
                ContextMenu menu = view.FindControl<Border>(@"viewport").ShouldNotBeNull().ContextMenu.ShouldNotBeNull();
                menu.DataContext = graph;

                ItemWithHeader(menu, Graphs_Menus.Menu_CopyImage).IsEnabled.ShouldBe(hasNodes);
                ItemWithHeader(menu, Graphs_Menus.Menu_SaveAs).IsEnabled.ShouldBe(hasNodes);
                return Task.CompletedTask;
            });

        private static MenuItem ItemWithHeader(ContextMenu menu, string header) =>
            menu.Items.OfType<MenuItem>().Single(x => Equals(x.Header, header));

        private static InteractiveGraphViewModel CreateGraph(TestGraphHost host) =>
            new(
                host,
                new MsaglGraphLayoutEngine(),
                new GraphSerializer(),
                GraphConfigurations.Vertex,
                edgeRouter: null,
                dispatcher: new InlineGraphDispatcher());

        // The view-model needs ReactiveUI, which the session's app sets up, and the views need the session's UI thread. The
        // lambda returns a value, so that the session waits for all of the test, and not only for the part before its first
        // await.
        private Task OnTheUIThreadAsync(Func<Task> test) =>
            m_Session.Dispatch(
                async () =>
                {
                    await test();
                    return 0;
                },
                CancellationToken.None);

        // A host whose diagram is the one it is given. Its rebuild notification never fires by itself, so each test decides
        // when a refresh happens.
        private sealed class TestGraphHost(DiagramGraphModel diagram)
            : IGraphHost
        {
            public DiagramGraphModel Diagram { get; set; } = diagram;

            public List<string> ReportedErrors { get; } = [];

            public GraphTheme Theme => GraphTheme.Light;

            public bool ShowNames { get; set; }

            public bool HasCompilationErrors => false;

            public DiagramGraphModel BuildDiagram(bool multiLineEdgeLabels) => Diagram;

            public IObservable<Unit> RebuildRequested => Observable.Never<Unit>();

            public Task<string?> PickSaveFileAsync() => Task.FromResult<string?>(null);

            public Task ReportErrorAsync(string message)
            {
                ReportedErrors.Add(message);
                return Task.CompletedTask;
            }
        }
    }
}
