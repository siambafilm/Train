#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class MetroLineBuilder : MonoBehaviour
{
    [System.Serializable]
    public class Station
    {
        [Tooltip("Название станции")]
        public string name = "Станция";

        [Tooltip("Длина станции вдоль пути")]
        [Min(0.1f)]
        public float length = 20f;

        [Tooltip("Расстояние от конца этой станции до начала следующей")]
        [Min(0f)]
        public float distanceToNext = 100f;

        [Tooltip("Поворот (в градусах) на участке до следующей станции.\n0 — прямой тоннель")]
        [Range(-180f, 180f)]
        public float turnAngleToNext = 0f;
    }

    [Header("Префабы")]
    [Tooltip("Префаб сегмента тоннеля. Pivot — в начале, длина вдоль +Z")]
    public GameObject tunnelSegmentPrefab;

    [Tooltip("Префаб станции. Pivot — в начале, длина вдоль +Z")]
    public GameObject stationPrefab;

    [Header("Параметры")]
    [Tooltip("Длина одного сегмента тоннеля")]
    [Min(0.1f)]
    public float segmentLength = 5f;

    [Tooltip("Опорная точка старта")]
    public Transform startPoint;

    [Tooltip("Смещение старта относительно transform")]
    public Vector3 startPosition = Vector3.zero;

    [Tooltip("Начальный поворот")]
    public Vector3 startEulerAngles = Vector3.zero;

    [Tooltip("Масштабировать сегменты по Z, чтобы точно стыковались")]
    public bool scaleSegmentsToFit = true;

    [Tooltip("Строить линию автоматически при старте игры")]
    public bool buildOnStart = false;

    [Header("Маркеры пути")]
    [Tooltip("Генерировать маркеры пути вдоль ветки")]
    public bool generateMarkers = true;

    [Tooltip("Префаб маркера — пустой объект с компонентом TrainTrackMarker")]
    public GameObject markerPrefab;

    [Tooltip("Расстояние между маркерами вдоль пути")]
    [Min(0.5f)]
    public float markerSpacing = 20f;

    [Tooltip("Режим камеры для маркеров ВНУТРИ станции")]
    public MarkerCameraMode stationMarkerCameraMode = MarkerCameraMode.Both;

    [Tooltip("Режим камеры для маркеров на перегонах (тоннель)")]
    public MarkerCameraMode tunnelMarkerCameraMode = MarkerCameraMode.Both;

    [Header("Станции (по порядку)")]
    public List<Station> stations = new List<Station>();

    [SerializeField, HideInInspector]
    private Transform generatedRoot;

    private const string RootName = "MetroLine_Generated";
    private const string MarkerRootName = "Markers";

    // Вспомогательная структура для отложенного создания маркеров
    private class MarkerPoint
    {
        public Vector3 pos;
        public Quaternion rot;
        public bool isStation;
    }

    private void Start()
    {
        if (buildOnStart) Build();
    }

    [ContextMenu("Build")]
    public void Build()
    {
        Clear();

        if (tunnelSegmentPrefab == null || stationPrefab == null)
        {
            Debug.LogError("[MetroLineBuilder] Не заданы префабы сегмента и/или станции", this);
            return;
        }
        if (stations == null || stations.Count == 0)
        {
            Debug.LogWarning("[MetroLineBuilder] Список станций пуст", this);
            return;
        }

        bool markersEnabled = generateMarkers && markerPrefab != null;
        if (generateMarkers && markerPrefab == null)
            Debug.LogWarning("[MetroLineBuilder] generateMarkers = true, но markerPrefab не задан", this);

        var rootGO = new GameObject(RootName);
        rootGO.transform.SetParent(transform, false);
        generatedRoot = rootGO.transform;

        Vector3 pos = startPoint != null
            ? startPoint.position
            : transform.TransformPoint(startPosition);
        Quaternion rot = startPoint != null
            ? startPoint.rotation
            : transform.rotation * Quaternion.Euler(startEulerAngles);

        var markerPoints = new List<MarkerPoint>();
        float distSinceMarker = 0f;

        for (int i = 0; i < stations.Count; i++)
        {
            var st = stations[i];

            // --- Станция ---
            var stObj = Instantiate(stationPrefab, pos, rot, generatedRoot);
            stObj.name = $"Station_{i:D2}_{st.name}";

            // Проходим станцию — маркеры форсируются на входе и выходе
            WalkStraight(ref pos, ref rot, st.length, markerPoints, ref distSinceMarker,
                forceStartMarker: true, forceEndMarker: true, isStation: true);

            if (i >= stations.Count - 1) continue;

            // --- Тоннель до следующей станции ---
            float dist = st.distanceToNext;
            if (dist <= 0.0001f) continue;

            int segCount = Mathf.Max(1, Mathf.RoundToInt(dist / segmentLength));
            float actualSegLen = dist / segCount;
            float segTurn = st.turnAngleToNext / segCount;
            float scaleZ = scaleSegmentsToFit ? (actualSegLen / segmentLength) : 1f;

            for (int s = 0; s < segCount; s++)
            {
                var segObj = Instantiate(tunnelSegmentPrefab, pos, rot, generatedRoot);
                segObj.name = $"Segment_{i:D2}_{s:D3}";

                if (scaleSegmentsToFit)
                {
                    var sc = segObj.transform.localScale;
                    sc.z *= scaleZ;
                    segObj.transform.localScale = sc;
                }

                WalkStraight(ref pos, ref rot, actualSegLen, markerPoints, ref distSinceMarker,
                    forceStartMarker: false, forceEndMarker: false, isStation: false);

                rot *= Quaternion.Euler(0f, segTurn, 0f);
            }
        }

        // --- Создаём пустой объект с маркерами ---
        if (markersEnabled && markerPoints.Count > 0)
            BuildMarkers(markerPoints);

        Debug.Log($"[MetroLineBuilder] Построено: {stations.Count} станций, {markerPoints.Count} маркеров", this);
    }

    /// <summary>
    /// Проходит прямолинейный участок пути, расставляя маркеры с шагом markerSpacing.
    /// </summary>
    private void WalkStraight(ref Vector3 pos, ref Quaternion rot, float distance,
        List<MarkerPoint> markerPoints, ref float distSinceMarker,
        bool forceStartMarker, bool forceEndMarker, bool isStation)
    {
        bool markersEnabled = generateMarkers && markerPrefab != null;
        float spacing = Mathf.Max(0.5f, markerSpacing);

        if (markersEnabled && forceStartMarker)
        {
            AddMarker(markerPoints, pos, rot, isStation);
            distSinceMarker = 0f;
        }

        float remaining = distance;

        if (markersEnabled)
        {
            // Размещаем маркеры каждые `spacing` единиц внутри участка
            while (distSinceMarker + remaining >= spacing)
            {
                float need = spacing - distSinceMarker;
                pos += rot * Vector3.forward * need;
                remaining -= need;
                distSinceMarker = 0f;
                AddMarker(markerPoints, pos, rot, isStation);
            }
            distSinceMarker += remaining;
        }
        pos += rot * Vector3.forward * remaining;

        if (markersEnabled && forceEndMarker)
        {
            AddMarker(markerPoints, pos, rot, isStation);
            distSinceMarker = 0f;
        }
    }

    private void AddMarker(List<MarkerPoint> pts, Vector3 pos, Quaternion rot, bool isStation)
    {
        // Дедупликация: не ставим маркер поверх предыдущего
        if (pts.Count > 0)
        {
            var last = pts[pts.Count - 1];
            if (Vector3.Distance(last.pos, pos) < 0.05f) return;
        }
        pts.Add(new MarkerPoint { pos = pos, rot = rot, isStation = isStation });
    }

    private void BuildMarkers(List<MarkerPoint> points)
    {
        // Пустой объект-контейнер для маркеров
        var markerRootGO = new GameObject(MarkerRootName);
        markerRootGO.transform.SetParent(generatedRoot, false);

        var markers = new List<TrainTrackMarker>(points.Count);

        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var go = Instantiate(markerPrefab, p.pos, p.rot, markerRootGO.transform);
            go.name = $"Marker_{i:D3}";

            var marker = go.GetComponent<TrainTrackMarker>();
            if (marker == null)
            {
                Debug.LogWarning($"[MetroLineBuilder] В префабе маркера нет TrainTrackMarker. Добавляю автоматически.", this);
                marker = go.AddComponent<TrainTrackMarker>();
            }

            marker.cameraMode = p.isStation ? stationMarkerCameraMode : tunnelMarkerCameraMode;
            marker.isSwitchPoint = false;

            markers.Add(marker);
        }

        // Связываем маркеры в цепочку
        for (int i = 0; i < markers.Count; i++)
        {
            if (i < markers.Count - 1)
                markers[i].nextForward = markers[i + 1];
            if (i > 0)
                markers[i].nextBackward = markers[i - 1];
        }
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        if (generatedRoot == null)
        {
            var found = transform.Find(RootName);
            if (found != null) generatedRoot = found;
        }
        if (generatedRoot == null) return;

        var go = generatedRoot.gameObject;
        generatedRoot = null;

        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    private void OnDrawGizmos()
    {
        if (stations == null || stations.Count == 0) return;

        Vector3 pos = startPoint != null
            ? startPoint.position
            : transform.TransformPoint(startPosition);
        Quaternion rot = startPoint != null
            ? startPoint.rotation
            : transform.rotation * Quaternion.Euler(startEulerAngles);

        for (int i = 0; i < stations.Count; i++)
        {
            var st = stations[i];

            Gizmos.color = new Color(0.25f, 0.75f, 1f, 0.6f);
            Vector3 center = pos + rot * Vector3.forward * (st.length * 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(center, rot, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(4f, 4f, st.length));
            Gizmos.matrix = Matrix4x4.identity;

            pos += rot * Vector3.forward * st.length;

            if (i >= stations.Count - 1) continue;
            float dist = st.distanceToNext;
            if (dist <= 0.0001f) continue;

            int segCount = Mathf.Max(1, Mathf.RoundToInt(dist / segmentLength));
            float actualSegLen = dist / segCount;
            float segTurn = st.turnAngleToNext / segCount;

            Gizmos.color = Color.white;
            for (int s = 0; s < segCount; s++)
            {
                Vector3 next = pos + rot * Vector3.forward * actualSegLen;
                Gizmos.DrawLine(pos, next);
                pos = next;
                rot *= Quaternion.Euler(0f, segTurn, 0f);
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(MetroLineBuilder))]
public class MetroLineBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var builder = (MetroLineBuilder)target;

        EditorGUILayout.HelpBox(
            "Pivot префабов — в начале, длина вдоль +Z.\n" +
            "Станции строятся по порядку; между ними прокладывается тоннель.\n" +
            "Маркеры пути создаются в отдельном объекте «Markers» и связываются цепочкой.",
            MessageType.Info);

        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button("▶ Построить линию и маркеры", GUILayout.Height(32)))
        {
            builder.Build();
            MarkDirty(builder);
        }

        GUI.backgroundColor = new Color(1f, 0.75f, 0.75f);
        if (GUILayout.Button("✖ Очистить", GUILayout.Height(22)))
        {
            builder.Clear();
            MarkDirty(builder);
        }

        GUI.backgroundColor = Color.white;
    }

    private void MarkDirty(MetroLineBuilder builder)
    {
        if (Application.isPlaying) return;
        EditorUtility.SetDirty(builder);
        if (builder.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
    }
}
#endif