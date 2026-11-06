using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Zametek.ProjectPlan.CommandLine
{
    // /health/ready's check: the server is ready once it has warmed up.
    internal class WarmUpHealthCheck
        : IHealthCheck
    {
        #region Fields

        private readonly WarmUpService m_WarmUp;

        #endregion

        #region Ctors

        public WarmUpHealthCheck(WarmUpService warmUp)
        {
            ArgumentNullException.ThrowIfNull(warmUp);
            m_WarmUp = warmUp;
        }

        #endregion

        #region IHealthCheck Members

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(m_WarmUp.IsReady
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy());
        }

        #endregion
    }
}
