using Newtonsoft.Json;
using Shouldly;
using System;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan.Tests
{
    public class ConverterTests
        : IClassFixture<ConverterFixture>
    {
        // Where the fixtures older than v0.3.0, whose times carry no offset, are read: the v0.3.0 fixture holds the
        // v0.2.1 fixture's project start as read in a zone whose offset in January is zero.
        private static readonly TimeZoneInfo s_LocalTimeZone = TimeZoneInfo.Utc;

        private static readonly TimeZoneInfo s_ZoneAheadOfUtc =
            TimeZoneInfo.CreateCustomTimeZone(@"Test +05:30", TimeSpan.FromMinutes(330), @"Test +05:30", @"Test +05:30");

        private readonly ConverterFixture m_Fixture;
        private readonly DateTimeOffset m_LocalNow;

        public ConverterTests(ConverterFixture fixture)
        {
            m_Fixture = fixture;
            m_LocalNow = TimeProvider.System.GetLocalNow();
        }

        private static void CompareModelsPreV0_6_0(ProjectModel model1, ProjectModel model2)
        {
            model1.Version.ShouldBe(model2.Version);
            model1.Nodes.Count.ShouldBe(model2.Nodes.Count);

            for (int i = 0; i < model1.Nodes.Count; i++)
            {
                model1.Nodes[i].NodeType.ShouldBe(model2.Nodes[i].NodeType);
                model1.Nodes[i].Name.ShouldBe(model2.Nodes[i].Name);
                model1.Nodes[i].CreatedOn.ShouldBe(model2.Nodes[i].CreatedOn);
                model1.Nodes[i].ModifiedOn.ShouldBe(model2.Nodes[i].ModifiedOn);
            }

            model1.Files.Count.ShouldBe(model2.Files.Count);

            for (int i = 0; i < model1.Files.Count; i++)
            {
                model1.Files[i].Scenario.ShouldBeEquivalentTo(model2.Files[i].Scenario);
            }

            model1.Tags.Count.ShouldBe(model2.Tags.Count);

            for (int i = 0; i < model1.Tags.Count; i++)
            {
                model1.Tags[i].Label.ShouldBeEquivalentTo(model2.Tags[i].Label);
            }
        }

        private static void CompareModels(ProjectModel model1, ProjectModel model2)
        {
            model1.Version.ShouldBe(model2.Version);
            model1.Nodes.Count.ShouldBe(model2.Nodes.Count);

            for (int i = 0; i < model1.Nodes.Count; i++)
            {
                model1.Nodes[i].NodeType.ShouldBe(model2.Nodes[i].NodeType);
                model1.Nodes[i].Name.ShouldBe(model2.Nodes[i].Name);

                //model1.Nodes[i].CreatedOn.ShouldBe(model2.Nodes[i].CreatedOn);
                //model1.Nodes[i].ModifiedOn.ShouldBe(model2.Nodes[i].ModifiedOn);
            }

            model1.Files.Count.ShouldBe(model2.Files.Count);

            for (int i = 0; i < model1.Files.Count; i++)
            {
                model1.Files[i].Scenario.ShouldBeEquivalentTo(model2.Files[i].Scenario);
            }

            model1.Tags.Count.ShouldBe(model2.Tags.Count);

            for (int i = 0; i < model1.Tags.Count; i++)
            {
                model1.Tags[i].Label.ShouldBeEquivalentTo(model2.Tags[i].Label);
            }
        }

        [Fact]
        public void Converter_Given_v0_1_0_Input_Then_ConvertsTo_v0_2_0()
        {
            v0_1_0.ProjectModel? project_v0_1_0 = JsonConvert.DeserializeObject<v0_1_0.ProjectModel>(m_Fixture.V0_1_0_JsonString);
            v0_2_0.ProjectModel? project_v0_2_0 = JsonConvert.DeserializeObject<v0_2_0.ProjectModel>(m_Fixture.V0_2_0_JsonString);
            v0_2_0.ProjectModel? project_v0_2_0_upgraded = v0_2_0.Converter.Upgrade(project_v0_1_0!);
            project_v0_2_0_upgraded.ShouldBeEquivalentTo(project_v0_2_0);

            ProjectModel model1 = Converter.Upgrade(m_LocalNow, s_LocalTimeZone, project_v0_1_0!);
            ProjectModel model2 = Converter.Upgrade(m_LocalNow, s_LocalTimeZone, project_v0_2_0!);
            CompareModelsPreV0_6_0(model1, model2);
        }

        [Fact]
        public void Converter_Given_v0_2_0_Input_Then_ConvertsTo_v0_2_1()
        {
            v0_2_0.ProjectModel? project_v0_2_0 = JsonConvert.DeserializeObject<v0_2_0.ProjectModel>(m_Fixture.V0_2_0_JsonString);
            v0_2_1.ProjectModel? project_v0_2_1 = JsonConvert.DeserializeObject<v0_2_1.ProjectModel>(m_Fixture.V0_2_1_JsonString);
            var mapper = new VersionMapper();
            v0_2_1.ProjectModel project_v0_2_1_upgraded = v0_2_1.Converter.Upgrade(mapper, project_v0_2_0!);
            project_v0_2_1_upgraded.ShouldBeEquivalentTo(project_v0_2_1);

            ProjectModel model1 = Converter.Upgrade(m_LocalNow, s_LocalTimeZone, project_v0_2_0!);
            ProjectModel model2 = Converter.Upgrade(m_LocalNow, s_LocalTimeZone, project_v0_2_1!);
            CompareModelsPreV0_6_0(model1, model2);
        }

        [Fact]
        public void Converter_Given_v0_2_1_Input_Then_ConvertsTo_v0_3_0()
        {
            v0_2_1.ProjectModel? project_v0_2_1 = JsonConvert.DeserializeObject<v0_2_1.ProjectModel>(m_Fixture.V0_2_1_JsonString);
            v0_3_0.ProjectModel? project_v0_3_0 = JsonConvert.DeserializeObject<v0_3_0.ProjectModel>(m_Fixture.V0_3_0_JsonString);
            var mapper = new VersionMapper(s_LocalTimeZone);
            v0_3_0.ProjectModel project_v0_3_0_upgraded = v0_3_0.Converter.Upgrade(mapper, project_v0_2_1!);
            project_v0_3_0_upgraded.ShouldBeEquivalentTo(project_v0_3_0);

            ProjectModel model1 = Converter.Upgrade(m_LocalNow, s_LocalTimeZone, project_v0_2_1!);
            ProjectModel model2 = Converter.Upgrade(m_LocalNow, project_v0_3_0!);
            CompareModelsPreV0_6_0(model1, model2);
        }

        [Fact]
        public void Converter_Given_v0_2_1_InputAndATimeZone_Then_ItsTimesAreReadThere()
        {
            // The project's start and an activity's earliest start and latest finish, all without an offset.
            v0_2_1.ProjectModel project = WithActivityDates(
                JsonConvert.DeserializeObject<v0_2_1.ProjectModel>(m_Fixture.V0_2_1_JsonString)!,
                new DateTime(2015, 1, 12, 9, 0, 0),
                new DateTime(2015, 1, 30, 17, 0, 0));

            ProjectScenarioModel scenario = Converter.Upgrade(m_LocalNow, s_ZoneAheadOfUtc, project).Files.ShouldHaveSingleItem().Scenario;

            TimeSpan offset = TimeSpan.FromMinutes(330);
            (scenario.ProjectStart.DateTime, scenario.ProjectStart.Offset).ShouldBe((new DateTime(2015, 1, 5, 8, 0, 0), offset));
            ActivityModel activity = scenario.DependentActivities.First(x => x.Activity.Id == project.DependentActivities[0].Activity!.Id).Activity;
            (activity.MinimumEarliestStartDateTime!.Value.DateTime, activity.MinimumEarliestStartDateTime.Value.Offset).ShouldBe((new DateTime(2015, 1, 12, 9, 0, 0), offset));
            (activity.MaximumLatestFinishDateTime!.Value.DateTime, activity.MaximumLatestFinishDateTime.Value.Offset).ShouldBe((new DateTime(2015, 1, 30, 17, 0, 0), offset));
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(7, 1)]
        public void Converter_Given_v0_2_1_Input_Then_EachTimeHasTheOffsetItsZoneHasOnItsDate(int month, int offsetHours)
        {
            // London is at +00:00 in January and +01:00 in July, whatever the date the file is opened on.
            TimeZoneInfo london = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? @"GMT Standard Time" : @"Europe/London");
            v0_2_1.ProjectModel project = JsonConvert.DeserializeObject<v0_2_1.ProjectModel>(m_Fixture.V0_2_1_JsonString)! with
            {
                ProjectStart = new DateTime(2015, month, 5, 8, 0, 0),
            };

            DateTimeOffset projectStart = Converter.Upgrade(m_LocalNow, london, project).Files.ShouldHaveSingleItem().Scenario.ProjectStart;

            (projectStart.DateTime, projectStart.Offset).ShouldBe((new DateTime(2015, month, 5, 8, 0, 0), TimeSpan.FromHours(offsetHours)));
        }

        [Fact]
        public void Converter_Given_v0_2_1_TimesWrittenWithAnOffsetOrInUtc_Then_EachKeepsItsInstant()
        {
            // The JSON reader turns a time written with an offset into the same instant in the machine's zone, and one
            // written with a Z into UTC: the first is shown in the zone's offset, and the second stays in UTC.
            v0_2_1.ProjectModel project = WithActivityDates(
                JsonConvert.DeserializeObject<v0_2_1.ProjectModel>(m_Fixture.V0_2_1_JsonString)!,
                new DateTimeOffset(2015, 1, 12, 9, 0, 0, TimeSpan.FromHours(1)).LocalDateTime,
                new DateTime(2015, 1, 30, 17, 0, 0, DateTimeKind.Utc));

            ProjectScenarioModel scenario = Converter.Upgrade(m_LocalNow, s_ZoneAheadOfUtc, project).Files.ShouldHaveSingleItem().Scenario;

            ActivityModel activity = scenario.DependentActivities.First(x => x.Activity.Id == project.DependentActivities[0].Activity!.Id).Activity;
            (activity.MinimumEarliestStartDateTime!.Value.DateTime, activity.MinimumEarliestStartDateTime.Value.Offset).ShouldBe((new DateTime(2015, 1, 12, 13, 30, 0), TimeSpan.FromMinutes(330)));
            (activity.MaximumLatestFinishDateTime!.Value.DateTime, activity.MaximumLatestFinishDateTime.Value.Offset).ShouldBe((new DateTime(2015, 1, 30, 17, 0, 0), TimeSpan.Zero));
        }

        // The project with its first activity's earliest start and latest finish set.
        private static v0_2_1.ProjectModel WithActivityDates(
            v0_2_1.ProjectModel project,
            DateTime minimumEarliestStart,
            DateTime maximumLatestFinish)
        {
            v0_2_1.DependentActivityModel first = project.DependentActivities[0];

            return project with
            {
                DependentActivities =
                [
                    first with
                    {
                        Activity = first.Activity! with
                        {
                            MinimumEarliestStartDateTime = minimumEarliestStart,
                            MaximumLatestFinishDateTime = maximumLatestFinish,
                        },
                    },
                    .. project.DependentActivities.Skip(1),
                ],
            };
        }

        [Fact]
        public void Converter_Given_v0_3_0_Input_Then_ConvertsTo_v0_3_1()
        {
            v0_3_0.ProjectModel? project_v0_3_0 = JsonConvert.DeserializeObject<v0_3_0.ProjectModel>(m_Fixture.V0_3_0_JsonString);
            v0_3_1.ProjectModel? project_v0_3_1 = JsonConvert.DeserializeObject<v0_3_1.ProjectModel>(m_Fixture.V0_3_1_JsonString);
            var mapper = new VersionMapper();
            v0_3_1.ProjectModel project_v0_3_0_upgraded = v0_3_1.Converter.Upgrade(mapper, project_v0_3_0!);
            project_v0_3_0_upgraded.ShouldBeEquivalentTo(project_v0_3_1);

            ProjectModel model1 = Converter.Upgrade(m_LocalNow, project_v0_3_0!);
            ProjectModel model2 = Converter.Upgrade(m_LocalNow, project_v0_3_1!);
            CompareModelsPreV0_6_0(model1, model2);
        }

        [Fact]
        public void Converter_Given_v0_3_1_Input_Then_ConvertsTo_v0_3_2()
        {
            v0_3_1.ProjectModel? project_v0_3_1 = JsonConvert.DeserializeObject<v0_3_1.ProjectModel>(m_Fixture.V0_3_1_JsonString);
            v0_3_2.ProjectModel? project_v0_3_2 = JsonConvert.DeserializeObject<v0_3_2.ProjectModel>(m_Fixture.V0_3_2_JsonString);
            var mapper = new VersionMapper();
            v0_3_2.ProjectModel project_v0_3_1_upgraded = v0_3_2.Converter.Upgrade(mapper, project_v0_3_1!);
            project_v0_3_1_upgraded.ShouldBeEquivalentTo(project_v0_3_2);

            ProjectModel model1 = Converter.Upgrade(m_LocalNow, project_v0_3_1!);
            ProjectModel model2 = Converter.Upgrade(m_LocalNow, project_v0_3_2!);
            CompareModelsPreV0_6_0(model1, model2);
        }

        [Fact]
        public void Converter_Given_v0_3_2_Input_Then_ConvertsTo_v0_4_0()
        {
            v0_3_2.ProjectModel? project_v0_3_2 = JsonConvert.DeserializeObject<v0_3_2.ProjectModel>(m_Fixture.V0_3_2a_JsonString);
            v0_4_0.ProjectModel? project_v0_4_0 = JsonConvert.DeserializeObject<v0_4_0.ProjectModel>(m_Fixture.V0_4_0a_JsonString);
            var mapper = new VersionMapper();
            v0_4_0.ProjectModel project_v0_3_2_upgraded = v0_4_0.Converter.Upgrade(mapper, project_v0_3_2!);
            project_v0_3_2_upgraded.ShouldBeEquivalentTo(project_v0_4_0);

            ProjectModel model1 = Converter.Upgrade(m_LocalNow, project_v0_3_2!);
            ProjectModel model2 = Converter.Upgrade(m_LocalNow, project_v0_4_0!);
            CompareModelsPreV0_6_0(model1, model2);
        }

        [Fact]
        public void Converter_Given_v0_4_0_Input_Then_ConvertsTo_v0_4_1()
        {
            v0_4_0.ProjectModel? project_v0_4_0 = JsonConvert.DeserializeObject<v0_4_0.ProjectModel>(m_Fixture.V0_4_0b_JsonString);
            v0_4_1.ProjectModel? project_v0_4_1 = JsonConvert.DeserializeObject<v0_4_1.ProjectModel>(m_Fixture.V0_4_1b_JsonString);
            var mapper = new VersionMapper();
            v0_4_1.ProjectModel project_v0_4_0_upgraded = v0_4_1.Converter.Upgrade(mapper, project_v0_4_0!);
            project_v0_4_0_upgraded.ShouldBeEquivalentTo(project_v0_4_1);

            ProjectModel model1 = Converter.Upgrade(m_LocalNow, project_v0_4_0!);
            ProjectModel model2 = Converter.Upgrade(m_LocalNow, project_v0_4_1!);
            CompareModelsPreV0_6_0(model1, model2);
        }

        [Fact]
        public void Converter_Given_v0_4_1_Input_Then_ConvertsTo_v0_4_2()
        {
            v0_4_1.ProjectModel? project_v0_4_1 = JsonConvert.DeserializeObject<v0_4_1.ProjectModel>(m_Fixture.V0_4_1c_JsonString);
            v0_4_2.ProjectModel? project_v0_4_2 = JsonConvert.DeserializeObject<v0_4_2.ProjectModel>(m_Fixture.V0_4_2c_JsonString);
            var mapper = new VersionMapper();
            v0_4_2.ProjectModel project_v0_4_1_upgraded = v0_4_2.Converter.Upgrade(mapper, project_v0_4_1!);
            project_v0_4_1_upgraded.ShouldBeEquivalentTo(project_v0_4_2);

            ProjectModel model1 = Converter.Upgrade(m_LocalNow, project_v0_4_1!);
            ProjectModel model2 = Converter.Upgrade(m_LocalNow, project_v0_4_2!);
            CompareModelsPreV0_6_0(model1, model2);
        }

        [Fact]
        public void Converter_Given_v0_4_2_Input_Then_ConvertsTo_v0_4_3()
        {
            v0_4_2.ProjectModel? project_v0_4_2 = JsonConvert.DeserializeObject<v0_4_2.ProjectModel>(m_Fixture.V0_4_2d_JsonString);
            v0_4_3.ProjectModel? project_v0_4_3 = JsonConvert.DeserializeObject<v0_4_3.ProjectModel>(m_Fixture.V0_4_3d_JsonString);
            var mapper = new VersionMapper();
            v0_4_3.ProjectModel project_v0_4_2_upgraded = v0_4_3.Converter.Upgrade(mapper, project_v0_4_2!);
            project_v0_4_2_upgraded.ShouldBeEquivalentTo(project_v0_4_3);

            ProjectModel model1 = Converter.Upgrade(m_LocalNow, project_v0_4_2!);
            ProjectModel model2 = Converter.Upgrade(m_LocalNow, project_v0_4_3!);
            CompareModelsPreV0_6_0(model1, model2);

            // The fixtures carry a Today distinct from ProjectStart; ensure it survives the upgrade.
            var expectedToday = new DateTimeOffset(2025, 1, 13, 0, 0, 0, TimeSpan.Zero);
            model1.Files[0].Scenario.Today.ShouldBe(expectedToday);
            model1.Files[0].Scenario.Today.ShouldNotBe(model1.Files[0].Scenario.ProjectStart);
            model2.Files[0].Scenario.Today.ShouldBe(expectedToday);
        }

        [Fact]
        public void Converter_Given_v0_4_3_Input_Then_ConvertsTo_v0_4_4()
        {
            v0_4_3.ProjectModel? project_v0_4_3 = JsonConvert.DeserializeObject<v0_4_3.ProjectModel>(m_Fixture.V0_4_3d_JsonString);
            v0_4_4.ProjectModel? project_v0_4_4 = JsonConvert.DeserializeObject<v0_4_4.ProjectModel>(m_Fixture.V0_4_4d_JsonString);
            var mapper = new VersionMapper();
            v0_4_4.ProjectModel project_v0_4_3_upgraded = v0_4_4.Converter.Upgrade(mapper, project_v0_4_3!);
            project_v0_4_3_upgraded.ShouldBeEquivalentTo(project_v0_4_4);

            ProjectModel model1 = Converter.Upgrade(m_LocalNow, project_v0_4_3!);
            ProjectModel model2 = Converter.Upgrade(m_LocalNow, project_v0_4_4!);
            CompareModelsPreV0_6_0(model1, model2);

            // The fixtures carry a Today distinct from ProjectStart; ensure it survives the upgrade.
            var expectedToday = new DateTimeOffset(2025, 1, 13, 0, 0, 0, TimeSpan.Zero);
            model1.Files[0].Scenario.Today.ShouldBe(expectedToday);
            model1.Files[0].Scenario.Today.ShouldNotBe(model1.Files[0].Scenario.ProjectStart);
            model2.Files[0].Scenario.Today.ShouldBe(expectedToday);
        }

        [Fact]
        public void Converter_Given_v0_4_4_Input_Then_ConvertsTo_v0_5_0()
        {
            v0_4_4.ProjectModel? project_v0_4_4 = JsonConvert.DeserializeObject<v0_4_4.ProjectModel>(m_Fixture.V0_4_4d_JsonString);
            v0_5_0.ProjectModel? project_v0_5_0 = JsonConvert.DeserializeObject<v0_5_0.ProjectModel>(m_Fixture.V0_5_0_JsonString);
            var mapper = new VersionMapper();
            v0_5_0.ProjectModel project_v0_4_4_upgraded = v0_5_0.Converter.Upgrade(mapper, project_v0_4_4!);
            project_v0_4_4_upgraded.ShouldBeEquivalentTo(project_v0_5_0);

            ProjectModel model1 = Converter.Upgrade(m_LocalNow, project_v0_4_4!);
            ProjectModel model2 = Converter.Upgrade(m_LocalNow, project_v0_5_0!);
            CompareModelsPreV0_6_0(model1, model2);

            // The fixtures carry a Today distinct from ProjectStart; ensure it survives the upgrade.
            var expectedToday = new DateTimeOffset(2025, 1, 13, 0, 0, 0, TimeSpan.Zero);
            model1.Files[0].Scenario.Today.ShouldBe(expectedToday);
            model1.Files[0].Scenario.Today.ShouldNotBe(model1.Files[0].Scenario.ProjectStart);
            model2.Files[0].Scenario.Today.ShouldBe(expectedToday);
        }

        [Fact]
        public void Converter_Given_v0_5_0a_Input_Then_ConvertsTo_v0_6_0()
        {
            v0_5_0.ProjectModel? project_v0_5_0 = JsonConvert.DeserializeObject<v0_5_0.ProjectModel>(m_Fixture.V0_5_0a_JsonString);
            v0_6_0.ProjectModel? project_v0_6_0 = JsonConvert.DeserializeObject<v0_6_0.ProjectModel>(m_Fixture.V0_6_0_JsonString);
            var mapper = new VersionMapper();
            //v0_6_0.ProjectModel project_v0_5_0_upgraded = v0_6_0.Converter.Upgrade(mapper, m_LocalNow, project_v0_5_0!);
            //project_v0_5_0_upgraded.ShouldBeEquivalentTo(project_v0_6_0);

            ProjectModel model1 = Converter.Upgrade(m_LocalNow, project_v0_5_0!);
            ProjectModel model2 = Converter.Upgrade(project_v0_6_0!);
            CompareModels(model1, model2);
        }

        [Fact]
        public void Converter_Given_v0_6_0_Input_Then_ConvertsTo_v0_6_1()
        {
            v0_6_0.ProjectModel? project_v0_6_0 = JsonConvert.DeserializeObject<v0_6_0.ProjectModel>(m_Fixture.V0_6_0_JsonString);
            v0_6_1.ProjectModel? project_v0_6_1 = JsonConvert.DeserializeObject<v0_6_1.ProjectModel>(m_Fixture.V0_6_1_JsonString);
            var mapper = new VersionMapper();
            v0_6_1.ProjectModel project_v0_6_0_upgraded = v0_6_1.Converter.Upgrade(mapper, project_v0_6_0!);
            project_v0_6_0_upgraded.ShouldBeEquivalentTo(project_v0_6_1);

            ProjectModel model1 = Converter.Upgrade(project_v0_6_0!);
            ProjectModel model2 = Converter.Upgrade(project_v0_6_1!);
            CompareModels(model1, model2);
        }
    }
}
