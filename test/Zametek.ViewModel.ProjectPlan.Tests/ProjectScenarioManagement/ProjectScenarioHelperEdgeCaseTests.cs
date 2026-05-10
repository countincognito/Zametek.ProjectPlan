using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Edge-case tests for ProjectScenarioHelper.RefineIdMaps that are not
    /// covered by the fixture-driven theory tests.
    /// All tests call RefineIdMaps directly — no file I/O is required.
    /// </summary>
    public class ProjectScenarioHelperEdgeCaseTests
    {
        #region Empty input

        [Fact]
        public void RefineIdMaps_EmptyOriginalIds_And_EmptyMaps_Returns_EmptyList()
        {
            List<(int, int)> result = ProjectScenarioHelper.RefineIdMaps([], []);
            result.ShouldBeEmpty();
        }

        #endregion

        #region Single element

        [Fact]
        public void RefineIdMaps_SingleId_NoMaps_Returns_IdentityMap()
        {
            List<(int, int)> result = ProjectScenarioHelper.RefineIdMaps([42], []);
            result.ShouldHaveSingleItem();
            result[0].ShouldBe((42, 42));
        }

        [Fact]
        public void RefineIdMaps_SingleId_MappedToItself_Returns_IdentityMap()
        {
            List<(int, int)> result = ProjectScenarioHelper.RefineIdMaps([7], [(7, 7)]);
            result.ShouldHaveSingleItem();
            result[0].ShouldBe((7, 7));
        }

        [Fact]
        public void RefineIdMaps_SingleId_MappedToHigherValue_Returns_RequestedMap()
        {
            // [5] remapped to [(5, 99)] — no conflicts, so the mapping is honoured exactly.
            List<(int, int)> result = ProjectScenarioHelper.RefineIdMaps([5], [(5, 99)]);
            result.ShouldHaveSingleItem();
            result[0].ShouldBe((5, 99));
        }

        #endregion

        #region No maps — identity

        [Fact]
        public void RefineIdMaps_NoMaps_Returns_IdentityForAllOriginalIds()
        {
            int[] ids = [3, 7, 15];
            List<(int, int)> result = ProjectScenarioHelper.RefineIdMaps([.. ids], []);
            result.Count.ShouldBe(ids.Length);
            foreach (int id in ids)
            {
                result.ShouldContain((id, id));
            }
        }

        #endregion

        #region Gaps in ID sequence

        [Fact]
        public void RefineIdMaps_GappedIds_NoMaps_Returns_IdentityForEachId()
        {
            // Non-contiguous IDs: 1, 5, 10, 100
            List<(int, int)> result = ProjectScenarioHelper.RefineIdMaps([1, 5, 10, 100], []);
            result.Count.ShouldBe(4);
            result.ShouldContain((1, 1));
            result.ShouldContain((5, 5));
            result.ShouldContain((10, 10));
            result.ShouldContain((100, 100));
        }

        [Fact]
        public void RefineIdMaps_GappedIds_WithMap_LockedIdIsPreserved()
        {
            // IDs: 1, 5, 10. Map ID 1 → 5. Since 5 is already in use, it gets bumped.
            List<(int, int)> result = ProjectScenarioHelper.RefineIdMaps([1, 5, 10], [(1, 5)]);
            // ID 1 must land on 5 (locked).
            result.ShouldContain((1, 5));
            // ID 5 must NOT land on 5 (it was the locked target, so it gets bumped).
            result.Single(x => x.Item1 == 5).Item2.ShouldNotBe(5);
        }

        #endregion

        #region Target higher than all existing IDs (no conflict)

        [Fact]
        public void RefineIdMaps_MapToIdFarAboveExisting_NoOtherIdsMoved()
        {
            // IDs: 1, 2, 3. Map 2 → 1000. IDs 1 and 3 should stay at 1 and 3.
            List<(int, int)> result = ProjectScenarioHelper.RefineIdMaps([1, 2, 3], [(2, 1000)]);
            result.ShouldContain((2, 1000));
            result.ShouldContain((1, 1));
            result.ShouldContain((3, 3));
        }

        #endregion

        #region Map where source and target IDs overlap (swap-like)

        [Fact]
        public void RefineIdMaps_HighIdsRemappedToLowValues_DoesNotDoubleMap()
        {
            // Based on the existing fixture case: [4..13], map 11→1, 12→2, 13→3.
            // This is a "downward" remap of the high IDs into the range below the existing IDs.
            List<int> original = [4, 5, 6, 7, 8, 9, 10, 11, 12, 13];
            List<(int, int)> maps = [(11, 1), (12, 2), (13, 3)];
            List<(int, int)> result = ProjectScenarioHelper.RefineIdMaps(original, maps);

            // Locked mappings must be honoured.
            result.ShouldContain((11, 1));
            result.ShouldContain((12, 2));
            result.ShouldContain((13, 3));

            // All ToIds must be unique (no two source IDs land on the same target).
            var toIds = result.Select(x => x.Item2).ToList();
            toIds.Distinct().Count().ShouldBe(toIds.Count);
        }

        #endregion

        #region All target IDs are unique (invariant)

        [Theory]
        [InlineData(new[] { 1, 2, 3, 4, 5 }, new int[] { }, new int[] { })]
        [InlineData(new[] { 1, 2, 3, 4, 5 }, new[] { 1, 3 }, new[] { 4, 7 })]
        [InlineData(new[] { 1, 2, 3, 4, 5 }, new[] { 5 }, new[] { 2 })]
        public void RefineIdMaps_AllResultingToIds_AreUnique(
            int[] originalIds,
            int[] fromIds,
            int[] toIds)
        {
            List<(int, int)> maps = fromIds.Zip(toIds, (f, t) => (f, t)).ToList();
            List<(int, int)> result = ProjectScenarioHelper.RefineIdMaps([.. originalIds], maps);

            var resultToIds = result.Select(x => x.Item2).ToList();
            resultToIds.Distinct().Count().ShouldBe(resultToIds.Count,
                "All ToIds in the result must be unique");
        }

        #endregion

        #region Order of result is by FromId

        [Fact]
        public void RefineIdMaps_Result_IsOrderedByFromId()
        {
            List<(int, int)> result = ProjectScenarioHelper.RefineIdMaps(
                [10, 5, 3, 1],
                []);

            for (int i = 0; i < result.Count - 1; i++)
            {
                result[i].Item1.ShouldBeLessThan(result[i + 1].Item1);
            }
        }

        #endregion
    }
}
