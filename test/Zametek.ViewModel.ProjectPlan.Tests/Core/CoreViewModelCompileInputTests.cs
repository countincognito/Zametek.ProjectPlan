using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the resources and the work streams that the core hands to a compiler: a compilation of the plan takes them
    /// in its snapshot, and a simulation takes them in the input that it captures. Both take the ones that the settings
    /// hold at that moment.
    /// </summary>
    /// <remarks>
    /// The resources are in the order of their ids, and none are given when the settings disable the resources. The work
    /// streams are in the order of the settings. The tests go through the public members of the core, and the compilation
    /// is seen through the gated core, which keeps what its last compilation was given.
    /// </remarks>
    public class CoreViewModelCompileInputTests
    {
        #region The input of a simulation

        [Fact]
        public void CaptureSimulationInput_Given_Resources_Then_TheyAreInTheOrderOfTheirIds()
        {
            using CoreViewModel core = Load(CoreViewModelFixture.Create(), resourceIds: [3, 1, 2]);

            SimulationInputModel input = core.CaptureSimulationInput();

            input.Resources.Select(x => x.Id).ShouldBe(new[] { 1, 2, 3 });
        }

        [Fact]
        public void CaptureSimulationInput_Given_ResourcesThatAreDisabled_Then_ThereAreNone()
        {
            using CoreViewModel core = Load(CoreViewModelFixture.Create(), resourceIds: [3, 1, 2], resourcesAreDisabled: true);

            SimulationInputModel input = core.CaptureSimulationInput();

            input.Resources.ShouldBeEmpty();
        }

        [Fact]
        public void CaptureSimulationInput_Given_WorkStreams_Then_TheyAreInTheOrderOfTheSettings()
        {
            using CoreViewModel core = Load(CoreViewModelFixture.Create(), workStreamIds: [2, 3, 1]);

            SimulationInputModel input = core.CaptureSimulationInput();

            input.WorkStreams.Select(x => x.Id).ShouldBe(new[] { 2, 3, 1 });
        }

        [Fact]
        public void CaptureSimulationInput_Given_NoResourcesAndNoWorkStreams_Then_BothAreEmpty()
        {
            using CoreViewModel core = Load(CoreViewModelFixture.Create(), resourceIds: [], workStreamIds: []);

            SimulationInputModel input = core.CaptureSimulationInput();

            input.Resources.ShouldBeEmpty();
            input.WorkStreams.ShouldBeEmpty();
        }

        [Fact]
        public void CaptureSimulationInput_Given_SettingsThatWereEdited_Then_TheInputHoldsTheEditedOnes()
        {
            using CoreViewModel core = Load(CoreViewModelFixture.Create(), resourceIds: [1, 2], workStreamIds: [1, 2]);

            core.ResourceSettings = core.ResourceSettings with { Resources = [new ResourceModel { Id = 7, Name = @"Seven" }] };
            core.WorkStreamSettings = new WorkStreamSettingsModel { WorkStreams = [new WorkStreamModel { Id = 9, Name = @"Nine" }] };
            SimulationInputModel input = core.CaptureSimulationInput();

            input.Resources.Select(x => x.Id).ShouldBe(new[] { 7 });
            input.WorkStreams.Select(x => x.Id).ShouldBe(new[] { 9 });
        }

        #endregion

        #region The snapshot of a compilation

        [Fact]
        public void RunCompile_Given_Resources_Then_TheCompilationGetsThemInTheOrderOfTheirIds()
        {
            (GatedCoreViewModel core, _) = CoreViewModelFixture.CreateGated();
            using (core)
            {
                Load(core, resourceIds: [3, 1, 2]);

                core.RunCompile();

                core.LastResources.Select(x => x.Id).ShouldBe(new[] { 1, 2, 3 });
            }
        }

        [Fact]
        public void RunCompile_Given_ResourcesThatAreDisabled_Then_TheCompilationGetsNone()
        {
            (GatedCoreViewModel core, _) = CoreViewModelFixture.CreateGated();
            using (core)
            {
                Load(core, resourceIds: [3, 1, 2], resourcesAreDisabled: true);

                core.RunCompile();

                core.LastResources.ShouldBeEmpty();
            }
        }

        [Fact]
        public void RunCompile_Given_WorkStreams_Then_TheCompilationGetsThemInTheOrderOfTheSettings()
        {
            (GatedCoreViewModel core, _) = CoreViewModelFixture.CreateGated();
            using (core)
            {
                Load(core, workStreamIds: [2, 3, 1]);

                core.RunCompile();

                core.LastWorkStreams.Select(x => x.Id).ShouldBe(new[] { 2, 3, 1 });
            }
        }

        [Fact]
        public void RunCompile_Given_SettingsThatWereEdited_Then_TheCompilationGetsTheEditedOnes()
        {
            (GatedCoreViewModel core, _) = CoreViewModelFixture.CreateGated();
            using (core)
            {
                Load(core, resourceIds: [1, 2], workStreamIds: [1, 2]);

                core.ResourceSettings = core.ResourceSettings with { Resources = [new ResourceModel { Id = 7, Name = @"Seven" }] };
                core.WorkStreamSettings = new WorkStreamSettingsModel { WorkStreams = [new WorkStreamModel { Id = 9, Name = @"Nine" }] };
                core.RunCompile();

                core.LastResources.Select(x => x.Id).ShouldBe(new[] { 7 });
                core.LastWorkStreams.Select(x => x.Id).ShouldBe(new[] { 9 });
            }
        }

        [Fact]
        public void ProcessProjectScenario_Given_ResourcesAndWorkStreams_Then_TheCompilationOfTheLoadGetsThem()
        {
            (GatedCoreViewModel core, _) = CoreViewModelFixture.CreateGated();
            using (core)
            {
                Load(core, resourceIds: [3, 1, 2], workStreamIds: [2, 1]);

                core.LastResources.Select(x => x.Id).ShouldBe(new[] { 1, 2, 3 });
                core.LastWorkStreams.Select(x => x.Id).ShouldBe(new[] { 2, 1 });
            }
        }

        #endregion

        // Loads a plan of two chained activities that target no resource and no work stream, with the resources and the work
        // streams that the test asks for, and gives the core back.
        private static TCore Load<TCore>(
            TCore core,
            int[]? resourceIds = null,
            int[]? workStreamIds = null,
            bool resourcesAreDisabled = false)
            where TCore : CoreViewModel
        {
            ProjectScenarioModel scenario = CoreViewModelFixture.CreateProjectScenario(2);

            scenario = scenario with
            {
                DependentActivities =
                [
                    .. scenario.DependentActivities.Select(x => x with { Activity = x.Activity with { TargetResources = [] } }),
                ],
                ResourceSettings = scenario.ResourceSettings with
                {
                    Resources = [.. (resourceIds ?? []).Select(id => new ResourceModel { Id = id, Name = $@"Resource {id}", DisplayOrder = id })],
                    AreDisabled = resourcesAreDisabled,
                },
                WorkStreamSettings = new WorkStreamSettingsModel
                {
                    WorkStreams = [.. (workStreamIds ?? []).Select(id => new WorkStreamModel { Id = id, Name = $@"Work stream {id}", DisplayOrder = id })],
                },
            };

            core.ProcessProjectScenario(scenario, Guid.NewGuid(), @"Test");

            return core;
        }
    }
}
