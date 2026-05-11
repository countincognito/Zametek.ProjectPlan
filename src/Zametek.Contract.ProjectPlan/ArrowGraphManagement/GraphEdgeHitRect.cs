namespace Zametek.Contract.ProjectPlan
{
    public record GraphEdgeHitRect(int ActivityId, double LabelX, double LabelY, double LabelWidth, double LabelHeight)
    {
        public bool Contains(double px, double py)
        {
            return px >= LabelX && px <= LabelX + LabelWidth && py >= LabelY && py <= LabelY + LabelHeight;
        }
    }
}
