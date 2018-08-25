using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Zametek.Common.Project;
using Zametek.Common.ProjectPlan;
using Zametek.Utility;

namespace Zametek.Engine.ProjectPlan
{
    public class GraphMLBuilder
    {
        #region Public Methods

        public static graphml ToGraphML(DiagramArrowGraphDto diagramArrowGraphDto)
        {
            if (diagramArrowGraphDto == null)
            {
                throw new ArgumentNullException(nameof(diagramArrowGraphDto));
            }
            IList<DiagramNodeDto> diagramNodes = diagramArrowGraphDto.Nodes.ToList();
            IList<DiagramEdgeDto> diagramEdges = diagramArrowGraphDto.Edges.ToList();
            var graph = new graphmlGraph
            {
                id = "G",
                edgedefault = "directed",
                node = diagramNodes.Select(BuildArrowGraphNode).ToArray(),
                edge = diagramEdges.Select(BuildArrowGraphEdge).ToArray()
            };
            return new graphml
            {
                Items = new object[]
                {
                    new graphmlKey { @for = "node", id = "d6", yfilestype = "nodegraphics" },
                    new graphmlKey { @for = "edge", id = "d10", yfilestype = "edgegraphics" },
                    graph
                }
            };
        }

        #endregion

        #region Private Methods

        private static graphmlGraphNode BuildArrowGraphNode(DiagramNodeDto diagramNodeDto)
        {
            if (diagramNodeDto == null)
            {
                throw new ArgumentNullException(nameof(diagramNodeDto));
            }
            var outputNode = new graphmlGraphNode
            {
                id = FormatArrowGraphNodeId(diagramNodeDto.Id),
                data = new data
                {
                    key = "d6",
                    ShapeNode = new ShapeNode
                    {
                        Geometry = new ShapeNodeGeometry
                        {
                            height = diagramNodeDto.Height.ToString(CultureInfo.InvariantCulture),
                            width = diagramNodeDto.Width.ToString(CultureInfo.InvariantCulture),
                            x = diagramNodeDto.X.ToString(CultureInfo.InvariantCulture),
                            y = diagramNodeDto.Y.ToString(CultureInfo.InvariantCulture)
                        },
                        Fill = new ShapeNodeFill
                        {
                            color = diagramNodeDto.FillColorHexCode,
                            hasColor = "true",
                            transparent = "false"
                        },
                        BorderStyle = new ShapeNodeBorderStyle
                        {
                            color = diagramNodeDto.BorderColorHexCode,
                            type = "line",
                            width = "1.0"
                        },
                        Shape = new ShapeNodeShape
                        {
                            type = "roundrectangle"
                        },
                        NodeLabel = new ShapeNodeNodeLabel
                        {
                            alignment = "center",
                            autoSizePolicy = "content",
                            fontFamily = "Dialog",
                            fontSize = "12",
                            fontStyle = "plain",
                            hasBackgroundColor = "false",
                            hasLineColor = "false",
                            hasText = "true",
                            height = "4.0",
                            modelName = "custom",
                            textColor = "#000000",
                            visible = "true",
                            width = "4.0",
                            x = "13.0",
                            y = "13.0",
                            Text = diagramNodeDto.Text,
                            LabelModel = new ShapeNodeNodeLabelLabelModel
                            {
                                SmartNodeLabelModel = new ShapeNodeNodeLabelLabelModelSmartNodeLabelModel
                                {
                                    distance = "4.0"
                                }
                            },
                            ModelParameter = new ShapeNodeNodeLabelModelParameter
                            {
                                SmartNodeLabelModelParameter =
                                    new ShapeNodeNodeLabelModelParameterSmartNodeLabelModelParameter
                                    {
                                        labelRatioX = "0.0",
                                        labelRatioY = "0.0",
                                        nodeRatioX = "0.0",
                                        nodeRatioY = "0.0",
                                        offsetX = "0.0",
                                        offsetY = "0.0",
                                        upX = "0.0",
                                        upY = "-1.0"
                                    }
                            }
                        }
                    }
                }
            };
            return outputNode;
        }

        private static graphmlGraphEdge BuildArrowGraphEdge(DiagramEdgeDto diagramEdgeDto)
        {
            if (diagramEdgeDto == null)
            {
                throw new ArgumentNullException(nameof(diagramEdgeDto));
            }
            var outputEdge = new graphmlGraphEdge
            {
                id = FormatArrowGraphEdgeId(diagramEdgeDto.Id),
                source = FormatArrowGraphNodeId(diagramEdgeDto.SourceId),
                target = FormatArrowGraphNodeId(diagramEdgeDto.TargetId)
            };

            string dashStyle = "line";
            diagramEdgeDto.DashStyle.ValueSwitchOn()
                .Case(EdgeDashStyle.Normal, x => dashStyle = "line")
                .Case(EdgeDashStyle.Dashed, x => dashStyle = "dashed")
                .Default(x =>
                {
                    // TODO
                    //throw new InvalidEnumArgumentException("Unknown EdgeDashStyle value");
                    throw new InvalidOperationException("Unknown EdgeDashStyle value");
                });

            outputEdge.data = new data
            {
                key = "d10",
                PolyLineEdge = new PolyLineEdge
                {
                    Path = new PolyLineEdgePath
                    {
                        sx = "0.0",
                        sy = "0.0",
                        tx = "0.0",
                        ty = "0.0"
                    },
                    LineStyle = new PolyLineEdgeLineStyle
                    {
                        color = diagramEdgeDto.ForegroundColorHexCode,
                        type = dashStyle,
                        width = diagramEdgeDto.StrokeThickness.ToString(CultureInfo.InvariantCulture)
                    },
                    Arrows = new PolyLineEdgeArrows
                    {
                        source = "none",
                        target = "standard"
                    },
                    EdgeLabel = new PolyLineEdgeEdgeLabel
                    {
                        alignment = "center",
                        backgroundColor = "#FFFFFF",
                        configuration = "AutoFlippingLabel",
                        distance = "2.0",
                        fontFamily = "Dialog",
                        fontSize = "12",
                        fontStyle = "plain",
                        hasLineColor = "false",
                        height = "18.701171875",
                        modelName = "centered",
                        modelPosition = "center",
                        preferredPlacement = "on_edge",
                        ratio = "0.5",
                        textColor = "#000000",
                        visible = diagramEdgeDto.ShowLabel ? "true" : "false",
                        width = "10.673828125",
                        x = "48.66937255859375",
                        y = "-10.915985107421875",
                        PreferredPlacementDescriptor = new PolyLineEdgeEdgeLabelPreferredPlacementDescriptor
                        {
                            angle = "0.0",
                            angleOffsetOnRightSide = "0",
                            angleReference = "absolute",
                            angleRotationOnRightSide = "co",
                            distance = "-1.0",
                            placement = "anywhere",
                            side = "on_edge",
                            sideReference = "relative_to_edge_flow"
                        },
                        hasText = "true",
                        Text = diagramEdgeDto.Label
                    },
                    BendStyle = new PolyLineEdgeBendStyle
                    {
                        smoothed = "false"
                    }
                }
            };
            return outputEdge;
        }

        private static string FormatArrowGraphNodeId(int id)
        {
            return $"n{id}";
        }

        private static string FormatArrowGraphEdgeId(int id)
        {
            return $"e{id}";
        }

        #endregion
    }
}
