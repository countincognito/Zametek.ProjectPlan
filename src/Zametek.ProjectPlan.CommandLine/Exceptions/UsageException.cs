namespace Zametek.ProjectPlan.CommandLine
{
    // Thrown for invalid options or combinations of them, by zpp and by zpp serve: caught where the run starts and
    // mapped to the usage-error exit code, distinct from runtime failures.
    internal sealed class UsageException
        : Exception
    {
        public UsageException(string message)
            : base(message)
        {
        }
    }
}
