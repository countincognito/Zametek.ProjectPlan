using Newtonsoft.Json;
using Shouldly;
using System.Collections.Generic;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    public class ProjectScenarioHelperTests
        : IClassFixture<ProjectScenarioHelperFixture>
    {
        private readonly ProjectScenarioHelperFixture m_Fixture;

        public ProjectScenarioHelperTests(ProjectScenarioHelperFixture fixture)
        {
            m_Fixture = fixture;
        }

        public static TheoryData<int[], (int, int)[], (int, int)[]> IdMappingData()
        {
            return ProjectScenarioHelperFixture.IdMappingData;
        }

        [Theory]
        [MemberData(nameof(IdMappingData))]
        public void ProjectScenarioHelper_Given_InputIdsAndIdUpdates_Then_ConvertsToExpectedIdUpdates(
            int[] inputIds,
            (int, int)[] idUpdates,
            (int, int)[] expectedIdUpdates)
        {
            List<(int, int)> newIdUpdates = ProjectScenarioHelper.UpdateIds([.. inputIds], [.. idUpdates]);
            newIdUpdates.ShouldBe(expectedIdUpdates);
        }





        [Fact]
        public void ProjectScenarioHelper_UpdateActivityIds()
        {




            ProjectScenarioModel? projectScenario = JsonConvert.DeserializeObject<ProjectScenarioModel>(m_Fixture.Input_JsonString);



            var remapped = ProjectScenarioHelper.UpdateActivityIds(projectScenario!, [(3, 5), (6, 8)]).ShouldNotBeNull();



        }
    }
}
