using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record GraphCompilationErrorModel
    {
        public GraphCompilationErrorCode ErrorCode { get; init; }

        public string ErrorMessage { get; init; } = string.Empty;
    }
}
