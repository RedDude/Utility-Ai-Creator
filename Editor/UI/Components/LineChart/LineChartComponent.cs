using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UniRxExtension;

internal class LineChartComponent: IMGUIContainer
{
    internal const bool isMinimized = false;

    private readonly float marginLeft = 50f;
    private readonly float marginRight = 50f;
    private readonly float marginTop = 25f;
    private readonly float marginBottom = 25f;
    private readonly float marginTextBottom = -7.5f;
    private readonly float marginTextLeft = 30f;
    private readonly float textXAxisAdjuster = -3f;
    private readonly float textYAxisAdjuster = 5f;
    public float ScreenHeight { get; private set; }
    public float ScreenWidth { get; private set; }
    private float graphHeight;
    private float graphWidth;
    private Vector3 graphOrigon;


    private float graphMinX = 45000f;
    private float graphMaxX = 50000f;
    private float graphRangeX => graphMaxX - graphMinX;

    private readonly float graphMinY = 0f;
    private readonly float graphMaxY = 1f;
    private float graphRangeY => graphMaxY - graphMinY;
    private float stepCountX = 10f;
    private readonly float stepCountY = 10f;
    private List<Vector2> points = new List<Vector2>();

    private static readonly Color[] segmentPalette =
    {
        new Color(0.30f, 0.62f, 0.95f), new Color(0.95f, 0.60f, 0.25f), new Color(0.40f, 0.80f, 0.50f),
        new Color(0.85f, 0.45f, 0.80f), new Color(0.90f, 0.80f, 0.30f), new Color(0.35f, 0.80f, 0.85f)
    };

    /// <summary>Colour shared by a function's chart region, its bar segment and its card.</summary>
    internal static Color SegmentColor(int index) => segmentPalette[index % segmentPalette.Length];

    private const float handleHitWidth = 8f;
    private List<float> segments = new List<float>();
    private int draggedSegment = -1;
    private int hoveredSegment = -1;

    /// <summary>Raised while a segment boundary is dragged in the chart: (segment index, graph x).</summary>
    internal System.Action<int, float> OnSegmentDragged;

    internal void SetSegments(IEnumerable<float> segmentXs)
    {
        segments = new List<float>(segmentXs);
    }

    public void DrawCurve(List<Vector2> points, float min = 0, float max = 1, int steps = 100, int stepCountX = 10)
    {
        this.stepCountX = stepCountX;
        graphMinX = min;
        graphMaxX = max;
        this.points = points;

        MarkDirtyRepaint();
    }

    public void DrawCurve()
    {
        if (points.Count < 2) return;

        // One anti-aliased polyline (joined corners, 2px) instead of 1px aliased segments; Y is kept inside the graph.
        var screen = new Vector3[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            screen[i] = GraphToScreenCoordinates(p.x, Mathf.Clamp(p.y, graphMinY, graphMaxY));
        }
        Handles.color = Color.green;
        Handles.DrawAAPolyLine(2f, screen);
    }

    public LineChartComponent()
    {
        SetSize();

        onGUIHandler = () =>
        {
            // Init
            ScreenHeight = resolvedStyle.height;
            ScreenWidth = resolvedStyle.width;
            graphHeight = ScreenHeight - marginTop - marginBottom;
            graphWidth = ScreenWidth - marginLeft - marginRight;
            graphOrigon = new Vector3(marginLeft, ScreenHeight - marginBottom, 0);

            DrawBaseGraph();
            DrawSegmentRegions();
            DrawCurve();
            DrawSegmentHandles();
        };
    }

    private void DrawSegmentRegions()
    {
        var from = graphMinX;
        for (var i = 0; i <= segments.Count; i++)
        {
            var to = i < segments.Count ? segments[i] : graphMaxX;
            var topLeft = GraphToScreenCoordinates(from, graphMaxY);
            var bottomRight = GraphToScreenCoordinates(to, graphMinY);
            var color = SegmentColor(i);
            color.a = 0.10f;
            EditorGUI.DrawRect(new Rect(topLeft.x, topLeft.y, bottomRight.x - topLeft.x, bottomRight.y - topLeft.y), color);
            from = to;
        }
    }

