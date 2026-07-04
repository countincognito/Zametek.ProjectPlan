using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;
using SkiaSharp;

namespace Zametek.Graphs.Avalonia
{
    // Records an SKPicture that mirrors the interactive graph canvas (the user's dragged
    // arrangement), so the exported image matches what is on screen rather than the default MSAGL
    // SVG layout. Drawing is kept in step with the node/edge XAML templates in InteractiveGraphView.axaml:
    // node boxes with their labels, edges with arrowheads and their optional labels (drawn only when
    // the edge has one, so vertex edges are label-free). The graph is rendered in its neutral state
    // (no selection ring, no dimming). The resulting picture is vector, so the existing exporter still
    // produces crisp SVG/PDF as well as raster PNG/JPEG. (Replaces the parallel
    // InteractiveArrowGraphRenderer/InteractiveVertexGraphRenderer.)
    internal static class InteractiveGraphRenderer
    {
        private const double c_Padding = 16.0;
        // Lift the label clear of the line, matching GraphEdgeViewModel.
        private const float c_LabelOffset = 9.0f;

        // Label fonts resolved from GraphAppearance, cached by (family, size) for the process lifetime.
        // Caching (rather than per-render create/dispose) keeps each SKTypeface alive for as long as any
        // recorded SKPicture might replay its text, and avoids rebuilding the font on every export. SKFont
        // is not thread-safe, but Render runs solely on the UI thread (invoked via Dispatcher.UIThread),
        // so the shared cache is safe. Antialias edging + subpixel positioning keep the exported labels
        // smooth (text rendering no longer reads SKPaint.IsAntialias).
        private static readonly Dictionary<(string Family, float Size), SKFont> s_FontCache = [];

        private static SKFont GetLabelFont(FontFamily fontFamily, double fontSize)
        {
            (string, float) key = (fontFamily.Name, (float)fontSize);
            if (!s_FontCache.TryGetValue(key, out SKFont? font))
            {
                font = new SKFont(SKTypeface.FromFamilyName(key.Item1), key.Item2)
                {
                    Edging = SKFontEdging.Antialias,
                    Subpixel = true,
                };
                s_FontCache[key] = font;
            }
            return font;
        }

        public static SKPicture? Render(
            IReadOnlyList<GraphNodeViewModel> nodes,
            IReadOnlyList<GraphEdgeViewModel> edges,
            GraphVectorExportStyle style,
            GraphAppearance appearance,
            GraphTheme theme)
        {
            ArgumentNullException.ThrowIfNull(nodes);
            ArgumentNullException.ThrowIfNull(edges);
            ArgumentNullException.ThrowIfNull(style);
            ArgumentNullException.ThrowIfNull(appearance);

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
            foreach (GraphNodeViewModel node in nodes)
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

            // Fill the whole picture with the theme background (drawn before the origin shift so it covers
            // the full bounds including the margin) so the copied / saved image matches the on-screen
            // canvas - which uses these same ColorHelper backgrounds via ThemeToBackgroundConverter.
            SKColor backgroundColor = ToSKColor(theme == GraphTheme.Dark
                ? ColorHelper.DarkThemeBackground
                : ColorHelper.LightThemeBackground);
            using (var backgroundPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = backgroundColor })
            {
                canvas.DrawRect(new SKRect(0, 0, width, height), backgroundPaint);
            }

            // Shift the content origin so the bounding box starts after the margin.
            canvas.Translate((float)(c_Padding - minX), (float)(c_Padding - minY));

            // Themable presentation resolved from the appearance (the colour/thickness/dash/arrow of the
            // nodes and edges already flow through their view-model properties below).
            SKFont nodeLabelFont = GetLabelFont(appearance.NodeLabelFontFamily, appearance.NodeLabelFontSize);
            SKFont edgeLabelFont = GetLabelFont(appearance.EdgeLabelFontFamily, appearance.EdgeLabelFontSize);
            float cornerRadius = (float)appearance.NodeCornerRadius;
            // Node labels sit on the (light) node fills, so they stay dark on either theme, matching the
            // on-screen node. Edge labels sit on the themed canvas background, so pick the light/dark label
            // brush exactly as the on-screen edge does, keeping them readable on either background.
            SKColor nodeLabelColor = ToColor(appearance.NodeLabelBrush, SKColors.Black);
            SKColor edgeLabelColor = theme == GraphTheme.Dark
                ? ToColor(appearance.EdgeDarkLabelBrush, SKColors.White)
                : ToColor(appearance.EdgeLightLabelBrush, SKColors.Black);

