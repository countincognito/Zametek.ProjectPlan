using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the two signals that the core raises around its operations: IsBusy, for the user interface, and
    /// IsBulkUpdating, which the pipelines mute themselves on. The operations nest, and a signal changes only when the
    /// outermost section starts and when it ends. An exception ends the sections that it passes through.
    /// </summary>
    public class CoreViewModelSignalTests
    {
        [Fact]
        public void Construction_Then_BothSignalsAreOff()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();

            core.IsBusy.ShouldBeFalse();
            core.IsBulkUpdating.ShouldBeFalse();
        }

        [Fact]
        public void ProcessProjectScenario_Then_IsBulkUpdatingChangesOnceOnAndOnceOff()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            List<bool> bulk = Watch(core, nameof(CoreViewModel.IsBulkUpdating), () => core.IsBulkUpdating);

            // The load resets the scenario inside its own bulk update, and so two sections are open at one time.
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(3), Guid.NewGuid(), @"Test");

            bulk.ShouldBe(new[] { true, false });
            core.IsBulkUpdating.ShouldBeFalse();
        }

        [Fact]
        public void ProcessProjectScenarioImport_Then_IsBulkUpdatingChangesOnceOnAndOnceOff()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            List<bool> bulk = Watch(core, nameof(CoreViewModel.IsBulkUpdating), () => core.IsBulkUpdating);

            core.ProcessProjectScenarioImport(new ProjectScenarioImportModel(), Guid.NewGuid(), @"Imported");

            bulk.ShouldBe(new[] { true, false });
            core.IsBulkUpdating.ShouldBeFalse();
        }

        [Fact]
        public void ResetProjectScenario_Then_IsBulkUpdatingChangesOnceOnAndOnceOff()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            List<bool> bulk = Watch(core, nameof(CoreViewModel.IsBulkUpdating), () => core.IsBulkUpdating);

            core.ResetProjectScenario();

            bulk.ShouldBe(new[] { true, false });
        }

        [Fact]
        public void ProcessProjectScenario_Then_IsBusyChangesOnceOnAndOnceOff()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            List<bool> busy = Watch(core, nameof(CoreViewModel.IsBusy), () => core.IsBusy);

            // The load calls the reset, the compile and the cascade, which are busy sections of their own.
            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(3), Guid.NewGuid(), @"Test");

            busy.ShouldBe(new[] { true, false });
            core.IsBusy.ShouldBeFalse();
        }

        [Fact]
        public void ResetProjectScenario_Then_IsBusyChangesOnceOnAndOnceOff()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            List<bool> busy = Watch(core, nameof(CoreViewModel.IsBusy), () => core.IsBusy);

            core.ResetProjectScenario();

            busy.ShouldBe(new[] { true, false });
        }

        [Fact]
        public void ClearSettings_Then_IsBusyChangesOnceOnAndOnceOff()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            List<bool> busy = Watch(core, nameof(CoreViewModel.IsBusy), () => core.IsBusy);

            // The method calls CreateEmptyProjectScenario, which is a busy section inside its own.
            core.ClearSettings();

            busy.ShouldBe(new[] { true, false });
        }

        [Fact]
        public void CreateEmptyProjectScenario_Given_TwoCalls_Then_EachChangesTheSignalOnAndOff()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            List<bool> busy = Watch(core, nameof(CoreViewModel.IsBusy), () => core.IsBusy);

            core.CreateEmptyProjectScenario();
            core.CreateEmptyProjectScenario();

            busy.ShouldBe(new[] { true, false, true, false });
        }

        [Fact]
        public void ProcessProjectScenario_Given_AScenarioThatFails_Then_BothSignalsAreOffAfter()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            List<bool> bulk = Watch(core, nameof(CoreViewModel.IsBulkUpdating), () => core.IsBulkUpdating);
            List<bool> busy = Watch(core, nameof(CoreViewModel.IsBusy), () => core.IsBusy);

            // The load fails after it has begun both sections, and after the reset inside it has begun and ended its own.
            Should.Throw<NullReferenceException>(() => core.ProcessProjectScenario(null!, Guid.NewGuid(), @"Test"));

            bulk.ShouldBe(new[] { true, false });
            busy.ShouldBe(new[] { true, false });
            core.IsBulkUpdating.ShouldBeFalse();
            core.IsBusy.ShouldBeFalse();
        }

        [Fact]
        public void ProcessProjectScenario_Given_AFailureAndThenALoad_Then_TheSignalsStillChange()
        {
            using CoreViewModel core = CoreViewModelFixture.Create();
            Should.Throw<NullReferenceException>(() => core.ProcessProjectScenario(null!, Guid.NewGuid(), @"Test"));
            List<bool> bulk = Watch(core, nameof(CoreViewModel.IsBulkUpdating), () => core.IsBulkUpdating);
            List<bool> busy = Watch(core, nameof(CoreViewModel.IsBusy), () => core.IsBusy);

            core.ProcessProjectScenario(CoreViewModelFixture.CreateProjectScenario(3), Guid.NewGuid(), @"Test");

            bulk.ShouldBe(new[] { true, false });
            busy.ShouldBe(new[] { true, false });
        }

        // The value that read gives each time that the core raises the change of the property.
        private static List<bool> Watch(CoreViewModel core, string property, Func<bool> read)
        {
            List<bool> values = [];

            core.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == property)
                {
                    values.Add(read());
                }
            };

            return values;
        }
    }
}
