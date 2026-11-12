using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class MetroMapBuilder : MonoBehaviour
{
    public enum TunnelPivot { Center, Start, End }
    public enum TunnelMode { TwoTunnels, TwoRails, SingleCenterline }

    [Header("Data")]
    [SerializeField] private string dataResourceName = "metro_map";
    [SerializeField] private bool generateOnStart = true;

    [Header("Per-Line Prefabs (override JSON)")]
    [SerializeField] private List<LinePrefabOverride> linePrefabOverrides = new List<LinePrefabOverride>();

    [Header("Track Layout")]
    [Tooltip("Режим тоннелей:\n" +
             "TwoTunnels       — тоннель 1 точно по маркерам (осевая), тоннель 2 смещён на trackSpacing вправо.\n" +
             "TwoRails         — два симметричных тоннеля на ±trackSpacing/2 от осевой.\n" +
             "SingleCenterline — один тоннель ровно по осевой.")]
    [SerializeField] private TunnelMode tunnelMode = TunnelMode.TwoTunnels;

    [Tooltip("Для TwoTunnels: смещение второго тоннеля от первого.\n" +
             "Для TwoRails: расстояние между двумя симметричными тоннелями.\n" +
             "Для SingleCenterline не используется.\n" +
             "Отрицательное значение = смещение влево.")]
    [SerializeField] private float trackSpacing = 1.5f;

    [Tooltip("Где находится pivot у префаба сегмента тоннеля.\n" +
             "Center — pivot в середине сегмента;\n" +
             "Start  — pivot в начале (длина вдоль +Z);\n" +
             "End    — pivot в конце (длина вдоль -Z).")]
    [SerializeField] private TunnelPivot tunnelPivot = TunnelPivot.Center;

    [Header("Tunnel Curve")]
    [Tooltip("Натяжение касательных Hermite-кривой на концах тоннеля.\n" +
             "0 — прямая между стыками; 1 — естественная кривая; >1 — резче у станции.")]
    [Range(0f, 3f)]
    [SerializeField] private float tangentStrength = 1f;

    [Tooltip("Количество точек Hermite-кривой на один тоннель.")]
    [Min(8)]
    [SerializeField] private int tunnelSamples = 32;

    [Tooltip("Минимальная длина тоннеля. Если суммарная длина меньше — тоннель не строится.")]
    [Min(0.01f)]
    [SerializeField] private float minTunnelLength = 0.5f;

    [Header("Anchors (опционально)")]
    [Tooltip("Имя дочернего объекта префаба станции/депо — точка ВХОДА рельса.")]
    [SerializeField] private string anchorEntryName = "RailEntryAnchor";

    [Tooltip("Имя дочернего объекта префаба станции/депо — точка ВЫХОДА рельса.")]
    [SerializeField] private string anchorExitName = "RailExitAnchor";

    [Header("Markers")]
    [SerializeField] private bool generateMarkers = true;
    [Tooltip("Создавать дополнительную цепочку маркеров для обратного направления " +
             "(по второму тоннелю / второй нити).")]
    [SerializeField] private bool generateReverseMarkers = true;
    [SerializeField] private GameObject markerPrefab;
    [Min(0.5f)]
    [SerializeField] private float markerSpacing = 20f;
    [SerializeField] private MarkerCameraMode stationMarkerCameraMode = MarkerCameraMode.Both;
    [SerializeField] private MarkerCameraMode tunnelMarkerCameraMode = MarkerCameraMode.Both;

    [Header("Debug")]
    [SerializeField] private bool logVerbose = true;

    // ---------------- Nested types ----------------

    [System.Serializable]
    public class LinePrefabOverride
    {
        public string lineId;
        public GameObject stationPrefab;
        public GameObject transferStationPrefab;
        public GameObject depotPrefab;
        public GameObject tunnelSegmentPrefab;
        public float stationLength = 0f;
        public float transferStationLength = 0f;
        public float depotLength = 0f;
        public float tunnelSegmentLength = 0f;
    }

    private class LineContext
    {
        public LineData line;
        public LinePrefabOverride ov;
        public float stationLength;
        public float transferStationLength;
        public float depotLength;
        public float tunnelSegmentLength;
    }

    private struct PathNode
    {
        public Vector3 center;
        public Quaternion rotation;
        public Vector3 travelForward;
        public Vector3 railEntry;
        public Vector3 railExit;
        public Vector3 travelAtEntry;
        public Vector3 travelAtExit;
        public bool isStation;
    }

    // ---------------- State ----------------

    private MetroMapData data;
    private readonly Dictionary<string, Color> colorMap = new();
    private readonly Dictionary<string, int> lineHeights = new();
    private readonly Dictionary<GameObject, float> measuredLengths = new();

    // ---------------- Lifecycle ----------------

    private void Start()
    {
        if (generateOnStart) Build();
    }

    [ContextMenu("Build Map")]
    public void Build()
    {
        ClearExisting();
        measuredLengths.Clear();
        if (!LoadData()) return;
        ParseColors();
        AssignHeights();
        WarnIfUnmatchedOverrides();
        foreach (var line in data.lines) BuildLine(line);
        if (logVerbose) Debug.Log($"[MetroMap] Built {data.lines.Length} lines.");
    }

    // ---------------- Loading / parsing ----------------

    private void ClearExisting()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    private bool LoadData()
    {
        var ta = Resources.Load<TextAsset>(dataResourceName);
        if (ta == null) { Debug.LogError($"[MetroMap] Resources/{dataResourceName}.json not found"); return false; }
        data = JsonUtility.FromJson<MetroMapData>(ta.text);
        return data != null && data.lines != null && data.lines.Length > 0;
    }

    private void ParseColors()
    {
        colorMap.Clear();
        if (data.lineColors == null) return;
        foreach (var c in data.lineColors)
            if (ColorUtility.TryParseHtmlString(c.hex, out Color col))
                colorMap[c.key] = col;
    }

    private void WarnIfUnmatchedOverrides()
    {
        if (linePrefabOverrides == null) return;
        foreach (var ov in linePrefabOverrides)
        {
            if (ov == null || string.IsNullOrEmpty(ov.lineId)) continue;
            bool found = false;
            foreach (var l in data.lines) if (l.id == ov.lineId) { found = true; break; }
            if (!found) Debug.LogWarning($"[MetroMap] LinePrefabOverride с lineId='{ov.lineId}' не найдена в JSON");
        }
    }

    // ---------------- Heights ----------------

    private void AssignHeights()
    {
        int n = data.lines.Length;
        var heights = new int[n];
        for (int i = 0; i < n; i++) heights[i] = -1;

        for (int i = 0; i < n; i++)
            if (data.lines[i].heightLayer >= 0)
                heights[i] = data.lines[i].heightLayer;

        var adj = new List<int>[n];
        var transfers = new List<int>[n];
        for (int i = 0; i < n; i++) { adj[i] = new List<int>(); transfers[i] = new List<int>(); }

        for (int i = 0; i < n; i++)
        for (int j = i + 1; j < n; j++)
        {
            bool tr = LinesTransfer(data.lines[i], data.lines[j]);
            bool cr = LinesCross2D(data.lines[i], data.lines[j]);
            if (tr || cr)
            {
                adj[i].Add(j); adj[j].Add(i);
                if (tr) { transfers[i].Add(j); transfers[j].Add(i); }
            }
        }

        for (int start = 0; start < n; start++)
        {
            if (heights[start] >= 0) continue;
            int seedH = 0;
            foreach (int u in adj[start])
                if (heights[u] >= 0) { seedH = heights[u] + 1; break; }

            heights[start] = seedH;
            var q = new Queue<int>();
            q.Enqueue(start);
            while (q.Count > 0)
            {
                int v = q.Dequeue();
                foreach (int u in adj[v])
                    if (heights[u] < 0)
                    {
                        int cand = heights[v] + 1;
                        while (Conflicts(u, cand, heights, adj)) cand++;
                        heights[u] = cand;
                        q.Enqueue(u);
                    }
            }
        }

        for (int i = 0; i < n; i++)
        for (int j = i + 1; j < n; j++)
            if (transfers[i].Contains(j) && heights[i] == heights[j])
            {
                int raise = (data.lines[j].heightLayer < 0) ? j :
                            (data.lines[i].heightLayer < 0) ? i : -1;
                if (raise >= 0)
                {
                    int cand = heights[raise] + 1;
                    while (Conflicts(raise, cand, heights, adj)) cand++;
                    heights[raise] = cand;
                }
            }

        lineHeights.Clear();
        for (int i = 0; i < n; i++) lineHeights[data.lines[i].id] = heights[i];

        if (logVerbose)
        {
            var parts = new List<string>();
            for (int i = 0; i < n; i++)
                parts.Add($"{data.lines[i].id}={heights[i]}" + (data.lines[i].heightLayer >= 0 ? "(cfg)" : "(auto)"));
            Debug.Log("[MetroMap] Heights: " + string.Join(", ", parts));
        }
    }

    private bool Conflicts(int idx, int h, int[] heights, List<int>[] adj)
    {
        foreach (int u in adj[idx]) if (heights[u] == h) return true;
        return false;
    }

    private bool LinesTransfer(LineData a, LineData b)
    {
        foreach (var sa in a.stations)
        {
            if (string.IsNullOrEmpty(sa.transferTo)) continue;
            foreach (var sb in b.stations) if (sa.transferTo == sb.id) return true;
        }
        return false;
    }

    private bool LinesCross2D(LineData a, LineData b)
    {
        for (int i = 0; i < a.stations.Length - 1; i++)
        for (int j = 0; j < b.stations.Length - 1; j++)
        {
            Vector2 a1 = new(a.stations[i].pos[0],   a.stations[i].pos[1]);
            Vector2 a2 = new(a.stations[i+1].pos[0], a.stations[i+1].pos[1]);
            Vector2 b1 = new(b.stations[j].pos[0],   b.stations[j].pos[1]);
            Vector2 b2 = new(b.stations[j+1].pos[0], b.stations[j+1].pos[1]);
            if (SegmentsIntersect(a1, a2, b1, b2)) return true;
        }
        return false;
    }

    private bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
    {
        float d = (p2.x - p1.x) * (p4.y - p3.y) - (p2.y - p1.y) * (p4.x - p3.x);
        if (Mathf.Abs(d) < 0.0001f) return false;
        float t = ((p3.x - p1.x) * (p4.y - p3.y) - (p3.y - p1.y) * (p4.x - p3.x)) / d;
        float u = ((p3.x - p1.x) * (p2.y - p1.y) - (p3.y - p1.y) * (p2.x - p1.x)) / d;
        return t > 0.01f && t < 0.99f && u > 0.01f && u < 0.99f;
    }

    // ---------------- Prefab resolution ----------------

    private LinePrefabOverride GetOverride(string lineId)
    {
        if (linePrefabOverrides == null) return null;
        foreach (var ov in linePrefabOverrides)
            if (ov != null && ov.lineId == lineId) return ov;
        return null;
    }

    private GameObject ResolvePrefab(LinePrefabOverride ov, string type)
    {
        if (ov != null)
        {
            GameObject p = type switch
            {
                "station"         => ov.stationPrefab,
                "transferStation" => ov.transferStationPrefab,
                "depot"           => ov.depotPrefab,
                "tunnelSegment"   => ov.tunnelSegmentPrefab,
                _ => null
            };
            if (p != null) return p;
        }

        if (data.prefabs == null) return null;
        string path = type switch
        {
            "station"         => data.prefabs.station,
            "transferStation" => data.prefabs.transferStation,
            "depot"           => data.prefabs.depot,
            "tunnelSegment"   => data.prefabs.tunnelSegment,
            _ => null
        };
        return string.IsNullOrEmpty(path) ? null : Resources.Load<GameObject>(path);
    }

    private float ResolveLength(float overrideValue, GameObject prefab, float jsonFallback)
    {
        if (overrideValue > 0.001f) return overrideValue;
        if (prefab != null)
        {
            float measured = MeasurePrefabLength(prefab);
            if (measured > 0.001f) return measured;
        }
        return jsonFallback;
    }

    private float MeasurePrefabLength(GameObject prefab)
    {
        if (prefab == null) return 0f;
        if (measuredLengths.TryGetValue(prefab, out float cached)) return cached;

        var temp = Instantiate(prefab);
        temp.transform.position = Vector3.zero;
        temp.transform.rotation = Quaternion.identity;
        temp.transform.localScale = Vector3.one;

        var renderers = temp.GetComponentsInChildren<Renderer>();
        float sizeZ = 0f;
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            sizeZ = b.size.z;
        }

        if (Application.isPlaying) Destroy(temp);
        else DestroyImmediate(temp);

        measuredLengths[prefab] = sizeZ;
        return sizeZ;
    }

    private LineContext MakeContext(LineData line)
    {
        var ov = GetOverride(line.id);
        var ctx = new LineContext { line = line, ov = ov };

        var stPrefab  = ResolvePrefab(ov, "station");
        var trPrefab  = ResolvePrefab(ov, "transferStation");
        var depPrefab = ResolvePrefab(ov, "depot");
        var tunPrefab = ResolvePrefab(ov, "tunnelSegment");

        ctx.stationLength         = ResolveLength(ov?.stationLength         ?? 0f, stPrefab,  data.settings.stationLength);
        ctx.transferStationLength = ResolveLength(ov?.transferStationLength ?? 0f, trPrefab,  data.settings.stationLength);
        ctx.depotLength           = ResolveLength(ov?.depotLength           ?? 0f, depPrefab, data.settings.depotLength);
        ctx.tunnelSegmentLength   = ResolveLength(ov?.tunnelSegmentLength   ?? 0f, tunPrefab, data.settings.tunnelSegmentLength);

        return ctx;
    }

    // ==========================================================
    //  BUILD LINE
    // ==========================================================

    private void BuildLine(LineData line)
    {
        var lineRoot = new GameObject($"Line_{line.id}");
        lineRoot.transform.SetParent(transform, false);

        var ctx = MakeContext(line);
        float y = lineHeights[line.id] * data.settings.heightStep;
        int n = line.stations.Length;

        var centers = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            var st = line.stations[i];
            centers[i] = new Vector3(
                st.pos[0] * data.settings.schematicScale,
                y,
                -st.pos[1] * data.settings.schematicScale);
        }

        var travelDirs = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 dir;
            if (n == 1) dir = Vector3.forward;
            else if (i == 0) dir = (centers[1] - centers[0]).normalized;
            else if (i == n - 1) dir = (centers[i] - centers[i-1]).normalized;
            else
            {
                Vector3 inc  = (centers[i] - centers[i-1]).normalized;
                Vector3 outg = (centers[i+1] - centers[i]).normalized;
                dir = (inc + outg).normalized;
            }
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            travelDirs[i] = dir;
        }

        Vector3 depotStartForward = n > 0 ? travelDirs[0] : Vector3.forward;
        Vector3 depotStartCenter  = centers[0] - depotStartForward * data.settings.depotOffset;

        Vector3 depotEndForward = n > 0 ? travelDirs[n-1] : Vector3.forward;
        Vector3 depotEndCenter  = centers[n-1] + depotEndForward * data.settings.depotOffset;

        var nodes = new List<PathNode>(n + 2);

        // --- DepotStart ---
        {
            var prefab = ResolvePrefab(ctx.ov, "depot");
            float len = ctx.depotLength;
            float halfLen = len * 0.5f;
            var rot = Quaternion.LookRotation(depotStartForward, Vector3.up);

            var node = new PathNode
            {
                center = depotStartCenter,
                rotation = rot,
                travelForward = depotStartForward,
                travelAtEntry = depotStartForward,
                travelAtExit = depotStartForward,
                isStation = false,
                railEntry = depotStartCenter - depotStartForward * halfLen
            };

            if (prefab != null && TryGetAnchorLocal(prefab, anchorExitName, out Vector3 ex))
                node.railExit = depotStartCenter + rot * ex;
            else
                node.railExit = depotStartCenter + depotStartForward * halfLen;

            SpawnOrFallback(prefab, depotStartCenter, rot, $"Depot_{line.id}_start", len, lineRoot.transform);
            nodes.Add(node);
        }

        // --- Станции ---
        for (int i = 0; i < n; i++)
        {
            var st = line.stations[i];
            bool isTransfer = !string.IsNullOrEmpty(st.transferTo);
            var prefab = ResolvePrefab(ctx.ov, isTransfer ? "transferStation" : "station");
            float len = isTransfer ? ctx.transferStationLength : ctx.stationLength;
            float halfLen = len * 0.5f;
            var rot = Quaternion.LookRotation(travelDirs[i], Vector3.up);

            var node = new PathNode
            {
                center = centers[i],
                rotation = rot,
                travelForward = travelDirs[i],
                travelAtEntry = travelDirs[i],
                travelAtExit = travelDirs[i],
                isStation = true
            };

            if (prefab != null && TryGetAnchorLocal(prefab, anchorEntryName, out Vector3 en))
                node.railEntry = centers[i] + rot * en;
            else
                node.railEntry = centers[i] - travelDirs[i] * halfLen;

            if (prefab != null && TryGetAnchorLocal(prefab, anchorExitName, out Vector3 ex))
                node.railExit = centers[i] + rot * ex;
            else
                node.railExit = centers[i] + travelDirs[i] * halfLen;

            SpawnOrFallback(prefab, centers[i], rot, $"Station_{line.id}_{st.id}", len, lineRoot.transform);
            nodes.Add(node);
        }

        // --- DepotEnd ---
        {
            var prefab = ResolvePrefab(ctx.ov, "depot");
            float len = ctx.depotLength;
            float halfLen = len * 0.5f;
            var rot = Quaternion.LookRotation(depotEndForward, Vector3.up);

            var node = new PathNode
            {
                center = depotEndCenter,
                rotation = rot,
                travelForward = depotEndForward,
                travelAtEntry = depotEndForward,
                travelAtExit = depotEndForward,
                isStation = false,
                railExit = depotEndCenter + depotEndForward * halfLen
            };

            if (prefab != null && TryGetAnchorLocal(prefab, anchorEntryName, out Vector3 en))
                node.railEntry = depotEndCenter + rot * en;
            else
                node.railEntry = depotEndCenter - depotEndForward * halfLen;

            SpawnOrFallback(prefab, depotEndCenter, rot, $"Depot_{line.id}_end", len, lineRoot.transform);
            nodes.Add(node);
        }

        // Тоннели + осевая линия для маркеров (прямой и обратный ход)
        var fullCenterline         = new List<Vector3>();
        var fullIsStation          = new List<bool>();
        var fullReverseCenterline  = new List<Vector3>();
        var fullReverseIsStation   = new List<bool>();

        for (int i = 0; i < nodes.Count - 1; i++)
        {
            BuildTunnelBetween(nodes[i], nodes[i+1], ctx, lineRoot.transform,
                fullCenterline, fullIsStation,
                fullReverseCenterline, fullReverseIsStation);

            if (nodes[i+1].isStation)
            {
                // --- Станция: прямой ход ---
                if (fullCenterline.Count > 0 &&
                    Vector3.Distance(fullCenterline[fullCenterline.Count - 1], nodes[i+1].railEntry) < 0.001f)
                    fullIsStation[fullCenterline.Count - 1] = true;

                Vector3 e = nodes[i+1].railEntry;
                Vector3 x = nodes[i+1].railExit;
                const int straightSamples = 8;
                for (int k = 1; k <= straightSamples; k++)
                {
                    float s = (float)k / straightSamples;
                    Vector3 pt = Vector3.Lerp(e, x, s);
                    if (fullCenterline.Count > 0 &&
                        Vector3.Distance(fullCenterline[fullCenterline.Count - 1], pt) < 0.001f) continue;
                    fullCenterline.Add(pt);
                    fullIsStation.Add(true);
                }

                // --- Станция: обратный ход (смещённая копия) ---
                if (TryGetReverseOffset(nodes[i+1], out Vector3 offset))
                {
                    Vector3 re = e + offset;
                    Vector3 rx = x + offset;

                    if (fullReverseCenterline.Count > 0 &&
                        Vector3.Distance(fullReverseCenterline[fullReverseCenterline.Count - 1], re) < 0.001f)
                        fullReverseIsStation[fullReverseCenterline.Count - 1] = true;

                    for (int k = 1; k <= straightSamples; k++)
                    {
                        float s = (float)k / straightSamples;
                        Vector3 pt = Vector3.Lerp(re, rx, s);
                        if (fullReverseCenterline.Count > 0 &&
                            Vector3.Distance(fullReverseCenterline[fullReverseCenterline.Count - 1], pt) < 0.001f) continue;
                        fullReverseCenterline.Add(pt);
                        fullReverseIsStation.Add(true);
                    }
                }
            }
        }

        // --- Маркеры прямого хода ---
        if (generateMarkers && markerPrefab != null)
        {
            var markersRoot = new GameObject("Markers");
            markersRoot.transform.SetParent(lineRoot.transform, false);
            BuildMarkersAlong(fullCenterline, fullIsStation, markersRoot.transform, "Marker");
        }
        else if (generateMarkers && markerPrefab == null)
        {
            Debug.LogWarning($"[MetroMap] generateMarkers = true, но markerPrefab не задан (line={line.id})", this);
        }

        // --- Маркеры обратного хода ---
        if (generateMarkers && generateReverseMarkers && markerPrefab != null && fullReverseCenterline.Count >= 2)
        {
            var markersRootRev = new GameObject("MarkersReverse");
            markersRootRev.transform.SetParent(lineRoot.transform, false);
            BuildMarkersAlongReverse(fullReverseCenterline, fullReverseIsStation, markersRootRev.transform, "MarkerRev");
        }
    }

    // ==========================================================
    //  TUNNEL BUILDING
    // ==========================================================

    private void BuildTunnelBetween(
        PathNode from, PathNode to, LineContext ctx, Transform parent,
        List<Vector3> fullCenterline, List<bool> fullIsStation,
        List<Vector3> fullReverseCenterline, List<bool> fullReverseIsStation)
    {
        Vector3 p0 = from.railExit;
        Vector3 p1 = to.railEntry;
        Vector3 t0 = from.travelAtExit;
        Vector3 t1 = to.travelAtEntry;

        float dist = Vector3.Distance(p0, p1);
        if (dist < minTunnelLength) return;

        // --- Осевая кривая Hermite ---
        float tangentScale = dist * tangentStrength;
        Vector3 m0 = t0.normalized * tangentScale;
        Vector3 m1 = t1.normalized * tangentScale;

        var centerline = new List<Vector3>(tunnelSamples + 1);
        for (int k = 0; k <= tunnelSamples; k++)
        {
            float s = (float)k / tunnelSamples;
            centerline.Add(HermitePoint(p0, m0, p1, m1, s));
        }

        // --- Копим осевую для прямых маркеров ---
        for (int k = 0; k < centerline.Count; k++)
        {
            if (fullCenterline.Count > 0 &&
                Vector3.Distance(fullCenterline[fullCenterline.Count - 1], centerline[k]) < 0.001f)
                continue;
            fullCenterline.Add(centerline[k]);
            fullIsStation.Add(false);
        }

        // --- Спавн тоннелей и сбор обратного пути ---
        List<Vector3> reversePath = null;

        switch (tunnelMode)
        {
            case TunnelMode.SingleCenterline:
                SpawnTunnelAlong(centerline, ctx, parent);
                break;

            case TunnelMode.TwoTunnels:
            {
                // Тоннель 1 — точно по осевой (там же, где прямые маркеры)
                SpawnTunnelAlong(centerline, ctx, parent);

                // Тоннель 2 — смещён на trackSpacing вправо от направления движения
                if (Mathf.Abs(trackSpacing) > 0.0001f)
                {
                    var offsetPath = OffsetCenterline(centerline, trackSpacing);

                    if (offsetPath.Count >= 2)
                    {
                        Vector3 rightStart = Vector3.Cross(Vector3.up, t0).normalized;
                        Vector3 rightEnd   = Vector3.Cross(Vector3.up, t1).normalized;
                        offsetPath[0] = p0 + rightStart * trackSpacing;
                        offsetPath[offsetPath.Count - 1] = p1 + rightEnd * trackSpacing;
                    }

                    SpawnTunnelAlong(offsetPath, ctx, parent);
                    reversePath = offsetPath;
                }
                break;
            }

            case TunnelMode.TwoRails:
            {
                for (int side = 0; side < 2; side++)
                {
                    float sign = side == 0 ? 1f : -1f;
                    var railPath = OffsetCenterline(centerline, sign * trackSpacing * 0.5f);

                    if (railPath.Count >= 2)
                    {
                        Vector3 rightStart = Vector3.Cross(Vector3.up, t0).normalized;
                        Vector3 rightEnd   = Vector3.Cross(Vector3.up, t1).normalized;
                        railPath[0] = p0 + rightStart * (sign * trackSpacing * 0.5f);
                        railPath[railPath.Count - 1] = p1 + rightEnd * (sign * trackSpacing * 0.5f);
                    }

                    SpawnTunnelAlong(railPath, ctx, parent);

                    // Левая нить (sign<0) считается путём обратного хода
                    if (sign < 0) reversePath = railPath;
                }
                break;
            }
        }

        // --- Копим обратный путь для маркеров обратного хода ---
        if (reversePath != null && fullReverseCenterline != null)
        {
            for (int k = 0; k < reversePath.Count; k++)
            {
                if (fullReverseCenterline.Count > 0 &&
                    Vector3.Distance(fullReverseCenterline[fullReverseCenterline.Count - 1], reversePath[k]) < 0.001f)
                    continue;
                fullReverseCenterline.Add(reversePath[k]);
                fullReverseIsStation.Add(false);
            }
        }
    }

    /// <summary>
    /// Смещение обратного пути относительно осевой в точке узла.
    /// Возвращает false, если для текущего режима обратного пути нет.
    /// </summary>
    private bool TryGetReverseOffset(PathNode node, out Vector3 offset)
    {
        offset = Vector3.zero;
        Vector3 r = Vector3.Cross(Vector3.up, node.travelForward);
        if (r.sqrMagnitude < 0.0001f) return false;
        r.Normalize();

        switch (tunnelMode)
        {
            case TunnelMode.TwoTunnels:
                if (Mathf.Abs(trackSpacing) < 0.0001f) return false;
                offset = r * trackSpacing;
                return true;

            case TunnelMode.TwoRails:
                if (Mathf.Abs(trackSpacing) < 0.0001f) return false;
                offset = r * (-trackSpacing * 0.5f); // левая нить
                return true;

            default:
                return false;
        }
    }

    /// <summary>Ресемплит путь на равные сегменты и спавнит их как сегменты тоннеля.</summary>
    private void SpawnTunnelAlong(List<Vector3> path, LineContext ctx, Transform parent)
    {
        var segs = ResampleToSegments(path, ctx.tunnelSegmentLength);
        for (int k = 0; k < segs.Count - 1; k++)
        {
            Vector3 a = segs[k];
            Vector3 b = segs[k + 1];
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.001f) continue;
            SpawnTunnelSegment(ctx, a, b,
                Quaternion.LookRotation(d / len, Vector3.up), len, parent);
        }
    }

    private static Vector3 HermitePoint(Vector3 p0, Vector3 m0, Vector3 p1, Vector3 m1, float s)
    {
        float s2 = s * s, s3 = s2 * s;
        float h00 =  2f * s3 - 3f * s2 + 1f;
        float h10 =        s3 - 2f * s2 + s;
        float h01 = -2f * s3 + 3f * s2;
        float h11 =        s3 -      s2;
        return h00 * p0 + h10 * m0 + h01 * p1 + h11 * m1;
    }

    private List<Vector3> OffsetCenterline(List<Vector3> centerline, float offset)
    {
        var result = new List<Vector3>(centerline.Count);
        for (int k = 0; k < centerline.Count; k++)
        {
            Vector3 tangent = TangentAt(centerline, k);
            Vector3 right = Vector3.Cross(Vector3.up, tangent);
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            right.Normalize();
            result.Add(centerline[k] + right * offset);
        }
        return result;
    }

    private static Vector3 TangentAt(List<Vector3> path, int k)
    {
        if (path.Count < 2) return Vector3.forward;
        if (k == 0) return (path[1] - path[0]).normalized;
        if (k == path.Count - 1) return (path[path.Count - 1] - path[path.Count - 2]).normalized;
        return (path[k + 1] - path[k - 1]).normalized;
    }

    private List<Vector3> ResampleToSegments(List<Vector3> path, float targetLen)
    {
        if (targetLen < 0.01f) targetLen = 2f;
        if (path.Count < 2) return new List<Vector3>(path);

        float total = 0;
        for (int i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);
        if (total < 0.01f) return new List<Vector3> { path[0] };

        int numSeg = Mathf.Max(1, Mathf.RoundToInt(total / targetLen));
        float step = total / numSeg;

        var result = new List<Vector3> { path[0] };
        float walked = 0;
        int segIdx = 0;

        for (int k = 1; k <= numSeg; k++)
        {
            float target = k * step;
            while (segIdx < path.Count - 1)
            {
                float d = Vector3.Distance(path[segIdx], path[segIdx + 1]);
                if (walked + d >= target - 0.0001f)
                {
                    float t = d > 0.0001f ? (target - walked) / d : 0f;
                    result.Add(Vector3.Lerp(path[segIdx], path[segIdx + 1], Mathf.Clamp01(t)));
                    break;
                }
                walked += d;
                segIdx++;
            }
        }

        Vector3 last = path[path.Count - 1];
        if (Vector3.Distance(result[result.Count - 1], last) > 0.001f) result.Add(last);

        return result;
    }

    private void SpawnTunnelSegment(LineContext ctx, Vector3 a, Vector3 b, Quaternion rot, float len, Transform parent)
    {
        var prefab = ResolvePrefab(ctx.ov, "tunnelSegment");

        Vector3 spawnPos;
        switch (tunnelPivot)
        {
            case TunnelPivot.Start: spawnPos = a; break;
            case TunnelPivot.End:   spawnPos = b; break;
            default:                spawnPos = (a + b) * 0.5f; break;
        }

        var go = SpawnOrFallback(prefab, spawnPos, rot, $"Tunnel_{ctx.line.id}", len, parent);
        if (go == null) return;

        if (prefab != null && len > 0.0001f)
        {
            float prefabScaleZ = prefab.transform.localScale.z;
            float baseLen = ctx.tunnelSegmentLength * prefabScaleZ;
            if (baseLen > 0.0001f)
            {
                var sc = go.transform.localScale;
                sc.z *= len / baseLen;
                go.transform.localScale = sc;
            }
        }

        var rend = go.GetComponentInChildren<Renderer>();
        if (rend != null && colorMap.TryGetValue(ctx.line.colorKey, out var col))
            rend.material.color = col;
    }

    // ==========================================================
    //  ANCHORS
    // ==========================================================

    private bool TryGetAnchorLocal(GameObject prefab, string anchorName, out Vector3 localPos)
    {
        localPos = Vector3.zero;
        if (prefab == null || string.IsNullOrEmpty(anchorName)) return false;
        var t = prefab.transform.Find(anchorName);
        if (t == null) return false;
        localPos = t.localPosition;
        return true;
    }

    // ==========================================================
    //  MARKERS
    // ==========================================================

    private void BuildMarkersAlong(List<Vector3> path, List<bool> isStation, Transform parent, string namePrefix)
    {
        BuildMarkersAlongImpl(path, isStation, parent, namePrefix, false);
    }

    private void BuildMarkersAlongReverse(List<Vector3> path, List<bool> isStation, Transform parent, string namePrefix)
    {
        // Разворачиваем путь целиком — тангенсы и порядок станут обратными автоматически
        var revPath = new List<Vector3>(path);
        revPath.Reverse();
        var revIsStation = new List<bool>(isStation);
        revIsStation.Reverse();

        BuildMarkersAlongImpl(revPath, revIsStation, parent, namePrefix, true);
    }

    private void BuildMarkersAlongImpl(List<Vector3> path, List<bool> isStation, Transform parent, string namePrefix, bool isReverse)
    {
        if (path.Count < 2) return;

        var cum = new float[path.Count];
        for (int i = 1; i < path.Count; i++)
            cum[i] = cum[i - 1] + Vector3.Distance(path[i - 1], path[i]);
        float total = cum[cum.Length - 1];
        if (total < 0.1f) return;

        float spacing = Mathf.Max(0.5f, markerSpacing);
        var arcs = new List<float> { 0f };
        int steps = Mathf.FloorToInt(total / spacing);
        for (int i = 1; i <= steps; i++) arcs.Add(i * spacing);
        if (total - arcs[arcs.Count - 1] > 0.5f) arcs.Add(total);

        var markers = new List<TrainTrackMarker>(arcs.Count);
        for (int i = 0; i < arcs.Count; i++)
        {
            Vector3 pos = SampleByArc(path, cum, arcs[i], out Vector3 fwd);
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);

            int idx = FindClosestIndex(cum, arcs[i]);
            bool inStation = isStation[Mathf.Clamp(idx, 0, isStation.Count - 1)];

            var go = Instantiate(markerPrefab, pos, rot, parent);
            go.name = $"{namePrefix}_{i:D3}";

            var m = go.GetComponent<TrainTrackMarker>();
            if (m == null) m = go.AddComponent<TrainTrackMarker>();
            m.cameraMode    = inStation ? stationMarkerCameraMode : tunnelMarkerCameraMode;
            m.isSwitchPoint = false;
            markers.Add(m);
        }

        for (int i = 0; i < markers.Count; i++)
        {
            if (i < markers.Count - 1) markers[i].nextForward  = markers[i + 1];
            if (i > 0)                 markers[i].nextBackward = markers[i - 1];
        }

        if (logVerbose)
            Debug.Log($"[MetroMap] {markers.Count} {(isReverse ? "REVERSE " : "")}markers generated");
    }

    private int FindClosestIndex(float[] cum, float arc)
    {
        int lo = 0, hi = cum.Length - 1;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) / 2;
            if (cum[mid] <= arc) lo = mid; else hi = mid;
        }
        return Mathf.Abs(cum[lo] - arc) < Mathf.Abs(cum[hi] - arc) ? lo : hi;
    }

    private Vector3 SampleByArc(List<Vector3> path, float[] cum, float arc, out Vector3 forward)
    {
        int last = cum.Length - 1;
        if (arc <= 0f) { forward = (path[1] - path[0]).normalized; return path[0]; }
        if (arc >= cum[last]) { forward = (path[last] - path[last - 1]).normalized; return path[last]; }

        int lo = 0, hi = last;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) / 2;
            if (cum[mid] <= arc) lo = mid; else hi = mid;
        }

        float segLen = cum[hi] - cum[lo];
        float t = segLen > 0.0001f ? (arc - cum[lo]) / segLen : 0f;
        Vector3 dir = path[hi] - path[lo];
        forward = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        return Vector3.Lerp(path[lo], path[hi], t);
    }

    // ==========================================================
    //  SPAWN HELPER
    // ==========================================================

    private GameObject SpawnOrFallback(GameObject prefab, Vector3 pos, Quaternion rot, string name, float fallbackSize, Transform parent)
    {
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, pos, rot);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = Vector3.one * fallbackSize;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = new Color(1f, 0f, 1f, 0.85f);
        }
        go.name = name;
        go.transform.SetParent(parent, true);
        return go;
    }

    // ==========================================================
    //  EDITOR UTILS
    // ==========================================================

