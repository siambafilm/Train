using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MetroMapBuilder : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private string dataResourceName = "metro_map";
    [SerializeField] private bool generateOnStart = true;

    [Header("Per-Line Prefabs (override JSON)")]
    [Tooltip("Для каждой ветки можно задать свои префабы и длины. " +
             "Если поле не задано — используется глобальный префаб из JSON. " +
             "ПКМ по заголовку компонента → Populate Line Overrides From JSON")]
    [SerializeField] private List<LinePrefabOverride> linePrefabOverrides = new List<LinePrefabOverride>();

    [Header("Markers")]
    [Tooltip("Генерировать маркеры пути вдоль каждой ветки (в отдельном объекте 'Markers' внутри ветки)")]
    [SerializeField] private bool generateMarkers = true;

    [Tooltip("Префаб маркера — объект с компонентом TrainTrackMarker (pivot в начале, смотрит по +Z)")]
    [SerializeField] private GameObject markerPrefab;

    [Tooltip("Расстояние между маркерами вдоль пути")]
    [Min(0.5f)]
    [SerializeField] private float markerSpacing = 20f;

    [Tooltip("Режим камеры для маркеров внутри станций")]
    [SerializeField] private MarkerCameraMode stationMarkerCameraMode = MarkerCameraMode.Both;

    [Tooltip("Режим камеры для маркеров на перегонах (тоннель, депо)")]
    [SerializeField] private MarkerCameraMode tunnelMarkerCameraMode = MarkerCameraMode.Both;

    [Header("Debug")]
    [SerializeField] private bool logVerbose = true;

    // ------------------ Публичный вложенный класс ------------------

    [System.Serializable]
    public class LinePrefabOverride
    {
        [Tooltip("ID линии из metro_map.json (например: red, green, purple, black)")]
        public string lineId;

        [Header("Префабы (пусто = взять из JSON)")]
        public GameObject stationPrefab;
        public GameObject transferStationPrefab;
        public GameObject depotPrefab;
        public GameObject tunnelSegmentPrefab;

        [Header("Длины (0 = авто из меша, >0 = принудительно)")]
        public float stationLength = 0f;
        public float transferStationLength = 0f;
        public float depotLength = 0f;
        public float tunnelSegmentLength = 0f;
    }

    // ------------------ Внутреннее состояние ------------------

    private MetroMapData data;
    private readonly Dictionary<string, Color> colorMap = new();
    private readonly Dictionary<string, int> lineHeights = new();
    private readonly Dictionary<GameObject, float> measuredLengths = new();

    private class LineContext
    {
        public LineData line;
        public LinePrefabOverride ov;
        public float stationLength;
        public float transferStationLength;
        public float depotLength;
        public float tunnelSegmentLength;
    }

    // ------------------ Lifecycle ------------------

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
        if (ta == null)
        {
            Debug.LogError($"[MetroMap] Resources/{dataResourceName}.json not found");
            return false;
        }
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
            if (!found)
                Debug.LogWarning($"[MetroMap] LinePrefabOverride с lineId='{ov.lineId}' не найдена в JSON");
        }
    }

    // ------------------ Высоты ------------------

    private void AssignHeights()
    {
        int n = data.lines.Length;
        var heights = new int[n];

        bool allExplicit = true;
        for (int i = 0; i < n; i++)
            if (data.lines[i].heightLayer < 0) { allExplicit = false; break; }

        if (allExplicit)
        {
            for (int i = 0; i < n; i++) heights[i] = data.lines[i].heightLayer;
        }
        else
        {
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

            for (int i = 0; i < n; i++) heights[i] = -1;

            for (int start = 0; start < n; start++)
            {
                if (heights[start] >= 0) continue;
                heights[start] = 0;
                var q = new Queue<int>();
                q.Enqueue(start);
                while (q.Count > 0)
                {
                    int v = q.Dequeue();
                    foreach (int u in adj[v])
                        if (heights[u] < 0) { heights[u] = heights[v] + 1; q.Enqueue(u); }
                }
            }

            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                if (transfers[i].Contains(j) && heights[i] == heights[j])
                {
                    int candidate = heights[j] + 1;
                    while (Conflicts(j, candidate, heights, adj)) candidate++;
                    heights[j] = candidate;
                }
        }

        lineHeights.Clear();
        for (int i = 0; i < n; i++) lineHeights[data.lines[i].id] = heights[i];

        if (logVerbose)
        {
            var parts = new List<string>();
            for (int i = 0; i < n; i++) parts.Add($"{data.lines[i].id}={heights[i]}");
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
            foreach (var sb in b.stations)
                if (sa.transferTo == sb.id) return true;
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

    // ------------------ Оверрайды и длины ------------------

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

    // ------------------ Линия ------------------

    private void BuildLine(LineData line)
    {
        // ---- Пустышка под ветку ----
        var lineRoot = new GameObject($"Line_{line.id}");
        lineRoot.transform.SetParent(transform, false);

        // ---- Пустышка под маркеры ----
        Transform markersRoot = null;
        if (generateMarkers)
        {
            if (markerPrefab != null)
            {
                var mg = new GameObject("Markers");
                mg.transform.SetParent(lineRoot.transform, false);
                markersRoot = mg.transform;
            }
            else
            {
                Debug.LogWarning($"[MetroMap] generateMarkers = true, но markerPrefab не задан (line={line.id})", this);
            }
        }

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

        var orientations = new Quaternion[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 dir;
            if (i == 0 && n > 1)          dir = (centers[1] - centers[0]).normalized;
            else if (i == n - 1 && n > 1) dir = (centers[i] - centers[i-1]).normalized;
            else if (n > 2)
            {
                Vector3 inc  = (centers[i] - centers[i-1]).normalized;
                Vector3 outg = (centers[i+1] - centers[i]).normalized;
                dir = (inc + outg).normalized;
            }
            else dir = Vector3.forward;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            orientations[i] = Quaternion.LookRotation(dir, Vector3.up);
        }

        Vector3 outStart = n > 1 ? (centers[0] - centers[1]).normalized : Vector3.forward;
        Vector3 depotStartPos = centers[0] + outStart * data.settings.depotOffset;
        Quaternion depotStartRot = Quaternion.LookRotation(-outStart, Vector3.up);

        Vector3 outEnd = n > 1 ? (centers[n-1] - centers[n-2]).normalized : Vector3.forward;
        Vector3 depotEndPos = centers[n-1] + outEnd * data.settings.depotOffset;
        Quaternion depotEndRot = Quaternion.LookRotation(-outEnd, Vector3.up);

        // Сборка единого «пути»: депо + станции + депо
        var pathPoints      = new List<Vector3>();
        var pathHalfLengths = new List<float>();
        var pathRenderers   = new List<GameObject>();

        pathPoints.Add(depotStartPos);
        pathHalfLengths.Add(ctx.depotLength * 0.5f);
        pathRenderers.Add(ResolvePrefab(ctx.ov, "depot"));

        for (int i = 0; i < n; i++)
        {
            var st = line.stations[i];
            bool isTransfer = !string.IsNullOrEmpty(st.transferTo);
            pathPoints.Add(centers[i]);
            pathHalfLengths.Add((isTransfer ? ctx.transferStationLength : ctx.stationLength) * 0.5f);
            pathRenderers.Add(ResolvePrefab(ctx.ov, isTransfer ? "transferStation" : "station"));
        }

        pathPoints.Add(depotEndPos);
        pathHalfLengths.Add(ctx.depotLength * 0.5f);
        pathRenderers.Add(ResolvePrefab(ctx.ov, "depot"));

        // Спавн станций и депо (родитель — lineRoot)
        for (int i = 0; i < n; i++)
            PlaceStation(ctx, line.stations[i], centers[i], orientations[i], lineRoot.transform);

        PlaceDepot(ctx, depotStartPos, depotStartRot, line, "start", lineRoot.transform);
        PlaceDepot(ctx, depotEndPos,   depotEndRot,   line, "end",   lineRoot.transform);

        // Тоннели (родитель — lineRoot)
        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            Vector3 p0 = pathPoints[Mathf.Max(0, i - 1)];
            Vector3 p1 = pathPoints[i];
            Vector3 p2 = pathPoints[i + 1];
            Vector3 p3 = pathPoints[Mathf.Min(pathPoints.Count - 1, i + 2)];

            BuildTunnel(ctx, p0, p1, p2, p3, pathHalfLengths[i], pathHalfLengths[i + 1], lineRoot.transform);
        }

        // Маркеры пути
        if (markersRoot != null)
            BuildMarkersForLine(ctx, line, pathPoints, markersRoot);
    }

    private void BuildTunnel(LineContext ctx,
                             Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
                             float trimStart, float trimEnd, Transform parent)
    {
        int subdiv = Mathf.Max(8, data.settings.smoothingSubdivisions * 2);
        var raw = new List<Vector3>();
        for (int s = 0; s <= subdiv; s++) raw.Add(CatmullRom(p0, p1, p2, p3, (float)s / subdiv));

        var trimmed = TrimPathEnds(raw, trimStart, trimEnd);
        if (trimmed.Count < 2) return;

        var resampled = ResamplePath(trimmed, ctx.tunnelSegmentLength);
        if (resampled.Count < 2) return;

        for (int side = 0; side < 2; side++)
        {
            float sign = side == 0 ? -1f : 1f;
            var offsetPath = OffsetPath(resampled, data.settings.trackSpacing * 0.5f * sign);

            for (int k = 0; k < offsetPath.Count - 1; k++)
            {
                Vector3 a = offsetPath[k];
                Vector3 b = offsetPath[k + 1];
                Vector3 mid = (a + b) * 0.5f;
                Vector3 d = b - a;
                if (d.sqrMagnitude < 0.0001f) continue;
                SpawnTunnel(ctx, mid, Quaternion.LookRotation(d.normalized, Vector3.up), parent);
            }
        }
    }

    private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private List<Vector3> TrimPathEnds(List<Vector3> path, float trimStart, float trimEnd)
    {
        if (path.Count < 2) return new List<Vector3>(path);
        float total = 0;
        for (int i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);
        if (total < 0.01f) return new List<Vector3> { path[0] };

        float startDist = Mathf.Clamp(trimStart, 0f, total * 0.45f);
        float endDist   = Mathf.Clamp(total - trimEnd, startDist + 0.01f, total);

        var result = new List<Vector3>();
        float walked = 0;

        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 a = path[i], b = path[i + 1];
            float segLen = Vector3.Distance(a, b);
            float segStart = walked, segEnd = walked + segLen;

            if (segEnd <= startDist) { walked = segEnd; continue; }
            if (segStart >= endDist) break;

            Vector3 entry = segStart < startDist ? Vector3.Lerp(a, b, (startDist - segStart) / segLen) : a;
            Vector3 exit  = segEnd   > endDist   ? Vector3.Lerp(a, b, (endDist   - segStart) / segLen) : b;

            if (result.Count == 0 || Vector3.Distance(result[result.Count - 1], entry) > 0.001f)
                result.Add(entry);
            if (Vector3.Distance(entry, exit) > 0.001f)
                result.Add(exit);

            walked = segEnd;
        }

        return result.Count >= 2 ? result : new List<Vector3> { path[path.Count / 2] };
    }

    private List<Vector3> ResamplePath(List<Vector3> path, float targetLen)
    {
        if (targetLen < 0.01f) targetLen = 2f;
        if (path.Count < 2) return new List<Vector3>(path);
        float total = 0;
        for (int i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);
        if (total < 0.01f) return new List<Vector3> { path[0] };

        int numSeg = Mathf.Max(1, Mathf.CeilToInt(total / targetLen));
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
                if (walked + d >= target)
                {
                    float t = (target - walked) / d;
                    result.Add(Vector3.Lerp(path[segIdx], path[segIdx + 1], t));
                    break;
                }
                walked += d;
                segIdx++;
            }
        }
        return result;
    }

    private List<Vector3> OffsetPath(List<Vector3> path, float offset)
    {
        var result = new List<Vector3>(path.Count);
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 prev = path[Mathf.Max(0, i - 1)];
            Vector3 next = path[Mathf.Min(path.Count - 1, i + 1)];
            Vector3 dir = next - prev;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            right.Normalize();
            result.Add(path[i] + right * offset);
        }
        return result;
    }

    // ------------------ Спавн ------------------

    private void PlaceStation(LineContext ctx, StationData st, Vector3 pos, Quaternion rot, Transform parent)
    {
        bool isTransfer = !string.IsNullOrEmpty(st.transferTo);
        var prefab = ResolvePrefab(ctx.ov, isTransfer ? "transferStation" : "station");
        float len = isTransfer ? ctx.transferStationLength : ctx.stationLength;
        SpawnOrFallback(prefab, pos, rot, $"Station_{ctx.line.id}_{st.id}", len, parent);
    }

    private void PlaceDepot(LineContext ctx, Vector3 pos, Quaternion rot, LineData line, string suffix, Transform parent)
    {
        var prefab = ResolvePrefab(ctx.ov, "depot");
        SpawnOrFallback(prefab, pos, rot, $"Depot_{line.id}_{suffix}", ctx.depotLength, parent);
    }

    private void SpawnTunnel(LineContext ctx, Vector3 pos, Quaternion rot, Transform parent)
    {
        var prefab = ResolvePrefab(ctx.ov, "tunnelSegment");
        var go = SpawnOrFallback(prefab, pos, rot, $"Tunnel_{ctx.line.id}", ctx.tunnelSegmentLength * 0.5f, parent);
        if (go == null) return;
        var rend = go.GetComponentInChildren<Renderer>();
        if (rend != null && colorMap.TryGetValue(ctx.line.colorKey, out var col))
            rend.material.color = col;
    }

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

    // ------------------ Маркеры пути ------------------

    /// <summary>
    /// Генерирует маркеры вдоль осевой линии ветки (Catmull-Rom через депо/станции).
    /// Внутри интервалов станций ставит маркеры с stationMarkerCameraMode,
    /// на перегонах — с tunnelMarkerCameraMode. Связывает цепочку nextForward/nextBackward.
    /// </summary>
    private void BuildMarkersForLine(LineContext ctx, LineData line, List<Vector3> pathPoints, Transform markersParent)
    {
        int n = line.stations.Length;
        if (pathPoints.Count < 2 || n == 0) return;

        float spacing = Mathf.Max(0.5f, markerSpacing);
        int subdiv    = Mathf.Max(8, data.settings.smoothingSubdivisions * 2);

        // ---- 1. Строим гладкую осевую линию (без тримминга) ----
        var smooth = new List<Vector3>();
        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            Vector3 p0 = pathPoints[Mathf.Max(0, i - 1)];
            Vector3 p1 = pathPoints[i];
            Vector3 p2 = pathPoints[i + 1];
            Vector3 p3 = pathPoints[Mathf.Min(pathPoints.Count - 1, i + 2)];

            int startS = (i == 0) ? 0 : 1; // не дублируем стык
            for (int s = startS; s <= subdiv; s++)
                smooth.Add(CatmullRom(p0, p1, p2, p3, (float)s / subdiv));
        }
        if (smooth.Count < 2) return;

        // ---- 2. Накопительная длина дуги ----
        var cum = new float[smooth.Count];
        for (int i = 1; i < smooth.Count; i++)
            cum[i] = cum[i - 1] + Vector3.Distance(smooth[i - 1], smooth[i]);
        float totalArc = cum[cum.Length - 1];
        if (totalArc < 0.1f) return;

        // ---- 3. Интервалы станций в дуговой длине ----
        // pathPoints[i+1] (центр станции i) лежит в smooth по индексу (i+1)*subdiv
        var stationArc  = new float[n];
        var stationHalf = new float[n];
        for (int i = 0; i < n; i++)
        {
            stationArc[i]  = cum[(i + 1) * subdiv];
            var st = line.stations[i];
            bool isTransfer = !string.IsNullOrEmpty(st.transferTo);
            stationHalf[i] = (isTransfer ? ctx.transferStationLength : ctx.stationLength) * 0.5f;
        }

        // ---- 4. Собираем дуговые позиции маркеров ----
        var arcs = new List<float>();
        arcs.Add(0f);
        int steps = Mathf.FloorToInt(totalArc / spacing);
        for (int i = 1; i <= steps; i++) arcs.Add(i * spacing);
        arcs.Add(totalArc);

        for (int i = 0; i < n; i++)
        {
            float a0 = stationArc[i] - stationHalf[i];
            float a1 = stationArc[i] + stationHalf[i];
            if (a0 > 0.5f)             arcs.Add(a0);
            if (a1 < totalArc - 0.5f)  arcs.Add(a1);
        }

        arcs.Sort();
        var filtered = new List<float>(arcs.Count);
        foreach (var a in arcs)
        {
            if (filtered.Count == 0 || a - filtered[filtered.Count - 1] > 0.5f)
                filtered.Add(a);
        }

        // ---- 5. Спавн маркеров + связка ----
        var markers = new List<TrainTrackMarker>(filtered.Count);
        for (int i = 0; i < filtered.Count; i++)
        {
            float arc = filtered[i];
            Vector3 pos = SamplePathByArc(smooth, cum, arc, out Vector3 fwd);
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);

            bool inStation = false;
            for (int k = 0; k < n; k++)
            {
                if (Mathf.Abs(arc - stationArc[k]) <= stationHalf[k] + 0.01f)
                { inStation = true; break; }
            }

            var go = Instantiate(markerPrefab, pos, rot, markersParent);
            go.name = $"Marker_{i:D3}";

            var marker = go.GetComponent<TrainTrackMarker>();
            if (marker == null)
            {
                Debug.LogWarning("[MetroMap] В префабе маркера нет TrainTrackMarker. Добавляю автоматически.", this);
                marker = go.AddComponent<TrainTrackMarker>();
            }

            marker.cameraMode  = inStation ? stationMarkerCameraMode : tunnelMarkerCameraMode;
            marker.isSwitchPoint = false;
            markers.Add(marker);
        }

        for (int i = 0; i < markers.Count; i++)
        {
            if (i < markers.Count - 1) markers[i].nextForward  = markers[i + 1];
            if (i > 0)                 markers[i].nextBackward = markers[i - 1];
        }

        if (logVerbose)
            Debug.Log($"[MetroMap] Line '{line.id}': {markers.Count} markers generated");
    }

    private Vector3 SamplePathByArc(List<Vector3> path, float[] cum, float arc, out Vector3 forward)
    {
        int last = cum.Length - 1;
        if (arc <= 0f)            { forward = (path[1] - path[0]).normalized;          return path[0]; }
        if (arc >= cum[last])     { forward = (path[last] - path[last - 1]).normalized; return path[last]; }

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

    // ------------------ Editor utils ------------------

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