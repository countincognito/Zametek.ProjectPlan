using Shouldly;
using System;
using System.Threading.Tasks;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Graphs.Avalonia;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    public class ArrowGraphDiagramBuilderTests
    {
        [Fact]
        public async Task Build_Given_MultiLineEdgeLabels_Then_LabelLinesEndWithLf()
        {
            // The GraphML and Dot exports ask for multi-line labels, which break before the slack of each non-critical
            // activity. The break is "\n" on every platform, so that the exports come out the same on Windows as on
            // Linux. Windows' own line end is "\r\n", which is where this test has teeth.
            using CoreViewModel core = CoreViewModelFixture.Create();
            ProjectScenarioModel scenario = await CoreViewModelFixture.LoadProjectScenarioAsync(@"sample_v0_6_1.zpp");
            core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"LineEnds");
            core.BuildArrowGraph();

            DiagramGraphModel diagram = ArrowGraphDiagramBuilder.Build(core.ArrowGraph, multiLineEdgeLabels: true, viewNames: false);

            // Only worth anything if some label actually breaks.
            diagram.Edges.ShouldContain(x => (x.Label ?? string.Empty).Contains('\n'));
            diagram.Edges.ShouldAllBe(x => !(x.Label ?? string.Empty).Contains('\r'));
        }
    }
}