            // Edges first so the nodes sit on top, mirroring the z-order in the view.
            foreach (GraphEdgeViewModel edge in edges)
            {
                DrawEdge(canvas, edge, style, edgeLabelFont, edgeLabelColor);
            }

            foreach (GraphNodeViewModel node in nodes)
            {
                DrawNode(canvas, node, style, cornerRadius, nodeLabelFont, nodeLabelColor);
            }

            return recorder.EndRecording();
        }

        private static void DrawEdge(SKCanvas canvas, GraphEdgeViewModel edge, GraphVectorExportStyle style, SKFont labelFont, SKColor labelColor)
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

                // Match the on-screen <Path>: the same contiguous bezier segments (a straight line for
                // non-spline modes, an orthogonal path for rectilinear modes).
                IReadOnlyList<GraphEdgeSegment> segments = edge.EdgeSegments;
                using var path = new SKPath();
                path.MoveTo((float)segments[0].Start.X, (float)segments[0].Start.Y);
                foreach (GraphEdgeSegment segment in segments)
                {
                    path.CubicTo(
                        (float)segment.Control1.X, (float)segment.Control1.Y,
                        (float)segment.Control2.X, (float)segment.Control2.Y,
                        (float)segment.End.X, (float)segment.End.Y);
                }
                canvas.DrawPath(path, linePaint);
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

