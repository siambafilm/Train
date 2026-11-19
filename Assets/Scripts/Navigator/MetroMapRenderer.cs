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

    [Header("Стиль депо")]
    [Tooltip("Половина длины перпендикулярной перекладины 'T' депо (схемные единицы)")]
    public float depotBarHalfLength = 15f;

    [Tooltip("Множитель толщины перекладины депо относительно толщины линии")]
    public float depotBarThicknessMultiplier = 1.5f;

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
            DrawDepots(vh, line, c, inactiveThickness);   // ← ДОБАВЛЕНО
            DrawPolyline(vh, line, c, inactiveThickness);
            DrawStations(vh, line, c, false);
        }

        // 2. Активная линия — поверх и ярко
        foreach (var line in data.lines)
        {
            if (line.id != activeLineId) continue;
            if (line.stations == null || line.stations.Length < 2) continue;
            Color c = GetLineColor(line); c.a = activeAlpha;
            DrawDepots(vh, line, c, activeThickness);     // ← ДОБАВЛЕНО
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

    // ==========================================================
    //  DEPOTS ("T"-маркеры на концах линии)
    // ==========================================================

    private void DrawDepots(VertexHelper vh, LineData line, Color color, float thickness)
    {
        if (line?.stations == null || line.stations.Length < 2) return;
        if (data?.settings == null) return;

        float scale = data.settings.schematicScale;
        if (scale < 0.0001f) scale = 0.05f;

        // depotOffset хранится в мировых единицах — переводим в схемные
        float depotOffset = data.settings.depotOffset / scale;
        if (depotOffset <= 0.0001f) return;

        var pts = line.stations;
        int n = pts.Length;

        // Депо-начало: за первой станцией (в сторону, противоположную линии)
        DrawSingleDepot(vh, pts[0],   pts[1],   depotOffset, color, thickness);

        // Депо-конец: за последней станцией (продолжение линии)
        DrawSingleDepot(vh, pts[n-1], pts[n-2], depotOffset, color, thickness);
    }

    private void DrawSingleDepot(
        VertexHelper vh, StationData station, StationData other,
        float depotOffset, Color color, float thickness)
    {
        if (station?.pos == null || station.pos.Length < 2) return;
        if (other?.pos   == null || other.pos.Length   < 2) return;

        Vector2 s = new(station.pos[0], station.pos[1]);
        Vector2 o = new(other.pos[0],   other.pos[1]);

        // Направление «наружу» от линии: (station − other).normalized
        Vector2 outward = s - o;
        float len = outward.magnitude;
        if (len < 0.0001f) return;
        outward /= len;

        Vector2 depotPos = s + outward * depotOffset;
        Vector2 perp     = new(-outward.y, outward.x);

        // --- Стебель T: от станции до депо ---
        AddLineQuad(vh, s, depotPos, thickness, color);

        // --- Перекладина T (перпендикулярно к направлению линии) ---
        float barHalf  = Mathf.Max(1f, depotBarHalfLength);
        float barThick = thickness * Mathf.Max(1f, depotBarThicknessMultiplier);

        Vector2 barA = depotPos - perp * barHalf;
        Vector2 barB = depotPos + perp * barHalf;
        AddLineQuad(vh, barA, barB, barThick, color);
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