namespace Zametek.Graphs.Avalonia
{
    // Where the vector exporter's optional node accent stripe takes its colour from
    // (GraphVectorExportStyle.ShowNodeAccentStripe).
    public enum GraphExportStripeSource
    {
        // The node's own border colour (the data-driven BorderBrush).
        BorderColour,

        // The node's own fill colour (the data-driven FillBrush).
        FillColour,

        // A fixed brush supplied on the style (GraphVectorExportStyle.NodeAccentStripeBrush).
        Custom
    }
}
