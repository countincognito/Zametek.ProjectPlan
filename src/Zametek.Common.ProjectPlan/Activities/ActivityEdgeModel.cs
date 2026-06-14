namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record ActivityEdgeModel
    {
        public ActivityModel Content { get; init; } = new ActivityModel();

        // Presentation resolved by the application (from graph settings + activity state) before
        // the model is handed to the graph serializer. StrokeWeight is the raw stroke weight; the
        // serializer applies its own size-correction factor.
        public string? ForegroundColorHexCode { get; init; }

        public EdgeDashStyle DashStyle { get; init; }

        public double StrokeWeight { get; init; }
    }
}
