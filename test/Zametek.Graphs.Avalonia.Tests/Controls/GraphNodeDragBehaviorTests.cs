using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using Shouldly;
using Xunit;

namespace Zametek.Graphs.Avalonia.Tests.Controls
{
    // A drag of a node ends when the user releases the button. If the window loses the pointer capture during the drag, for
    // example with Alt+Tab, no release arrives: the node then followed the pointer with no button down (from the review of
    // src, test and pkg). A lost capture now stops the drag, and puts the node back where the drag began.
    [Collection("Headless rendering")]
    public class GraphNodeDragBehaviorTests
    {
        private readonly HeadlessUnitTestSession m_Session;

        public GraphNodeDragBehaviorTests(HeadlessSessionFixture fixture)
        {
            m_Session = fixture.Session;
        }

        [Fact]
        public async Task A_drag_that_loses_the_pointer_capture_stops_and_puts_the_node_back()
        {
            (double X, double Y, double AfterMoveX, bool FollowsAfterLoss) result = await m_Session.Dispatch(() =>
            {
                (Window window, Border border, GraphNodeViewModel node, Func<IPointer?> pointer) = Show();

                window.MouseDown(new Point(20, 20), MouseButton.Left);
                window.MouseMove(new Point(50, 60));
                double afterMoveX = node.X;

                // The window loses the capture, as with Alt+Tab: no release arrives.
                pointer()?.Capture(null);
                window.MouseMove(new Point(80, 90));
                bool follows = node.X != 10.0;

                window.Close();
                return (node.X, node.Y, afterMoveX, follows);
            }, CancellationToken.None);

            result.AfterMoveX.ShouldBe(40.0);
            result.X.ShouldBe(10.0);
            result.Y.ShouldBe(10.0);
            result.FollowsAfterLoss.ShouldBeFalse();
        }

        [Fact]
        public async Task A_drag_that_ends_with_a_release_keeps_the_new_place()
        {
            (double X, double Y) result = await m_Session.Dispatch(() =>
            {
                (Window window, _, GraphNodeViewModel node, _) = Show();

                window.MouseDown(new Point(20, 20), MouseButton.Left);
                window.MouseMove(new Point(50, 60));
                window.MouseUp(new Point(50, 60), MouseButton.Left);

                window.Close();
                return (node.X, node.Y);
            }, CancellationToken.None);

            result.ShouldBe((40.0, 50.0));
        }

        // A window with a canvas and one node at (10, 10), 40 by 40, with the behavior of the drag. The pointer of the last
        // press, for a test that takes the capture away.
        private static (Window Window, Border Border, GraphNodeViewModel Node, Func<IPointer?> Pointer) Show()
        {
            var node = new GraphNodeViewModel(
                new GraphNodeLayoutModel { Id = 1, X = 10.0, Y = 10.0, Width = 40.0, Height = 40.0, Label = @"1" },
                GraphAppearance.Default);
            var border = new Border { Width = 40.0, Height = 40.0, DataContext = node, Background = global::Avalonia.Media.Brushes.Gray };
            Canvas.SetLeft(border, 10.0);
            Canvas.SetTop(border, 10.0);
            Interaction.GetBehaviors(border).Add(new GraphNodeDragBehavior());

            IPointer? pressed = null;
            border.AddHandler(InputElement.PointerPressedEvent, (_, e) => pressed = e.Pointer, RoutingStrategies.Bubble, handledEventsToo: true);

            var window = new Window { Width = 200, Height = 200, Content = new Canvas { Children = { border } } };
            window.Show();

            return (window, border, node, () => pressed);
        }
    }
}
