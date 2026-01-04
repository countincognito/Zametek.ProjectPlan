using AutoMapper;
using Newtonsoft.Json;
using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan.Tests
{
    public class ConverterTests
        : IClassFixture<ConverterFixture>
    {
        private readonly ConverterFixture m_Fixture;

        public ConverterTests(ConverterFixture fixture)
        {
            m_Fixture = fixture;
        }

        [Fact]
        public void Converter_Given_v0_1_0_Input_ThenConvertsTo_v0_2_0()
        {
            v0_1_0.ProjectModel? project_v0_1_0 = JsonConvert.DeserializeObject<v0_1_0.ProjectModel>(m_Fixture.V0_1_0_JsonString);
            v0_2_0.ProjectModel? project_v0_2_0 = JsonConvert.DeserializeObject<v0_2_0.ProjectModel>(m_Fixture.V0_2_0_JsonString);
            v0_2_0.ProjectModel? project_v0_2_0_upgraded = v0_2_0.Converter.Upgrade(project_v0_1_0!);
            project_v0_2_0_upgraded.ShouldBeEquivalentTo(project_v0_2_0);

            ProjectModel model1 = Converter.Upgrade(project_v0_1_0!);
            ProjectModel model2 = Converter.Upgrade(project_v0_2_0!);
            model1.ShouldBeEquivalentTo(model2);
        }

        [Fact]
        public void Converter_Given_v0_2_0_Input_ThenConvertsTo_v0_2_1()
        {
            v0_2_0.ProjectModel? project_v0_2_0 = JsonConvert.DeserializeObject<v0_2_0.ProjectModel>(m_Fixture.V0_2_0_JsonString);
            v0_2_1.ProjectModel? project_v0_2_1 = JsonConvert.DeserializeObject<v0_2_1.ProjectModel>(m_Fixture.V0_2_1_JsonString);
            IMapper mapper = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>()).CreateMapper();
            v0_2_1.ProjectModel project_v0_2_1_upgraded = v0_2_1.Converter.Upgrade(mapper, project_v0_2_0!);
            project_v0_2_1_upgraded.ShouldBeEquivalentTo(project_v0_2_1);

            ProjectModel model1 = Converter.Upgrade(project_v0_2_0!);
            ProjectModel model2 = Converter.Upgrade(project_v0_2_1!);
            model1.ShouldBeEquivalentTo(model2);
        }

        [Fact]
        public void Converter_Given_v0_2_1_Input_ThenConvertsTo_v0_3_0()
        {
            v0_2_1.ProjectModel? project_v0_2_1 = JsonConvert.DeserializeObject<v0_2_1.ProjectModel>(m_Fixture.V0_2_1_JsonString);
            v0_3_0.ProjectModel? project_v0_3_0 = JsonConvert.DeserializeObject<v0_3_0.ProjectModel>(m_Fixture.V0_3_0_JsonString);
            IMapper mapper = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>()).CreateMapper();
            v0_3_0.ProjectModel project_v0_3_0_upgraded = v0_3_0.Converter.Upgrade(mapper, project_v0_2_1!);
            project_v0_3_0_upgraded.ShouldBeEquivalentTo(project_v0_3_0);

            ProjectModel model1 = Converter.Upgrade(project_v0_2_1!);
            ProjectModel model2 = Converter.Upgrade(project_v0_3_0!);
            model1.ShouldBeEquivalentTo(model2);
        }

        [Fact]
        public void Converter_Given_v0_3_0_Input_ThenConvertsTo_v0_3_1()
        {
            v0_3_0.ProjectModel? project_v0_3_0 = JsonConvert.DeserializeObject<v0_3_0.ProjectModel>(m_Fixture.V0_3_0_JsonString);
            v0_3_1.ProjectModel? project_v0_3_1 = JsonConvert.DeserializeObject<v0_3_1.ProjectModel>(m_Fixture.V0_3_1_JsonString);
            IMapper mapper = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>()).CreateMapper();
            v0_3_1.ProjectModel project_v0_3_0_upgraded = v0_3_1.Converter.Upgrade(mapper, project_v0_3_0!);
            project_v0_3_0_upgraded.ShouldBeEquivalentTo(project_v0_3_1);

            ProjectModel model1 = Converter.Upgrade(project_v0_3_0!);
            ProjectModel model2 = Converter.Upgrade(project_v0_3_1!);
            model1.ShouldBeEquivalentTo(model2);
        }

        [Fact]
        public void Converter_Given_v0_3_1_Input_ThenConvertsTo_v0_3_2()
        {
            v0_3_1.ProjectModel? project_v0_3_1 = JsonConvert.DeserializeObject<v0_3_1.ProjectModel>(m_Fixture.V0_3_1_JsonString);
            v0_3_2.ProjectModel? project_v0_3_2 = JsonConvert.DeserializeObject<v0_3_2.ProjectModel>(m_Fixture.V0_3_2_JsonString);
            IMapper mapper = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>()).CreateMapper();
            v0_3_2.ProjectModel project_v0_3_1_upgraded = v0_3_2.Converter.Upgrade(mapper, project_v0_3_1!);
            project_v0_3_1_upgraded.ShouldBeEquivalentTo(project_v0_3_2);

            ProjectModel model1 = Converter.Upgrade(project_v0_3_1!);
            ProjectModel model2 = Converter.Upgrade(project_v0_3_2!);
            model1.ShouldBeEquivalentTo(model2);
        }

        [Fact]
        public void Converter_Given_v0_3_2_Input_ThenConvertsTo_v0_4_0()
        {
            v0_3_2.ProjectModel? project_v0_3_2 = JsonConvert.DeserializeObject<v0_3_2.ProjectModel>(m_Fixture.V0_3_2a_JsonString);
            v0_4_0.ProjectModel? project_v0_4_0 = JsonConvert.DeserializeObject<v0_4_0.ProjectModel>(m_Fixture.V0_4_0a_JsonString);
            IMapper mapper = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>()).CreateMapper();
            v0_4_0.ProjectModel project_v0_3_2_upgraded = v0_4_0.Converter.Upgrade(mapper, project_v0_3_2!);
            project_v0_3_2_upgraded.ShouldBeEquivalentTo(project_v0_4_0);

            ProjectModel model1 = Converter.Upgrade(project_v0_3_2!);
            ProjectModel model2 = Converter.Upgrade(project_v0_4_0!);
            model1.ShouldBeEquivalentTo(model2);
        }

        [Fact]
        public void Converter_Given_v0_4_0_Input_ThenConvertsTo_v0_4_1()
        {
            v0_4_0.ProjectModel? project_v0_4_0 = JsonConvert.DeserializeObject<v0_4_0.ProjectModel>(m_Fixture.V0_4_0b_JsonString);
            v0_4_1.ProjectModel? project_v0_4_1 = JsonConvert.DeserializeObject<v0_4_1.ProjectModel>(m_Fixture.V0_4_1b_JsonString);
            IMapper mapper = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>()).CreateMapper();
            v0_4_1.ProjectModel project_v0_4_0_upgraded = v0_4_1.Converter.Upgrade(mapper, project_v0_4_0!);
            project_v0_4_0_upgraded.ShouldBeEquivalentTo(project_v0_4_1);

            ProjectModel model1 = Converter.Upgrade(project_v0_4_0!);
            ProjectModel model2 = Converter.Upgrade(project_v0_4_1!);
            model1.ShouldBeEquivalentTo(model2);
        }

        [Fact]
        public void Converter_Given_v0_4_1_Input_ThenConvertsTo_v0_4_2()
        {
            v0_4_1.ProjectModel? project_v0_4_1 = JsonConvert.DeserializeObject<v0_4_1.ProjectModel>(m_Fixture.V0_4_1c_JsonString);
            v0_4_2.ProjectModel? project_v0_4_2 = JsonConvert.DeserializeObject<v0_4_2.ProjectModel>(m_Fixture.V0_4_2c_JsonString);
            IMapper mapper = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>()).CreateMapper();
            v0_4_2.ProjectModel project_v0_4_1_upgraded = v0_4_2.Converter.Upgrade(mapper, project_v0_4_1!);
            project_v0_4_1_upgraded.ShouldBeEquivalentTo(project_v0_4_2);

            ProjectModel model1 = Converter.Upgrade(project_v0_4_1!);
            ProjectModel model2 = Converter.Upgrade(project_v0_4_2!);
            model1.ShouldBeEquivalentTo(model2);
        }

        [Fact]
        public void Converter_Given_v0_4_2_Input_ThenConvertsTo_v0_4_3()
        {
            v0_4_2.ProjectModel? project_v0_4_2 = JsonConvert.DeserializeObject<v0_4_2.ProjectModel>(m_Fixture.V0_4_2d_JsonString);
            v0_4_3.ProjectModel? project_v0_4_3 = JsonConvert.DeserializeObject<v0_4_3.ProjectModel>(m_Fixture.V0_4_3d_JsonString);
            IMapper mapper = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>()).CreateMapper();
            v0_4_3.ProjectModel project_v0_4_2_upgraded = v0_4_3.Converter.Upgrade(mapper, project_v0_4_2!);
            project_v0_4_2_upgraded.ShouldBeEquivalentTo(project_v0_4_3);

            ProjectModel model1 = Converter.Upgrade(project_v0_4_2!);
            ProjectModel model2 = Converter.Upgrade(project_v0_4_3!);
            model1.ShouldBeEquivalentTo(model2);
        }

        [Fact]
        public void Converter_Given_v0_4_3_Input_ThenConvertsTo_v0_4_4()
        {
            v0_4_3.ProjectModel? project_v0_4_3 = JsonConvert.DeserializeObject<v0_4_3.ProjectModel>(m_Fixture.V0_4_3d_JsonString);
            v0_4_4.ProjectModel? project_v0_4_4 = JsonConvert.DeserializeObject<v0_4_4.ProjectModel>(m_Fixture.V0_4_4d_JsonString);
            IMapper mapper = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>()).CreateMapper();
            v0_4_4.ProjectModel project_v0_4_3_upgraded = v0_4_4.Converter.Upgrade(mapper, project_v0_4_3!);
            project_v0_4_3_upgraded.ShouldBeEquivalentTo(project_v0_4_4);

            ProjectModel model1 = Converter.Upgrade(project_v0_4_3!);
            ProjectModel model2 = Converter.Upgrade(project_v0_4_4!);
            model1.ShouldBeEquivalentTo(model2);
        }

        [Fact]
        public void Converter_Given_v0_4_4_Input_ThenConvertsTo_v0_5_0()
        {
            v0_4_4.ProjectModel? project_v0_4_4 = JsonConvert.DeserializeObject<v0_4_4.ProjectModel>(m_Fixture.V0_4_4d_JsonString);
            v0_5_0.ProjectModel? project_v0_5_0 = JsonConvert.DeserializeObject<v0_5_0.ProjectModel>(m_Fixture.V0_5_0_JsonString);
            IMapper mapper = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>()).CreateMapper();
            v0_5_0.ProjectModel project_v0_4_4_upgraded = v0_5_0.Converter.Upgrade(mapper, project_v0_4_4!);
            project_v0_4_4_upgraded.ShouldBeEquivalentTo(project_v0_5_0);

            ProjectModel model1 = Converter.Upgrade(project_v0_4_4!);
            ProjectModel model2 = Converter.Upgrade(project_v0_5_0!);
            model1.ShouldBeEquivalentTo(model2);
        }
    }
}