using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
[DefaultExecutionOrder(200)] // Start выполнится ПОСЛЕ MetroMapBuilder.Start()
[AddComponentMenu("Metro/Metro Line Switcher")]
public class MetroLineSwitcher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MetroMapBuilder mapBuilder;
    [SerializeField] private GameObject trainPrefab;

    [Header("Startup")]
    [Tooltip("Если у MetroMapBuilder ещё нет построенных линий — вызвать Build() в Start.")]
    [SerializeField] private bool buildMapIfMissing = true;
    [Tooltip("С какой линии стартовать в Play Mode. Пусто — первая в списке.")]
    [SerializeField] private string startLineId = "";

    [Header("Train")]
    [Tooltip("Индекс маркера (0-based), на котором спавнить поезд. 1 = ВТОРОЙ маркер от начала.")]
    [Min(0)]
    [SerializeField] private int trainStartMarkerIndex = 1;
    [Tooltip("Спавнить поезд при каждом переключении линии.")]
    [SerializeField] private bool spawnTrainOnSwitch = true;
    [Tooltip("Уничтожать предыдущий поезд перед спавном нового.")]
    [SerializeField] private bool destroyPreviousTrain = true;
    [Tooltip("Родитель для поездов. Если пусто — создаётся автоматически 'MetroTrains'.")]
    [SerializeField] private Transform trainRoot;

    [Header("Debug")]
    [SerializeField] private bool logVerbose = true;

    // ---------------- Public state ----------------

    /// <summary>id линии, которая сейчас включена.</summary>
    public string CurrentLineId { get; private set; }

    /// <summary>Текущий заспавненный поезд (может быть null).</summary>
    public GameObject CurrentTrain { get; private set; }

    /// <summary>Маркер, на котором стоит текущий поезд (может быть null).</summary>
    public TrainTrackMarker CurrentTrainMarker { get; private set; }

    /// <summary>Все id линий, найденные в сцене (Line_&lt;id&gt;).</summary>
    public IReadOnlyList<string> LineIds => _lineIds;

    /// <summary>Ссылка на MetroMapBuilder.</summary>
    public MetroMapBuilder MapBuilder => mapBuilder;

    // ---------------- Events ----------------

    /// <summary>Вызывается после успешного переключения линии (параметр — id).</summary>
    public event Action<string> OnLineSwitched;

    /// <summary>Вызывается после спавна поезда: (поезд, стартовый маркер).</summary>
    public event Action<GameObject, TrainTrackMarker> OnTrainSpawned;

    /// <summary>Вызывается после RebuildMapAndRespawn.</summary>
    public event Action OnMapRebuilt;

    // ---------------- Internal ----------------

    private readonly List<string> _lineIds = new List<string>();
    private const string LinePrefix = "Line_";
    private const string TrainRootName = "MetroTrains";

    private void Awake()
    {
        if (mapBuilder == null) mapBuilder = GetComponent<MetroMapBuilder>();
    }

    private void Start()
    {
        if (mapBuilder == null)
        {
            Debug.LogError("[MetroLineSwitcher] MetroMapBuilder не задан и не найден на этом объекте.", this);
            return;
        }

        // К этому моменту (DefaultExecutionOrder 200) Start у MetroMapBuilder уже отработал.
        if (mapBuilder.transform.childCount == 0)
        {
            if (buildMapIfMissing) mapBuilder.Build();
            else
            {
                Debug.LogWarning("[MetroLineSwitcher] Карта не построена, а buildMapIfMissing = false.", this);
                return;
            }
        }

        RefreshLineList();
        if (_lineIds.Count == 0)
        {
            Debug.LogWarning("[MetroLineSwitcher] В сцене нет ни одной линии (Line_*).", this);
            return;
        }

        // Убираем поезда, оставшиеся от Edit Mode-сессии
        ClearAllTrains();

        string target = (!string.IsNullOrEmpty(startLineId) && _lineIds.Contains(startLineId))
            ? startLineId
            : _lineIds[0];
        SwitchToLine(target);
    }

    // ---------------- Public API ----------------

    /// <summary>Обновить список линий, прочитав дочерние объекты Line_* у MetroMapBuilder.</summary>
    public void RefreshLineList()
    {
        _lineIds.Clear();
        if (mapBuilder == null) return;
        var root = mapBuilder.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (!c.name.StartsWith(LinePrefix, StringComparison.Ordinal)) continue;
            _lineIds.Add(c.name.Substring(LinePrefix.Length));
        }
    }

    /// <summary>Снимок списка линий (для редактора и внешних вызовов).</summary>
    public List<string> GetLineIdsSnapshot() => new List<string>(_lineIds);

    /// <summary>
    /// Включить указанную линию, выключить остальные, при необходимости заспавнить поезд.
    /// Если lineId = null/пусто — включит первую доступную.
    /// </summary>
    public void SwitchToLine(string lineId)
    {
        if (string.IsNullOrEmpty(lineId))
        {
            if (_lineIds.Count == 0) RefreshLineList();
            if (_lineIds.Count == 0) { Debug.LogWarning("[MetroLineSwitcher] Нет линий."); return; }
            lineId = _lineIds[0];
        }

        if (!_lineIds.Contains(lineId))
        {
            RefreshLineList();
            if (!_lineIds.Contains(lineId))
            {
                Debug.LogError($"[MetroLineSwitcher] Линия '{lineId}' не найдена.", this);
                return;
            }
        }

        ApplyVisibility(lineId);
        CurrentLineId = lineId;

        if (spawnTrainOnSwitch)
            SpawnTrainOnLine(lineId);

        if (logVerbose)
            Debug.Log($"[MetroLineSwitcher] Активна линия '{lineId}'", this);

        OnLineSwitched?.Invoke(lineId);
    }

    /// <summary>Переключиться на следующую линию (циклически).</summary>
    public void SwitchToNextLine()
    {
        if (_lineIds.Count == 0) RefreshLineList();
        if (_lineIds.Count == 0) return;
        int idx = _lineIds.IndexOf(CurrentLineId);
        int next = (idx < 0) ? 0 : (idx + 1) % _lineIds.Count;
        SwitchToLine(_lineIds[next]);
    }

    /// <summary>Переключиться на предыдущую линию (циклически).</summary>
    public void SwitchToPreviousLine()
    {
        if (_lineIds.Count == 0) RefreshLineList();
        if (_lineIds.Count == 0) return;
        int idx = _lineIds.IndexOf(CurrentLineId);
        int prev = (idx < 0) ? _lineIds.Count - 1 : (idx - 1 + _lineIds.Count) % _lineIds.Count;
        SwitchToLine(_lineIds[prev]);
    }

    /// <summary>Перестроить карту (MetroMapBuilder.Build) и переключиться на ту же/первую линию.</summary>
    public void RebuildMapAndRespawn()
    {
        if (mapBuilder == null) { Debug.LogError("[MetroLineSwitcher] mapBuilder не задан."); return; }
        ClearAllTrains();
        mapBuilder.Build();
        RefreshLineList();
        if (_lineIds.Count == 0) return;
        string target = _lineIds.Contains(CurrentLineId) ? CurrentLineId : _lineIds[0];
        SwitchToLine(target);
        OnMapRebuilt?.Invoke();
    }

    /// <summary>
    /// Заспавнить поезд на указанной линии (или на текущей, если передать null).
    /// Возвращает инстанс поезда или null.
    /// </summary>
    public GameObject SpawnTrainOnLine(string lineId)
    {
        if (string.IsNullOrEmpty(lineId)) lineId = CurrentLineId;

        if (trainPrefab == null)
        {
            Debug.LogWarning("[MetroLineSwitcher] trainPrefab не задан.", this);
            return null;
        }
        if (string.IsNullOrEmpty(lineId))
        {
            Debug.LogWarning("[MetroLineSwitcher] Не указана линия для спавна поезда.", this);
            return null;
        }

        var lineRoot = GetLineRoot(lineId);
        if (lineRoot == null)
        {
            Debug.LogWarning($"[MetroLineSwitcher] Не найден корень 'Line_{lineId}'.", this);
            return null;
        }

        var markers = CollectMarkers(lineRoot);
        if (markers.Count == 0)
        {
            Debug.LogWarning($"[MetroLineSwitcher] На линии '{lineId}' нет маркеров.", this);
            return null;
        }

        if (destroyPreviousTrain)
        {
            if (CurrentTrain != null) DestroySafe(CurrentTrain);
            CurrentTrain = null;
            CurrentTrainMarker = null;
        }

        int idx = Mathf.Clamp(trainStartMarkerIndex, 0, markers.Count - 1);
        var marker = markers[idx];

        var root = GetTrainRoot();
        var go = Instantiate(trainPrefab, marker.transform.position, marker.transform.rotation, root);
        go.name = $"Train_{lineId}";

        CurrentTrain = go;
        CurrentTrainMarker = marker;

        if (logVerbose)
            Debug.Log($"[MetroLineSwitcher] Поезд на линии '{lineId}', маркер #{idx} ({marker.name})", go);

        OnTrainSpawned?.Invoke(go, marker);
        return go;
    }

    /// <summary>Удалить все поезда, заспавненные этим свитчером.</summary>
    public void ClearAllTrains()
    {
        if (CurrentTrain != null) DestroySafe(CurrentTrain);
        CurrentTrain = null;
        CurrentTrainMarker = null;

        Transform root = trainRoot;
        if (root == null) root = transform.Find(TrainRootName);
        if (root == null) return;

        for (int i = root.childCount - 1; i >= 0; i--)
            DestroySafe(root.GetChild(i).gameObject);
    }

    /// <summary>Получить GameObject линии Line_&lt;id&gt;.</summary>
    public GameObject GetLineRoot(string lineId)
    {
        if (mapBuilder == null || string.IsNullOrEmpty(lineId)) return null;
        var t = mapBuilder.transform.Find(LinePrefix + lineId);
        return t != null ? t.gameObject : null;
    }

    // ---------------- Internal helpers ----------------

    private void ApplyVisibility(string activeLineId)
    {
        if (mapBuilder == null) return;
        var root = mapBuilder.transform;
        string activeName = LinePrefix + activeLineId;
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i).gameObject;
            if (!c.name.StartsWith(LinePrefix, StringComparison.Ordinal)) continue;
            bool active = c.name == activeName;
            if (c.activeSelf != active) c.SetActive(active);
        }
    }

    private Transform GetTrainRoot()
    {
        if (trainRoot != null) return trainRoot;
        var existing = transform.Find(TrainRootName);
        if (existing != null) { trainRoot = existing; return trainRoot; }
        var go = new GameObject(TrainRootName);
        go.transform.SetParent(transform, false);
        trainRoot = go.transform;
        return trainRoot;
    }

    /// <summary>Собирает маркеры линии в порядке цепочки nextForward (или по имени, если связей нет).</summary>
    private List<TrainTrackMarker> CollectMarkers(GameObject lineRoot)
    {
        var result = new List<TrainTrackMarker>();
        if (lineRoot == null) return result;

        Transform markersRoot = lineRoot.transform.Find("Markers");
        if (markersRoot == null) markersRoot = lineRoot.transform.Find("MarkersReverse");
        if (markersRoot == null) return result;

        var all = markersRoot.GetComponentsInChildren<TrainTrackMarker>();
        if (all.Length == 0) return result;

        // Ищем голову связного списка (nextBackward == null) и проходим по nextForward
        TrainTrackMarker head = null;
        foreach (var m in all)
            if (m.nextBackward == null) { head = m; break; }

        if (head != null)
        {
            var cur = head;
            int guard = 0;
            while (cur != null && guard++ < 10000)
            {
                result.Add(cur);
                cur = cur.nextForward;
            }
            return result;
        }

        // Fallback — по имени
        var list = new List<TrainTrackMarker>(all);
        list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return list;
    }

    private static void DestroySafe(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
        else UnityEngine.Object.DestroyImmediate(obj);
    }

    // ---------------- Context menu ----------------

    [ContextMenu("Switch To Next Line")]     private void CtxNext()      => SwitchToNextLine();
    [ContextMenu("Switch To Previous Line")] private void CtxPrev()      => SwitchToPreviousLine();
    [ContextMenu("Rebuild Map & Respawn")]   private void CtxRebuild()   => RebuildMapAndRespawn();
    [ContextMenu("Clear All Trains")]        private void CtxClear()     => ClearAllTrains();
}


