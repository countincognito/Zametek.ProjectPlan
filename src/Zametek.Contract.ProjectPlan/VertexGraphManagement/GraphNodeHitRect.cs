namespace Zametek.Contract.ProjectPlan
{
    public record GraphNodeHitRect(int ActivityId, double X, double Y, double Width, double Height)
    {
        public bool Contains(double px, double py)
        {
            return px >= X && px <= X + Width && py >= Y && py <= Y + Height;
        }
    }
}
