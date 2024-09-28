using AutoMapper;
using FluentAssertions;
using Newtonsoft.Json;
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
            v0_1_0.ProjectPlanModel? projectPlan_v0_1_0 = JsonConvert.DeserializeObject<v0_1_0.ProjectPlanModel>(m_Fixture.V0_1_0_JsonString);
            v0_2_0.ProjectPlanModel? projectPlan_v0_2_0 = JsonConvert.DeserializeObject<v0_2_0.ProjectPlanModel>(m_Fixture.V0_2_0_JsonString);
            v0_2_0.ProjectPlanModel? projectPlan_v0_2_0_upgraded = v0_2_0.Converter.Upgrade(projectPlan_v0_1_0!);
            projectPlan_v0_2_0_upgraded.Should().BeEquivalentTo(projectPlan_v0_2_0);

            ProjectPlanModel model1 = Converter.Upgrade(projectPlan_v0_1_0!);
            ProjectPlanModel model2 = Converter.Upgrade(projectPlan_v0_2_0!);
            model1.Should().BeEquivalentTo(model2);
        }

        [Fact]
        public void Converter_Given_v0_2_0_Input_ThenConvertsTo_v0_2_1()
        {
            v0_2_0.ProjectPlanModel? projectPlan_v0_2_0 = JsonConvert.DeserializeObject<v0_2_0.ProjectPlanModel>(m_Fixture.V0_2_0_JsonString);
            v0_2_1.ProjectPlanModel? projectPlan_v0_2_1 = JsonConvert.DeserializeObject<v0_2_1.ProjectPlanModel>(m_Fixture.V0_2_1_JsonString);
            IMapper mapper = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>()).CreateMapper();
            v0_2_1.ProjectPlanModel projectPlan_v0_2_1_upgraded = v0_2_1.Converter.Upgrade(mapper, projectPlan_v0_2_0!);
            projectPlan_v0_2_1_upgraded.Should().BeEquivalentTo(projectPlan_v0_2_1);

            ProjectPlanModel model1 = Converter.Upgrade(projectPlan_v0_2_0!);
            ProjectPlanModel model2 = Converter.Upgrade(projectPlan_v0_2_1!);
            model1.Should().BeEquivalentTo(model2);
        }

        [Fact]
        public void Converter_Given_v0_2_1_Input_ThenConvertsTo_v0_3_0()
        {
            v0_2_1.ProjectPlanModel? projectPlan_v0_2_1 = JsonConvert.DeserializeObject<v0_2_1.ProjectPlanModel>(m_Fixture.V0_2_1_JsonString);
            v0_3_0.ProjectPlanModel? projectPlan_v0_3_0 = JsonConvert.DeserializeObject<v0_3_0.ProjectPlanModel>(m_Fixture.V0_3_0_JsonString);
            IMapper mapper = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>()).CreateMapper();
            v0_3_0.ProjectPlanModel projectPlan_v0_3_0_upgraded = v0_3_0.Converter.Upgrade(mapper, projectPlan_v0_2_1!);
            projectPlan_v0_3_0_upgraded.Should().BeEquivalentTo(projectPlan_v0_3_0);

            ProjectPlanModel model1 = Converter.Upgrade(projectPlan_v0_2_1!);
            ProjectPlanModel model2 = Converter.Upgrade(projectPlan_v0_3_0!);
            model1.Should().BeEquivalentTo(model2);
        }

        [Fact]
        public void Converter_Given_v0_3_0_Input_ThenConvertsTo_v0_3_1()
        {
            v0_3_0.ProjectPlanModel? projectPlan_v0_3_0 = JsonConvert.DeserializeObject<v0_3_0.ProjectPlanModel>(m_Fixture.V0_3_0_JsonString);
            v0_3_1.ProjectPlanModel? projectPlan_v0_3_1 = JsonConvert.DeserializeObject<v0_3_1.ProjectPlanModel>(m_Fixture.V0_3_1_JsonString);
            IMapper mapper = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>()).CreateMapper();
            v0_3_1.ProjectPlanModel projectPlan_v0_3_0_upgraded = v0_3_1.Converter.Upgrade(mapper, projectPlan_v0_3_0!);
            projectPlan_v0_3_0_upgraded.Should().BeEquivalentTo(projectPlan_v0_3_1);

            ProjectPlanModel model1 = Converter.Upgrade(projectPlan_v0_3_0!);
            ProjectPlanModel model2 = Converter.Upgrade(projectPlan_v0_3_1!);
            model1.Should().BeEquivalentTo(model2);
        }

        [Fact]
        public void Converter_Given_v0_3_1_Input_ThenConvertsTo_v0_3_2()
        {
            v0_3_1.ProjectPlanModel? projectPlan_v0_3_1 = JsonConvert.DeserializeObject<v0_3_1.ProjectPlanModel>(m_Fixture.V0_3_1_JsonString);
            v0_3_2.ProjectPlanModel? projectPlan_v0_3_2 = JsonConvert.DeserializeObject<v0_3_2.ProjectPlanModel>(m_Fixture.V0_3_2_JsonString);
            IMapper mapper = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>()).CreateMapper();
            v0_3_2.ProjectPlanModel projectPlan_v0_3_1_upgraded = v0_3_2.Converter.Upgrade(mapper, projectPlan_v0_3_1!);
            projectPlan_v0_3_1_upgraded.Should().BeEquivalentTo(projectPlan_v0_3_2);

            ProjectPlanModel model1 = Converter.Upgrade(projectPlan_v0_3_1!);
            ProjectPlanModel model2 = Converter.Upgrade(projectPlan_v0_3_2!);
            model1.Should().BeEquivalentTo(model2);
        }


        [Fact]
        public void Converter_Given_v0_3_2_Input_ThenConvertsTo_v0_4_0()
        {
            v0_3_2.ProjectPlanModel? projectPlan_v0_3_2 = JsonConvert.DeserializeObject<v0_3_2.ProjectPlanModel>(m_Fixture.Va_0_3_2_JsonString);
            v0_4_0.ProjectPlanModel? projectPlan_v0_4_0 = JsonConvert.DeserializeObject<v0_4_0.ProjectPlanModel>(m_Fixture.Va_0_4_0_JsonString);
            IMapper mapper = new MapperConfiguration(cfg => cfg.AddProfile<MapperProfile>()).CreateMapper();
            v0_4_0.ProjectPlanModel projectPlan_v0_3_2_upgraded = v0_4_0.Converter.Upgrade(mapper, projectPlan_v0_3_2!);
            projectPlan_v0_3_2_upgraded.Should().BeEquivalentTo(projectPlan_v0_4_0);

            ProjectPlanModel model1 = Converter.Upgrade(projectPlan_v0_3_2!);
            ProjectPlanModel model2 = Converter.Upgrade(projectPlan_v0_4_0!);
            model1.Should().BeEquivalentTo(model2);
        }
    }
}