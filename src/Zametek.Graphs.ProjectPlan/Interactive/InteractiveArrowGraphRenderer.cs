using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;
using SkiaSharp;

namespace Zametek.Graphs.ProjectPlan
{
    // Records an SKPicture that mirrors the interactive arrow-graph canvas (the user's dragged
    // arrangement), so the exported image matches what is on screen rather than the default MSAGL
    // SVG layout. Drawing is kept in step with the node/edge XAML templates in
    // InteractiveArrowGraphView.axaml: event boxes with their finish-time labels, activity edges
    // with arrowheads and their id/duration/slack labels. The graph is rendered in its neutral
    // state (no selection ring, no dimming). The resulting picture is vector, so the existing
    // exporter still produces crisp SVG/PDF as well as raster PNG/JPEG.
    public static class InteractiveArrowGraphRenderer
    {
        private const float c_CornerRadius = 3.0f;
        private const float c_NodeLabelFontSize = 11.0f;
        private const float c_EdgeLabelFontSize = 12.0f;
        private const double c_Padding = 16.0;
        // Lift the label clear of the line, matching ArrowGraphEdgeViewModel.
        private const float c_LabelOffset = 9.0f;

        // Matches the label TextBlocks (FontFamily="Consolas").
        private static readonly SKTypeface s_LabelTypeface = SKTypeface.FromFamilyName("Consolas");

        public static SKPicture? Render(
            IReadOnlyList<ArrowGraphNodeViewModel> nodes,
            IReadOnlyList<ArrowGraphEdgeViewModel> edges)
        {
            ArgumentNullException.ThrowIfNull(nodes);
            ArgumentNullException.ThrowIfNull(edges);

            if (nodes.Count == 0)
            {
                return null;
            }

            // Crop to the node bounding box (+ a margin), not the whole padded workspace, so the
            // exported image has no large empty borders.
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;
            foreach (ArrowGraphNodeViewModel node in nodes)
            {
                minX = Math.Min(minX, node.X);
                minY = Math.Min(minY, node.Y);
                maxX = Math.Max(maxX, node.X + node.Width);
                maxY = Math.Max(maxY, node.Y + node.Height);
            }

            float width = (float)((maxX - minX) + (2.0 * c_Padding));
            float height = (float)((maxY - minY) + (2.0 * c_Padding));

            using var recorder = new SKPictureRecorder();
            SKCanvas canvas = recorder.BeginRecording(new SKRect(0, 0, width, height));

            // Shift the content origin so the bounding box starts after the margin.
            canvas.Translate((float)(c_Padding - minX), (float)(c_Padding - minY));

            // Edges first so the nodes sit on top, mirroring the z-order in the view.
            foreach (ArrowGraphEdgeViewModel edge in edges)
            {
                DrawEdge(canvas, edge);
            }

            foreach (ArrowGraphNodeViewModel node in nodes)
            {
                DrawNode(canvas, node);
            }

            return recorder.EndRecording();
        }

        private static void DrawEdge(SKCanvas canvas, ArrowGraphEdgeViewModel edge)
        {
            SKColor color = ToColor(edge.BaseStroke, SKColors.Gray);
            float thickness = (float)edge.BaseStrokeThickness;

            using (var linePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = color,
                StrokeWidth = thickness,
                IsAntialias = true,
            })
            {
                using SKPathEffect? dash = BuildDash(edge.StrokeDashArray, thickness);
                linePaint.PathEffect = dash;

                Point start = edge.StartPoint;
                Point end = edge.EndPoint;
                canvas.DrawLine((float)start.X, (float)start.Y, (float)end.X, (float)end.Y, linePaint);
            }

            IList<Point> arrowPoints = edge.ArrowPoints;
            if (arrowPoints.Count >= 3)
            {
                using var arrowPaint = new SKPaint
                {
                    Style = SKPaintStyle.Fill,
                    Color = color,
                    IsAntialias = true,
                };
                using var path = new SKPath();
                path.MoveTo((float)arrowPoints[0].X, (float)arrowPoints[0].Y);
                path.LineTo((float)arrowPoints[1].X, (float)arrowPoints[1].Y);
                path.LineTo((float)arrowPoints[2].X, (float)arrowPoints[2].Y);
                path.Close();
                canvas.DrawPath(path, arrowPaint);
            }

