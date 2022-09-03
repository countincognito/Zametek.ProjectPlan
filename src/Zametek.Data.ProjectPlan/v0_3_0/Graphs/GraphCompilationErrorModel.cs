namespace Zametek.Data.ProjectPlan.v0_3_0
{
    [Serializable]
    public record GraphCompilationErrorModel
    {
        public GraphCompilationErrorCode ErrorCode { get; init; }

        public string? ErrorMessage { get; init; }
    }
}