    private void DrawSegmentHandles()
    {
        var controlId = GUIUtility.GetControlID(FocusType.Passive);
        var e = Event.current;
        var top = GraphToScreenCoordinates(graphMinX, graphMaxY).y;

        for (var i = 0; i < segments.Count; i++)
        {
            var x = GraphToScreenCoordinates(segments[i], graphMinY).x;
            var hit = new Rect(x - handleHitWidth / 2f, top, handleHitWidth, graphHeight);

            if (e.type == EventType.Repaint)
            {
                EditorGUIUtility.AddCursorRect(hit, MouseCursor.ResizeHorizontal);

                var active = i == draggedSegment || (draggedSegment < 0 && i == hoveredSegment);
                var color = SegmentColor(i);
                Handles.color = active ? Color.white : color;
                Handles.DrawDottedLine(new Vector3(x, top, 0), new Vector3(x, top + graphHeight, 0), 4f);
                EditorGUI.DrawRect(new Rect(x - 3f, top - 6f, 6f, 10f), active ? Color.white : color);
                if (active)
                {
                    Handles.Label(new Vector2(x + 6f, top - 18f), segments[i].ToString("0.00"));
                }
            }
        }

        switch (e.type)
        {
            case EventType.MouseDown when e.button == 0:
                var pressed = SegmentAt(e.mousePosition, top);
                if (pressed >= 0)
                {
                    draggedSegment = pressed;
                    GUIUtility.hotControl = controlId;
                    e.Use();
                }
                break;

            case EventType.MouseDrag when GUIUtility.hotControl == controlId && draggedSegment >= 0:
                OnSegmentDragged?.Invoke(draggedSegment, ScreenToGraphX(e.mousePosition.x));
                e.Use();
                break;

            case EventType.MouseUp when GUIUtility.hotControl == controlId:
                GUIUtility.hotControl = 0;
                draggedSegment = -1;
                e.Use();
                MarkDirtyRepaint();
                break;

            case EventType.MouseMove:
                var hovered = SegmentAt(e.mousePosition, top);
                if (hovered != hoveredSegment)
                {
                    hoveredSegment = hovered;
                    MarkDirtyRepaint();
                }
                break;
        }
    }

    private int SegmentAt(Vector2 mousePosition, float top)
    {
        var closest = -1;
        var closestDistance = handleHitWidth / 2f;
        for (var i = 0; i < segments.Count; i++)
        {
            var x = GraphToScreenCoordinates(segments[i], graphMinY).x;
            var distance = Mathf.Abs(mousePosition.x - x);
            if (distance <= closestDistance && mousePosition.y >= top - 8f && mousePosition.y <= top + graphHeight)
            {
                closest = i;
                closestDistance = distance;
            }
        }
        return closest;
    }

    private float ScreenToGraphX(float screenX)
    {
        return graphMinX + (screenX - graphOrigon.x) / graphWidth * graphRangeX;
    }

    private void SetSize()
    {
        if (isMinimized)
        {
            //SetMinimized();
        }
        else
        {
            SetExpanded();
        }
    }

    private void SetMinimized()
    {
        style.flexGrow = 1;
        style.minHeight = 80;
        style.minWidth = 400;
    }

    private void SetExpanded()
    {
        style.flexGrow = 1;
        style.minHeight = 200;
        style.minWidth = 400;
    }

    private void DrawBaseGraph()
    {
        if (!isMinimized)
        {
            // X labels
            Handles.color = Color.grey;
            var stepSizeX = graphRangeX / stepCountX;
            for (var i = 0; i <= stepCountX; i++)
            {
                var x = i * stepSizeX + graphMinX;
                var basePosition = GraphToScreenCoordinates(x, graphMinY);
                Handles.DrawLine(basePosition, GraphToScreenCoordinates(x, graphMaxY), 0.01f);

                var labelPosition = new Vector2(basePosition.x + textXAxisAdjuster, basePosition.y - marginTextBottom);
                Handles.Label(labelPosition, x.ToString("0.00"));
            }

            // Y labels
            Handles.color = Color.grey;
            var stepSizeY = graphRangeY / stepCountY;
            for (var i = 0; i <= stepCountY; i++)
            {
                var y = i * stepSizeY + graphMinY;
                var basePosition = GraphToScreenCoordinates(graphMinX, y);
                Handles.DrawLine(basePosition, GraphToScreenCoordinates(graphMaxX, y), 0.01f);

                var labelPosition = new Vector2(basePosition.x - marginTextLeft, basePosition.y - textYAxisAdjuster);
                Handles.Label(labelPosition, y.ToString("0.0"));
            }
        }

        // Base Lines
        Handles.color = Color.white;
        var xAxixEnd = GraphToScreenCoordinates(graphMaxX, graphMinY);
        var yAxixEnd = GraphToScreenCoordinates(graphMinX, graphMaxY);
        Handles.DrawLine(graphOrigon, xAxixEnd);
        Handles.DrawLine(graphOrigon, yAxixEnd);
    }

    private Vector3 GraphToScreenCoordinates(float graphX, float graphY)
    {
        return GraphToScreenCoordinates(new Vector2(graphX, graphY));
    }

    private Vector3 GraphToScreenCoordinates(Vector2 graphPos)
    {
        var x = graphOrigon.x + (graphPos.x - graphMinX) / graphRangeX * graphWidth;
        var y = graphOrigon.y - (graphPos.y - graphMinY) / graphRangeY * graphHeight;
        return new Vector3(x,y,0);
    }
}


