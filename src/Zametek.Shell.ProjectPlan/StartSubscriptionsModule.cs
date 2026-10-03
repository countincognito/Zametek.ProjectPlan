using Autofac;
using Autofac.Core;
using Autofac.Core.Registration;
using Autofac.Core.Resolving.Pipeline;
using Zametek.Contract.ProjectPlan;

namespace Zametek.Shell.ProjectPlan
{
    /// <summary>
    /// Starts the reactive pipelines of every view model the container builds, as soon as it has built it: see
    /// <see cref="IStartSubscriptions"/>. A view model is started after its constructor has returned and before it
    /// is handed to whatever asked for it, so each is started in the order the constructors ran, and none is
    /// delivering anything on another thread while it is still being built. Every head composes through
    /// <see cref="CompositionRoot"/>, so every head gets this.
    /// </summary>
    internal sealed class StartSubscriptionsModule
        : Module
    {
        protected override void AttachToComponentRegistration(
            IComponentRegistryBuilder componentRegistry,
            IComponentRegistration registration)
        {
            registration.PipelineBuilding += (_, pipeline) =>
                pipeline.Use(new StartSubscriptionsMiddleware(), MiddlewareInsertionMode.StartOfPhase);
        }

        private sealed class StartSubscriptionsMiddleware
            : IResolveMiddleware
        {
            // The last phase of a resolve, where the instance is made. Taken at the start of the phase, the middleware
            // wraps everything else the phase does, so it acts once the instance exists and is whole.
            public PipelinePhase Phase => PipelinePhase.Activation;

            public void Execute(ResolveRequestContext context, Action<ResolveRequestContext> next)
            {
                next(context);

                if (context.NewInstanceActivated
                    && context.Instance is IStartSubscriptions startable)
                {
                    startable.StartSubscriptions();
                }
            }

            public override string ToString() => nameof(StartSubscriptionsMiddleware);
        }
    }
}
