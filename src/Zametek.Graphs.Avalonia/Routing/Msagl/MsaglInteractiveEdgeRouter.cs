using Avalonia;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Core.Routing;
using Microsoft.Msagl.Routing;
using Microsoft.Msagl.Routing.Rectilinear;
using MsaglPoint = Microsoft.Msagl.Core.Geometry.Point;

namespace Zametek.Graphs.Avalonia
{
    // The default IInteractiveEdgeRouter: builds a throwaway MSAGL GeometryGraph from the request's
    // node rectangles, runs the mode-appropriate MSAGL router with the node positions held fixed, and
    // converts each routed edge curve into our neutral cubic-bezier segments. Everything is done in
    // interactive (screen) coordinates - the obstacles are the on-screen node rectangles - so the
    // result needs no coordinate transform and the routed edges avoid exactly what the user sees. The
    // work runs on the thread pool (see RouteAsync); this type holds no state between calls, so a B'
    // drag-end reroute and a future B per-move reroute can share one instance safely.
    internal sealed class MsaglInteractiveEdgeRouter
        : IInteractiveEdgeRouter
    {
        // Spline routing padding/shape, in interactive (screen) pixels. Tunable.
        private const double c_SplineTightPadding = 4.0;
        private const double c_SplineLoosePadding = 12.0;
        private const double c_SplineConeAngle = Math.PI / 6.0; // 30 degrees (MSAGL's usual default).

        // Rectilinear routing padding and corner-rounding radius, in interactive (screen) pixels.
        private const double c_RectilinearPadding = 8.0;
        private const double c_RectilinearCornerRadius = 3.0;

        // How many straight pieces to split a curve segment we cannot represent exactly (e.g. a
        // rounded-corner arc) into.
        private const int c_SampleSegments = 8;

        public Task<IReadOnlyList<RoutedEdge>> RouteAsync(EdgeRoutingRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Task.Run(() => Route(request, cancellationToken), cancellationToken);
        }

        private static IReadOnlyList<RoutedEdge> Route(EdgeRoutingRequest request, CancellationToken cancellationToken)
        {
            // Straight/None need no obstacle-aware routing: the client-side approximation already draws
            // an exact clipped straight line, so there is nothing to override.
            if (!RoutesWithMsagl(request.Mode) || request.Edges.Count == 0 || request.Nodes.Count == 0)
            {
                return [];
            }

            cancellationToken.ThrowIfCancellationRequested();

            var graph = new GeometryGraph();
            var nodeLookup = new Dictionary<int, Node>(request.Nodes.Count);
            foreach (EdgeRoutingNode node in request.Nodes)
            {
                // The obstacle rectangle, centred on the node, directly in screen coordinates.
                var centre = new MsaglPoint(node.X + (node.Width / 2.0), node.Y + (node.Height / 2.0));
                var geometryNode = new Node(CurveFactory.CreateRectangle(node.Width, node.Height, centre));
                graph.Nodes.Add(geometryNode);
                nodeLookup[node.Id] = geometryNode;
            }

            var geometryEdges = new List<(int Id, Edge Edge)>(request.Edges.Count);
            foreach (EdgeRoutingEdge edge in request.Edges)
            {
                if (!nodeLookup.TryGetValue(edge.SourceId, out Node? source)
                    || !nodeLookup.TryGetValue(edge.TargetId, out Node? target))
                {
                    continue;
                }
                var geometryEdge = new Edge(source, target);
                graph.Edges.Add(geometryEdge);
                geometryEdges.Add((edge.Id, geometryEdge));
            }

            if (geometryEdges.Count == 0)
            {
                return [];
            }

            graph.UpdateBoundingBox();
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                RunRouter(graph, request.Mode);
            }
            catch (Exception)
            {
                // Best-effort overlay: if the router fails (e.g. nodes dragged into overlap), keep the
                // client-side approximation rather than surfacing an error.
                return [];
            }

            cancellationToken.ThrowIfCancellationRequested();

            var routed = new List<RoutedEdge>(geometryEdges.Count);
            foreach ((int id, Edge geometryEdge) in geometryEdges)
            {
                if (geometryEdge.Curve is null)
                {
                    continue;
                }
                IReadOnlyList<GraphEdgeSegment> segments = ToSegments(geometryEdge.Curve);
                if (segments.Count > 0)
                {
                    routed.Add(new RoutedEdge(id, segments));
                }
            }
            return routed;
        }

