using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the version mappings, driven by reflection rather than by hand-built
    /// fixtures.
    ///
    /// Two things can go wrong here and neither announces itself. A property that is not
    /// carried through a mapping is not an error on either side, it is simply a default
    /// where a value used to be; and a collection whose type is the same on both sides is
    /// assigned across rather than copied, so the mapped result and the thing it was
    /// mapped from turn out to be the same list. The first is caught by filling every
    /// property with a value that is not its default and comparing everything back; the
    /// second by walking both graphs afterwards looking for a list that appears twice.
    ///
    /// Where a version genuinely has no room for something, the test names the property
    /// rather than relaxing the comparison, so this file doubles as the record of what
    /// each stored version can and cannot hold.
    /// </summary>
    public class VersionMapperTests
    {
        #region What no stored version holds

        // The version stamp is the format's own, not the plan's: a mapping to v0.6.1
        // writes "v0.6.1" over whatever it was given, which is the point of it.
        private const string c_VersionStamp = @"$.Version";

        // Slack and the finish and late-start times are derived from the duration and
        // the schedule window, so no stored version carries them - the compiler works
        // them out again on load.
        private static readonly string[] s_DerivedActivityValues =
        [
            @"$.DependentActivities[].Activity.TotalSlack",
            @"$.DependentActivities[].Activity.LatestStartTime",
            @"$.DependentActivities[].Activity.EarliestFinishTime",
        ];

        // A resource's activity trackers record which activity was worked on by id; the
        // name shown beside it is looked up when the plan is loaded rather than stored.
        private const string c_TrackedActivityName =
            @"$.ResourceSettings.Resources[].Trackers[].ActivityTrackers[].ActivityName";

        private static string[] StoredScenarioLosses(params string[] extra) =>
            [.. s_DerivedActivityValues, c_TrackedActivityName, .. extra];

        private static string[] ForFilesIn(ProjectModel _, params string[] scenarioPaths) =>
            [.. scenarioPaths.Select(x => x.Replace(@"$.", @"$.Files[].Scenario.", StringComparison.Ordinal))];

        #endregion

        #region Current <-> v0.6.1

        /// <summary>
        /// The v0.6.1 models mirror the current ones, so a project that goes down to
        /// v0.6.1 and comes back is the project it started as, apart from the version
        /// stamp and the values no version stores.
        /// </summary>
        [Fact]
        public void Project_RoundTrippedThroughV0_6_1_Then_NothingIsLost()
        {
            var mapper = new VersionMapper();
            ProjectModel original = ModelReflection.Fill<ProjectModel>();

            ProjectModel roundTripped =
                mapper.FromV0_6_1ToCurrent(mapper.FromCurrentToV0_6_1(original));

            ModelReflection
                .Differences(original, roundTripped, [c_VersionStamp, .. ForFilesIn(original, StoredScenarioLosses())])
                .ShouldBeEmpty();

            roundTripped.Version.ShouldBe(@"v0.6.1");
        }

        [Fact]
        public void ProjectScenario_RoundTrippedThroughV0_6_1_Then_NothingIsLost()
        {
            var mapper = new VersionMapper();
            ProjectScenarioModel original = ModelReflection.Fill<ProjectScenarioModel>();

            ProjectScenarioModel roundTripped =
                mapper.FromV0_6_1ToCurrent(mapper.FromCurrentToV0_6_1(original));

            ModelReflection.Differences(original, roundTripped, StoredScenarioLosses()).ShouldBeEmpty();

            // The graph layouts and the per-resource metrics are what v0.6.1 added, so
            // this is the pair that has to carry them.
            roundTripped.ArrowGraphLayout.Nodes.Count.ShouldBe(original.ArrowGraphLayout.Nodes.Count);
            roundTripped.VertexGraphLayout.Nodes.Count.ShouldBe(original.VertexGraphLayout.Nodes.Count);
            roundTripped.ResourceMetrics.Count.ShouldBe(original.ResourceMetrics.Count);
        }

        [Fact]
        public void AppSettings_RoundTrippedThroughV0_6_1_Then_NothingIsLost()
        {
            var mapper = new VersionMapper();
            AppSettingsModel original = ModelReflection.Fill<AppSettingsModel>();

            AppSettingsModel roundTripped =
                mapper.FromV0_6_1ToCurrent(mapper.FromCurrentToV0_6_1(original));

            ModelReflection.Differences(original, roundTripped, c_VersionStamp).ShouldBeEmpty();

            roundTripped.Version.ShouldBe(@"v0.6.1");
            roundTripped.RecentProjectFilePaths.ShouldBe(original.RecentProjectFilePaths);
            roundTripped.CompilationTimeoutMilliseconds.ShouldBe(original.CompilationTimeoutMilliseconds);
        }

        [Fact]
        public void ProjectDisplaySettings_RoundTrippedThroughV0_6_1_Then_NothingIsLost()
        {
            var mapper = new VersionMapper();
            ProjectDisplaySettingsModel original = ModelReflection.Fill<ProjectDisplaySettingsModel>();

            ProjectDisplaySettingsModel roundTripped =
                mapper.FromV0_6_1ToCurrent(mapper.FromCurrentToV0_6_1(original));

            ModelReflection.Differences(original, roundTripped).ShouldBeEmpty();
        }

        /// <summary>
        /// The activity flags in particular, spelled out rather than left to the sweeps
        /// above, because losing one of these is the difference between a plan that
        /// charges for an activity and one that does not.
        /// </summary>
        [Theory]
        [InlineData(false, false, false, false)]
        [InlineData(true, false, false, false)]
        [InlineData(false, true, false, false)]
        [InlineData(false, false, true, false)]
        [InlineData(false, false, false, true)]
        [InlineData(true, true, true, true)]
        public void ActivityFlags_RoundTrippedThroughV0_6_1_Then_EachOneSurvives(
            bool hasNoCost,
            bool hasNoBilling,
            bool hasNoEffort,
            bool hasNoRisk)
        {
            var mapper = new VersionMapper();
            var original = new ActivityModel
            {
                Id = 1,
                HasNoCost = hasNoCost,
                HasNoBilling = hasNoBilling,
                HasNoEffort = hasNoEffort,
                HasNoRisk = hasNoRisk,
            };

            ActivityModel roundTripped =
                mapper.FromV0_6_1ToCurrent(mapper.FromCurrentToV0_6_1(original));

            roundTripped.HasNoCost.ShouldBe(hasNoCost);
            roundTripped.HasNoBilling.ShouldBe(hasNoBilling);
            roundTripped.HasNoEffort.ShouldBe(hasNoEffort);
            roundTripped.HasNoRisk.ShouldBe(hasNoRisk);
        }

        /// <summary>
        /// The same flags one version further down. v0.6.0 carries all four, so a plan
        /// saved by the previous release still knows which activities are exempt.
        /// </summary>
        [Theory]
        [InlineData(false, false, false, false)]
        [InlineData(true, false, false, false)]
        [InlineData(false, true, false, false)]
        [InlineData(false, false, true, false)]
        [InlineData(false, false, false, true)]
        [InlineData(true, true, true, true)]
        public void ActivityFlags_RoundTrippedThroughV0_6_0_Then_EachOneSurvives(
            bool hasNoCost,
            bool hasNoBilling,
            bool hasNoEffort,
            bool hasNoRisk)
        {
            var mapper = new VersionMapper();
            var original = new ActivityModel
            {
                Id = 1,
                HasNoCost = hasNoCost,
                HasNoBilling = hasNoBilling,
                HasNoEffort = hasNoEffort,
                HasNoRisk = hasNoRisk,
            };

            ActivityModel roundTripped =
                mapper.FromV0_6_0ToCurrent(mapper.FromCurrentToV0_6_0(original));

            roundTripped.HasNoCost.ShouldBe(hasNoCost);
            roundTripped.HasNoBilling.ShouldBe(hasNoBilling);
            roundTripped.HasNoEffort.ShouldBe(hasNoEffort);
            roundTripped.HasNoRisk.ShouldBe(hasNoRisk);
        }

        #endregion

        #region Current <-> v0.6.0, and what that version cannot hold

        /// <summary>
        /// Everything v0.6.0 has no room for, named. Anything that starts being lost
        /// besides these fails the test below, and anything that stops being lost fails
        /// it too, so the list cannot quietly drift away from what the mapper does.
        /// </summary>
        [Fact]
        public void ProjectScenario_RoundTrippedThroughV0_6_0_Then_LosesOnlyWhatThatVersionCannotHold()
        {
            var mapper = new VersionMapper();
            ProjectScenarioModel original = ModelReflection.Fill<ProjectScenarioModel>();

            ProjectScenarioModel roundTripped =
                mapper.FromV0_6_0ToCurrent(mapper.FromCurrentToV0_6_0(original));

            // The graph layouts and the per-resource metrics arrived in v0.6.1: the
            // graphs fall back to a fresh layout and the first compile repopulates the
            // metrics.
            ProjectScenarioModel expected = original with
            {
                ArrowGraphLayout = new GraphLayoutModel(),
                VertexGraphLayout = new GraphLayoutModel(),
                ResourceMetrics = [],
            };

            ModelReflection
                .Differences(expected, roundTripped, StoredScenarioLosses(
                    // Per-activity colour overrides arrived in v0.6.1.
                    @"$.DependentActivities[].Activity.OverrideColor",
                    @"$.DependentActivities[].Activity.ColorFormat.A",
                    @"$.DependentActivities[].Activity.ColorFormat.R",
                    @"$.DependentActivities[].Activity.ColorFormat.G",
                    @"$.DependentActivities[].Activity.ColorFormat.B",
                    // A v0.6.0 scenario stores its resources as v0.4.4 models, which
                    // predate the split of a resource's activity allocation from its
                    // inter-activity allocation.
                    @"$.ResourceSettings.Resources[].ActivityAllocationType",
                    // Graph edge routing and the earned value resource breakdown are
                    // both v0.6.1 additions.
                    @"$.DisplaySettings.ArrowGraphEdgeRoutingMode",
                    @"$.DisplaySettings.VertexGraphEdgeRoutingMode",
                    @"$.DisplaySettings.EarnedValueCombineResources",
                    @"$.DisplaySettings.EarnedValueScaleToOwnPlan",
                    @"$.DisplaySettings.EarnedValueShowResources"))
                .ShouldBeEmpty();

            // Guards the guard: the scenario has to be carrying the things this test
            // says are lost, or losing them would prove nothing.
            original.ArrowGraphLayout.Nodes.ShouldNotBeEmpty();
            original.ResourceMetrics.ShouldNotBeEmpty();
            original.DisplaySettings.EarnedValueShowResources.ShouldNotBeEmpty();
            original.DependentActivities.ShouldAllBe(x => x.Activity.OverrideColor);
        }

        /// <summary>
        /// v0.6.0 has one scenario chart metric where the current model has two, so the
        /// old one becomes Y1 and the Y2 members, the derivative flags and the absolute
        /// curve fitting flags come back at their defaults.
        /// </summary>
        [Fact]
        public void ProjectDisplaySettings_RoundTrippedThroughV0_6_0_Then_TheOldMetricBecomesY1()
        {
            var mapper = new VersionMapper();
            ProjectDisplaySettingsModel original = ModelReflection.Fill<ProjectDisplaySettingsModel>();

            ProjectDisplaySettingsModel roundTripped =
                mapper.FromV0_6_0ToCurrent(mapper.FromCurrentToV0_6_0(original));

            ProjectDisplaySettingsModel expected = original with
            {
                ScenarioChartShowNamesY2 = default,
                ScenarioChartTrackedMetricY2Axis = default,
                ScenarioChartCurveFittingTypeY2 = default,
                ScenarioChartShowDerivativeY1 = default,
                ScenarioChartShowDerivativeY2 = default,
                ScenarioChartAbsoluteCurveFittingY1 = default,
                ScenarioChartAbsoluteCurveFittingY2 = default,
            };

            ModelReflection.Differences(expected, roundTripped).ShouldBeEmpty();

            // The Y1 members are the ones that bridge to the old single metric, so they
            // are the ones that must come back unchanged.
            roundTripped.ScenarioChartTrackedMetricY1Axis.ShouldBe(original.ScenarioChartTrackedMetricY1Axis);
            roundTripped.ScenarioChartCurveFittingTypeY1.ShouldBe(original.ScenarioChartCurveFittingTypeY1);
            roundTripped.ScenarioChartShowNamesY1.ShouldBe(original.ScenarioChartShowNamesY1);
        }

        /// <summary>
        /// v0.6.0 predates the recently opened files and the compilation timeout. Coming
        /// back up, those three take the application's defaults rather than zero, so a
        /// plan saved by the previous release opens with a working timeout rather than
        /// none at all.
        /// </summary>
        [Fact]
        public void AppSettings_RoundTrippedThroughV0_6_0_Then_TheV0_6_1MembersTakeTheirDefaults()
        {
            var mapper = new VersionMapper();
            AppSettingsModel original = ModelReflection.Fill<AppSettingsModel>();

            AppSettingsModel roundTripped =
                mapper.FromV0_6_0ToCurrent(mapper.FromCurrentToV0_6_0(original));

            var defaults = new AppSettingsModel();

            AppSettingsModel expected = original with
            {
                MaxRecentProjectFilePaths = defaults.MaxRecentProjectFilePaths,
                RecentProjectFilePaths = [],
                CompilationTimeoutMilliseconds = defaults.CompilationTimeoutMilliseconds,
            };

            ModelReflection.Differences(expected, roundTripped, c_VersionStamp).ShouldBeEmpty();

            roundTripped.CompilationTimeoutMilliseconds.ShouldBe(AppSettingsModel.DefaultCompilationTimeoutMilliseconds);
            roundTripped.Version.ShouldBe(@"v0.6.0");
        }

        #endregion

        #region Nothing that is mapped is shared

        /// <summary>
        /// Every mapping in the class, in one sweep: fill the source, map it, and walk
        /// both graphs looking for a collection that appears on both sides. A mapping
        /// that hands its result the very list it was given is a mapping whose output
        /// changes when its input is edited, and there is no call site where that is
        /// what was wanted.
        ///
        /// Reflective so that a mapping added later is covered without anyone
        /// remembering to come back here.
        /// </summary>
        [Fact]
        public void EveryMapping_Given_AFilledSource_Then_SharesNoCollectionWithIt()
        {
            var mapper = new VersionMapper();

            List<MethodInfo> mappings =
                [.. typeof(VersionMapper)
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .Where(IsModelToModelMapping)
                    .OrderBy(x => x.Name)
                    .ThenBy(x => x.GetParameters()[0].ParameterType.FullName)];

            // Guards the guard: a filter that stopped matching would leave this test
            // passing over nothing at all.
            mappings.Count.ShouldBeGreaterThan(100);

            var failures = new List<string>();

            foreach (MethodInfo mapping in mappings)
            {
                Type sourceType = mapping.GetParameters()[0].ParameterType;
                object source;
                object? target;

                try
                {
                    source = ModelReflection.Fill(sourceType);
                    target = mapping.Invoke(mapping.IsStatic ? null : mapper, [source]);
                }
                catch (Exception ex)
                {
                    Exception cause = ex.InnerException ?? ex;
                    failures.Add($@"{Describe(mapping)} threw {cause.GetType().Name}: {cause.Message}");
                    continue;
                }

                failures.AddRange(ModelReflection
                    .SharedCollections(source, target)
                    .Select(path => $@"{Describe(mapping)} shares {path}"));
            }

            failures.ShouldBeEmpty(
                $@"{failures.Count} mapping(s) shared a collection or failed:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
        }

        private static bool IsModelToModelMapping(MethodInfo method)
        {
            if (method.IsSpecialName || method.DeclaringType != typeof(VersionMapper))
            {
                return false;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 1)
            {
                return false;
            }

            return IsMappableModel(parameters[0].ParameterType) && IsMappableModel(method.ReturnType);
        }

        private static bool IsMappableModel(Type type) =>
            type.IsClass
            && !type.IsAbstract
            && type != typeof(string)
            && type.Namespace is not null
            && type.Namespace.StartsWith(@"Zametek", StringComparison.Ordinal)
            && type.GetConstructor(Type.EmptyTypes) is not null;

        private static string Describe(MethodInfo method) =>
            $@"{method.Name}({method.GetParameters()[0].ParameterType.Name} in {method.GetParameters()[0].ParameterType.Namespace})";

        #endregion
    }
}
