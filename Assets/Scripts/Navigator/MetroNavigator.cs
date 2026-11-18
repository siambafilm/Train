using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Metro/Metro Navigator")]
[DisallowMultipleComponent]
[DefaultExecutionOrder(300)]
public class MetroNavigator : MonoBehaviour
{
    public static MetroNavigator Instance { get; private set; }
    public bool IsFullMapOpen => isFullOpen;

    [Header("Данные")]
    [SerializeField] private string mapResourceName = "metro_map";

    [Header("Ссылки")]
    [SerializeField] private MetroLineSwitcher lineSwitcher;
    [Tooltip("Камера игрока. Если пусто — Camera.main.")]
    [SerializeField] private Camera playerCamera;

    [Header("Цвета")]
    [SerializeField] private Color minimapBackground   = new(0.05f, 0.08f, 0.14f, 0.88f);
    [SerializeField] private Color fullMapBackground    = new(0.04f, 0.06f, 0.10f, 0.97f);
    [SerializeField] private Color backdropColor        = new(0f, 0f, 0f, 0.65f);
    [SerializeField] private Color playerTriangleColor  = new(1f, 0.85f, 0.20f, 1f);
    [SerializeField] private Color titleColor           = new(0.90f, 0.94f, 1f, 1f);

    [Header("Мини-карта")]
    [SerializeField] private Vector2 minimapSize    = new(240, 240);
    [SerializeField] private Vector2 minimapMargin  = new(18, 18);
    [SerializeField, Range(0.5f, 1f)] private float minimapFitPadding = 0.86f;
    [SerializeField] private float[] minimapZoomLevels = { 1f, 1.4f, 2f, 3f };
    [SerializeField, Range(0.2f, 0.8f)] private float minimapAnchorX = 0.5f;
    [SerializeField, Range(0.2f, 0.8f)] private float minimapAnchorY = 0.5f;

    [Header("Большая карта")]
    [SerializeField] private float fullMapPadding    = 60f;
    [SerializeField, Range(0.5f, 1f)] private float fullFitPadding = 0.86f;
    [SerializeField] private float fullZoomMin       = 0.4f;
    [SerializeField] private float fullZoomMax       = 6f;
    [SerializeField] private float fullZoomStep      = 1.15f;

    [Tooltip("Стартовый зум большой карты (множитель к fit-scale). 1 = вся карта, 2.5 = крупнее.")]
    [SerializeField] private float fullInitialZoom   = 2.5f;   // ← НОВОЕ

    [Header("Анимация / замедление")]
    [SerializeField] private float openDuration  = 0.42f;
    [SerializeField] private float closeDuration = 0.28f;
    [SerializeField, Range(0.001f, 1f)] private float timeScaleOnOpen = 0.05f;



    // ------- Состояние -------
    private MetroMapData data;
    private Canvas canvas;
    private RectTransform rootRect;

    private RectTransform miniPanel;
    private RectTransform miniMaskRect;
    private RectTransform miniContentRoot;
    private MetroMapRenderer miniRenderer;
    private RectTransform miniTriangle;
    private Image miniBgImage;
    private int miniZoomIndex;

    private RectTransform fullBackdrop;
    private Image fullBackdropImage;
    private RectTransform fullPanel;
    private RectTransform fullMaskRect;
    private RectTransform fullContentRoot;
    private MetroMapRenderer fullRenderer;
    private RectTransform fullTriangle;
    private Image fullBgImage;
    private Text fullTitle;

    private float fullZoom = 1f;
    private bool isDragging;
    private bool isFullOpen;
    private bool isTransitioning;
    private float prevTimeScale = 1f;

    private Vector2 bboxMin, bboxMax;
    private Vector2 mapCenter => (bboxMin + bboxMax) * 0.5f;

    private float miniFitScale = 1f;
    private float fullFitScale = 1f;

    private Vector2 lastMouseLocal;

