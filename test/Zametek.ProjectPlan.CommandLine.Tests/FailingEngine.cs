using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Zametek.Contract.ProjectPlan;
using Zametek.Engine.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    // The engine as zpp serve builds it, with one thing in it made to fail, for a test of how the server answers a job that
    // fails in a way that a job run on a good plan does not - and of what it does not say of it: each failure has a message
    // that names a path of the server's, which no answer may.
    public sealed class FailingEngine
        : IAsyncDisposable
    {
        // What the failures' messages say, and an answer must not.
        public const string Secret = @"C:\secret\folder\Renderer.dll";

        private readonly ServiceProvider? m_Services;

        private FailingEngine(
            JobRunner jobRunner,
            ServiceProvider? services = null)
        {
            JobRunner = jobRunner;
            m_Services = services;
        }

        public JobRunner JobRunner { get; }

        // An engine whose Gantt chart cannot be drawn: every other output is.
        public static FailingEngine WithAGanttChartThatCannotBeDrawn()
        {
            return Build(services => services.AddScoped<IGanttChartManagerViewModel>(provider => Faulting<IGanttChartManagerViewModel>.Wrap(
                ActivatorUtilities.CreateInstance<GanttChartManagerViewModel>(provider),
                method => method.Name == nameof(IGanttChartManagerViewModel.WriteGanttChartImageAsync)
                    ? new InvalidOperationException($@"Could not draw the chart with {Secret}")
                    : null)));
        }

        // An engine whose compilation runs out of its time, as the watchdog around it reports: a compilation that a limit of
        // milliseconds would not reliably stop.
        public static FailingEngine WithACompilationThatRunsOutOfTime()
        {
            return Build(services => services.AddScoped<ICoreViewModel>(provider => Faulting<ICoreViewModel>.Wrap(
                ActivatorUtilities.CreateInstance<CoreViewModel>(provider),
                method => method.Name == nameof(ICoreViewModel.RunCompile)
                    ? CompilationTimeoutHelper.TimedOut(2_500, new OperationCanceledException())
                    : null)));
        }

        // An engine that fails with something nobody expected, before it has read the plan.
        public static FailingEngine ThatFailsUnexpectedly()
        {
            return new FailingEngine(new JobRunner(new ThrowingScopeFactory()));
        }

        public async ValueTask DisposeAsync()
        {
            if (m_Services is not null)
            {
                await m_Services.DisposeAsync();
            }
        }

        private static FailingEngine Build(Action<IServiceCollection> fault)
        {
            ProjectPlanEngine.Initialize();

            IServiceCollection collection = new ServiceCollection().AddProjectPlanEngine();
            fault(collection);
            ServiceProvider services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

            return new FailingEngine(services.GetRequiredService<JobRunner>(), services);
        }

        private sealed class ThrowingScopeFactory
            : IServiceScopeFactory
        {
            public IServiceScope CreateScope()
            {
                throw new InvalidOperationException($@"Could not load {Secret}");
            }
        }

        // A view model that does everything the real one does but what the fault says to fail: the fault gives, for a call, the
        // exception it fails with, if it does.
        public class Faulting<T>
            : DispatchProxy
            where T : class
        {
            private T? m_Target;
            private Func<MethodInfo, Exception?>? m_Fault;

            public static T Wrap(
                T target,
                Func<MethodInfo, Exception?> fault)
            {
                T proxy = Create<T, Faulting<T>>();
                var faulting = (Faulting<T>)(object)proxy;
                faulting.m_Target = target;
                faulting.m_Fault = fault;
                return proxy;
            }

            protected override object? Invoke(
                MethodInfo? targetMethod,
                object?[]? args)
            {
                ArgumentNullException.ThrowIfNull(targetMethod);

                if (m_Fault?.Invoke(targetMethod) is Exception fault)
                {
                    throw fault;
                }

                try
                {
                    return targetMethod.Invoke(m_Target, args);
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    throw;
                }
            }
        }
    }
}
