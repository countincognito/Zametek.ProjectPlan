using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    public class PlantUmlExportServiceTests
    {
        private static DependentActivityModel AnActivity(int id, string name, int duration, List<int>? dependencies = null, List<int>? targetWorkStreams = null)
        {
            return new DependentActivityModel
            {
                Activity = new ActivityModel
                {
                    Id = id,
                    Name = name,
                    Duration = duration,
                    TargetWorkStreams = targetWorkStreams ?? [],
                },
                Dependencies = dependencies ?? [],
            };
        }

        private static WorkStreamSettingsModel MakeWorkStreamSettings(params WorkStreamModel[] workStreams)
        {
            return new WorkStreamSettingsModel { WorkStreams = workStreams.ToList() };
        }

        private static WorkStreamModel MakeWorkStream(int id, byte r, byte g, byte b, byte a = 255)
        {
            return new WorkStreamModel
            {
                Id = id,
                Name = $"WS{id}",
                ColorFormat = new ColorFormatModel { R = r, G = g, B = b, A = a },
            };
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldReturnStartToEnd_WhenEmptyList()
        {
            // Given: An empty list of activities
            var activities = new List<DependentActivityModel>();

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Should contain skinparams, labeled start/end nodes connected
            result.ShouldBe("@startuml\ntop to bottom direction\nskinparam nodesep 25\nskinparam ranksep 45\ncircle \"Project Start\" as start #Black\ncircle \"Project Finish\" as end #Black\nstart --> end\n@enduml");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldGenerateSingleActivity_WhenOneActivityProvided()
        {
            // Given: A single activity with no dependencies
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "Task A", 5),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Should have start → activity → end
            result.ShouldContain("rectangle \"Task A\\n(5d)\" as ID1");
            result.ShouldContain("start --> ID1");
            result.ShouldContain("ID1 --> end");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldGenerateLinearChain_WhenActivitiesFormSequence()
        {
            // Given: Three activities forming A → B → C
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "A", 2),
                AnActivity(2, "B", 3, [1]),
                AnActivity(3, "C", 1, [2]),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Correct arrow chain from start through A, B, C to end
            result.ShouldContain("start --> ID3");
            result.ShouldContain("ID2 --> ID1");
            result.ShouldContain("ID3 --> ID2");
            result.ShouldContain("ID1 --> end");
            result.ShouldNotContain("start --> ID1");
            result.ShouldNotContain("start --> ID2");
            result.ShouldNotContain("ID2 --> end");
            result.ShouldNotContain("ID3 --> end");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldGenerateParallelActivities_WhenNoDependenciesBetweenThem()
        {
            // Given: Two independent activities
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "Alpha", 4),
                AnActivity(2, "Beta", 6),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Both connect from start and to end
            result.ShouldContain("start --> ID1");
            result.ShouldContain("start --> ID2");
            result.ShouldContain("ID1 --> end");
            result.ShouldContain("ID2 --> end");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldHandleIsolatedActivityAmongConnected()
        {
            // Given: A chain A→B and an isolated activity C
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "A", 2),
                AnActivity(2, "B", 3, [1]),
                AnActivity(3, "Isolated", 1),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Isolated activity connects start→C→end, chain is correct
            result.ShouldContain("start --> ID3");
            result.ShouldContain("ID3 --> end");
            result.ShouldContain("start --> ID2");
            result.ShouldContain("ID2 --> ID1");
            result.ShouldContain("ID1 --> end");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldFormatLabelCorrectly_WithNameAndDuration()
        {
            // Given: An activity with specific name and duration
            var activities = new List<DependentActivityModel>
            {
                AnActivity(42, "Design Phase", 10),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Label should be formatted as "Name\n(Xd)" with correct alias
            result.ShouldContain("rectangle \"Design Phase\\n(10d)\" as ID42");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldEscapeQuotesInActivityName()
        {
            // Given: An activity with quotes in the name
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "Task \"Important\"", 3),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Quotes should be escaped with backslash
            result.ShouldContain("rectangle \"Task \\\"Important\\\"\\n(3d)\" as ID1");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldStartWithStartumlAndEndWithEnduml()
        {
            // Given: Any list of activities
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "X", 1),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Should have proper PlantUML delimiters
            result.ShouldStartWith("@startuml\n");
            result.ShouldEndWith("\n@enduml");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldDeclareStartAndEndCircles()
        {
            // Given: Any list of activities
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "X", 1),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Should declare labeled start and end circles with black color
            result.ShouldContain("circle \"Project Finish\" as start #Black");
            result.ShouldContain("circle \"Project Start\" as end #Black");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldOrderTopologically_WhenPrerequisitesBeforeDependents()
        {
            // Given: Activities provided in reverse order (C depends on B depends on A)
            var activities = new List<DependentActivityModel>
            {
                AnActivity(3, "C", 1, [2]),
                AnActivity(1, "A", 2),
                AnActivity(2, "B", 3, [1]),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Rectangle declarations should appear in topological order (A before B before C)
            int posA = result.IndexOf("as ID1");
            int posB = result.IndexOf("as ID2");
            int posC = result.IndexOf("as ID3");
            posA.ShouldBeLessThan(posB);
            posB.ShouldBeLessThan(posC);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldHandleDiamondDependency()
        {
            // Given: Diamond pattern: A → B, A → C, B → D, C → D
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "A", 1),
                AnActivity(2, "B", 2, [1]),
                AnActivity(3, "C", 3, [1]),
                AnActivity(4, "D", 1, [2, 3]),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Correct arrows for diamond pattern
            result.ShouldContain("start --> ID4");
            result.ShouldContain("ID2 --> ID1");
            result.ShouldContain("ID3 --> ID1");
            result.ShouldContain("ID4 --> ID3");
            result.ShouldContain("ID4 --> ID3");
            result.ShouldContain("ID1 --> end");
            // A, B, C should NOT connect to end; only D does
            result.ShouldNotContain("ID2 --> end");
            result.ShouldNotContain("ID3 --> end");
            result.ShouldNotContain("ID4 --> end");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldColorActivity_WhenWorkStreamAssigned()
        {
            // Given: An activity assigned to WorkStream 1 with color #ffca08
            var ws = MakeWorkStream(1, 255, 202, 8);
            var settings = MakeWorkStreamSettings(ws);
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "Task", 5, targetWorkStreams: [1]),
            };

            // When: Generating PlantUML with work stream settings
            string result = PlantUmlExportService.GeneratePlantUml(activities, settings);

            // Then: Activity rectangle should include the hex color suffix
            result.ShouldContain("rectangle \"Task\\n(5d)\" as ID1 #ffca08");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldNotColorActivity_WhenNoWorkStreamAssigned()
        {
            // Given: An activity with no work stream assignment and no name-based color match
            var ws = MakeWorkStream(1, 255, 202, 8);
            var settings = MakeWorkStreamSettings(ws);
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "Task", 5),
            };

            // When: Generating PlantUML with work stream settings
            string result = PlantUmlExportService.GeneratePlantUml(activities, settings);

            // Then: Activity rectangle should NOT have a color suffix
            result.ShouldContain("rectangle \"Task\\n(5d)\" as ID1\n");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldRenderMilestoneAsCircle_WhenDurationIsZero()
        {
            // Given: A milestone activity with duration 0
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "Milestone", 0),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Should render as circle with milestone color, without duration label
            result.ShouldContain("circle \"Milestone\" as ID1 #cccccc");
            result.ShouldNotContain("rectangle");
            result.ShouldNotContain("(0d)");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldColorMilestone_WhenWorkStreamAssigned()
        {
            // Given: A milestone assigned to WorkStream 2 with a non-grey color
            var ws = MakeWorkStream(2, 255, 0, 0);
            var settings = MakeWorkStreamSettings(ws);
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "Milestone", 0, targetWorkStreams: [2]),
            };

            // When: Generating PlantUML with work stream settings
            string result = PlantUmlExportService.GeneratePlantUml(activities, settings);

            // Then: Milestone always gets #cccccc regardless of WorkStream color
            result.ShouldContain("circle \"Milestone\" as ID1 #cccccc");
            result.ShouldNotContain("#ff0000");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldUseFirstWorkStream_WhenMultipleAssigned()
        {
            // Given: An activity assigned to two work streams with different colors
            var ws1 = MakeWorkStream(1, 255, 0, 0);
            var ws2 = MakeWorkStream(2, 0, 0, 255);
            var settings = MakeWorkStreamSettings(ws1, ws2);
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "Task", 5, targetWorkStreams: [1, 2]),
            };

            // When: Generating PlantUML with work stream settings
            string result = PlantUmlExportService.GeneratePlantUml(activities, settings);

            // Then: Should use the color from the first assigned work stream (id=1, red)
            result.ShouldContain("rectangle \"Task\\n(5d)\" as ID1 #ff0000");
            result.ShouldNotContain("#0000ff");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldIgnoreUnknownWorkStreamId()
        {
            // Given: An activity assigned to a work stream id that doesn't exist in settings
            var ws = MakeWorkStream(1, 255, 0, 0);
            var settings = MakeWorkStreamSettings(ws);
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "Task", 5, targetWorkStreams: [99]),
            };

            // When: Generating PlantUML with work stream settings
            string result = PlantUmlExportService.GeneratePlantUml(activities, settings);

            // Then: Activity should render without any color suffix
            result.ShouldContain("rectangle \"Task\\n(5d)\" as ID1\n");
        }

        // === NEW TESTS ===

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldIncludeSkinparams()
        {
            // Given: Any list of activities
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "X", 1),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Should contain layout directives after @startuml
            result.ShouldContain("top to bottom direction");
            result.ShouldContain("skinparam nodesep 25");
            result.ShouldContain("skinparam ranksep 45");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldColorGateway_WhenNameContainsGateway()
        {
            // Given: An activity with "Gateway" in the name
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "Api Gateway", 5),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Should use gateway color
            result.ShouldContain("rectangle \"Api Gateway\\n(5d)\" as ID1 #fa5252");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldColorEngine_WhenNameContainsEngine()
        {
            // Given: An activity with "Engine" in the name
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "Search Engine", 3),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Should use engine color
            result.ShouldContain("rectangle \"Search Engine\\n(3d)\" as ID1 #ffca08");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldColorClient_WhenNameContainsClient()
        {
            // Given: An activity with "Client" in the name
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "Scheduling Client", 4),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Should use client color
            result.ShouldContain("rectangle \"Scheduling Client\\n(4d)\" as ID1 #a6ce39");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldColorClient_WhenNameContainsUINoDuration()
        {
            // Given: An activity named "UI" with duration 0 (milestone)
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "UI", 0),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Milestone override takes precedence over name-based "UI" → client color
            result.ShouldContain("circle \"UI\" as ID1 #cccccc");
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldColorClient_WhenNameContainsUI()
        {
            // Given: An activity named "UI" with duration 5 
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "UI", 5),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Milestone override takes precedence over name-based "UI" → client color
            result.ShouldContain("rectangle \"UI\\n(5d)\" as ID1 #a6ce39"); //"rectangle \"UI\" as ID1 #a6ce39");
        }
        
        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldColorAccess_WhenNameContainsUI()
        {
            // Given: An activity named "UI" with duration 0 (milestone)
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "Layer Access", 5),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Milestone override takes precedence over name-based "UI" → client color
            result.ShouldContain("rectangle \"Layer Access\\n(5d)\" as ID1 #bcbdc1"); //"rectangle \"UI\" as ID1 #a6ce39");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldUseMilestoneColor_OverNameDetection()
        {
            // Given: An activity named "Gateway" with duration 0 (milestone)
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "Gateway", 0),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Milestone color overrides name-based gateway color
            result.ShouldContain("circle \"Gateway\" as ID1 #cccccc");
            result.ShouldNotContain("#fa5252");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldUseNameColor_OverWorkStream()
        {
            // Given: An activity named "Api Gateway" assigned to a blue WorkStream
            var ws = MakeWorkStream(1, 0, 0, 255);
            var settings = MakeWorkStreamSettings(ws);
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "Api Gateway", 5, targetWorkStreams: [1]),
            };

            // When: Generating PlantUML with work stream settings
            string result = PlantUmlExportService.GeneratePlantUml(activities, settings);

            // Then: Name-based color wins over WorkStream color
            result.ShouldContain("rectangle \"Api Gateway\\n(5d)\" as ID1 #fa5252");
            result.ShouldNotContain("#0000ff");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldFallbackToWorkStream_WhenNoNameMatch()
        {
            // Given: An activity with no name-based color match, assigned to a WorkStream
            var ws = MakeWorkStream(1, 0, 128, 255);
            var settings = MakeWorkStreamSettings(ws);
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "Custom Task", 5, targetWorkStreams: [1]),
            };

            // When: Generating PlantUML with work stream settings
            string result = PlantUmlExportService.GeneratePlantUml(activities, settings);

            // Then: Should use WorkStream color as fallback
            result.ShouldContain("rectangle \"Custom Task\\n(5d)\" as ID1 #0080ff");
        }

        [Theory]
        [Trait("Category", "Unit")]
        [InlineData("gateway service")]
        [InlineData("GATEWAY SERVICE")]
        [InlineData("Gateway Service")]
        public void GeneratePlantUml_ShouldDetectNameColor_CaseInsensitively(string name)
        {
            // Given: An activity with "gateway" in various cases
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, name, 5),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Should detect gateway color regardless of case
            result.ShouldContain("#fa5252");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldColorResource_WhenNameContainsResource()
        {
            // Given: An activity with "Resource" in the name
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "Resource Pool", 3),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Should use resource color
            result.ShouldContain("rectangle \"Resource Pool\\n(3d)\" as ID1 #73cee1");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldColorUtility_WhenNameContainsUtility()
        {
            // Given: An activity with "Utility" in the name
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "Utility Service", 2),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Should use utility color
            result.ShouldContain("rectangle \"Utility Service\\n(2d)\" as ID1 #de9dc7");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void GeneratePlantUml_ShouldColorManager_WhenNameContainsManager()
        {
            // Given: An activity with "Manager" in the name
            var activities = new List<DependentActivityModel>
            {
                AnActivity(1, "Task Manager", 4),
            };

            // When: Generating PlantUML
            string result = PlantUmlExportService.GeneratePlantUml(activities);

            // Then: Should use manager color
            result.ShouldContain("rectangle \"Task Manager\\n(4d)\" as ID1 #fef200");
        }
    }
}
