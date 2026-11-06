namespace Zametek.ProjectPlan.CommandLine
{
    // Thrown when the server zpp sent a job to did not run it: caught where the run starts and mapped to the
    // server-failure exit code, distinct from a job that ran and failed.
    internal sealed class ServerException
        : Exception
    {
        public ServerException(string message)
            : base(message)
        {
        }
    }
}
