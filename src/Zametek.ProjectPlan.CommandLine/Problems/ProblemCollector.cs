namespace Zametek.ProjectPlan.CommandLine
{
    // What is wrong with a request, found a problem at a time and kept until all are found, so that the answer lists every
    // one. A problem is about the request as it was sent - a parameter, the syntax of what it holds - or about what it
    // says; the answer's status is that of the more general, as a request with both is answered.
    internal sealed class ProblemCollector
    {
        private readonly List<ProblemError> m_Errors = [];

        public IReadOnlyList<ProblemError> Errors => m_Errors;

        public bool HasErrors => m_Errors.Count > 0;

        // Whether any problem is about the request as it was sent, which a client cannot mend by changing what it says.
        public bool IsMalformed { get; private set; }

        // A problem with the request as it was sent: a parameter the endpoint does not take, or options that are not JSON.
        public void AddMalformed(ProblemError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            m_Errors.Add(error);
            IsMalformed = true;
        }

        // A problem with what the request says, which is readable and does not fit what the endpoint takes.
        public void AddInvalid(ProblemError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            m_Errors.Add(error);
        }
    }
}
