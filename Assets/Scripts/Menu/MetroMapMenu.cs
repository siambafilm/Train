// Assets/Scripts/Menu/MetroMapMenu.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public static class MetroSession
{
    public static string SelectedLineId;
    public static string SelectedLineName;
    public static string SelectedLineColorKey;
}

public class MetroMapMenu : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string mapResourcePath = "metro_map";

    [Header("Line Style")]
    [SerializeField] private float lineWidth = 11f;
    [SerializeField] private float glowInnerMult = 2.1f;
    [SerializeField] private float glowOuterMult = 4.0f;

    [Header("Stations")]
    [SerializeField] private float stationSize = 20f;
    [SerializeField] private float transferStationSize = 30f;

    [Header("Interaction")]
    [SerializeField] private float hoverRadius = 45f;

    [Header("Colors")]
    [SerializeField] private Color bgInner = new Color(0.09f, 0.12f, 0.20f);
    [SerializeField] private Color bgOuter = new Color(0.02f, 0.03f, 0.06f);
    [SerializeField] private Color titleColor = new Color(0.93f, 0.95f, 1f);
    [SerializeField] private Color subtitleColor = new Color(0.55f, 0.62f, 0.80f);
    [SerializeField] private Color hintColor = new Color(0.55f, 0.62f, 0.80f);

    [Header("Animation")]
    [SerializeField] private float hoverLerp = 9f;
    [SerializeField] private float pulseSpeed = 3.5f;
    [SerializeField] private float pulseAmount = 0.12f;

    private MetroMapData data;
    private Canvas canvas;
    private CanvasGroup mapGroup;
    private CanvasGroup titleGroup;
    private RectTransform mapRoot;
    private RectTransform linesRoot;
    private RectTransform stationsRoot;
    private Text titleText;
    private Text subtitleText;
    private Text hintText;
    private Image fadeOverlay;

    private readonly List<LineVisual> lines = new List<LineVisual>();
    private LineVisual hovered;
    private Vector2 mapCenter;
    private float mapScale = 1f;
    private bool transitioning = false;
    private float hoverTime = 0f;

    private Sprite circleSprite;
    private Sprite ringSprite;
    private Sprite whiteSprite;

    private class LineVisual
    {
        public LineData data;
        public Color color;
        public RectTransform root;
        public readonly List<SegmentVis> segments = new List<SegmentVis>();
        public readonly List<Graphic> lineGraphics = new List<Graphic>();
        public readonly List<GlowGraphic> glowGraphics = new List<GlowGraphic>();
        public float currentAlpha = 0f;
        public float currentGlow = 0f;
    }

    private class SegmentVis
    {
        public Vector2 a;
        public Vector2 b;
    }

    private class GlowGraphic
    {
        public Graphic graphic;
        public bool outer;
    }

    // =====================================================================
    private void Start()
    {
        EnsureEventSystem();
        LoadData();
        GenerateSprites();
        BuildUI();
        BuildMap();
        Canvas.ForceUpdateCanvases();
        LayoutMap();
        StartCoroutine(IntroRoutine());
    }

    private void Update()
    {
        if (transitioning) return;
        hoverTime += Time.deltaTime;
        HandleHoverAndClick();
        AnimateLines();
    }

    // =====================================================================
    private void EnsureEventSystem()
    {
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    private void LoadData()
    {
        var ta = Resources.Load<TextAsset>(mapResourcePath);
        if (ta == null) { Debug.LogError($"[MetroMapMenu] Resources/{mapResourcePath}.json не найден"); return; }
        data = JsonUtility.FromJson<MetroMapData>(ta.text);
        if (data == null || data.lines == null) Debug.LogError("[MetroMapMenu] JSON не содержит lines");
    }

    // =====================================================================
    private void GenerateSprites()
    {
        circleSprite = CreateCircleSprite(128);
        ringSprite   = CreateRingSprite(128, 14f);
        whiteSprite  = CreateSolidSprite();
    }

    private Sprite CreateSolidSprite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    private Sprite CreateCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;
        float c = size / 2f; float radius = c - 1f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float dx = x + 0.5f - c, dy = y + 0.5f - c;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(radius - d + 0.5f);
            px[y * size + x] = new Color(1, 1, 1, a);
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private Sprite CreateRingSprite(int size, float width)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;
        float c = size / 2f; float outer = c - 1f, inner = outer - width;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float dx = x + 0.5f - c, dy = y + 0.5f - c;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float aOut = Mathf.Clamp01(outer - d + 0.5f);
            float aIn  = Mathf.Clamp01(d - inner + 0.5f);
            px[y * size + x] = new Color(1, 1, 1, Mathf.Min(aOut, aIn));
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private Sprite CreateRadialGradient(int size, Color inner, Color outer)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;
        float c = size / 2f, maxD = c;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float dx = x + 0.5f - c, dy = y + 0.5f - c;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / maxD;
            px[y * size + x] = Color.Lerp(inner, outer, Mathf.Clamp01(d));
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // =====================================================================
    private void BuildUI()
    {
        var canvasGO = new GameObject("MetroMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var canvasRT = canvasGO.GetComponent<RectTransform>();

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var bg = MakeImage("Background", canvasRT, CreateRadialGradient(512, bgInner, bgOuter), Color.white);
        Stretch(bg);

        var titleContainer = NewUI("TitleContainer", canvasRT);
        var titleRT = RT(titleContainer);
        titleRT.anchorMin = new Vector2(0, 1); titleRT.anchorMax = new Vector2(1, 1);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = Vector2.zero;
        titleRT.sizeDelta = new Vector2(0, 160);
        titleGroup = titleContainer.AddComponent<CanvasGroup>();

        titleText = MakeText("Title", titleRT, "СХЕМА СКОРОСТНОГО ТРАНСПОРТА", 46, FontStyle.Bold, titleColor, TextAnchor.MiddleCenter);
        var tt = titleText.rectTransform;
        tt.anchorMin = new Vector2(0, 0.55f); tt.anchorMax = new Vector2(1, 1);
        tt.offsetMin = Vector2.zero; tt.offsetMax = Vector2.zero;

        subtitleText = MakeText("Subtitle", titleRT, "Наведите на линию и кликните, чтобы отправиться на неё", 20, FontStyle.Normal, subtitleColor, TextAnchor.MiddleCenter);
        var st = subtitleText.rectTransform;
        st.anchorMin = new Vector2(0, 0); st.anchorMax = new Vector2(1, 0.55f);
        st.offsetMin = Vector2.zero; st.offsetMax = Vector2.zero;

        var hintContainer = NewUI("HintContainer", canvasRT);
        var hintRT = RT(hintContainer);
        hintRT.anchorMin = new Vector2(0, 0); hintRT.anchorMax = new Vector2(1, 0);
        hintRT.pivot = new Vector2(0.5f, 0);
        hintRT.anchoredPosition = Vector2.zero;
        hintRT.sizeDelta = new Vector2(0, 70);
        hintText = MakeText("Hint", hintRT, "Наведите на линию · кликните для входа", 17, FontStyle.Normal, hintColor, TextAnchor.MiddleCenter);
        Stretch(hintText.gameObject);

        mapRoot = RT(NewUI("MapRoot", canvasRT));
        mapRoot.anchorMin = Vector2.zero; mapRoot.anchorMax = Vector2.one;
        mapRoot.offsetMin = new Vector2(40, 80); mapRoot.offsetMax = new Vector2(-40, -160);
        mapRoot.pivot = new Vector2(0.5f, 0.5f);
        mapGroup = mapRoot.gameObject.AddComponent<CanvasGroup>();
        mapGroup.alpha = 0f;

        linesRoot    = RT(NewUI("LinesRoot", mapRoot));    Stretch(linesRoot.gameObject);
        stationsRoot = RT(NewUI("StationsRoot", mapRoot)); Stretch(stationsRoot.gameObject);

        var fade = NewUI("Fade", canvasRT); Stretch(fade);
        fadeOverlay = fade.AddComponent<Image>();
        fadeOverlay.color = new Color(0, 0, 0, 0);
        fadeOverlay.raycastTarget = false;
    }

    // =====================================================================
    private void BuildMap()
    {
        if (data == null) return;

        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (var ln in data.lines) foreach (var st in ln.stations)
        {
            if (st.pos == null || st.pos.Length < 2) continue;
            minX = Mathf.Min(minX, st.pos[0]); maxX = Mathf.Max(maxX, st.pos[0]);
            minY = Mathf.Min(minY, st.pos[1]); maxY = Mathf.Max(maxY, st.pos[1]);
        }
        if (minX > maxX) return;
        mapCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);

        var sorted = new List<LineData>(data.lines);
        sorted.Sort((a, b) => a.heightLayer.CompareTo(b.heightLayer));

        var colorDict = new Dictionary<string, Color>();
        foreach (var lc in data.lineColors)
            if (ColorUtility.TryParseHtmlString(lc.hex, out var c)) colorDict[lc.key] = c;

        foreach (var line in sorted)
        {
            var lv = new LineVisual
            {
                data = line,
                color = colorDict.TryGetValue(line.colorKey, out var col) ? col : Color.white,
                root = RT(NewUI("Line_" + line.id, linesRoot))
            };
            Stretch(lv.root.gameObject);
            lv.currentAlpha = 0f;

            for (int i = 0; i < line.stations.Length - 1; i++)
            {
                var a = line.stations[i].pos;
                var b = line.stations[i + 1].pos;
                if (a == null || b == null || a.Length < 2 || b.Length < 2) continue;
                Vector2 pa = new Vector2(a[0], a[1]);
                Vector2 pb = new Vector2(b[0], b[1]);
                CreateSegment(lv, pa, pb);
                lv.segments.Add(new SegmentVis { a = pa, b = pb });
            }
            lines.Add(lv);
        }

        BuildStations(colorDict);
    }

    private void BuildStations(Dictionary<string, Color> colorDict)
    {
        var groups = new Dictionary<string, List<StationRef>>();
        foreach (var line in data.lines) foreach (var st in line.stations)
        {
            if (st.pos == null || st.pos.Length < 2) continue;
            string key = st.id;
            if (!string.IsNullOrEmpty(st.transferTo))
            {
                var pair = new[] { st.id, st.transferTo };
                System.Array.Sort(pair);
                key = pair[0] + "|" + pair[1];
            }
            if (!groups.TryGetValue(key, out var list)) { list = new List<StationRef>(); groups[key] = list; }
            list.Add(new StationRef
            {
                station = st,
                color = colorDict.TryGetValue(line.colorKey, out var c) ? c : Color.white
            });
        }

        foreach (var kv in groups)
        {
            var list = kv.Value;
            var st0 = list[0].station;
            Vector2 pos = new Vector2(st0.pos[0], st0.pos[1]);
            if (list.Count >= 2) CreateTransferStation(pos, list[0].color, list[1].color);
            else                 CreateStation(pos, list[0].color);
        }
    }

    private class StationRef
    {
        public StationData station;
        public Color color;
    }

    // =====================================================================
    private void CreateSegment(LineVisual lv, Vector2 a, Vector2 b)
    {
        Vector2 mid = (a + b) * 0.5f;
        Vector2 dir = b - a;
        float length = dir.magnitude;
        if (length < 0.01f) return;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        CreateLinePart(lv, mid, angle, length, lineWidth * glowOuterMult,
            new Color(lv.color.r, lv.color.g, lv.color.b, 1f), true, true);
        CreateLinePart(lv, mid, angle, length, lineWidth * glowInnerMult,
            new Color(lv.color.r, lv.color.g, lv.color.b, 1f), true, false);
        CreateLinePart(lv, mid, angle, length, lineWidth, lv.color, false, false);
    }

    private void CreateLinePart(LineVisual lv, Vector2 mid, float angle, float length, float width,
        Color color, bool isGlow, bool outer)
    {
        var bodyGO = NewUI("seg", lv.root);
        var rt = RT(bodyGO);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(length, width);
        rt.localRotation = Quaternion.Euler(0, 0, angle);
        var img = bodyGO.AddComponent<Image>();
        img.sprite = whiteSprite;
        img.color = color;
        img.raycastTarget = false;
        if (isGlow) lv.glowGraphics.Add(new GlowGraphic { graphic = img, outer = outer });
        else        lv.lineGraphics.Add(img);

        Vector2 dirVec = DirFrom(angle);
        CreateCap(lv, mid - dirVec * (length * 0.5f), width, color, isGlow, outer);
        CreateCap(lv, mid + dirVec * (length * 0.5f), width, color, isGlow, outer);
    }

    private Vector2 DirFrom(float angleDeg)
    {
        float r = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
    }

    private void CreateCap(LineVisual lv, Vector2 localPos, float diameter, Color color, bool isGlow, bool outer)
    {
        var go = NewUI("cap", lv.root);
        var rt = RT(go);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(diameter, diameter);
        rt.anchoredPosition = localPos;
        var img = go.AddComponent<Image>();
        img.sprite = circleSprite;
        img.color = color;
        img.raycastTarget = false;
        if (isGlow) lv.glowGraphics.Add(new GlowGraphic { graphic = img, outer = outer });
        else        lv.lineGraphics.Add(img);
    }

    private void CreateStation(Vector2 mapPos, Color color)
    {
        var go = NewUI("station", stationsRoot);
        var rt = RT(go);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(stationSize, stationSize);
        rt.anchoredPosition = MapToLocal(mapPos);

        var marker = go.AddComponent<StationMarker>();
        marker.rect = rt; marker.mapPos = mapPos;

        var ring = go.AddComponent<Image>();
        ring.sprite = ringSprite; ring.color = color; ring.raycastTarget = false;

        var innerGO = NewUI("inner", rt);
        var innerRT = RT(innerGO);
        innerRT.anchorMin = Vector2.zero; innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = Vector2.zero; innerRT.offsetMax = Vector2.zero;
        var inner = innerGO.AddComponent<Image>();
        inner.sprite = circleSprite; inner.color = Color.white; inner.raycastTarget = false;
    }

    private void CreateTransferStation(Vector2 mapPos, Color colorA, Color colorB)
    {
        var go = NewUI("transfer", stationsRoot);
        var rt = RT(go);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(transferStationSize, transferStationSize);
        rt.anchoredPosition = MapToLocal(mapPos);

        var marker = go.AddComponent<StationMarker>();
        marker.rect = rt; marker.mapPos = mapPos;

        var halo = go.AddComponent<Image>();
        halo.sprite = circleSprite; halo.color = new Color(1, 1, 1, 0.9f); halo.raycastTarget = false;

        float pad = transferStationSize * 0.16f;

        var leftGO = NewUI("left", rt);
        var lrt = RT(leftGO);
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(pad, pad); lrt.offsetMax = new Vector2(-pad, -pad);
        var leftImg = leftGO.AddComponent<Image>();
        leftImg.sprite = circleSprite; leftImg.color = colorA; leftImg.raycastTarget = false;

        var rightGO = NewUI("right", rt);
        var rrt = RT(rightGO);
        rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
        rrt.offsetMin = new Vector2(pad, pad); rrt.offsetMax = new Vector2(-pad, -pad);
        var rightImg = rightGO.AddComponent<Image>();
        rightImg.sprite = circleSprite; rightImg.color = colorB; rightImg.raycastTarget = false;
        rightImg.type = Image.Type.Filled;
        rightImg.fillMethod = Image.FillMethod.Radial360;
        rightImg.fillOrigin = (int)Image.Origin360.Top;
        rightImg.fillClockwise = true;
        rightImg.fillAmount = 0.5f;

        var divGO = NewUI("div", rt);
        var drt = RT(divGO);
        drt.anchorMin = new Vector2(0.5f, 0f); drt.anchorMax = new Vector2(0.5f, 1f);
        drt.pivot = new Vector2(0.5f, 0.5f);
        drt.sizeDelta = new Vector2(transferStationSize * 0.13f, -pad * 2);
        drt.anchoredPosition = Vector2.zero;
        var divImg = divGO.AddComponent<Image>();
        divImg.sprite = circleSprite; divImg.color = Color.white; divImg.raycastTarget = false;
    }

    // =====================================================================
    private void LayoutMap()
    {
        if (mapRoot == null || data == null) return;
        Vector2 size = mapRoot.rect.size;

        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (var ln in data.lines) foreach (var st in ln.stations)
        {
            if (st.pos == null || st.pos.Length < 2) continue;
            minX = Mathf.Min(minX, st.pos[0]); maxX = Mathf.Max(maxX, st.pos[0]);
            minY = Mathf.Min(minY, st.pos[1]); maxY = Mathf.Max(maxY, st.pos[1]);
        }
        float w = Mathf.Max(1, maxX - minX);
        float h = Mathf.Max(1, maxY - minY);
        float padding = 100f;
        float sx = (size.x - padding * 2f) / w;
        float sy = (size.y - padding * 2f) / h;
        mapScale = Mathf.Min(sx, sy);

        foreach (var lv in lines)
        {
            for (int j = 0; j < lv.segments.Count; j++)
            {
                var seg = lv.segments[j];
                Vector2 a = MapToLocal(seg.a);
                Vector2 b = MapToLocal(seg.b);
                Vector2 mid = (a + b) * 0.5f;
                Vector2 dir = b - a;
                float length = dir.magnitude;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                Transform root = lv.root;
                int baseIdx = j * 9;
                SetSegmentBody(root.GetChild(baseIdx + 0), mid, length, angle);
                SetSegmentBody(root.GetChild(baseIdx + 3), mid, length, angle);
                SetSegmentBody(root.GetChild(baseIdx + 6), mid, length, angle);
                SetCapPos(root.GetChild(baseIdx + 1), a); SetCapPos(root.GetChild(baseIdx + 2), b);
                SetCapPos(root.GetChild(baseIdx + 4), a); SetCapPos(root.GetChild(baseIdx + 5), b);
                SetCapPos(root.GetChild(baseIdx + 7), a); SetCapPos(root.GetChild(baseIdx + 8), b);
            }
        }

        var markers = stationsRoot.GetComponentsInChildren<StationMarker>(true);
        foreach (var m in markers) m.rect.anchoredPosition = MapToLocal(m.mapPos);
    }

    private void SetSegmentBody(Transform t, Vector2 mid, float length, float angle)
    {
        var rt = t as RectTransform;
        if (rt == null) return;
        rt.anchoredPosition = mid;
        rt.sizeDelta = new Vector2(length, rt.sizeDelta.y);
        rt.localRotation = Quaternion.Euler(0, 0, angle);
    }

    private void SetCapPos(Transform t, Vector2 pos)
    {
        var rt = t as RectTransform; if (rt != null) rt.anchoredPosition = pos;
    }

    private Vector2 MapToLocal(Vector2 mapPos)
    {
        return new Vector2((mapPos.x - mapCenter.x) * mapScale, (mapPos.y - mapCenter.y) * mapScale);
    }

    public class StationMarker : MonoBehaviour
    {
        public RectTransform rect;
        public Vector2 mapPos;
    }

    // =====================================================================
    private void HandleHoverAndClick()
    {
        Vector2 localMouse;
        bool inside = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            mapRoot, Input.mousePosition, null, out localMouse);

        LineVisual newHover = null;
        if (inside)
        {
            float best = hoverRadius;
            foreach (var lv in lines)
            {
                float d = DistanceToLine(lv, localMouse);
                if (d < best) { best = d; newHover = lv; }
            }
        }

        if (newHover != hovered) { hovered = newHover; OnHoverChanged(); }

        if (hovered != null && Input.GetMouseButtonDown(0)) OnLineClicked(hovered);
    }

    private void OnHoverChanged()
    {
        if (hovered != null)
        {
            hovered.root.SetAsLastSibling();
            titleText.text = hovered.data.name.ToUpperInvariant();
            titleText.color = hovered.color;
            subtitleText.text = "Кликните, чтобы начать игру на этой линии";
            subtitleText.color = new Color(hovered.color.r, hovered.color.g, hovered.color.b, 0.85f);
            hintText.text = $"→ Перейти на «{hovered.data.name}»";
        }
        else
        {
            titleText.text = "СХЕМА СКОРОСТНОГО ТРАНСПОРТА";
            titleText.color = titleColor;
            subtitleText.text = "Наведите на линию и кликните, чтобы отправиться на неё";
            subtitleText.color = subtitleColor;
            hintText.text = "Наведите на линию · кликните для входа";
        }
    }

    private void OnLineClicked(LineVisual lv)
    {
        if (transitioning) return;
        transitioning = true;

        MetroSession.SelectedLineId = lv.data.id;
        MetroSession.SelectedLineName = lv.data.name;
        MetroSession.SelectedLineColorKey = lv.data.colorKey;
        PlayerPrefs.SetString("Metro.SelectedLineId", lv.data.id);
        PlayerPrefs.SetString("Metro.SelectedLineColor", lv.data.colorKey);
        PlayerPrefs.Save();

        StartCoroutine(TransitionRoutine(lv));
    }

    private IEnumerator TransitionRoutine(LineVisual lv)
    {
        hovered = null;

        float t = 0f;
        while (t < 0.35f) { t += Time.deltaTime; yield return null; }

        float dur = 0.7f;
        t = 0f;
        Vector3 startScale = mapRoot.localScale;
        Vector3 endScale = Vector3.one * 1.15f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            k = 1f - Mathf.Pow(1f - k, 3f);
            mapRoot.localScale = Vector3.Lerp(startScale, endScale, k);
            fadeOverlay.color = new Color(0, 0, 0, k);
            titleGroup.alpha = 1f - k;
            yield return null;
        }

        if (!string.IsNullOrEmpty(gameSceneName))
            SceneManager.LoadScene(gameSceneName);
    }

    // =====================================================================
    private void AnimateLines()
    {
        float dt = Time.deltaTime;
        float pulse = (Mathf.Sin(hoverTime * pulseSpeed) * 0.5f + 0.5f) * pulseAmount;

        foreach (var lv in lines)
        {
            bool isHover = (lv == hovered);
            float targetA = isHover ? 1f : (hovered != null ? 0.32f : 1f);
            float targetG = isHover ? (1f + pulse) : 0f;

            lv.currentAlpha = Mathf.Lerp(lv.currentAlpha, targetA, dt * hoverLerp);
            lv.currentGlow  = Mathf.Lerp(lv.currentGlow,  targetG, dt * hoverLerp);

            foreach (var g in lv.lineGraphics)
            {
                var c = lv.color; c.a = lv.currentAlpha; g.color = c;
            }
            foreach (var g in lv.glowGraphics)
            {
                var c = lv.color;
                float baseA = g.outer ? 0.28f : 0.65f;
                float idle  = g.outer ? 0.06f : 0.10f;
                c.a = (lv.currentGlow * baseA) + idle * lv.currentAlpha;
                g.graphic.color = c;
            }
        }
    }

    // =====================================================================
    private IEnumerator IntroRoutine()
    {
        yield return null;
        LayoutMap();

        float dur = 1.2f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            k = 1f - Mathf.Pow(1f - k, 3f);
            mapGroup.alpha = k;
            titleGroup.alpha = k;
            mapRoot.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, k);
            yield return null;
        }
        mapGroup.alpha = 1f; titleGroup.alpha = 1f; mapRoot.localScale = Vector3.one;
    }

    // =====================================================================
    // HELPERS
    // =====================================================================
    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }
    private static GameObject NewUI(string name, RectTransform parent) => NewUI(name, (Transform)parent);

    // Мостик: GameObject → его RectTransform
    private static RectTransform RT(GameObject go) => (RectTransform)go.transform;

    private static void Stretch(GameObject go)
    {
        var rt = RT(go);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static GameObject MakeImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = NewUI(name, parent);
        var img = go.AddComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = false;
        Stretch(go);
        return go;
    }

    private static Text MakeText(string name, RectTransform parent, string content, int size,
        FontStyle style, Color color, TextAnchor align)
    {
        var go = NewUI(name, parent);
        var txt = go.AddComponent<Text>();
        txt.text = content;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (txt.font == null) txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = size; txt.fontStyle = style; txt.color = color; txt.alignment = align;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.raycastTarget = false;
        return txt;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.0001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
        return Vector2.Distance(p, a + t * ab);
    }

    private static float DistanceToLine(LineVisual lv, Vector2 p)
    {
        float best = float.MaxValue;
        foreach (var seg in lv.segments)
        {
            float d = DistanceToSegment(p, seg.a, seg.b);
            if (d < best) best = d;
        }
        return best;
    }
}