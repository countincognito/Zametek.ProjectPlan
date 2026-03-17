using Newtonsoft.Json;
using Shouldly;
using System;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan.Tests
{
    public class ConverterTests
        : IClassFixture<ConverterFixture>
    {
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
        public void Converter_Given_v0_1_0_Input_ThenConvertsTo_v0_2_0()
        {
            v0_1_0.ProjectModel? project_v0_1_0 = JsonConvert.DeserializeObject<v0_1_0.ProjectModel>(m_Fixture.V0_1_0_JsonString);
            v0_2_0.ProjectModel? project_v0_2_0 = JsonConvert.DeserializeObject<v0_2_0.ProjectModel>(m_Fixture.V0_2_0_JsonString);
            v0_2_0.ProjectModel? project_v0_2_0_upgraded = v0_2_0.Converter.Upgrade(project_v0_1_0!);
            project_v0_2_0_upgraded.ShouldBeEquivalentTo(project_v0_2_0);

            ProjectModel model1 = Converter.Upgrade(m_LocalNow, project_v0_1_0!);
            ProjectModel model2 = Converter.Upgrade(m_LocalNow, project_v0_2_0!);
            CompareModelsPreV0_6_0(model1, model2);
        }

        [Fact]
        public void Converter_Given_v0_2_0_Input_ThenConvertsTo_v0_2_1()
        {
            v0_2_0.ProjectModel? project_v0_2_0 = JsonConvert.DeserializeObject<v0_2_0.ProjectModel>(m_Fixture.V0_2_0_JsonString);
            v0_2_1.ProjectModel? project_v0_2_1 = JsonConvert.DeserializeObject<v0_2_1.ProjectModel>(m_Fixture.V0_2_1_JsonString);
            var mapper = new VersionMapper();
            v0_2_1.ProjectModel project_v0_2_1_upgraded = v0_2_1.Converter.Upgrade(mapper, project_v0_2_0!);
            project_v0_2_1_upgraded.ShouldBeEquivalentTo(project_v0_2_1);

            ProjectModel model1 = Converter.Upgrade(m_LocalNow, project_v0_2_0!);
            ProjectModel model2 = Converter.Upgrade(m_LocalNow, project_v0_2_1!);
            CompareModelsPreV0_6_0(model1, model2);
        }

        [Fact]
        public void Converter_Given_v0_2_1_Input_ThenConvertsTo_v0_3_0()
        {
            v0_2_1.ProjectModel? project_v0_2_1 = JsonConvert.DeserializeObject<v0_2_1.ProjectModel>(m_Fixture.V0_2_1_JsonString);
            v0_3_0.ProjectModel? project_v0_3_0 = JsonConvert.DeserializeObject<v0_3_0.ProjectModel>(m_Fixture.V0_3_0_JsonString);
            var mapper = new VersionMapper();
            v0_3_0.ProjectModel project_v0_3_0_upgraded = v0_3_0.Converter.Upgrade(mapper, project_v0_2_1!);
            project_v0_3_0_upgraded.ShouldBeEquivalentTo(project_v0_3_0);

            ProjectModel model1 = Converter.Upgrade(m_LocalNow, project_v0_2_1!);
            ProjectModel model2 = Converter.Upgrade(m_LocalNow, project_v0_3_0!);
            CompareModelsPreV0_6_0(model1, model2);
        }

        [Fact]
        public void Converter_Given_v0_3_0_Input_ThenConvertsTo_v0_3_1()
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
        public void Converter_Given_v0_3_1_Input_ThenConvertsTo_v0_3_2()
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
        public void Converter_Given_v0_3_2_Input_ThenConvertsTo_v0_4_0()
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
        public void Converter_Given_v0_4_0_Input_ThenConvertsTo_v0_4_1()
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
        public void Converter_Given_v0_4_1_Input_ThenConvertsTo_v0_4_2()
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
        public void Converter_Given_v0_4_2_Input_ThenConvertsTo_v0_4_3()
        {
            v0_4_2.ProjectModel? project_v0_4_2 = JsonConvert.DeserializeObject<v0_4_2.ProjectModel>(m_Fixture.V0_4_2d_JsonString);
            v0_4_3.ProjectModel? project_v0_4_3 = JsonConvert.DeserializeObject<v0_4_3.ProjectModel>(m_Fixture.V0_4_3d_JsonString);
            var mapper = new VersionMapper();
            v0_4_3.ProjectModel project_v0_4_2_upgraded = v0_4_3.Converter.Upgrade(mapper, project_v0_4_2!);
            project_v0_4_2_upgraded.ShouldBeEquivalentTo(project_v0_4_3);

            ProjectModel model1 = Converter.Upgrade(m_LocalNow, project_v0_4_2!);
            ProjectModel model2 = Converter.Upgrade(m_LocalNow, project_v0_4_3!);
            CompareModelsPreV0_6_0(model1, model2);
        }

        [Fact]
        public void Converter_Given_v0_4_3_Input_ThenConvertsTo_v0_4_4()
        {
            v0_4_3.ProjectModel? project_v0_4_3 = JsonConvert.DeserializeObject<v0_4_3.ProjectModel>(m_Fixture.V0_4_3d_JsonString);
            v0_4_4.ProjectModel? project_v0_4_4 = JsonConvert.DeserializeObject<v0_4_4.ProjectModel>(m_Fixture.V0_4_4d_JsonString);
            var mapper = new VersionMapper();
            v0_4_4.ProjectModel project_v0_4_3_upgraded = v0_4_4.Converter.Upgrade(mapper, project_v0_4_3!);
            project_v0_4_3_upgraded.ShouldBeEquivalentTo(project_v0_4_4);

            ProjectModel model1 = Converter.Upgrade(m_LocalNow, project_v0_4_3!);
            ProjectModel model2 = Converter.Upgrade(m_LocalNow, project_v0_4_4!);
            CompareModelsPreV0_6_0(model1, model2);
        }

        [Fact]
        public void Converter_Given_v0_4_4_Input_ThenConvertsTo_v0_5_0()
        {
            v0_4_4.ProjectModel? project_v0_4_4 = JsonConvert.DeserializeObject<v0_4_4.ProjectModel>(m_Fixture.V0_4_4d_JsonString);
            v0_5_0.ProjectModel? project_v0_5_0 = JsonConvert.DeserializeObject<v0_5_0.ProjectModel>(m_Fixture.V0_5_0_JsonString);
            var mapper = new VersionMapper();
            v0_5_0.ProjectModel project_v0_4_4_upgraded = v0_5_0.Converter.Upgrade(mapper, project_v0_4_4!);
            project_v0_4_4_upgraded.ShouldBeEquivalentTo(project_v0_5_0);

            ProjectModel model1 = Converter.Upgrade(m_LocalNow, project_v0_4_4!);
            ProjectModel model2 = Converter.Upgrade(m_LocalNow, project_v0_5_0!);
            CompareModelsPreV0_6_0(model1, model2);
        }

        [Fact]
        public void Converter_Given_v0_5_0_Input_ThenConvertsTo_v0_6_0()
        {
            v0_5_0.ProjectModel? project_v0_5_0 = JsonConvert.DeserializeObject<v0_5_0.ProjectModel>(m_Fixture.V0_5_0_JsonString);
            v0_6_0.ProjectModel? project_v0_6_0 = JsonConvert.DeserializeObject<v0_6_0.ProjectModel>(m_Fixture.V0_6_0_JsonString);
            var mapper = new VersionMapper();
            v0_6_0.ProjectModel project_v0_5_0_upgraded = v0_6_0.Converter.Upgrade(mapper, m_LocalNow, project_v0_5_0!);
            //project_v0_5_0_upgraded.ShouldBeEquivalentTo(project_v0_6_0);

            ProjectModel model1 = Converter.Upgrade(m_LocalNow, project_v0_5_0!);
            ProjectModel model2 = Converter.Upgrade(project_v0_6_0!);
            CompareModels(model1, model2);
        }
    }
}