            DrawEdgeLabel(canvas, edge, style, labelFont, labelColor);
        }

        private static void DrawEdgeLabel(SKCanvas canvas, GraphEdgeViewModel edge, GraphVectorExportStyle style, SKFont labelFont, SKColor labelColor)
        {
            if (!edge.ShowLabel || string.IsNullOrEmpty(edge.Label))
            {
                return;
            }

            Point start = edge.StartPoint;
            Point end = edge.EndPoint;
            // Anchor at the path midpoint, matching GraphEdgeViewModel's on-screen label.
            Point mid = GraphEdgeGeometry.Midpoint(edge.EdgeSegments);
            float midX = (float)mid.X;
            float midY = (float)mid.Y;

            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double length = Math.Sqrt((dx * dx) + (dy * dy));
            if (length >= 1e-6)
            {
                midX += (float)(-dy / length) * c_LabelOffset;
                midY += (float)(dx / length) * c_LabelOffset;
            }

            SKFontMetrics metrics = labelFont.Metrics;
            float baseline = midY - ((metrics.Ascent + metrics.Descent) / 2.0f);
            SKColor textColor = labelColor;

            // Optional chip: a filled rounded background behind the label, centred on the same anchor.
            if (style.ShowEdgeLabelChip)
            {
                float textWidth = labelFont.MeasureText(edge.Label);
                float textHeight = metrics.Descent - metrics.Ascent;
                float halfWidth = (textWidth / 2.0f) + (float)style.EdgeLabelChipPaddingX;
                float halfHeight = (textHeight / 2.0f) + (float)style.EdgeLabelChipPaddingY;
                var chipRect = new SKRect(midX - halfWidth, midY - halfHeight, midX + halfWidth, midY + halfHeight);
                float chipRadius = (float)style.EdgeLabelChipCornerRadius;
                using (var chipPaint = new SKPaint
                {
                    Style = SKPaintStyle.Fill,
                    Color = ToColor(style.EdgeLabelChipBrush, SKColors.Black),
                    IsAntialias = true,
                })
                {
                    canvas.DrawRoundRect(chipRect, chipRadius, chipRadius, chipPaint);
                }
                if (style.EdgeLabelChipTextBrush is not null)
                {
                    textColor = ToColor(style.EdgeLabelChipTextBrush, labelColor);
                }
            }

            using var textPaint = new SKPaint { Color = textColor };
            canvas.DrawText(edge.Label, midX, baseline, SKTextAlign.Center, labelFont, textPaint);
        }

        private static void DrawNode(SKCanvas canvas, GraphNodeViewModel node, GraphVectorExportStyle style, float cornerRadius, SKFont labelFont, SKColor labelColor)
        {
            var rect = new SKRect(
                (float)node.X,
                (float)node.Y,
                (float)(node.X + node.Width),
                (float)(node.Y + node.Height));

            // The fill is the per-node data brush unless the style overrides it (for templates whose fill
            // is not data-driven, e.g. a fixed gradient the vector export cannot read).
            SKColor fillColor = style.NodeFillOverride is not null
                ? ToColor(style.NodeFillOverride, SKColors.LightGray)
                : ToColor(node.FillBrush, SKColors.LightGray);

            using (var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = fillColor,
                IsAntialias = true,
            })
            {
                DrawNodeShape(canvas, rect, style.NodeShape, cornerRadius, fillPaint);
            }

            // Optional accent stripe down the left edge, clipped to the node shape so it follows the
            // rounded corners / ellipse. Drawn over the fill but under the border.
            if (style.ShowNodeAccentStripe && style.NodeAccentStripeWidth > 0.0)
            {
                using var stripePaint = new SKPaint
                {
                    Style = SKPaintStyle.Fill,
                    Color = StripeColor(node, style),
                    IsAntialias = true,
                };
                var stripeRect = new SKRect(rect.Left, rect.Top, rect.Left + (float)style.NodeAccentStripeWidth, rect.Bottom);
                using SKPath clipPath = BuildNodeShapePath(rect, style.NodeShape, cornerRadius);
                canvas.Save();
                canvas.ClipPath(clipPath, SKClipOperation.Intersect, antialias: true);
                canvas.DrawRect(stripeRect, stripePaint);
                canvas.Restore();
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
                DrawNodeShape(canvas, rect, style.NodeShape, cornerRadius, borderPaint);
            }

            DrawNodeLabel(canvas, node, rect, labelFont, labelColor);
        }

        // Draw the node silhouette with the given paint (fill or stroke), using the direct Skia primitives.
        private static void DrawNodeShape(SKCanvas canvas, SKRect rect, GraphExportNodeShape shape, float cornerRadius, SKPaint paint)
        {
            switch (shape)
            {
                case GraphExportNodeShape.Ellipse:
                    canvas.DrawOval(rect, paint);
                    break;
                case GraphExportNodeShape.Rectangle:
                    canvas.DrawRect(rect, paint);
                    break;
                case GraphExportNodeShape.Capsule:
                    float capsuleRadius = Math.Min(rect.Width, rect.Height) / 2.0f;
                    canvas.DrawRoundRect(rect, capsuleRadius, capsuleRadius, paint);
                    break;
                case GraphExportNodeShape.RoundedRectangle:
                default:
                    canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, paint);
                    break;
            }
        }

        // Build the node silhouette selected by the vector export style, at the node's bounds.
        private static SKPath BuildNodeShapePath(SKRect rect, GraphExportNodeShape shape, float cornerRadius)
        {
            var path = new SKPath();
            switch (shape)
            {
                case GraphExportNodeShape.Ellipse:
                    path.AddOval(rect);
                    break;
                case GraphExportNodeShape.Rectangle:
                    path.AddRect(rect);
                    break;
                case GraphExportNodeShape.Capsule:
                    float capsuleRadius = Math.Min(rect.Width, rect.Height) / 2.0f;
                    path.AddRoundRect(rect, capsuleRadius, capsuleRadius);
                    break;
                case GraphExportNodeShape.RoundedRectangle:
                default:
                    path.AddRoundRect(rect, cornerRadius, cornerRadius);
                    break;
            }
            return path;
        }

        private static SKColor StripeColor(GraphNodeViewModel node, GraphVectorExportStyle style)
        {
            return style.NodeAccentStripeSource switch
            {
                GraphExportStripeSource.FillColour => ToColor(node.FillBrush, SKColors.LightGray),
                GraphExportStripeSource.Custom => ToColor(style.NodeAccentStripeBrush, SKColors.Gray),
                _ => ToColor(node.BorderBrush, SKColors.Black),
            };
        }

        private static void DrawNodeLabel(SKCanvas canvas, GraphNodeViewModel node, SKRect rect, SKFont labelFont, SKColor labelColor)
        {
            if (string.IsNullOrEmpty(node.Label))
            {
                return;
            }

            using var textPaint = new SKPaint { Color = labelColor };

            // The label is monospace, centred in the node just like the TextBlock. Stack any lines
            // about the node centre.
            string[] lines = node.Label.Split('\n');
            SKFontMetrics metrics = labelFont.Metrics;
            float lineHeight = metrics.Descent - metrics.Ascent;
            float blockHeight = lineHeight * lines.Length;
            float centreX = rect.MidX;
            float blockTop = rect.MidY - (blockHeight / 2.0f);

            for (int i = 0; i < lines.Length; i++)
            {
                float baseline = blockTop + (i * lineHeight) - metrics.Ascent;
                canvas.DrawText(lines[i], centreX, baseline, SKTextAlign.Center, labelFont, textPaint);
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
                return ToSKColor(solid.Color);
            }
            return fallback;
        }

        private static SKColor ToSKColor(Color color)
        {
            return new SKColor(color.R, color.G, color.B, color.A);
        }
    }
}
