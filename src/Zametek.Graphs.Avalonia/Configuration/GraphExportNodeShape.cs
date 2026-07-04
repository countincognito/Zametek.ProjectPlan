namespace Zametek.Graphs.Avalonia
{
    // The node silhouette the vector exporter draws (GraphExportMode.Vector). It lets a consumer whose
    // interactive NodeTemplate uses a non-default shape choose a matching vector silhouette for the crisp
    // vector export, independently of the on-screen template.
    public enum GraphExportNodeShape
    {
        // A rounded rectangle using GraphAppearance.NodeCornerRadius (the original look).
        RoundedRectangle,

        // A full ellipse inscribed in the node bounds.
        Ellipse,

        // A plain rectangle (no corner rounding).
        Rectangle,

        // A "stadium"/pill: a rectangle with fully rounded ends (corner radius = half the shorter side).
        Capsule
    }
}
