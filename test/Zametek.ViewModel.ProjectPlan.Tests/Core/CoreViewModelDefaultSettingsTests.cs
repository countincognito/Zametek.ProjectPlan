using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// Tests for the five default display settings that the core passes to the setting service: the properties read the
    /// service, a value goes to the service and to no other setting, and the core raises the change of that property, and of
    /// no other, each time that it is set - also when the value is the one that the service holds.
    /// </summary>
    public class CoreViewModelDefaultSettingsTests
    {
        #region A value goes to its own setting

        [Fact]
        public void DefaultShowDates_Given_AValue_Then_TheSettingServiceHoldsItAndNoOtherSettingChanges()
        {
            using Made made = Made.Make();

            made.Core.DefaultShowDates = true;

            made.Settings().ShouldBe((true, false, NonWorkingDayMode.None, false, false));
            made.Raised.ShouldBe(new[] { nameof(CoreViewModel.DefaultShowDates) });
        }

        [Fact]
        public void DefaultUseClassicDates_Given_AValue_Then_TheSettingServiceHoldsItAndNoOtherSettingChanges()
        {
            using Made made = Made.Make();

            made.Core.DefaultUseClassicDates = true;

            made.Settings().ShouldBe((false, true, NonWorkingDayMode.None, false, false));
            made.Raised.ShouldBe(new[] { nameof(CoreViewModel.DefaultUseClassicDates) });
        }

        [Fact]
        public void DefaultNonWorkingDayMode_Given_AValue_Then_TheSettingServiceHoldsItAndNoOtherSettingChanges()
        {
            using Made made = Made.Make();

            made.Core.DefaultNonWorkingDayMode = NonWorkingDayMode.CustomCalendar;

            made.Settings().ShouldBe((false, false, NonWorkingDayMode.CustomCalendar, false, false));
            made.Raised.ShouldBe(new[] { nameof(CoreViewModel.DefaultNonWorkingDayMode) });
        }

        [Fact]
        public void DefaultHideCost_Given_AValue_Then_TheSettingServiceHoldsItAndNoOtherSettingChanges()
        {
            using Made made = Made.Make();

            made.Core.DefaultHideCost = true;

            made.Settings().ShouldBe((false, false, NonWorkingDayMode.None, true, false));
            made.Raised.ShouldBe(new[] { nameof(CoreViewModel.DefaultHideCost) });
        }

        [Fact]
        public void DefaultHideBilling_Given_AValue_Then_TheSettingServiceHoldsItAndNoOtherSettingChanges()
        {
            using Made made = Made.Make();

            made.Core.DefaultHideBilling = true;

            made.Settings().ShouldBe((false, false, NonWorkingDayMode.None, false, true));
            made.Raised.ShouldBe(new[] { nameof(CoreViewModel.DefaultHideBilling) });
        }

        #endregion

        #region The properties read the service

        [Fact]
        public void Defaults_Given_ValuesInTheSettingService_Then_ThePropertiesReadThem()
        {
            using Made made = Made.Make();

            made.Service.DefaultShowDates = true;
            made.Service.DefaultNonWorkingDayMode = NonWorkingDayMode.Weekends;
            made.Service.DefaultHideBilling = true;

            (made.Core.DefaultShowDates, made.Core.DefaultUseClassicDates, made.Core.DefaultNonWorkingDayMode, made.Core.DefaultHideCost, made.Core.DefaultHideBilling)
                .ShouldBe((true, false, NonWorkingDayMode.Weekends, false, true));
        }

        #endregion

        #region Every set is raised

        [Fact]
        public void Defaults_Given_ThePresentValueTwice_Then_EachSetRaisesTheChange()
        {
            using Made made = Made.Make();

            // The present value of each is its default, and a set raises the change also when nothing changed.
            made.Core.DefaultShowDates = false;
            made.Core.DefaultShowDates = false;
            made.Core.DefaultUseClassicDates = false;
            made.Core.DefaultUseClassicDates = false;
            made.Core.DefaultNonWorkingDayMode = NonWorkingDayMode.None;
            made.Core.DefaultNonWorkingDayMode = NonWorkingDayMode.None;
            made.Core.DefaultHideCost = false;
            made.Core.DefaultHideCost = false;
            made.Core.DefaultHideBilling = false;
            made.Core.DefaultHideBilling = false;

            made.Raised.ShouldBe(
                new[]
                {
                    nameof(CoreViewModel.DefaultShowDates),
                    nameof(CoreViewModel.DefaultShowDates),
                    nameof(CoreViewModel.DefaultUseClassicDates),
                    nameof(CoreViewModel.DefaultUseClassicDates),
                    nameof(CoreViewModel.DefaultNonWorkingDayMode),
                    nameof(CoreViewModel.DefaultNonWorkingDayMode),
                    nameof(CoreViewModel.DefaultHideCost),
                    nameof(CoreViewModel.DefaultHideCost),
                    nameof(CoreViewModel.DefaultHideBilling),
                    nameof(CoreViewModel.DefaultHideBilling),
                });
        }

        #endregion

        // A core over a setting service, and the changes of the default settings that the core raised.
        private sealed class Made
            : IDisposable
        {
            private Made(CoreViewModel core, ISettingService service, List<string> raised)
            {
                Core = core;
                Service = service;
                Raised = raised;
            }

            public CoreViewModel Core { get; }

            public ISettingService Service { get; }

            public List<string> Raised { get; }

            public static Made Make()
            {
                (CoreViewModel core, ISettingService service) = CoreViewModelFixture.CreateWithSettingService();
                List<string> raised = [];

                core.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is not null && e.PropertyName.StartsWith(@"Default", StringComparison.Ordinal))
                    {
                        raised.Add(e.PropertyName);
                    }
                };

                return new Made(core, service, raised);
            }

            // What the setting service holds for the five.
            public (bool, bool, NonWorkingDayMode, bool, bool) Settings() =>
                (Service.DefaultShowDates, Service.DefaultUseClassicDates, Service.DefaultNonWorkingDayMode, Service.DefaultHideCost, Service.DefaultHideBilling);

            public void Dispose() => Core.Dispose();
        }
    }
}