#if UNITY_EDITOR
[CustomEditor(typeof(MetroLineSwitcher))]
public class MetroLineSwitcherEditor : Editor
{
    private string _selectedLine;

    public override void OnInspectorGUI()
    {
        var sw = (MetroLineSwitcher)target;

        EditorGUILayout.HelpBox(
            "Оставляет включённой только одну линию (Line_*) и спавнит поезд на заданном маркере.\n" +
            "Работает в Edit Mode (кнопки ниже) и в Play Mode (через Start).\n" +
            "Публичное API: SwitchToLine / SwitchToNextLine / SwitchToPreviousLine /\n" +
            "SpawnTrainOnLine / ClearAllTrains / RebuildMapAndRespawn + события OnLineSwitched, OnTrainSpawned, OnMapRebuilt.",
            MessageType.Info);

        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        var lines = sw.GetLineIdsSnapshot();
        if (lines.Count == 0)
        {
            EditorGUILayout.HelpBox("Линии не найдены. Постройте карту (MetroMapBuilder).", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField("Переключение линий", EditorStyles.boldLabel);

            if (string.IsNullOrEmpty(_selectedLine) || !lines.Contains(_selectedLine))
                _selectedLine = (!string.IsNullOrEmpty(sw.CurrentLineId) && lines.Contains(sw.CurrentLineId))
                    ? sw.CurrentLineId
                    : lines[0];

            int idx = Mathf.Clamp(lines.IndexOf(_selectedLine), 0, lines.Count - 1);
            idx = EditorGUILayout.Popup("Линия", idx, lines.ToArray());
            _selectedLine = lines[idx];

            GUI.backgroundColor = new Color(0.6f, 0.85f, 1f);
            if (GUILayout.Button($"▶ Включить линию '{_selectedLine}'", GUILayout.Height(28)))
            {
                sw.SwitchToLine(_selectedLine);
                MarkDirty(sw);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("◀ Предыдущая")) { sw.SwitchToPreviousLine(); MarkDirty(sw); }
            if (GUILayout.Button("Следующая ▶")) { sw.SwitchToNextLine();     MarkDirty(sw); }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(8);

        GUI.backgroundColor = new Color(1f, 0.9f, 0.55f);
        if (GUILayout.Button("↻ Перестроить карту и переключиться", GUILayout.Height(30)))
        {
            sw.RebuildMapAndRespawn();
            MarkDirty(sw);
        }

        GUI.backgroundColor = new Color(0.65f, 1f, 0.65f);
        if (GUILayout.Button("🚂 Спавнить поезд на текущей линии"))
        {
            sw.SpawnTrainOnLine(sw.CurrentLineId);
            MarkDirty(sw);
        }

        GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
        if (GUILayout.Button("✖ Удалить все поезда"))
        {
            sw.ClearAllTrains();
            MarkDirty(sw);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("State", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("CurrentLineId",      sw.CurrentLineId ?? "—");
        EditorGUILayout.LabelField("CurrentTrain",       sw.CurrentTrain != null ? sw.CurrentTrain.name : "—");
        EditorGUILayout.LabelField("CurrentTrainMarker", sw.CurrentTrainMarker != null ? sw.CurrentTrainMarker.name : "—");
    }

    private void MarkDirty(MetroLineSwitcher sw)
    {
        if (Application.isPlaying) return;
        EditorUtility.SetDirty(sw);
        if (sw.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(sw.gameObject.scene);
    }
}
#endif

