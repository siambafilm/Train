using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Metro/Metro Map Renderer")]
public class MetroMapRenderer : MaskableGraphic
{
    public MetroMapData data;
    public string activeLineId;

    [Header("Стиль линий")]
    public float activeThickness   = 5f;
    public float inactiveThickness = 2f;
    public float activeAlpha       = 1f;
    public float inactiveAlpha     = 0.28f;

    [Header("Стиль станций")]
    public float stationRadius         = 4f;
    public float transferStationRadius = 7f;
    public float transferStationBorder = 2f;
    public Color transferStationFill   = Color.white;

    private readonly Dictionary<string, Color> colorMap = new();

    public void RefreshColors()
    {
        colorMap.Clear();
        if (data?.lineColors != null)
            foreach (var ce in data.lineColors)
                if (ColorUtility.TryParseHtmlString(ce.hex, out Color c))
                    colorMap[ce.key] = c;
        SetVerticesDirty();
    }

    public Color GetLineColor(LineData line)
    {
        if (!string.IsNullOrEmpty(line?.colorKey) && colorMap.TryGetValue(line.colorKey, out var c))
            return c;
        return Color.white;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (data == null || data.lines == null) return;

        // 1. Неактивные линии — приглушённые
        foreach (var line in data.lines)
        {
            if (line.id == activeLineId) continue;
            if (line.stations == null || line.stations.Length < 2) continue;
            Color c = GetLineColor(line); c.a = inactiveAlpha;
            DrawPolyline(vh, line, c, inactiveThickness);
            DrawStations(vh, line, c, false);
        }

        // 2. Активная линия — поверх и ярко
        foreach (var line in data.lines)
        {
            if (line.id != activeLineId) continue;
            if (line.stations == null || line.stations.Length < 2) continue;
            Color c = GetLineColor(line); c.a = activeAlpha;
            DrawPolyline(vh, line, c, activeThickness);
            DrawStations(vh, line, c, true);
        }
    }

    private void DrawPolyline(VertexHelper vh, LineData line, Color color, float thickness)
    {
        var pts = line.stations;
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector2 a = new(pts[i].pos[0],   pts[i].pos[1]);
            Vector2 b = new(pts[i+1].pos[0], pts[i+1].pos[1]);
            AddLineQuad(vh, a, b, thickness, color);
        }
    }

    private void DrawStations(VertexHelper vh, LineData line, Color lineColor, bool active)
    {
        float r = active ? stationRadius : stationRadius * 0.85f;
        foreach (var st in line.stations)
        {
            Vector2 p = new(st.pos[0], st.pos[1]);
            bool isTransfer = !string.IsNullOrEmpty(st.transferTo);

            if (isTransfer)
            {
                float tr = active ? transferStationRadius : transferStationRadius * 0.85f;
                AddCircle(vh, p, tr, lineColor, 28);
                Color fill = transferStationFill;
                fill.a = active ? 1f : 0.55f;
                AddCircle(vh, p, Mathf.Max(0.1f, tr - transferStationBorder), fill, 28);
            }
            else
            {
                AddCircle(vh, p, r, lineColor, 18);
            }
        }
    }

    private void AddLineQuad(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color color)
    {
        Vector2 dir = b - a;
        float len = dir.magnitude;
        if (len < 0.0001f) return;
        dir /= len;
        Vector2 n = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

        int idx = vh.currentVertCount;
        vh.AddVert(a - n, color, Vector2.zero);
        vh.AddVert(a + n, color, Vector2.zero);
        vh.AddVert(b + n, color, Vector2.zero);
        vh.AddVert(b - n, color, Vector2.zero);
        vh.AddTriangle(idx,   idx+1, idx+2);
        vh.AddTriangle(idx,   idx+2, idx+3);
    }

    private void AddCircle(VertexHelper vh, Vector2 center, float radius, Color color, int segments)
    {
        int c0 = vh.currentVertCount;
        vh.AddVert(center, color, Vector2.zero);
        for (int i = 0; i <= segments; i++)
        {
            float ang = i * Mathf.PI * 2f / segments;
            vh.AddVert(center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius, color, Vector2.zero);
        }
        for (int i = 0; i < segments; i++)
            vh.AddTriangle(c0, c0 + 1 + i, c0 + 2 + i);
    }

    public override bool Raycast(Vector2 sp, Camera eventCamera) => false;
}