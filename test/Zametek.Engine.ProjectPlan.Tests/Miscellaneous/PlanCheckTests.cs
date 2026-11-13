using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Engine.ProjectPlan.Tests
{
    /// <summary>
    /// The check of a plan before a job loads it. A load could not get past some plans, and the server answered 500: a plan
    /// with no project start, a date that the calendar cannot count to, a choice that does not exist, or two resources with
    /// one id. Other plans loaded and gave a result that they do not say: two activities with one id, or no data for the
    /// scenario that the load loads. The check gives each problem with its place in the file. It also counts what the
    /// compiler limits, so that a plan out of the limits does not wait for its load.
    /// </summary>
    public class PlanCheckTests
    {
        private static readonly Guid s_Root = Guid.Parse(@"b0a4f078-4de5-4d3b-a2be-b9e2f2a4b6f1");
        private static readonly Guid s_Alpha = Guid.Parse(@"8f4d2f43-4c1b-4f16-9df8-40e1a2b3c4d5");
        private static readonly Guid s_Beta = Guid.Parse(@"17c3e2d9-95a4-4b47-b7ff-51f0a1b2c3d4");
        private static readonly Guid s_Gamma = Guid.Parse(@"5c1d9e7a-2b4f-4c6d-8e0f-1a2b3c4d5e6f");
        private static readonly DateTimeOffset s_Monday = new(2027, 1, 4, 0, 0, 0, TimeSpan.Zero);

        private const string c_Scenario = @"/Files/0/Scenario";

        public PlanCheckTests()
        {
            ProjectPlanEngine.Initialize();
        }

        [Fact]
        public void FindScenarioProblems_Given_AnOrdinaryPlan_Then_None()
        {
            PlanCheck.FindScenarioProblems(Project(Scenario()), 0).ShouldBeEmpty();
        }

        [Fact]
        public void FindScenarioProblems_Given_NoProjectStart_Then_ItIsRequired()
        {
            Problems(Scenario() with { ProjectStart = default })
                .ShouldBe([($@"{c_Scenario}/ProjectStart", PlanCheck.Required)]);
        }

        [Theory]
        [InlineData(1899, 12, 31)]
        [InlineData(3000, 1, 1)]
        public void FindScenarioProblems_Given_AProjectStartOutsideTheDatesThatTheCalendarCountsFrom_Then_OutOfRange(int year, int month, int day)
        {
            Problems(Scenario() with { ProjectStart = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero) })
                .ShouldBe([($@"{c_Scenario}/ProjectStart", PlanCheck.OutOfRange)]);
        }

        [Fact]
        public void FindScenarioProblems_Given_AConstraintDateInTheYear9999_Then_OutOfRange()
        {
            ProjectScenarioModel scenario = Scenario();
            scenario = scenario with
            {
                DependentActivities =
                [
                    scenario.DependentActivities[0],
                    scenario.DependentActivities[1] with
                    {
                        Activity = scenario.DependentActivities[1].Activity with
                        {
                            MaximumLatestFinishDateTime = new DateTimeOffset(9999, 12, 31, 0, 0, 0, TimeSpan.Zero),
                        },
                    },
                ],
            };

            Problems(scenario).ShouldBe([($@"{c_Scenario}/DependentActivities/1/Activity/MaximumLatestFinishDateTime", PlanCheck.OutOfRange)]);
        }

        [Fact]
        public void FindScenarioProblems_Given_AConstraintDateThatTheLimitsAllow_Then_None()
        {
            // 100,000 days from the start, which is the limit of a time.
            ProjectScenarioModel scenario = Scenario();
            scenario = scenario with
            {
                DependentActivities =
                [
                    scenario.DependentActivities[0] with
                    {
                        Activity = scenario.DependentActivities[0].Activity with { MinimumEarliestStartDateTime = s_Monday.AddDays(GraphLimits.MaximumTimeValue) },
                    },
                ],
            };

            Problems(scenario).ShouldBeEmpty();
        }

        [Fact]
        public void FindScenarioProblems_Given_ANonWorkingDayModeThatDoesNotExist_Then_NotAllowed()
        {
            ProjectScenarioModel scenario = Scenario();

            Problems(scenario with { DisplaySettings = scenario.DisplaySettings with { NonWorkingDayMode = (NonWorkingDayMode)7 } })
                .ShouldBe([($@"{c_Scenario}/DisplaySettings/NonWorkingDayMode", PlanCheck.NotAllowed)]);
        }

        [Fact]
        public void FindScenarioProblems_Given_ATargetResourceOperatorThatDoesNotExist_Then_NotAllowed()
        {
            ProjectScenarioModel scenario = Scenario();
            scenario = scenario with
            {
                DependentActivities =
                [
                    scenario.DependentActivities[0] with
                    {
                        Activity = scenario.DependentActivities[0].Activity with { TargetResourceOperator = (LogicalOperator)9 },
                    },
                    scenario.DependentActivities[1],
                ],
            };

            Problems(scenario).ShouldBe([($@"{c_Scenario}/DependentActivities/0/Activity/TargetResourceOperator", PlanCheck.NotAllowed)]);
        }

        [Fact]
        public void FindScenarioProblems_Given_TwoResourcesWithOneId_Then_TheSecondIsADuplicate()
        {
            ProjectScenarioModel scenario = Scenario();
            scenario = scenario with
            {
                ResourceSettings = scenario.ResourceSettings with
                {
                    Resources = [new ResourceModel { Id = 1 }, new ResourceModel { Id = 2 }, new ResourceModel { Id = 1 }],
                },
            };

            Problems(scenario).ShouldBe([($@"{c_Scenario}/ResourceSettings/Resources/2/Id", PlanCheck.DuplicateId)]);
        }

        [Fact]
        public void FindScenarioProblems_Given_TwoActivitiesWithOneId_Then_TheSecondIsADuplicate()
        {
            ProjectScenarioModel scenario = Scenario();
            scenario = scenario with
            {
                DependentActivities = [.. scenario.DependentActivities, new DependentActivityModel { Activity = new ActivityModel { Id = 2, Duration = 3 } }],
            };

            Problems(scenario).ShouldBe([($@"{c_Scenario}/DependentActivities/2/Activity/Id", PlanCheck.DuplicateId)]);
        }

        [Fact]
        public void FindScenarioProblems_Given_TwoWorkStreamsWithOneId_Then_TheSecondIsADuplicate()
        {
            ProjectScenarioModel scenario = Scenario();
            scenario = scenario with
            {
                WorkStreamSettings = scenario.WorkStreamSettings with
                {
                    WorkStreams = [new WorkStreamModel { Id = 4 }, new WorkStreamModel { Id = 4 }],
                },
            };

            Problems(scenario).ShouldBe([($@"{c_Scenario}/WorkStreamSettings/WorkStreams/1/Id", PlanCheck.DuplicateId)]);
        }

        [Fact]
        public void FindScenarioProblems_Given_AProblem_Then_ItsDetailContinuesFromItsPlace()
        {
            ProjectScenarioModel scenario = Scenario();

            PlanProblem problem = PlanCheck.FindScenarioProblems(
                Project(scenario with { DisplaySettings = scenario.DisplaySettings with { NonWorkingDayMode = (NonWorkingDayMode)7 } }),
                0).ShouldHaveSingleItem();

            problem.Detail.ShouldBe(@"must be one of: 0 (None), 1 (Weekends), 2 (CustomCalendar)");
        }

        [Fact]
        public void FindProjectProblems_Given_ANodeWithNoDataAndNoOtherScenario_Then_TheFilesHaveNoData()
        {
            ProjectModel project = Project(Scenario()) with { Files = [] };

            PlanCheck.IndexOfScenarioToLoad(project).ShouldBeNull();
            PlanCheck.FindProjectProblems(project).Select(x => (x.Pointer, x.Code)).ShouldBe([(@"/Files", PlanCheck.HasNoData)]);
        }

        [Fact]
        public void FindProjectProblems_Given_NoNodes_Then_None()
        {
            // A plan with no nodes gets a new, empty scenario, as the documents say.
            ProjectModel project = new() { Root = s_Root };

            PlanCheck.IndexOfScenarioToLoad(project).ShouldBeNull();
            PlanCheck.FindProjectProblems(project).ShouldBeEmpty();
        }

        [Fact]
        public void IndexOfScenarioToLoad_Given_ACurrentWithNoData_Then_TheLastNodeThatHasData()
        {
            // The load takes the last node when Current has no data.
            ProjectModel project = Project(Scenario()) with
            {
                Current = s_Beta,
                Nodes =
                [
                    new ProjectScenarioNodeModel { Id = s_Alpha, ParentId = s_Root, NodeType = ProjectScenarioNodeType.File, Name = @"Alpha" },
                    new ProjectScenarioNodeModel { Id = s_Beta, ParentId = s_Root, NodeType = ProjectScenarioNodeType.File, Name = @"Beta" },
                    new ProjectScenarioNodeModel { Id = s_Gamma, ParentId = s_Root, NodeType = ProjectScenarioNodeType.File, Name = @"Gamma" },
                ],
                Files = [new ProjectScenarioFileModel { NodeId = s_Alpha, Scenario = Scenario() }, new ProjectScenarioFileModel { NodeId = s_Gamma, Scenario = Scenario() }],
            };

            PlanCheck.IndexOfScenarioToLoad(project).ShouldBe(1);
        }

        [Fact]
        public void FindProjectProblems_Given_AChoiceOfTheProjectThatDoesNotExist_Then_NotAllowed()
        {
            ProjectModel project = Project(Scenario());
            project = project with
            {
                Nodes = [project.Nodes[0] with { NodeType = (ProjectScenarioNodeType)5 }],
            };

            PlanCheck.FindProjectProblems(project).Select(x => (x.Pointer, x.Code)).ShouldContain((@"/Nodes/0/NodeType", PlanCheck.NotAllowed));
        }

        [Fact]
        public void FindLimitViolations_Given_MoreActivitiesThanTheCompilerTakes_Then_TheCountInTheWordsOfTheCompiler()
        {
            ProjectScenarioModel scenario = Scenario() with
            {
                DependentActivities = [.. Enumerable.Range(1, GraphLimits.MaximumActivityCount + 1).Select(x => new DependentActivityModel { Activity = new ActivityModel { Id = x, Duration = 1 } })],
            };

            PlanCheck.FindLimitViolations(scenario).ShouldBe([@"The number of activities is 2001, which exceeds the maximum supported count of 2000"]);
        }

        [Fact]
        public void FindLimitViolations_Given_MoreResourcesThanTheCompilerTakesButOneInactive_Then_None()
        {
            // The compiler counts the active resources only.
            List<ResourceModel> resources = [.. Enumerable.Range(1, GraphLimits.MaximumResourceCount + 1).Select(x => new ResourceModel { Id = x, IsInactive = x == 1 })];
            ProjectScenarioModel scenario = Scenario();

            PlanCheck.FindLimitViolations(scenario with { ResourceSettings = scenario.ResourceSettings with { Resources = resources } }).ShouldBeEmpty();
            PlanCheck.FindLimitViolations(scenario with { ResourceSettings = scenario.ResourceSettings with { Resources = [.. resources.Select(x => x with { IsInactive = false })] } })
                .ShouldBe([@"The number of resources is 1001, which exceeds the maximum supported count of 1000"]);
            PlanCheck.FindLimitViolations(scenario with { ResourceSettings = scenario.ResourceSettings with { Resources = [.. resources.Select(x => x with { IsInactive = false })], AreDisabled = true } })
                .ShouldBeEmpty();
        }

        [Fact]
        public async Task FindProblems_Given_EachPlanOfTheRepository_Then_None()
        {
            // The plans of the documents and the plans that the tests read, of each version: the check must not refuse a plan
            // that zpp loads.
            await using ServiceProvider services = new ServiceCollection().AddProjectPlanEngine().BuildServiceProvider();
            await using AsyncServiceScope scope = services.CreateAsyncScope();
            IProjectFileOpen open = scope.ServiceProvider.GetRequiredService<IProjectFileOpen>();
            string[] files = [.. Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, @"RepositoryPlans"), @"*.zpp", SearchOption.AllDirectories)];

            files.Length.ShouldBeGreaterThan(20);

            foreach (string file in files)
            {
                await using FileStream stream = File.OpenRead(file);
                ProjectModel project = await open.OpenProjectFileAsync(stream);
                List<PlanProblem> problems = PlanCheck.FindProjectProblems(project);

                for (int index = 0; index < project.Files.Count; index++)
                {
                    problems.AddRange(PlanCheck.FindScenarioProblems(project, index));
                }

                problems.ShouldBeEmpty(Path.GetFileName(file));
            }
        }

        #region Helpers

        private static List<(string Pointer, string Code)> Problems(ProjectScenarioModel scenario) =>
            [.. PlanCheck.FindScenarioProblems(Project(scenario), 0).Select(x => (x.Pointer, x.Code))];

        // A project with one scenario, at the top of the tree.
        private static ProjectModel Project(ProjectScenarioModel scenario) =>
            new()
            {
                Root = s_Root,
                Current = s_Alpha,
                Nodes = [new ProjectScenarioNodeModel { Id = s_Alpha, ParentId = s_Root, NodeType = ProjectScenarioNodeType.File, Name = @"Alpha" }],
                Files = [new ProjectScenarioFileModel { NodeId = s_Alpha, Scenario = scenario }],
            };

        // Two activities in a chain, from Monday 4 January 2027, with one resource and one work stream.
        private static ProjectScenarioModel Scenario() =>
            new()
            {
                ProjectStart = s_Monday,
                Today = s_Monday,
                DependentActivities =
                [
                    new DependentActivityModel { Activity = new ActivityModel { Id = 1, Duration = 5 } },
                    new DependentActivityModel { Activity = new ActivityModel { Id = 2, Duration = 3 }, Dependencies = [1] },
                ],
                ResourceSettings = new ResourceSettingsModel { Resources = [new ResourceModel { Id = 1 }] },
                WorkStreamSettings = new WorkStreamSettingsModel { WorkStreams = [new WorkStreamModel { Id = 1 }] },
            };

        #endregion
    }
}