#if UNITY_EDITOR
    [ContextMenu("Populate Line Overrides From JSON")]
    private void PopulateOverridesFromJSON()
    {
        if (Application.isPlaying)
            Debug.LogWarning("[MetroMap] Populate лучше делать в Edit Mode — в Play Mode изменения не сохранятся.");

        if (!LoadData()) return;
        if (linePrefabOverrides == null) linePrefabOverrides = new List<LinePrefabOverride>();

        Undo.RecordObject(this, "Populate Line Overrides");
        foreach (var line in data.lines)
        {
            bool exists = false;
            foreach (var o in linePrefabOverrides)
                if (o != null && o.lineId == line.id) { exists = true; break; }
            if (!exists)
                linePrefabOverrides.Add(new LinePrefabOverride { lineId = line.id });
        }
        EditorUtility.SetDirty(this);
        Undo.FlushUndoRecordObjects();
        Debug.Log($"[MetroMap] Line overrides populated: {linePrefabOverrides.Count} entries");
    }

    [ContextMenu("Clear Line Overrides")]
    private void ClearOverrides()
    {
        if (linePrefabOverrides == null) return;
        Undo.RecordObject(this, "Clear Line Overrides");
        linePrefabOverrides.Clear();
        EditorUtility.SetDirty(this);
    }
#endif
}