            DrawEdgeLabel(canvas, edge);
        }

        private static void DrawEdgeLabel(SKCanvas canvas, ArrowGraphEdgeViewModel edge)
        {
            if (!edge.ShowLabel || string.IsNullOrEmpty(edge.Label))
            {
                return;
            }

            Point start = edge.StartPoint;
            Point end = edge.EndPoint;
            float midX = (float)((start.X + end.X) / 2.0);
            float midY = (float)((start.Y + end.Y) / 2.0);

            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double length = Math.Sqrt((dx * dx) + (dy * dy));
            if (length >= 1e-6)
            {
                midX += (float)(-dy / length) * c_LabelOffset;
                midY += (float)(dx / length) * c_LabelOffset;
            }

            // Exports default to a light background, so a dark label reads best.
            using var textPaint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true,
                TextSize = c_EdgeLabelFontSize,
                Typeface = s_LabelTypeface,
                TextAlign = SKTextAlign.Center,
            };

            SKFontMetrics metrics = textPaint.FontMetrics;
            float baseline = midY - ((metrics.Ascent + metrics.Descent) / 2.0f);
            canvas.DrawText(edge.Label, midX, baseline, textPaint);
        }

        private static void DrawNode(SKCanvas canvas, ArrowGraphNodeViewModel node)
        {
            var rect = new SKRect(
                (float)node.X,
                (float)node.Y,
                (float)(node.X + node.Width),
                (float)(node.Y + node.Height));

            using (var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = ToColor(node.FillBrush, SKColors.LightGray),
                IsAntialias = true,
            })
            {
                canvas.DrawRoundRect(rect, c_CornerRadius, c_CornerRadius, fillPaint);
            }

            float borderThickness = (float)node.BorderThickness;
            using (var borderPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = ToColor(node.BorderBrush, SKColors.Black),
                StrokeWidth = borderThickness,
                IsAntialias = true,
            })
            using (SKPathEffect? dash = BuildDash(node.StrokeDashArray, borderThickness))
            {
                borderPaint.PathEffect = dash;
                canvas.DrawRoundRect(rect, c_CornerRadius, c_CornerRadius, borderPaint);
            }

            DrawNodeLabel(canvas, node, rect);
        }

        private static void DrawNodeLabel(SKCanvas canvas, ArrowGraphNodeViewModel node, SKRect rect)
        {
            if (string.IsNullOrEmpty(node.Label))
            {
                return;
            }

            using var textPaint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true,
                TextSize = c_NodeLabelFontSize,
                Typeface = s_LabelTypeface,
                TextAlign = SKTextAlign.Center,
            };

            // The label is monospace, centred in the node just like the TextBlock. Stack any lines
            // about the node centre.
            string[] lines = node.Label.Split('\n');
            SKFontMetrics metrics = textPaint.FontMetrics;
            float lineHeight = metrics.Descent - metrics.Ascent;
            float blockHeight = lineHeight * lines.Length;
            float centreX = rect.MidX;
            float blockTop = rect.MidY - (blockHeight / 2.0f);

            for (int i = 0; i < lines.Length; i++)
            {
                float baseline = blockTop + (i * lineHeight) - metrics.Ascent;
                canvas.DrawText(lines[i], centreX, baseline, textPaint);
            }
        }

        // Avalonia dash arrays are expressed in multiples of the stroke thickness; SkiaSharp uses
        // absolute lengths, so scale them up to match the on-screen dashes.
        private static SKPathEffect? BuildDash(AvaloniaList<double>? dashArray, float thickness)
        {
            if (dashArray is null || dashArray.Count == 0 || thickness <= 0.0f)
            {
                return null;
            }

            var intervals = new float[dashArray.Count];
            for (int i = 0; i < dashArray.Count; i++)
            {
                intervals[i] = (float)dashArray[i] * thickness;
            }

            // SKPathEffect.CreateDash requires an even number of intervals.
            if (intervals.Length % 2 != 0)
            {
                return null;
            }

            return SKPathEffect.CreateDash(intervals, 0.0f);
        }

        private static SKColor ToColor(IBrush? brush, SKColor fallback)
        {
            if (brush is ISolidColorBrush solid)
            {
                Color color = solid.Color;
                return new SKColor(color.R, color.G, color.B, color.A);
            }
            return fallback;
        }
    }
}