    // TrainHUD save-state
    private bool _hudStateSaved;
    private bool _savedHudDotVisible;
    private bool _savedHudMenuVisible;
    private bool _savedHudHotkeyActive;

    // ------- Lifecycle -------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (lineSwitcher == null) lineSwitcher = FindObjectOfType<MetroLineSwitcher>();
        if (playerCamera  == null) playerCamera  = Camera.main;

        if (!LoadData()) return;

        ComputeBBox();
        BuildUI();
        RefreshActiveLine();
    }

    private void Update()
    {
        if (data == null) return;

        UpdatePlayerTriangle();

        // Видимость мини-карты: скрыта в паузе, при открытой большой карте и во время переходов.
        bool paused = IsGamePaused();
        bool shouldShowMini = !paused && !isFullOpen && !isTransitioning;
        if (miniPanel != null && miniPanel.gameObject.activeSelf != shouldShowMini)
            miniPanel.gameObject.SetActive(shouldShowMini);

        if (paused) return;

        if (!isFullOpen && Input.GetKeyDown(KeyCode.F5))
            CycleMiniZoom();

        if (Input.GetKeyDown(KeyCode.M) && !isTransitioning)
        {
            if (isFullOpen) StartCoroutine(AnimateClose());
            else            StartCoroutine(AnimateOpen());
        }

        if (isFullOpen && !isTransitioning)
            HandleFullInput();

        if (!isFullOpen && !isTransitioning && miniPanel.gameObject.activeSelf)
            UpdateMiniTransform();
    }

    private static bool IsGamePaused()
    {
        var p = PauseMenu.PauseMenuController.Instance;
        return p != null && p.IsPaused;
    }

    // ------- Данные -------

    private bool LoadData()
    {
        var ta = Resources.Load<TextAsset>(mapResourceName);
        if (ta == null) { Debug.LogError($"[MetroNavigator] Resources/{mapResourceName}.json не найден"); return false; }
        data = JsonUtility.FromJson<MetroMapData>(ta.text);
        if (data == null || data.lines == null || data.lines.Length == 0)
        {
            Debug.LogError("[MetroNavigator] JSON пустой или без линий"); return false;
        }
        return true;
    }

    private void ComputeBBox()
    {
        bool first = true;
        foreach (var line in data.lines)
        {
            if (line.stations == null) continue;
            foreach (var st in line.stations)
            {
                Vector2 p = new(st.pos[0], st.pos[1]);
                if (first) { bboxMin = bboxMax = p; first = false; }
                else
                {
                    bboxMin = Vector2.Min(bboxMin, p);
                    bboxMax = Vector2.Max(bboxMax, p);
                }
            }
        }
        if (first) { bboxMin = new(-50, -50); bboxMax = new(50, 50); }
        if (bboxMax.x - bboxMin.x < 1f) bboxMax.x = bboxMin.x + 1f;
        if (bboxMax.y - bboxMin.y < 1f) bboxMax.y = bboxMin.y + 1f;
    }

    // ------- Построение UI -------

    private void BuildUI()
    {
        var canvasGO = new GameObject("MetroNavigatorCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        rootRect = canvasGO.GetComponent<RectTransform>();

        BuildMini();
        BuildFull();
    }

    private void BuildMini()
    {
        miniPanel = NewRect("MiniMapPanel", rootRect);
        miniPanel.anchorMin = miniPanel.anchorMax = new Vector2(1, 1);
        miniPanel.pivot = new Vector2(1, 1);
        miniPanel.sizeDelta = minimapSize;
        miniPanel.anchoredPosition = new Vector2(-minimapMargin.x, -minimapMargin.y);

        miniBgImage = NewImage("Background", miniPanel, minimapBackground);
        StretchFull(miniBgImage.rectTransform);

        miniMaskRect = NewRect("Mask", miniPanel);
        StretchFull(miniMaskRect, 4f);
        miniMaskRect.gameObject.AddComponent<RectMask2D>();

        miniContentRoot = NewRect("Content", miniMaskRect);
        miniContentRoot.anchorMin = miniContentRoot.anchorMax = new Vector2(0.5f, 0.5f);
        miniContentRoot.pivot = new Vector2(0.5f, 0.5f);
        miniContentRoot.sizeDelta = Vector2.zero;

        miniRenderer = BuildRenderer("MapRenderer", miniContentRoot);
        miniTriangle = BuildTriangle("PlayerTriangle", miniContentRoot);
    }

    private void BuildFull()
    {
        fullBackdrop = NewRect("FullBackdrop", rootRect);
        StretchFull(fullBackdrop);
        fullBackdropImage = fullBackdrop.gameObject.AddComponent<Image>();
        fullBackdropImage.color = new Color(backdropColor.r, backdropColor.g, backdropColor.b, 0f);
        fullBackdropImage.raycastTarget = true;
        fullBackdrop.gameObject.SetActive(false);

        fullPanel = NewRect("FullMapPanel", rootRect);
        fullPanel.anchorMin = fullPanel.anchorMax = new Vector2(0.5f, 0.5f);
        fullPanel.pivot = new Vector2(0.5f, 0.5f);
        fullPanel.sizeDelta = minimapSize;
        fullPanel.anchoredPosition = Vector2.zero;
        fullPanel.gameObject.SetActive(false);

        fullBgImage = NewImage("Background", fullPanel, fullMapBackground);
        StretchFull(fullBgImage.rectTransform);

        fullMaskRect = NewRect("Mask", fullPanel);
        StretchFull(fullMaskRect, 4f);
        fullMaskRect.gameObject.AddComponent<RectMask2D>();

        fullContentRoot = NewRect("Content", fullMaskRect);
        fullContentRoot.anchorMin = fullContentRoot.anchorMax = new Vector2(0.5f, 0.5f);
        fullContentRoot.pivot = new Vector2(0.5f, 0.5f);
        fullContentRoot.sizeDelta = Vector2.zero;

        fullRenderer = BuildRenderer("MapRenderer", fullContentRoot);
        fullTriangle = BuildTriangle("PlayerTriangle", fullContentRoot);

        var titleGO = new GameObject("Title", typeof(RectTransform));
        var titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.SetParent(fullPanel, false);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0, 60);
        titleRect.anchoredPosition = new Vector2(0, -8);
        fullTitle = titleGO.AddComponent<Text>();
        fullTitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        fullTitle.fontSize = 26;
        fullTitle.fontStyle = FontStyle.Bold;
        fullTitle.color = titleColor;
        fullTitle.alignment = TextAnchor.MiddleCenter;
        fullTitle.text = "";
    }

    private RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    private Image NewImage(string name, Transform parent, Color c)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    private void StretchFull(RectTransform rt, float padding = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    private MetroMapRenderer BuildRenderer(string name, Transform parent)
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        // ОГРОМНЫЙ rect — Unity-овский куллинг Graphic-а идёт по rect, а не по вершинам.
        // Маленький rect (10×10) выпадал из вьюпорта при зуме/пане → весь меш переставал рисоваться.
        rt.sizeDelta = new Vector2(100000, 100000);
        rt.anchoredPosition = Vector2.zero;

        if (rt.gameObject.GetComponent<CanvasRenderer>() == null)
            rt.gameObject.AddComponent<CanvasRenderer>();

        var r = rt.gameObject.AddComponent<MetroMapRenderer>();
        r.raycastTarget = false;
        r.data = data;
        r.RefreshColors();
        return r;
    }

    private RectTransform BuildTriangle(string name, Transform parent)
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(16, 16);
        rt.anchoredPosition = Vector2.zero;

        var img = rt.gameObject.AddComponent<Image>();
        img.color = playerTriangleColor;
        img.raycastTarget = false;
        img.sprite = CreateTriangleSprite();
        return rt;
    }

    private static Sprite CreateTriangleSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];
        Vector2 c = new(size / 2f, size / 2f);
        float r = size * 0.45f;
        Vector2 p1 = c + new Vector2(0, r);
        Vector2 p2 = c + new Vector2(-r * 0.866f, -r * 0.5f);
        Vector2 p3 = c + new Vector2( r * 0.866f, -r * 0.5f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new(x + 0.5f, y + 0.5f);
            bool inside = PointInTri(p, p1, p2, p3);
            pixels[y * size + x] = inside
                ? new Color32(255, 255, 255, 255)
                : new Color32(0, 0, 0, 0);
        }
        tex.SetPixels32(pixels); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float s = (a.x - c.x) * (p.y - c.y) - (a.y - c.y) * (p.x - c.x);
        float t = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        if ((s < 0) != (t < 0) && s != 0 && t != 0) return false;
        float d = (c.x - b.x) * (p.y - b.y) - (c.y - b.y) * (p.x - b.x);
        return d == 0 || (d < 0) == (s + t <= 0);
    }

    // ------- Синхронизация с активной линией -------

    private void RefreshActiveLine()
    {
        string id = lineSwitcher != null ? lineSwitcher.CurrentLineId : null;
        if (string.IsNullOrEmpty(id) && data.lines.Length > 0) id = data.lines[0].id;

        miniRenderer.activeLineId = id;
        miniRenderer.SetVerticesDirty();
        fullRenderer.activeLineId = id;
        fullRenderer.SetVerticesDirty();

        foreach (var l in data.lines)
            if (l.id == id) { fullTitle.text = string.IsNullOrEmpty(l.name) ? l.id : l.name; break; }

        miniFitScale = ComputeFitScale(minimapSize.x * minimapFitPadding, minimapSize.y * minimapFitPadding);
        fullFitScale = ComputeFitScale(GetFullFrameWidth() * fullFitPadding, GetFullFrameHeight() * fullFitPadding);

        if (!isFullOpen && !isTransitioning) UpdateMiniTransform();
    }

    private float ComputeFitScale(float w, float h)
    {
        float bw = Mathf.Max(1f, bboxMax.x - bboxMin.x);
        float bh = Mathf.Max(1f, bboxMax.y - bboxMin.y);
        return Mathf.Min(w / bw, h / bh);
    }

    private float GetFullFrameWidth()  => rootRect.rect.width  - fullMapPadding * 2f;
    private float GetFullFrameHeight() => rootRect.rect.height - fullMapPadding * 2f;

    // ------- Мини-карта: трансформ -------

    private void CycleMiniZoom()
    {
        if (minimapZoomLevels == null || minimapZoomLevels.Length == 0) return;
        miniZoomIndex = (miniZoomIndex + 1) % minimapZoomLevels.Length;
        UpdateMiniTransform();
    }

    private void UpdateMiniTransform()
    {
        if (miniContentRoot == null) return;
        float scale = miniFitScale * minimapZoomLevels[miniZoomIndex];

        // Навигаторский режим: карта вращается так, чтобы треугольник игрока смотрел вверх.
        float angle = GetPlayerAngle();
        Quaternion rot = Quaternion.Euler(0, 0, -angle);

        miniContentRoot.localRotation = rot;
        miniContentRoot.localScale = Vector3.one * scale;

        Vector2 maskSize = miniMaskRect.rect.size;
        Vector2 desired = new(
            (minimapAnchorX - 0.5f) * maskSize.x,
            (minimapAnchorY - 0.5f) * maskSize.y);

        Vector2 ps = GetPlayerSchematicPosition();
        Vector3 rotatedPs = rot * new Vector3(ps.x * scale, ps.y * scale, 0f);
        Vector2 contentPos = desired - new Vector2(rotatedPs.x, rotatedPs.y);
        miniContentRoot.localPosition = contentPos;
    }

    // ------- Получение позиции / угла игрока -------

    private Vector2 GetPlayerSchematicPosition()
    {
        if (!TryGetPlayerWorld(out Vector3 world)) return Vector2.zero;
        float s = (data.settings != null && data.settings.schematicScale > 0.0001f)
            ? data.settings.schematicScale : 0.05f;
        Vector2 raw = new(world.x / s, -world.z / s);
        return SnapToActiveLine(raw);
    }

    private bool TryGetPlayerWorld(out Vector3 world)
    {
        if (lineSwitcher != null && lineSwitcher.CurrentTrain != null)
        {
            world = lineSwitcher.CurrentTrain.transform.position;
            return true;
        }
        if (playerCamera != null)
        {
            world = playerCamera.transform.position;
            return true;
        }
        world = Vector3.zero;
        return false;
    }

    private Vector3 GetPlayerWorldForward()
    {
        if (lineSwitcher != null && lineSwitcher.CurrentTrain != null)
            return lineSwitcher.CurrentTrain.transform.forward;
        if (playerCamera != null)
            return playerCamera.transform.forward;
        return Vector3.forward;
    }

    private float GetPlayerAngle()
    {
        Vector3 fwdWorld = GetPlayerWorldForward();
        Vector2 fwd = new(fwdWorld.x, -fwdWorld.z);
        if (fwd.sqrMagnitude < 0.0001f) return 0f;
        return Mathf.Atan2(fwd.y, fwd.x) * Mathf.Rad2Deg - 90f;
    }

    private Vector2 SnapToActiveLine(Vector2 p)
    {
        if (lineSwitcher == null || string.IsNullOrEmpty(lineSwitcher.CurrentLineId))
            return p;

        LineData line = null;
        foreach (var l in data.lines) if (l.id == lineSwitcher.CurrentLineId) { line = l; break; }
        if (line == null || line.stations == null || line.stations.Length < 2) return p;

        Vector2 best = p;
        float bestD = float.MaxValue;
        for (int i = 0; i < line.stations.Length - 1; i++)
        {
            Vector2 a = new(line.stations[i].pos[0],   line.stations[i].pos[1]);
            Vector2 b = new(line.stations[i+1].pos[0], line.stations[i+1].pos[1]);
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            Vector2 proj = a + ab * t;
            float d = (p - proj).sqrMagnitude;
            if (d < bestD) { bestD = d; best = proj; }
        }
        return best;
    }

    private void UpdatePlayerTriangle()
    {
        Vector2 ps = GetPlayerSchematicPosition();
        float angle = GetPlayerAngle();

        // Треугольник всегда локально повёрнут на angle.
        // На мини-карте Content повёрнут на -angle → на экране треугольник смотрит вверх.
        // На большой карте Content не повёрнут → треугольник честно вращается.
        if (miniTriangle != null)
        {
            miniTriangle.anchoredPosition = ps;
            miniTriangle.localRotation = Quaternion.Euler(0, 0, angle);
        }
        if (fullTriangle != null)
        {
            fullTriangle.anchoredPosition = ps;
            fullTriangle.localRotation = Quaternion.Euler(0, 0, angle);
        }
    }

    // ------- Большая карта: ввод -------

    private void HandleFullInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                fullMaskRect, Input.mousePosition, null, out lastMouseLocal);
        }
        if (Input.GetMouseButtonUp(0)) isDragging = false;

        if (isDragging)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                fullMaskRect, Input.mousePosition, null, out Vector2 cur))
            {
                Vector2 delta = cur - lastMouseLocal;
                fullContentRoot.localPosition += (Vector3)delta;
                lastMouseLocal = cur;
            }
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                fullMaskRect, Input.mousePosition, null, out Vector2 mouseLocal))
            {
                float oldScale = fullContentRoot.localScale.x;
                Vector2 before = (mouseLocal - (Vector2)fullContentRoot.localPosition) / oldScale;

                fullZoom = Mathf.Clamp(
                    fullZoom * (scroll > 0 ? fullZoomStep : 1f / fullZoomStep),
                    fullZoomMin, fullZoomMax);

                float newScale = fullFitScale * fullZoom;
                fullContentRoot.localScale = Vector3.one * newScale;
                fullContentRoot.localPosition = (Vector3)(mouseLocal - before * newScale);
            }
        }
    }

    // ------- Анимации открытия/закрытия -------

    private IEnumerator AnimateOpen()
    {
        isTransitioning = true;
        isFullOpen = true;

        prevTimeScale = Time.timeScale;
        Time.timeScale = timeScaleOnOpen;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        HideOtherUI();

        Vector2 miniCenterLocal = GetMiniCenterInRoot();
        Vector2 miniSize = miniPanel.rect.size;

        miniPanel.gameObject.SetActive(false);
        fullBackdrop.gameObject.SetActive(true);
        fullPanel.gameObject.SetActive(true);

        fullPanel.anchoredPosition = miniCenterLocal;
        fullPanel.sizeDelta = miniSize;

        Vector2 endSize = new(GetFullFrameWidth(), GetFullFrameHeight());
        Vector2 endPos  = Vector2.zero;

        float startScale = miniFitScale * minimapZoomLevels[Mathf.Clamp(miniZoomIndex, 0, minimapZoomLevels.Length - 1)];
        
        // НОВОЕ: финальный зум = fit-scale * fullInitialZoom
        fullZoom = Mathf.Clamp(fullInitialZoom, fullZoomMin, fullZoomMax);
        float endScale = fullFitScale * fullZoom;

        float startAngle = GetPlayerAngle();
        Quaternion startRot = Quaternion.Euler(0, 0, -startAngle);
        Quaternion endRot   = Quaternion.identity;

        Vector2 psAtStart = GetPlayerSchematicPosition();
        Vector3 rPsStart = startRot * new Vector3(psAtStart.x * startScale, psAtStart.y * startScale, 0f);
        Vector2 startContentPos = -new Vector2(rPsStart.x, rPsStart.y);
        
        // НОВОЕ: финальный центр — не весь mapCenter, а игрок (как в навигаторе после открытия)
        // Если хотите по центру карты — оставьте -mapCenter * endScale
        Vector3 rPsEnd = endRot * new Vector3(psAtStart.x * endScale, psAtStart.y * endScale, 0f);
        Vector2 endContentPos = -new Vector2(rPsEnd.x, rPsEnd.y);

        fullZoom = 1f;
        fullContentRoot.localScale = Vector3.one * startScale;
        fullContentRoot.localRotation = startRot;
        fullContentRoot.localPosition = startContentPos;

        float t = 0;
        while (t < 1)
        {
            t += Time.unscaledDeltaTime / openDuration;
            float e = EaseOutCubic(Mathf.Clamp01(t));

            fullPanel.sizeDelta = Vector2.Lerp(miniSize, endSize, e);
            fullPanel.anchoredPosition = Vector2.Lerp(miniCenterLocal, endPos, e);

            fullBackdropImage.color = new Color(
                backdropColor.r, backdropColor.g, backdropColor.b, backdropColor.a * e);
            fullBgImage.color = Color.Lerp(minimapBackground, fullMapBackground, e);

            float sc = Mathf.Lerp(startScale, endScale, e);
            fullContentRoot.localScale = Vector3.one * sc;
            fullContentRoot.localRotation = Quaternion.Slerp(startRot, endRot, e);
            fullContentRoot.localPosition = Vector2.Lerp(startContentPos, endContentPos, e);

            yield return null;
        }

        fullPanel.sizeDelta = endSize;
        fullPanel.anchoredPosition = endPos;
        fullBackdropImage.color = backdropColor;
        fullBgImage.color = fullMapBackground;
        fullContentRoot.localScale = Vector3.one * endScale;
        fullContentRoot.localRotation = Quaternion.identity;
        fullContentRoot.localPosition = endContentPos;

        isTransitioning = false;
    }

    private IEnumerator AnimateClose()
    {
        isTransitioning = true;

        Vector2 miniCenterLocal = GetMiniCenterInRoot();
        Vector2 miniSize = minimapSize;

        Vector2 startSize = fullPanel.sizeDelta;
        Vector2 startPos  = fullPanel.anchoredPosition;

        float startScale = fullContentRoot.localScale.x;
        float endScale   = miniFitScale * minimapZoomLevels[Mathf.Clamp(miniZoomIndex, 0, minimapZoomLevels.Length - 1)];

        Quaternion startRot = fullContentRoot.localRotation;
        float endAngle = GetPlayerAngle();
        Quaternion endRot = Quaternion.Euler(0, 0, -endAngle);

        Vector2 startContentPos = fullContentRoot.localPosition;
        Vector2 psAtEnd = GetPlayerSchematicPosition();
        Vector3 rPsEnd = endRot * new Vector3(psAtEnd.x * endScale, psAtEnd.y * endScale, 0f);
        Vector2 endContentPos = -new Vector2(rPsEnd.x, rPsEnd.y);

        Color startBackdrop = fullBackdropImage.color;
        Color startBg       = fullBgImage.color;

        float t = 0;
        while (t < 1)
        {
            t += Time.unscaledDeltaTime / closeDuration;
            float e = EaseInCubic(Mathf.Clamp01(t));

            fullPanel.sizeDelta = Vector2.Lerp(startSize, miniSize, e);
            fullPanel.anchoredPosition = Vector2.Lerp(startPos, miniCenterLocal, e);

            fullBackdropImage.color = new Color(startBackdrop.r, startBackdrop.g, startBackdrop.b,
                                                startBackdrop.a * (1f - e));
            fullBgImage.color = Color.Lerp(startBg, minimapBackground, e);

            float sc = Mathf.Lerp(startScale, endScale, e);
            fullContentRoot.localScale = Vector3.one * sc;
            fullContentRoot.localRotation = Quaternion.Slerp(startRot, endRot, e);
            fullContentRoot.localPosition = Vector2.Lerp(startContentPos, endContentPos, e);

            yield return null;
        }

        fullBackdrop.gameObject.SetActive(false);
        fullPanel.gameObject.SetActive(false);
        miniPanel.gameObject.SetActive(true);

        Time.timeScale = prevTimeScale;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        isFullOpen = false;
        isTransitioning = false;
        UpdateMiniTransform();
        RestoreOtherUI();
    }

    private Vector2 GetMiniCenterInRoot()
    {
        Vector3[] corners = new Vector3[4];
        miniPanel.GetWorldCorners(corners);
        Vector2 centerWorld = (corners[0] + corners[2]) * 0.5f;
        Vector2 centerScreen = RectTransformUtility.WorldToScreenPoint(null, centerWorld);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootRect, centerScreen, null, out Vector2 local);
        return local;
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private static float EaseInCubic(float t)  => t * t * t;

    // ------- Скрытие других UI (по образцу PauseMenuController) -------

    private void HideOtherUI()
    {
        var hud = TrainHUD.Instance;
        if (hud == null) return;

        _savedHudDotVisible   = hud.IsDotVisible;
        _savedHudMenuVisible  = hud.IsMenuVisible;
        _savedHudHotkeyActive = hud.HotkeyEnabled;
        _hudStateSaved        = true;

        hud.SetHotkeyActive(false);
        hud.HideAll();
    }

    private void RestoreOtherUI()
    {
        if (!_hudStateSaved) return;
        _hudStateSaved = false;

        var hud = TrainHUD.Instance;
        if (hud == null) return;

        hud.SetDotVisible(_savedHudDotVisible);
        hud.SetMenuVisible(_savedHudMenuVisible);
        hud.SetHotkeyActive(_savedHudHotkeyActive);
    }
}