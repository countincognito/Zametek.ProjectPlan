using Avalonia.Headless;
using Avalonia.Media;
using Shouldly;
using System.Reactive;
using System.Reactive.Linq;
using Xunit;

namespace Zametek.Graphs.Avalonia.Tests.Internal
{
    // A color of a diagram is #RRGGBB or #RRGGBBAA, with the alpha value last, as the application writes it. The README said
    // #AARRGGBB, the order of Color.Parse of Avalonia, and a text that the library could not read gave a color that is fully
    // transparent: a node that the user cannot see (from the review of src, test and pkg). Such a text now gives the fallback
    // brush of GraphAppearance.
    [Collection("Headless rendering")]
    public class ColorFormatTests
    {
        private readonly HeadlessUnitTestSession m_Session;

        public ColorFormatTests(HeadlessSessionFixture fixture)
        {
            ArgumentNullException.ThrowIfNull(fixture);
            m_Session = fixture.Session;
        }

        [Theory]
        [InlineData(@"#112233", 0xFF, 0x11, 0x22, 0x33)]
        [InlineData(@"#11223344", 0x44, 0x11, 0x22, 0x33)]
        [InlineData(@"#aabbcc80", 0x80, 0xAA, 0xBB, 0xCC)]
        public void TryHtmlHexCodeToColor_Given_AColor_Then_TheAlphaValueIsLast(string input, byte a, byte r, byte g, byte b)
        {
            ColorHelper.TryHtmlHexCodeToColor(input, out Color color).ShouldBeTrue();

            color.ShouldBe(Color.FromArgb(a, r, g, b));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(@"")]
        [InlineData(@"red")]
        [InlineData(@"112233")]
        [InlineData(@"#12345")]
        [InlineData(@"#1122334455")]
        [InlineData(@"#11223G")]
        public void TryHtmlHexCodeToColor_Given_ATextThatIsNotAColor_Then_False(string? input)
        {
            ColorHelper.TryHtmlHexCodeToColor(input, out _).ShouldBeFalse();
        }

        [Theory]
        [InlineData(@"red", true)]
        [InlineData(@"#11223344", false)]
        public Task Refresh_Given_ANodeColor_Then_TheFallbackOnlyForATextThatIsNotAColor(string fill, bool fallback) =>
            m_Session.Dispatch(
                () =>
                {
                    var appearance = new GraphAppearance();
                    var host = new TestGraphHost(new DiagramGraphModel
                    {
                        Nodes = [new DiagramNodeModel { Id = 1, Width = 40.0, Height = 40.0, Text = @"1", FillColorHexCode = fill }],
                    });
                    using var graph = new InteractiveGraphViewModel(
                        host,
                        new MsaglGraphLayoutEngine(),
                        new GraphSerializer(),
                        GraphConfigurations.Vertex,
                        appearance: appearance,
                        edgeRouter: null,
                        dispatcher: new InlineGraphDispatcher());

                    graph.Refresh();

                    IBrush brush = graph.GraphNodes.ShouldHaveSingleItem().FillBrush;

                    if (fallback)
                    {
                        brush.ShouldBeSameAs(appearance.NodeFillFallbackBrush);
                    }
                    else
                    {
                        brush.ShouldBeAssignableTo<ISolidColorBrush>().ShouldNotBeNull().Color.ShouldBe(Color.FromArgb(0x44, 0x11, 0x22, 0x33));
                    }

                    return 0;
                },
                CancellationToken.None);

        // A host whose diagram is the one it is given. Its rebuild notification never fires by itself.
        private sealed class TestGraphHost(DiagramGraphModel diagram)
            : IGraphHost
        {
            public GraphTheme Theme => GraphTheme.Light;

            public bool ShowNames { get; set; }

            public bool HasCompilationErrors => false;

            public DiagramGraphModel BuildDiagram(bool multiLineEdgeLabels) => diagram;

            public IObservable<Unit> RebuildRequested => Observable.Never<Unit>();

            public Task<string?> PickSaveFileAsync() => Task.FromResult<string?>(null);

            public Task ReportErrorAsync(string message) => Task.CompletedTask;
        }
    }
}
