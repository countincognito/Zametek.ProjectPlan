using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.Engine.ProjectPlan.Tests
{
    /// <summary>
    /// Each of the view models a job builds makes no reactive pipeline until it is started (see IStartSubscriptions),
    /// makes its own when it is, and makes none once it has been killed - and a view model that holds others starts them
    /// with it.
    /// </summary>
    /// <remarks>
    /// A job never starts them, which <see cref="UnstartedViewModelTests"/> says of all of them at once. These say it of
    /// each, so that a failure names the view model, and they say the other half: that starting one does start what it is
    /// meant to, which a job's own run cannot show, as it never does. The schedulers are held, so that what is handed to
    /// the thread pool or the main thread is counted and not run.
    /// </remarks>
    [Collection(nameof(SchedulerSwapCollection))]
    public class StartableViewModelTests
    {
        public static TheoryData<Type> ViewModels => new()
        {
            typeof(ICoreViewModel),
            typeof(IProjectScenarioManagerViewModel),
            typeof(IOutputManagerViewModel),
            typeof(IGanttChartManagerViewModel),
            typeof(IResourceChartManagerViewModel),
            typeof(IEarnedValueChartManagerViewModel),
            typeof(IScenarioChartManagerViewModel),
            typeof(IArrowGraphManagerViewModel),
            typeof(IVertexGraphManagerViewModel),
        };

        public StartableViewModelTests()
        {
            ProjectPlanEngine.Initialize();
        }

        [Theory]
        [MemberData(nameof(ViewModels))]
        public void Construction_Given_AViewModelAJobBuilds_Then_NothingIsHandedToAnotherThread(Type type)
        {
            using var held = new ManualSchedulersScope();
            using ServiceProvider services = BuildServices();
            using IServiceScope scope = services.CreateScope();

            scope.ServiceProvider.GetRequiredService(type);

            held.Pool.PendingCount.ShouldBe(0, "the view model, or one it needs, handed work to the thread pool");
            held.Main.PendingCount.ShouldBe(0, "the view model, or one it needs, handed work to the main thread");
        }

        [Theory]
        [MemberData(nameof(ViewModels))]
        public void StartSubscriptions_Given_AViewModelNotYetStarted_Then_HandsItsPipelinesTheirFirstValues(Type type)
        {
            using var held = new ManualSchedulersScope();
            using ServiceProvider services = BuildServices();
            using IServiceScope scope = services.CreateScope();
            var viewModel = (IStartSubscriptions)scope.ServiceProvider.GetRequiredService(type);

            viewModel.StartSubscriptions();

            (held.Pool.PendingCount + held.Main.PendingCount).ShouldBeGreaterThan(0);
        }

        [Theory]
        [MemberData(nameof(ViewModels))]
        public void StartSubscriptions_Given_AKilledViewModel_Then_StartsNothing(Type type)
        {
            using var held = new ManualSchedulersScope();
            using ServiceProvider services = BuildServices();
            using IServiceScope scope = services.CreateScope();
            var viewModel = scope.ServiceProvider.GetRequiredService(type);
            ((IKillSubscriptions)viewModel).KillSubscriptions();

            ((IStartSubscriptions)viewModel).StartSubscriptions();

            held.Pool.PendingCount.ShouldBe(0);
            held.Main.PendingCount.ShouldBe(0);
        }

        [Fact]
        public void StartSubscriptions_Given_AnActivitySelectorNotYetStarted_Then_HandsItsPipelinesTheirFirstValues()
        {
            using var held = new ManualSchedulersScope();
            using ServiceProvider services = BuildServices();
            using IServiceScope scope = services.CreateScope();
            var gantt = scope.ServiceProvider.GetRequiredService<IGanttChartManagerViewModel>();

            gantt.ActivitySelector.StartSubscriptions();

            (held.Pool.PendingCount + held.Main.PendingCount).ShouldBeGreaterThan(0);
        }

        [Fact]
        public void StartSubscriptions_Given_AResourceSelectorNotYetStarted_Then_HandsItsPipelinesTheirFirstValues()
        {
            using var held = new ManualSchedulersScope();
            using ServiceProvider services = BuildServices();
            using IServiceScope scope = services.CreateScope();
            var ev = scope.ServiceProvider.GetRequiredService<IEarnedValueChartManagerViewModel>();

            ((IStartSubscriptions)ev.ResourceSelector).StartSubscriptions();

            (held.Pool.PendingCount + held.Main.PendingCount).ShouldBeGreaterThan(0);
        }

        [Fact]
        public void StartSubscriptions_Given_AGanttChartManager_Then_StartsItsActivitySelector()
        {
            using var held = new ManualSchedulersScope();
            using ServiceProvider services = BuildServices();
            using IServiceScope scope = services.CreateScope();
            var gantt = scope.ServiceProvider.GetRequiredService<IGanttChartManagerViewModel>();

            gantt.StartSubscriptions();
            int pending = held.Pool.PendingCount + held.Main.PendingCount;
            gantt.ActivitySelector.StartSubscriptions();

            // Started already, so starting it again hands over nothing more.
            (held.Pool.PendingCount + held.Main.PendingCount).ShouldBe(pending);
        }

        [Fact]
        public void StartSubscriptions_Given_AnEarnedValueChartManager_Then_StartsItsResourceSelector()
        {
            using var held = new ManualSchedulersScope();
            using ServiceProvider services = BuildServices();
            using IServiceScope scope = services.CreateScope();
            var ev = scope.ServiceProvider.GetRequiredService<IEarnedValueChartManagerViewModel>();

            ev.StartSubscriptions();
            int pending = held.Pool.PendingCount + held.Main.PendingCount;
            ((IStartSubscriptions)ev.ResourceSelector).StartSubscriptions();

            (held.Pool.PendingCount + held.Main.PendingCount).ShouldBe(pending);
        }

        [Fact]
        public void KillSubscriptions_Given_AStartedGanttChartManager_Then_ItsActivitySelectorDeliversNothingMore()
        {
            using var held = new ManualSchedulersScope();
            using ServiceProvider services = BuildServices();
            using IServiceScope scope = services.CreateScope();
            var gantt = scope.ServiceProvider.GetRequiredService<IGanttChartManagerViewModel>();
            IProjectScenarioDisplaySettingsViewModel display = scope.ServiceProvider.GetRequiredService<ICoreViewModel>().DisplaySettingsViewModel;
            gantt.StartSubscriptions();
            held.Drain();

            // The selector re-reads the connections a plan has saved when it is told they are ready, and says it has.
            display.IsReadyToReviseGanttChartShowConnections = ReadyToRevise.Yes;
            (held.Pool.PendingCount + held.Main.PendingCount).ShouldBeGreaterThan(0, "the pipelines were not running to begin with");
            held.Drain();

            gantt.KillSubscriptions();
            display.IsReadyToReviseGanttChartShowConnections = ReadyToRevise.Yes;

            held.Pool.PendingCount.ShouldBe(0);
            held.Main.PendingCount.ShouldBe(0);
        }

        [Fact]
        public void KillSubscriptions_Given_AStartedEarnedValueChartManager_Then_ItsResourceSelectorDeliversNothingMore()
        {
            using var held = new ManualSchedulersScope();
            using ServiceProvider services = BuildServices();
            using IServiceScope scope = services.CreateScope();
            var ev = scope.ServiceProvider.GetRequiredService<IEarnedValueChartManagerViewModel>();
            IProjectScenarioDisplaySettingsViewModel display = scope.ServiceProvider.GetRequiredService<ICoreViewModel>().DisplaySettingsViewModel;
            ev.StartSubscriptions();
            held.Drain();

            // The selector re-reads the resources a plan has saved when it is told they are ready, and says it has.
            display.IsReadyToReviseEarnedValueShowResources = ReadyToRevise.Yes;
            (held.Pool.PendingCount + held.Main.PendingCount).ShouldBeGreaterThan(0, "the pipelines were not running to begin with");
            held.Drain();

            ev.KillSubscriptions();
            display.IsReadyToReviseEarnedValueShowResources = ReadyToRevise.Yes;

            held.Pool.PendingCount.ShouldBe(0);
            held.Main.PendingCount.ShouldBe(0);
        }

        private static ServiceProvider BuildServices()
        {
            return new ServiceCollection()
                .AddProjectPlanEngine()
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }
    }
}
