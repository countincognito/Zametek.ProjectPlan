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
        public void ProjectScenarioHelper_Given_InputIdsAndIdMaps_Then_ConvertsToExpectedIdMaps(
            int[] inputIds,
            (int, int)[] idMaps,
            (int, int)[] expectedIdMaps)
        {
            List<(int, int)> newIdUpdates = ProjectScenarioHelper.RefineIdMaps([.. inputIds], [.. idMaps]);
            newIdUpdates.ShouldBe(expectedIdMaps);
        }

        [Fact]
        public void ProjectScenarioHelper_Given_UpdateActivityIds_Then_MappedToExpectedIds()
        {
            ProjectScenarioModel? input = JsonConvert.DeserializeObject<ProjectScenarioModel>(m_Fixture.Input_JsonString);
            ProjectScenarioModel? expectedOutput = JsonConvert.DeserializeObject<ProjectScenarioModel>(m_Fixture.RemappedActivities_JsonString);
            var remapped = ProjectScenarioHelper.UpdateActivityIds(input!, [(3, 5), (6, 8)]).ShouldNotBeNull();
            remapped.ShouldBeEquivalentTo(expectedOutput!);
        }

        [Fact]
        public void ProjectScenarioHelper_Given_UpdateResourceIds_Then_MappedToExpectedIds()
        {
            ProjectScenarioModel? input = JsonConvert.DeserializeObject<ProjectScenarioModel>(m_Fixture.Input_JsonString);
            ProjectScenarioModel? expectedOutput = JsonConvert.DeserializeObject<ProjectScenarioModel>(m_Fixture.RemappedResources_JsonString);
            var remapped = ProjectScenarioHelper.UpdateResourceIds(input!, [(3, 5), (6, 8)]).ShouldNotBeNull();
            remapped.ShouldBeEquivalentTo(expectedOutput!);
        }

        [Fact]
        public void ProjectScenarioHelper_Given_UpdateWorkStreamIds_Then_MappedToExpectedIds()
        {
            ProjectScenarioModel? input = JsonConvert.DeserializeObject<ProjectScenarioModel>(m_Fixture.Input_JsonString);
            ProjectScenarioModel? expectedOutput = JsonConvert.DeserializeObject<ProjectScenarioModel>(m_Fixture.RemappedWorkStreams_JsonString);
            var remapped = ProjectScenarioHelper.UpdateWorkStreamIds(input!, [(2, 5), (1, 2)]).ShouldNotBeNull();
            remapped.ShouldBeEquivalentTo(expectedOutput!);
        }
    }
}
