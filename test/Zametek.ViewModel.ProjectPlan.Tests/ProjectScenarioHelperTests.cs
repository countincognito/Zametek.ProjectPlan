using Xunit;
using Shouldly;
using System.Collections.Generic;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    public class ProjectScenarioHelperTests
    {
        public static IEnumerable<object[]> GetTestData()
        {
            return
            [
                [
                    new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
                    new[] { (3, 4), (4, 9), (5, 8) },
                    new[] { (1, 1), (2, 2), (3, 4), (4, 9), (5, 8), (6, 6), (7, 7), (8, 10), (9, 11), (10, 12)  },
                ],
                [
                    new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
                    new[] { (8, 4), (9, 5), (10, 6) },
                    new[] { (1, 1), (2, 2), (3, 3), (4, 7), (5, 8), (6, 9), (7, 10), (8, 4), (9, 5), (10, 6)  },
                ]
            ];
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public void ProjectScenarioHelper_Given_InputIdsAndIdUpdates_ThenConvertsToExpectedIdUpdates(
            int[] inputIds,
            (int, int)[] idUpdates,
            (int, int)[] expectedIdUpdates)
        {
            List<(int, int)> newIdUpdates = ProjectScenarioHelper.UpdateIds([.. inputIds], [.. idUpdates]);
            newIdUpdates.ShouldBe(expectedIdUpdates);
        }
    }
}
