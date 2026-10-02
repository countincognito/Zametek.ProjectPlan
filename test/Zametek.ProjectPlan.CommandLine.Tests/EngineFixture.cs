using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    // The engine, as zpp serve builds it, for the servers a test class starts.
    public sealed class EngineFixture
        : IAsyncLifetime
    {
        private readonly ServiceProvider m_Services;

        public EngineFixture()
        {
            ProjectPlanEngine.Initialize();
            m_Services = Program.BuildServices(validate: true);
            JobRunner = m_Services.GetRequiredService<JobRunner>();
        }

        public JobRunner JobRunner { get; }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            await m_Services.DisposeAsync();
        }
    }
}
