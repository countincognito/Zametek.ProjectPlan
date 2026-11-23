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
        public void FindScenarioProblems_Given_ATargetDateOfTheSimulationThatTheCalendarCannotCountTo_Then_OutOfRange()
        {
            var settings = new SimulationSettingsModel { TargetFinishDateTime = s_Monday.AddDays(GraphLimits.MaximumTimeValue + 1) };

            Problems(Scenario() with { SimulationSettings = settings })
                .ShouldBe([($@"{c_Scenario}/SimulationSettings/TargetFinishDateTime", PlanCheck.OutOfRange)]);
        }

        [Fact]
        public void FindScenarioProblems_Given_ATargetDateOfTheSimulationThatTheLimitsAllow_Then_None()
        {
            var settings = new SimulationSettingsModel { TargetFinishDateTime = s_Monday.AddDays(GraphLimits.MaximumTimeValue) };

            Problems(Scenario() with { SimulationSettings = settings }).ShouldBeEmpty();
        }

        [Fact]
        public void FindScenarioWarnings_Given_ATargetDateOfTheSimulationBeforeTheStart_Then_AWarning()
        {
            // No day of work is before the end of such a date, and so the target is day 0. It is usually a mistake.
            var settings = new SimulationSettingsModel { TargetFinishDateTime = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero) };

            List<PlanProblem> warnings = PlanCheck.FindScenarioWarnings(Project(Scenario() with { SimulationSettings = settings }), 0);

            warnings.Select(x => (x.Pointer, x.Code)).ShouldBe([($@"{c_Scenario}/SimulationSettings/TargetFinishDateTime", PlanCheck.OutOfRange)]);
            warnings[0].Detail.ShouldBe(@"is 2026-12-01, before ProjectStart (2027-01-04), and so the target of the simulation is day 0");
        }

        [Fact]
        public void FindScenarioWarnings_Given_ATargetDateOfTheSimulationOnTheDayOfTheStart_Then_None()
        {
            var settings = new SimulationSettingsModel { TargetFinishDateTime = s_Monday.AddHours(9) };

            PlanCheck.FindScenarioWarnings(Project(Scenario() with { SimulationSettings = settings }), 0).ShouldBeEmpty();
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
        public void FindScenarioWarnings_Given_AnOrdinaryPlan_Then_None()
        {
            PlanCheck.FindScenarioWarnings(Project(Scenario()), 0).ShouldBeEmpty();
        }

        [Fact]
        public void FindScenarioWarnings_Given_IdsThatMatchNothing_Then_AWarningAtEach()
        {
            // The load drops a target resource that no resource has, and the activity can then go to any resource (A6 of the
            // stress test of 0.11.0-beta.1).
            ProjectScenarioModel scenario = Scenario();
            scenario = scenario with
            {
                DependentActivities =
                [
                    scenario.DependentActivities[0] with
                    {
                        Activity = scenario.DependentActivities[0].Activity with { TargetResources = [1, 99], TargetWorkStreams = [7] },
                    },
                    scenario.DependentActivities[1],
                ],
                ResourceSettings = scenario.ResourceSettings with { Resources = [new ResourceModel { Id = 1, InterActivityPhases = [1, 8] }] },
            };

            PlanCheck.FindScenarioWarnings(Project(scenario), 0).Select(x => (x.Pointer, x.Code)).ShouldBe(
            [
                ($@"{c_Scenario}/DependentActivities/0/Activity/TargetResources/1", PlanCheck.NotFound),
                ($@"{c_Scenario}/DependentActivities/0/Activity/TargetWorkStreams/0", PlanCheck.NotFound),
                ($@"{c_Scenario}/ResourceSettings/Resources/0/InterActivityPhases/1", PlanCheck.NotFound),
            ]);
        }

        [Fact]
        public void FindScenarioWarnings_Given_ConstraintDatesBeforeTheStart_Then_AWarningAtEach()
        {
            // The calendar gives such a date day 0, and nothing said so. A minimum EST then has no effect, and an activity that
            // takes time cannot meet the maximum LFT (C0010). A date before the start is usually a mistake, for example the
            // wrong year (phase 3 of the zpp skill).
            ProjectScenarioModel scenario = Scenario();
            scenario = scenario with
            {
                DependentActivities =
                [
                    scenario.DependentActivities[0] with
                    {
                        Activity = scenario.DependentActivities[0].Activity with { MinimumEarliestStartDateTime = s_Monday.AddDays(-1) },
                    },
                    scenario.DependentActivities[1] with
                    {
                        Activity = scenario.DependentActivities[1].Activity with { MaximumLatestFinishDateTime = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero) },
                    },
                ],
            };

            List<PlanProblem> warnings = PlanCheck.FindScenarioWarnings(Project(scenario), 0);

            warnings.Select(x => (x.Pointer, x.Code)).ShouldBe(
            [
                ($@"{c_Scenario}/DependentActivities/0/Activity/MinimumEarliestStartDateTime", PlanCheck.OutOfRange),
                ($@"{c_Scenario}/DependentActivities/1/Activity/MaximumLatestFinishDateTime", PlanCheck.OutOfRange),
            ]);
            warnings[1].Detail.ShouldBe(@"is 2026-12-01, before ProjectStart (2027-01-04), and so the schedule uses day 0 for activity 2");
        }

        [Fact]
        public void FindScenarioWarnings_Given_ConstraintDatesOnTheDayOfTheStart_Then_None()
        {
            // Each date of a plan is a day, and so the time of the day does not count. The start itself is day 0.
            ProjectScenarioModel scenario = Scenario();
            scenario = scenario with
            {
                ProjectStart = s_Monday.AddHours(9),
                DependentActivities =
                [
                    scenario.DependentActivities[0] with
                    {
                        Activity = scenario.DependentActivities[0].Activity with { MinimumEarliestStartDateTime = s_Monday },
                    },
                    scenario.DependentActivities[1] with
                    {
                        Activity = scenario.DependentActivities[1].Activity with { MaximumLatestFinishDateTime = s_Monday.AddDays(10) },
                    },
                ],
            };

            PlanCheck.FindScenarioWarnings(Project(scenario), 0).ShouldBeEmpty();
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
        public void FindScenarioProblems_Given_AHolidayRuleThatRepeatsMoreOftenThanEachDay_Then_NotAllowedAtItsRule()
        {
            // A count of 30 days took more than 2 minutes with FREQ=SECONDLY, in the load (measured on 2026-11-23).
            ProjectScenarioModel scenario = Scenario() with
            {
                HolidaySettings = new HolidaySettingsModel
                {
                    Holidays =
                    [
                        new HolidayModel { Id = 1, RecurrencePattern = @"FREQ=WEEKLY;BYDAY=SA,SU" },
                        new HolidayModel { Id = 2, RecurrencePattern = @"FREQ=SECONDLY" },
                        new HolidayModel { Id = 3, RecurrencePattern = @"FREQ=DAILY;BYHOUR=9" },
                    ],
                },
            };

            List<PlanProblem> problems = PlanCheck.FindScenarioProblems(Project(scenario), 0);

            problems.Select(x => (x.Pointer, x.Code)).ShouldBe(
            [
                ($@"{c_Scenario}/HolidaySettings/Holidays/1/RecurrencePattern", PlanCheck.NotAllowed),
                ($@"{c_Scenario}/HolidaySettings/Holidays/2/RecurrencePattern", PlanCheck.NotAllowed),
            ]);
            problems[0].Detail.ShouldBe(@"must repeat each day or less often (FREQ=DAILY, WEEKLY, MONTHLY or YEARLY), with no BYHOUR, BYMINUTE or BYSECOND");
        }

        [Fact]
        public void FindProjectProblems_Given_TwoNodesWithOneId_Then_DuplicateIdAtTheSecond()
        {
            ProjectModel project = Project(Scenario());
            project = project with
            {
                Nodes =
                [
                    project.Nodes[0],
                    new ProjectScenarioNodeModel { Id = s_Beta, ParentId = s_Root, NodeType = ProjectScenarioNodeType.File, Name = @"Beta" },
                    new ProjectScenarioNodeModel { Id = s_Alpha, ParentId = s_Root, NodeType = ProjectScenarioNodeType.File, Name = @"Alpha again" },
                ],
            };

            List<PlanProblem> problems = PlanCheck.FindProjectProblems(project);

            problems.Select(x => (x.Pointer, x.Code)).ShouldBe([(@"/Nodes/2/Id", PlanCheck.DuplicateId)]);
            problems[0].Detail.ShouldBe(@"is also the Id of item 0 of this list");
        }

        [Fact]
        public void FindProjectProblems_Given_ANodeThatIsItsOwnParent_Then_NotAllowedAtItsParentId()
        {
            ProjectModel project = Project(Scenario());
            project = project with { Nodes = [project.Nodes[0] with { ParentId = s_Alpha }] };

            List<PlanProblem> problems = PlanCheck.FindProjectProblems(project);

            problems.Select(x => (x.Pointer, x.Code)).ShouldBe([(@"/Nodes/0/ParentId", PlanCheck.NotAllowed)]);
            problems[0].Detail.ShouldBe(@"makes the node a parent of itself");
        }

        [Fact]
        public void FindNodeProblems_Given_TwoNodesThatAreTheParentOfEachOtherAndAChildOfThem_Then_EachNodeOfTheCycleOnly()
        {
            // Gamma is below the cycle, and its walk ends at a node that it saw, which is not Gamma.
            ProjectModel project = Project(Scenario()) with
            {
                Nodes =
                [
                    new ProjectScenarioNodeModel { Id = s_Gamma, ParentId = s_Alpha, NodeType = ProjectScenarioNodeType.File, Name = @"Gamma" },
                    new ProjectScenarioNodeModel { Id = s_Alpha, ParentId = s_Beta, NodeType = ProjectScenarioNodeType.File, Name = @"Alpha" },
                    new ProjectScenarioNodeModel { Id = s_Beta, ParentId = s_Alpha, NodeType = ProjectScenarioNodeType.Folder, Name = @"Beta" },
                ],
            };

            PlanCheck.FindNodeProblems(project).Select(x => (x.Pointer, x.Code)).ShouldBe(
            [
                (@"/Nodes/1/ParentId", PlanCheck.NotAllowed),
                (@"/Nodes/2/ParentId", PlanCheck.NotAllowed),
            ]);
        }

        [Fact]
        public void FindNodeProblems_Given_ATreeOfFoldersAndAnOrphan_Then_None()
        {
            // A node whose parent is not a node goes to the top of the tree in the load, and it is not a problem.
            ProjectModel project = Project(Scenario()) with
            {
                Nodes =
                [
                    new ProjectScenarioNodeModel { Id = s_Beta, ParentId = s_Root, NodeType = ProjectScenarioNodeType.Folder, Name = @"Beta" },
                    new ProjectScenarioNodeModel { Id = s_Gamma, ParentId = s_Beta, NodeType = ProjectScenarioNodeType.Folder, Name = @"Gamma" },
                    new ProjectScenarioNodeModel { Id = s_Alpha, ParentId = s_Gamma, NodeType = ProjectScenarioNodeType.File, Name = @"Alpha" },
                    new ProjectScenarioNodeModel { Id = Guid.NewGuid(), ParentId = Guid.NewGuid(), NodeType = ProjectScenarioNodeType.Folder, Name = @"Orphan" },
                ],
            };

            PlanCheck.FindNodeProblems(project).ShouldBeEmpty();
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

        // An import has the same counts as a scenario, and the check gives the same words for them.
        [Fact]
        public void FindLimitViolations_Given_AnImportWithMoreThanTheCompilerTakes_Then_TheCountsInTheWordsOfTheCompiler()
        {
            List<ResourceModel> resources = [.. Enumerable.Range(1, GraphLimits.MaximumResourceCount + 2).Select(x => new ResourceModel { Id = x, IsInactive = x == 1 })];
            var import = new ProjectScenarioImportModel
            {
                DependentActivities = [.. Enumerable.Range(1, GraphLimits.MaximumActivityCount + 1).Select(x => new DependentActivityModel { Activity = new ActivityModel { Id = x, Duration = 1 } })],
                ResourceSettings = new ResourceSettingsModel { Resources = resources },
                WorkStreams = [.. Enumerable.Range(1, GraphLimits.MaximumWorkStreamCount + 1).Select(x => new WorkStreamModel { Id = x })],
            };

            PlanCheck.FindLimitViolations(import).ShouldBe(
            [
                @"The number of activities is 2001, which exceeds the maximum supported count of 2000",
                @"The number of resources is 1001, which exceeds the maximum supported count of 1000",
                @"The number of work streams is 101, which exceeds the maximum supported count of 100",
            ]);

            // The compiler counts the active resources only, and none when the resources are disabled.
            PlanCheck.FindLimitViolations(new ProjectScenarioImportModel { ResourceSettings = new ResourceSettingsModel { Resources = resources, AreDisabled = true } }).ShouldBeEmpty();
            PlanCheck.FindLimitViolations(new ProjectScenarioImportModel()).ShouldBeEmpty();
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