        // Spline/rectilinear modes route through MSAGL; straight/None do not (their exact shape is a
        // clipped straight line, which the client-side approximation already produces).
        private static bool RoutesWithMsagl(GraphEdgeRoutingMode mode)
        {
            return mode switch
            {
                GraphEdgeRoutingMode.StraightLine or GraphEdgeRoutingMode.None => false,
                _ => true,
            };
        }

        private static void RunRouter(GeometryGraph graph, GraphEdgeRoutingMode mode)
        {
            switch (mode)
            {
                case GraphEdgeRoutingMode.Rectilinear:
                case GraphEdgeRoutingMode.RectilinearToCenter:
                    new RectilinearEdgeRouter(graph, c_RectilinearPadding, c_RectilinearCornerRadius, useSparseVisibilityGraph: true).Run();
                    break;

                case GraphEdgeRoutingMode.SplineBundling:
                    new SplineRouter(graph, c_SplineTightPadding, c_SplineLoosePadding, c_SplineConeAngle, new BundlingSettings()).Run();
                    break;

                // Spline and SugiyamaSplines: once node positions are fixed there is no Sugiyama-specific
                // router, so both use the general spline router.
                default:
                    new SplineRouter(graph, c_SplineTightPadding, c_SplineLoosePadding, c_SplineConeAngle).Run();
                    break;
            }
        }

        // Convert an MSAGL routed curve (in screen coordinates) into our contiguous cubic-bezier
        // segments. Line and cubic segments convert exactly; anything else (e.g. a corner arc) is
        // flattened into short straight pieces via the curve's parametric indexer.
        private static IReadOnlyList<GraphEdgeSegment> ToSegments(ICurve curve)
        {
            var segments = new List<GraphEdgeSegment>();
            AppendCurve(curve, segments);
            return segments;
        }

        private static void AppendCurve(ICurve curve, List<GraphEdgeSegment> segments)
        {
            switch (curve)
            {
                case Curve composite:
                    foreach (ICurve segment in composite.Segments)
                    {
                        AppendCurve(segment, segments);
                    }
                    break;

                case LineSegment line:
                    segments.Add(GraphEdgeGeometry.StraightSegment(ToPoint(line.Start), ToPoint(line.End)));
                    break;

                case CubicBezierSegment bezier:
                    segments.Add(new GraphEdgeSegment(
                        ToPoint(bezier.B(0)),
                        ToPoint(bezier.B(1)),
                        ToPoint(bezier.B(2)),
                        ToPoint(bezier.B(3))));
                    break;

                default:
                    AppendSampled(curve, segments);
                    break;
            }
        }

        // Flatten any curve into c_SampleSegments straight pieces using its parametric indexer. Used for
        // segment types we do not convert exactly (arcs, polylines, etc.).
        private static void AppendSampled(ICurve curve, List<GraphEdgeSegment> segments)
        {
            double start = curve.ParStart;
            double end = curve.ParEnd;
            if (end <= start)
            {
                return;
            }

            MsaglPoint previous = curve[start];
            for (int i = 1; i <= c_SampleSegments; i++)
            {
                double t = start + ((end - start) * i / c_SampleSegments);
                MsaglPoint next = curve[t];
                segments.Add(GraphEdgeGeometry.StraightSegment(ToPoint(previous), ToPoint(next)));
                previous = next;
            }
        }

        private static Point ToPoint(MsaglPoint point)
        {
            return new Point(point.X, point.Y);
        }
    }
}
