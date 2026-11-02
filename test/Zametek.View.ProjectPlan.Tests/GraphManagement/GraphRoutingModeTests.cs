using Avalonia.Threading;
using Shouldly;
using System.Collections.Concurrent;
using System.ComponentModel;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Graphs.Avalonia;

namespace Zametek.View.ProjectPlan.Tests
{
    /// <summary>
    /// A scenario holds a routing mode for each graph. A load applies them on its own thread, while the UI thread can rebuild
    /// the edges of a graph. When the load also changed the edges, the two threads met, and the load failed with the message
    /// "Collection was modified". These tests work the real application, and its graphs.
    /// </summary>
    [Collection(ApplicationCollection.Name)]
    public class GraphRoutingModeTests
    {
        // The modes that the plan of the fixture holds.
        private const GraphEdgeRoutingMode c_ArrowMode = GraphEdgeRoutingMode.Spline;
        private const GraphEdgeRoutingMode c_VertexMode = GraphEdgeRoutingMode.SugiyamaSplines;

        private readonly ApplicationFixture m_Fixture;

        public GraphRoutingModeTests(ApplicationFixture fixture)
        {
            m_Fixture = fixture;
        }

        [Fact]
        public Task Load_Then_TheEdgesOfTheGraphsChangeOnTheUIThreadOnly() =>
            m_Fixture.RunAsync(async app =>
            {
                IManagedNodeViewModel duplicate = await app.DuplicateAsync(app.Scenarios[0]);
                await app.GraphsShouldShowAsync(c_ArrowMode, c_VertexMode);

                ConcurrentQueue<string> offTheUIThread = [];

                void Watch(object? sender, PropertyChangedEventArgs e)
                {
                    if (!Dispatcher.UIThread.CheckAccess())
                    {
                        offTheUIThread.Enqueue($@"{sender?.GetType().Name}.{e.PropertyName}");
                    }
                }

                List<INotifyPropertyChanged> watched = [app.ArrowGraph, app.VertexGraph, .. app.ArrowGraph.GraphEdges, .. app.VertexGraph.GraphEdges];
                watched.ForEach(x => x.PropertyChanged += Watch);

                try
                {
                    await app.LoadAsync(duplicate);
                    await app.GraphsShouldShowAsync(c_ArrowMode, c_VertexMode);
                }
                finally
                {
                    watched.ForEach(x => x.PropertyChanged -= Watch);
                }

                offTheUIThread.ShouldBeEmpty();
            });

        [Fact]
        public Task Load_Given_AChoiceOfTheUserOnOneScenario_Then_EachScenarioKeepsItsOwnModes() =>
            m_Fixture.RunAsync(async app =>
            {
                IManagedNodeViewModel original = app.Scenarios[0];
                IManagedNodeViewModel duplicate = await app.DuplicateAsync(original);
                await app.LoadAsync(duplicate);

                app.ArrowGraph.ChangeEdgeRoutingModeCommand.Execute(GraphEdgeRoutingMode.StraightLine);
                app.VertexGraph.ChangeEdgeRoutingModeCommand.Execute(GraphEdgeRoutingMode.Rectilinear);

                app.Core.IsProjectScenarioUpdated.ShouldBeTrue(@"a choice of the user is an edit");
                await app.GraphsShouldShowAsync(GraphEdgeRoutingMode.StraightLine, GraphEdgeRoutingMode.Rectilinear);

                await app.LoadAsync(original);

                await app.GraphsShouldShowAsync(c_ArrowMode, c_VertexMode);
                app.Core.IsProjectScenarioUpdated.ShouldBeFalse(@"a load is not an edit");

                await app.LoadAsync(duplicate);

                await app.GraphsShouldShowAsync(GraphEdgeRoutingMode.StraightLine, GraphEdgeRoutingMode.Rectilinear);
                app.Core.IsProjectScenarioUpdated.ShouldBeFalse(@"a load is not an edit");

                app.SavedDisplaySettings(original).ArrowGraphEdgeRoutingMode.ShouldBe(EdgeRoutingMode.Spline);
                app.SavedDisplaySettings(original).VertexGraphEdgeRoutingMode.ShouldBe(EdgeRoutingMode.SugiyamaSplines);
                app.SavedDisplaySettings(duplicate).ArrowGraphEdgeRoutingMode.ShouldBe(EdgeRoutingMode.StraightLine);
                app.SavedDisplaySettings(duplicate).VertexGraphEdgeRoutingMode.ShouldBe(EdgeRoutingMode.Rectilinear);
            });
    }
}
