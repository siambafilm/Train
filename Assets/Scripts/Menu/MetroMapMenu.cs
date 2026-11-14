// Assets/Scripts/Menu/MetroMapMenu.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public static class MetroSession
{
    public static string SelectedLineId;
    public static string SelectedLineName;
    public static string SelectedLineColorKey;
    public static string SelectedLineColorHex;
    public static Color  SelectedLineColor;

    public static void EnsureLoaded()
    {
        if (string.IsNullOrEmpty(SelectedLineId))
            SelectedLineId = PlayerPrefs.GetString("Metro.SelectedLineId", "");
        if (string.IsNullOrEmpty(SelectedLineName))
            SelectedLineName = PlayerPrefs.GetString("Metro.SelectedLineName", "");
        if (string.IsNullOrEmpty(SelectedLineColorKey))
            SelectedLineColorKey = PlayerPrefs.GetString("Metro.SelectedLineColor", "");
        if (string.IsNullOrEmpty(SelectedLineColorHex))
            SelectedLineColorHex = PlayerPrefs.GetString("Metro.SelectedLineColorHex", "");

        if (!string.IsNullOrEmpty(SelectedLineColorHex)
            && ColorUtility.TryParseHtmlString(SelectedLineColorHex, out var c))
        {
            SelectedLineColor = c;
        }
    }
}

public class MetroMapMenu : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string mapResourcePath = "metro_map";

    [Header("Branding")]
    [Tooltip("Название игры — отображается сверху по центру.")]
    [SerializeField] private string gameTitle = "METRO";
    [Tooltip("Строка версии/сборки в левом нижнем углу.")]
    [SerializeField] private string versionInfo = "v0.1.0 · build 001";
    [Tooltip("Акцентный цвет для заголовка, разделителя и кнопки выхода.")]
    [SerializeField] private Color accentColor = new Color(0.42f, 0.72f, 1f, 1f);

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

    [Header("Audio")]
    [Tooltip("Звук при наведении на новую линию.")]
    [SerializeField] private AudioClip hoverSound;
    [Tooltip("Звук при подтверждении выбора линии (клик).")]
    [SerializeField] private AudioClip selectSound;
    [Tooltip("Громкость hover-звука (0..1).")]
    [SerializeField, Range(0f, 1f)] private float hoverVolume = 0.55f;
    [Tooltip("Громкость select-звука (0..1).")]
    [SerializeField, Range(0f, 1f)] private float selectVolume = 1f;
    [Tooltip("Минимальный интервал между hover-звуками (сек), чтобы не спамило.")]
    [SerializeField] private float hoverSoundCooldown = 0.06f;
    [Tooltip("Случайный разброс высоты тона hover-звука (0 = без разброса).")]
    [SerializeField, Range(0f, 0.3f)] private float hoverPitchJitter = 0.06f;
    [Tooltip("Необязательно: свой AudioSource (например, с AudioMixer-группой). Если пусто — будет создан автоматически.")]
    [SerializeField] private AudioSource sfxSource;

    [Header("Loading")]
    [SerializeField] private int   spinnerBallCount = 8;
    [SerializeField] private float spinnerRadius    = 55f;
    [SerializeField] private float spinnerBallSize  = 20f;
    [SerializeField] private float spinnerSpeed     = 1.8f;

    [Header("Loading Timing")]
    [SerializeField] private float minLoadingDisplayTime = 1.4f;

    [Header("Living Background")]
    [SerializeField] private bool  enableLivingBackground = true;
    [Tooltip("Первый цвет мягкого пятна (по умолчанию — синий).")]
    [SerializeField] private Color livingBlob1Color = new Color(0.22f, 0.55f, 1.00f);
    [Tooltip("Второй цвет мягкого пятна (по умолчанию — фиолетовый).")]
    [SerializeField] private Color livingBlob2Color = new Color(0.60f, 0.28f, 0.90f);
    [Tooltip("Третий цвет мягкого пятна (по умолчанию — бирюзовый).")]
    [SerializeField] private Color livingBlob3Color = new Color(0.20f, 0.75f, 0.75f);
    [Tooltip("Скорость орбитального движения пятен.")]
    [SerializeField] private float livingMovementSpeed = 1f;
    [Tooltip("Скорость пульсации размера/альфы пятен.")]
    [SerializeField] private float livingPulseSpeed = 1f;
    [Tooltip("Общая интенсивность живого градиента (0 = выключено, 1 = норма).")]
    [SerializeField] private float livingIntensity = 1f;

    private MetroMapData data;
    private Canvas canvas;
    private CanvasGroup mapGroup;
    private CanvasGroup titleGroup;
    private CanvasGroup brandingGroup;
    private RectTransform mapRoot;
    private RectTransform linesRoot;
    private RectTransform stationsRoot;
    private Text titleText;
    private Text subtitleText;
    private Text hintText;
    private Image fadeOverlay;
    private Image bgBaseImage;

    // --- Loading UI ---
    private CanvasGroup loadingGroup;
    private RectTransform spinnerRoot;
    private readonly List<Image> spinnerBalls = new List<Image>();
    private Text loadingText;

    private readonly List<LineVisual> lines = new List<LineVisual>();
    private LineVisual hovered;
    private Vector2 mapCenter;
    private float mapScale = 1f;
    private bool transitioning = false;
    private float hoverTime = 0f;

    private float lastHoverSoundTime = -999f;

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
        EnsureAudioSource();
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
        UpdateSpinner();
        if (transitioning) return;
        hoverTime += Time.deltaTime;
        HandleHoverAndClick();
        AnimateLines();
    }

    // =====================================================================
    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private void EnsureAudioSource()
    {
        if (sfxSource != null) return;

        var audioGO = new GameObject("MetroMenuAudio");
        audioGO.transform.SetParent(transform, false);

        sfxSource = audioGO.AddComponent<AudioSource>();
        sfxSource.playOnAwake  = false;
        sfxSource.loop         = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.volume       = 1f;
    }

    private void PlayHoverSound()
    {
        if (hoverSound == null || sfxSource == null) return;

        if (Time.unscaledTime - lastHoverSoundTime < hoverSoundCooldown) return;
        lastHoverSoundTime = Time.unscaledTime;

        float oldPitch = sfxSource.pitch;
        sfxSource.pitch = 1f + Random.Range(-hoverPitchJitter, hoverPitchJitter);
        sfxSource.PlayOneShot(hoverSound, hoverVolume);
        sfxSource.pitch = oldPitch;
    }

    private void PlaySelectSound()
    {
        if (selectSound == null || sfxSource == null) return;
        sfxSource.PlayOneShot(selectSound, selectVolume);
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

    // === сетка для фона ===
    private Texture2D CreateGridTexture(int size, int lineWidth)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Repeat;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            bool onLine = x < lineWidth || y < lineWidth;
            px[y * size + x] = onLine ? Color.white : Color.clear;
        }
        tex.SetPixels(px); tex.Apply();
        return tex;
    }

    // === виньетка (затемнение по краям) ===
    private Sprite CreateVignetteSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;
        float c = size / 2f, maxD = c;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float dx = x + 0.5f - c, dy = y + 0.5f - c;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / maxD;
            float a = Mathf.Clamp01((d - 0.35f) / 0.65f);
            a = a * a;
            px[y * size + x] = new Color(0, 0, 0, a * 0.7f);
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

        // ---- Фон: базовый радиальный градиент ----
        var bg = MakeImage("Background", canvasRT, CreateRadialGradient(512, bgInner, bgOuter), Color.white);
        Stretch(bg);
        bgBaseImage = bg.GetComponent<Image>();

        // ---- Фон: живой анимированный градиент (поверх базового, под сеткой) ----
        if (enableLivingBackground)
            BuildLivingBackground(canvasRT);

        // ---- Фон: тонкая сетка (RawImage с uvRect для тайлинга) ----
        var gridGO = NewUI("Grid", canvasRT);
        Stretch(gridGO);
        var gridRaw = gridGO.AddComponent<RawImage>();
        gridRaw.texture = CreateGridTexture(120, 1);
        gridRaw.color = new Color(0.35f, 0.5f, 0.8f, 0.055f);
        gridRaw.raycastTarget = false;
        gridRaw.uvRect = new Rect(0, 0, 1920f / 120f, 1080f / 120f);

        // ---- Фон: виньетка ----
        var vignette = MakeImage("Vignette", canvasRT, CreateVignetteSprite(512), Color.white);
        Stretch(vignette);

        // ============================================================
        // BRANDING (название игры + свечение + разделитель)
        // ============================================================
        var brandGO = NewUI("Branding", canvasRT);
        var brandRT = RT(brandGO);
        brandRT.anchorMin = new Vector2(0, 1);
        brandRT.anchorMax = new Vector2(1, 1);
        brandRT.pivot = new Vector2(0.5f, 1);
        brandRT.anchoredPosition = Vector2.zero;
        brandRT.sizeDelta = new Vector2(0, 100);
        brandingGroup = brandGO.AddComponent<CanvasGroup>();
        brandingGroup.alpha = 0f;

        // мягкое свечение за названием
        var glowGO = NewUI("GameTitleGlow", brandRT);
        var glowRT = RT(glowGO);
        glowRT.anchorMin = glowRT.anchorMax = new Vector2(0.5f, 0.5f);
        glowRT.pivot = new Vector2(0.5f, 0.5f);
        glowRT.sizeDelta = new Vector2(900, 130);
        glowRT.anchoredPosition = Vector2.zero;
        var glowImg = glowGO.AddComponent<Image>();
        glowImg.sprite = CreateRadialGradient(128,
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.22f),
            new Color(accentColor.r, accentColor.g, accentColor.b, 0f));
        glowImg.raycastTarget = false;

        var gameTitleTxt = MakeText("GameTitle", brandRT, gameTitle, 50,
            FontStyle.Bold, accentColor, TextAnchor.MiddleCenter);
        Stretch(gameTitleTxt.gameObject);

        // тонкий разделитель снизу брендинга
        var sepGO = NewUI("Separator", brandRT);
        var sepRT = RT(sepGO);
        sepRT.anchorMin = new Vector2(0.5f, 0);
        sepRT.anchorMax = new Vector2(0.5f, 0);
        sepRT.pivot = new Vector2(0.5f, 0);
        sepRT.sizeDelta = new Vector2(300, 2);
        sepRT.anchoredPosition = new Vector2(0, 6);
        var sepImg = sepGO.AddComponent<Image>();
        sepImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.55f);
        sepImg.raycastTarget = false;

        // ============================================================
        // TITLE (схема / hover)
        // ============================================================
        var titleContainer = NewUI("TitleContainer", canvasRT);
        var titleRT = RT(titleContainer);
        titleRT.anchorMin = new Vector2(0, 1);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0, -100);
        titleRT.sizeDelta = new Vector2(0, 150);
        titleGroup = titleContainer.AddComponent<CanvasGroup>();
        titleGroup.alpha = 0f;

        titleText = MakeText("Title", titleRT, "СХЕМА СКОРОСТНОГО ТРАНСПОРТА", 42,
            FontStyle.Bold, titleColor, TextAnchor.MiddleCenter);
        var tt = titleText.rectTransform;
        tt.anchorMin = new Vector2(0, 0.55f); tt.anchorMax = new Vector2(1, 1);
        tt.offsetMin = Vector2.zero; tt.offsetMax = Vector2.zero;

        subtitleText = MakeText("Subtitle", titleRT, "Наведите на линию и кликните, чтобы отправиться на неё",
            20, FontStyle.Normal, subtitleColor, TextAnchor.MiddleCenter);
        var st = subtitleText.rectTransform;
        st.anchorMin = new Vector2(0, 0); st.anchorMax = new Vector2(1, 0.55f);
        st.offsetMin = Vector2.zero; st.offsetMax = Vector2.zero;

        // ============================================================
        // HINT (низ по центру)
        // ============================================================
        var hintContainer = NewUI("HintContainer", canvasRT);
        var hintRT = RT(hintContainer);
        hintRT.anchorMin = new Vector2(0, 0);
        hintRT.anchorMax = new Vector2(1, 0);
        hintRT.pivot = new Vector2(0.5f, 0);
        hintRT.anchoredPosition = Vector2.zero;
        hintRT.sizeDelta = new Vector2(0, 60);
        hintText = MakeText("Hint", hintRT, "Наведите на линию · кликните для входа",
            17, FontStyle.Normal, hintColor, TextAnchor.MiddleCenter);
        Stretch(hintText.gameObject);

        // ============================================================
        // MAP ROOT
        // ============================================================
        mapRoot = RT(NewUI("MapRoot", canvasRT));
        mapRoot.anchorMin = Vector2.zero; mapRoot.anchorMax = Vector2.one;
        mapRoot.offsetMin = new Vector2(40, 90);
        mapRoot.offsetMax = new Vector2(-40, -250);
        mapRoot.pivot = new Vector2(0.5f, 0.5f);
        mapGroup = mapRoot.gameObject.AddComponent<CanvasGroup>();
        mapGroup.alpha = 0f;

        linesRoot    = RT(NewUI("LinesRoot", mapRoot));    Stretch(linesRoot.gameObject);
        stationsRoot = RT(NewUI("StationsRoot", mapRoot)); Stretch(stationsRoot.gameObject);

        // ============================================================
        // VERSION (левый нижний угол)
        // ============================================================
        var versionGO = NewUI("Version", canvasRT);
        var versionRT = RT(versionGO);
        versionRT.anchorMin = new Vector2(0, 0);
        versionRT.anchorMax = new Vector2(0, 0);
        versionRT.pivot = new Vector2(0, 0);
        versionRT.anchoredPosition = new Vector2(30, 22);
        versionRT.sizeDelta = new Vector2(420, 28);
        var versionTxt = versionGO.AddComponent<Text>();
        versionTxt.text = versionInfo;
        versionTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (versionTxt.font == null) versionTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        versionTxt.fontSize = 14;
        versionTxt.fontStyle = FontStyle.Normal;
        versionTxt.color = new Color(0.4f, 0.5f, 0.7f, 0.7f);
        versionTxt.alignment = TextAnchor.MiddleLeft;
        versionTxt.raycastTarget = false;

        // ============================================================
        // EXIT BUTTON (правый нижний угол, иконка-крестик)
        // ============================================================
        BuildExitButton(canvasRT);

        // ============================================================
        // FADE + LOADING
        // ============================================================
        var fade = NewUI("Fade", canvasRT); Stretch(fade);
        fadeOverlay = fade.AddComponent<Image>();
        fadeOverlay.color = new Color(0, 0, 0, 0);
        fadeOverlay.raycastTarget = false;

        BuildLoadingUI(canvasRT);
    }

    // =====================================================================
    //  Living background: медленно движущиеся мягкие пятна + дыхание базы
    // =====================================================================
    private void BuildLivingBackground(RectTransform canvasRT)
    {
        var livingGO = NewUI("LivingBackground", canvasRT);
        Stretch(livingGO);
        var living = livingGO.AddComponent<LivingBackground>();

        // мягкий белый радиальный градиент — красим тинтом через Image.color
        var blobSprite = CreateRadialGradient(256, Color.white, new Color(1f, 1f, 1f, 0f));

        var blobs = new[]
        {
            new LivingBackground.BlobDef
            {
                color       = livingBlob1Color,
                basePos     = new Vector2(-0.32f,  0.25f),
                baseSize    = 1700f,
                phase       = 0f,
                orbitRadius = 130f,
                orbitSpeed  = 0.12f,
                pulseSpeed  = 0.22f,
                alphaBase   = 0.42f,
                alphaAmp    = 0.10f,
                colorDrift  = 0.06f
            },
            new LivingBackground.BlobDef
            {
                color       = livingBlob2Color,
                basePos     = new Vector2( 0.40f, -0.30f),
                baseSize    = 1500f,
                phase       = 2.1f,
                orbitRadius = 160f,
                orbitSpeed  = 0.09f,
                pulseSpeed  = 0.18f,
                alphaBase   = 0.35f,
                alphaAmp    = 0.09f,
                colorDrift  = 0.08f
            },
            new LivingBackground.BlobDef
            {
                color       = livingBlob3Color,
                basePos     = new Vector2( 0.28f,  0.35f),
                baseSize    = 1300f,
                phase       = 4.3f,
                orbitRadius = 150f,
                orbitSpeed  = 0.11f,
                pulseSpeed  = 0.26f,
                alphaBase   = 0.30f,
                alphaAmp    = 0.08f,
                colorDrift  = 0.10f
            }
        };

        for (int i = 0; i < blobs.Length; i++)
        {
            blobs[i].orbitSpeed *= livingMovementSpeed;
            blobs[i].pulseSpeed *= livingPulseSpeed;
            blobs[i].alphaBase  = Mathf.Clamp01(blobs[i].alphaBase * livingIntensity);
            blobs[i].alphaAmp   = Mathf.Clamp01(blobs[i].alphaAmp  * livingIntensity);
        }

        living.Initialize(blobSprite, blobs, bgBaseImage);
    }

    // =====================================================================
    //  Exit button (круглая иконка в правом нижнем углу)
    // =====================================================================
    private void BuildExitButton(RectTransform canvasRT)
    {
        var container = NewUI("ExitButton", canvasRT);
        var rt = RT(container);
        rt.anchorMin = rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(1, 0);
        rt.sizeDelta = new Vector2(64, 64);
        rt.anchoredPosition = new Vector2(-30, 24);

        var bg = container.AddComponent<Image>();
        bg.sprite = circleSprite;
        bg.color = new Color(0.13f, 0.16f, 0.26f, 0.92f);
        bg.raycastTarget = true;

        var ringGO = NewUI("Ring", rt);
        Stretch(ringGO);
        var ringImg = ringGO.AddComponent<Image>();
        ringImg.sprite = ringSprite;
        ringImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.35f);
        ringImg.raycastTarget = false;

        // крестик (две скрещённые линии)
        float iconSize = 22f;
        float thickness = 3f;

        var line1 = NewUI("X1", rt);
        var l1rt = RT(line1);
        l1rt.anchorMin = l1rt.anchorMax = new Vector2(0.5f, 0.5f);
        l1rt.pivot = new Vector2(0.5f, 0.5f);
        l1rt.sizeDelta = new Vector2(iconSize, thickness);
        l1rt.localRotation = Quaternion.Euler(0, 0, 45);
        var l1img = line1.AddComponent<Image>();
        l1img.sprite = whiteSprite;
        l1img.color = new Color(0.88f, 0.92f, 1f);
        l1img.raycastTarget = false;

        var line2 = NewUI("X2", rt);
        var l2rt = RT(line2);
        l2rt.anchorMin = l2rt.anchorMax = new Vector2(0.5f, 0.5f);
        l2rt.pivot = new Vector2(0.5f, 0.5f);
        l2rt.sizeDelta = new Vector2(iconSize, thickness);
        l2rt.localRotation = Quaternion.Euler(0, 0, -45);
        var l2img = line2.AddComponent<Image>();
        l2img.sprite = whiteSprite;
        l2img.color = new Color(0.88f, 0.92f, 1f);
        l2img.raycastTarget = false;

        var btn = container.AddComponent<ExitButton>();
        btn.bg = bg;
        btn.ring = ringImg;
        btn.container = rt;
        btn.accent = accentColor;
    }

    // =====================================================================
    //  Loading screen
    // =====================================================================
    private void BuildLoadingUI(RectTransform canvasRT)
    {
        var loadingContainer = NewUI("Loading", canvasRT);
        Stretch(loadingContainer);
        loadingGroup = loadingContainer.AddComponent<CanvasGroup>();
        loadingGroup.alpha = 0f;
        loadingGroup.blocksRaycasts = false;
        loadingGroup.interactable = false;

        spinnerRoot = RT(NewUI("Spinner", loadingContainer));
        spinnerRoot.anchorMin = spinnerRoot.anchorMax = new Vector2(0.5f, 0.5f);
        spinnerRoot.pivot = new Vector2(0.5f, 0.5f);
        spinnerRoot.sizeDelta = new Vector2(160f, 160f);
        spinnerRoot.anchoredPosition = new Vector2(0, 40f);

        for (int i = 0; i < spinnerBallCount; i++)
        {
            float ang = (i / (float)spinnerBallCount) * Mathf.PI * 2f;
            var ballGO = NewUI("ball_" + i, spinnerRoot);
            var brt = RT(ballGO);
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(spinnerBallSize, spinnerBallSize);
            brt.anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spinnerRadius;
            var bimg = ballGO.AddComponent<Image>();
            bimg.sprite = circleSprite;
            bimg.color = new Color(1f, 1f, 1f, 0.15f);
            bimg.raycastTarget = false;
            spinnerBalls.Add(bimg);
        }

        loadingText = MakeText("LoadingText", RT(loadingContainer), "ЗАГРУЗКА...", 34,
            FontStyle.Bold, new Color(0.93f, 0.95f, 1f), TextAnchor.MiddleCenter);
        var ltr = loadingText.rectTransform;
        ltr.anchorMin = ltr.anchorMax = new Vector2(0.5f, 0.5f);
        ltr.pivot = new Vector2(0.5f, 0.5f);
        ltr.sizeDelta = new Vector2(900f, 60f);
        ltr.anchoredPosition = new Vector2(0, -100f);
    }

    private void UpdateSpinner()
    {
        if (loadingGroup == null || loadingGroup.alpha <= 0.001f) return;
        float time = Time.unscaledTime;
        int n = spinnerBalls.Count;
        if (n == 0) return;
        for (int i = 0; i < n; i++)
        {
            float phase = (float)i / n;
            float v = Mathf.Repeat(time * spinnerSpeed - phase, 1f);
            float a = Mathf.Lerp(0.15f, 1f, Mathf.Pow(1f - v, 2.5f));
            var c = spinnerBalls[i].color;
            c.a = a;
            spinnerBalls[i].color = c;
        }
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
    //  Exit button component
    // =====================================================================
    public class ExitButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Image bg;
        public Image ring;
        public RectTransform container;
        public Color accent = new Color(0.42f, 0.72f, 1f, 1f);

        private float hover, targetHover;

        public void OnPointerEnter(PointerEventData e) { targetHover = 1f; }
        public void OnPointerExit(PointerEventData e)  { targetHover = 0f; }
        public void OnPointerClick(PointerEventData e)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Update()
        {
            hover = Mathf.Lerp(hover, targetHover, Time.unscaledDeltaTime * 12f);
            if (container != null)
                container.localScale = Vector3.one * (1f + hover * 0.12f);
            if (bg != null)
            {
                bg.color = Color.Lerp(
                    new Color(0.13f, 0.16f, 0.26f, 0.92f),
                    new Color(0.55f, 0.18f, 0.22f, 0.95f),
                    hover);
            }
            if (ring != null)
                ring.color = new Color(accent.r, accent.g, accent.b, 0.35f + hover * 0.55f);
        }
    }

    // =====================================================================
    //  Living background component
    // =====================================================================
    public class LivingBackground : MonoBehaviour
    {
        public struct BlobDef
        {
            public Color   color;
            public Vector2 basePos;
            public float   baseSize;
            public float   phase;
            public float   orbitRadius;
            public float   orbitSpeed;
            public float   pulseSpeed;
            public float   alphaBase;
            public float   alphaAmp;
            public float   colorDrift;
        }

        private class Blob
        {
            public BlobDef def;
            public RectTransform rt;
            public Image img;
        }

        private Blob[] blobs;
        private Image  baseBg;
        private Color  baseBgColor;
        private float  time;

        public void Initialize(Sprite blobSprite, BlobDef[] defs, Image baseBgImage)
        {
            baseBg = baseBgImage;
            if (baseBg != null) baseBgColor = baseBg.color;

            blobs = new Blob[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                var def = defs[i];

                var go = new GameObject("Blob_" + i, typeof(RectTransform));
                go.transform.SetParent(transform, false);

                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f + def.basePos.x, 0.5f + def.basePos.y);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(def.baseSize, def.baseSize);
                rt.anchoredPosition = Vector2.zero;

                var img = go.AddComponent<Image>();
                img.sprite = blobSprite;
                img.color  = def.color;
                img.raycastTarget = false;

                blobs[i] = new Blob { def = def, rt = rt, img = img };
            }
        }

        private void Update()
        {
            time += Time.unscaledDeltaTime;

            // Очень мягкое «дыхание» базового градиента
            if (baseBg != null)
            {
                float breath = Mathf.Sin(time * 0.18f) * 0.5f + 0.5f;
                var c = baseBgColor;
                c.r *= Mathf.Lerp(0.90f, 1.05f, breath);
                c.g *= Mathf.Lerp(0.94f, 1.08f, breath);
                c.b *= Mathf.Lerp(1.02f, 1.15f, breath);
                baseBg.color = c;
            }

            if (blobs == null) return;

            for (int i = 0; i < blobs.Length; i++)
            {
                var b = blobs[i];
                if (b.rt == null || b.img == null) continue;
                var d = b.def;

                float t = time;

                // Орбитальное движение (эллипс, чуть разный период по осям)
                float ox = Mathf.Cos(d.phase + t * d.orbitSpeed) * d.orbitRadius;
                float oy = Mathf.Sin(d.phase + t * d.orbitSpeed * 1.27f) * d.orbitRadius * 0.75f;
                b.rt.anchoredPosition = new Vector2(ox, oy);

                // Пульсация размера
                float sizePulse = 1f + Mathf.Sin(t * d.pulseSpeed + d.phase) * 0.09f;
                b.rt.sizeDelta = new Vector2(d.baseSize, d.baseSize) * sizePulse;

                // Пульсация альфы + цветовой дрейф
                float drift = Mathf.Sin(t * 0.06f + d.phase * 1.7f) * d.colorDrift;

                var c = d.color;
                c.r = Mathf.Clamp01(d.color.r + drift);
                c.g = Mathf.Clamp01(d.color.g - drift * 0.6f);
                c.b = Mathf.Clamp01(d.color.b + drift * 0.4f);
                c.a = Mathf.Clamp01(d.alphaBase
                    + Mathf.Sin(t * d.pulseSpeed * 0.85f + d.phase * 1.5f) * d.alphaAmp);

                b.img.color = c;
            }
        }
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

            PlayHoverSound();
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

        PlaySelectSound();

        MetroSession.SelectedLineId        = lv.data.id;
        MetroSession.SelectedLineName      = lv.data.name;
        MetroSession.SelectedLineColorKey  = lv.data.colorKey;
        MetroSession.SelectedLineColor     = lv.color;
        MetroSession.SelectedLineColorHex  = "#" + ColorUtility.ToHtmlStringRGB(lv.color);

        PlayerPrefs.SetString("Metro.SelectedLineId",       MetroSession.SelectedLineId);
        PlayerPrefs.SetString("Metro.SelectedLineName",     MetroSession.SelectedLineName);
        PlayerPrefs.SetString("Metro.SelectedLineColor",    MetroSession.SelectedLineColorKey);
        PlayerPrefs.SetString("Metro.SelectedLineColorHex", MetroSession.SelectedLineColorHex);
        PlayerPrefs.Save();

        StartCoroutine(TransitionRoutine(lv));
    }

    private IEnumerator TransitionRoutine(LineVisual lv)
    {
        hovered = null;

        float fadeDur = 0.7f;
        float t = 0f;
        Vector3 startScale = mapRoot.localScale;
        Vector3 endScale = Vector3.one * 1.15f;
        while (t < fadeDur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / fadeDur);
            k = 1f - Mathf.Pow(1f - k, 3f);

            mapRoot.localScale = Vector3.Lerp(startScale, endScale, k);
            fadeOverlay.color  = new Color(0, 0, 0, k);
            titleGroup.alpha   = 1f - k;
            brandingGroup.alpha = 1f - k;
            loadingGroup.alpha = k;

            yield return null;
        }
        mapRoot.localScale = endScale;
        fadeOverlay.color  = new Color(0, 0, 0, 1f);
        loadingGroup.alpha = 1f;

        yield return null;

        AsyncOperation loadOp = null;
        bool canLoad = !string.IsNullOrEmpty(gameSceneName);
        if (canLoad)
        {
            loadOp = SceneManager.LoadSceneAsync(gameSceneName, LoadSceneMode.Single);
            if (loadOp != null) loadOp.allowSceneActivation = false;
            else
            {
                Debug.LogError($"[MetroMapMenu] Не удалось начать загрузку сцены '{gameSceneName}'. Проверьте, что она добавлена в Build Settings.");
                canLoad = false;
            }
        }
        else
        {
            Debug.LogError("[MetroMapMenu] gameSceneName не задан — сцена не будет загружена.");
        }

        float minDisplay = Mathf.Max(0f, minLoadingDisplayTime);
        float elapsed = 0f;
        while (elapsed < minDisplay) { elapsed += Time.deltaTime; yield return null; }

        if (canLoad && loadOp != null)
        {
            while (loadOp.progress < 0.9f)
            {
                float p = Mathf.Clamp01(loadOp.progress / 0.9f);
                if (loadingText != null)
                    loadingText.text = $"ЗАГРУЗКА... {Mathf.RoundToInt(p * 100f)}%";
                yield return null;
            }

            if (loadingText != null)
                loadingText.text = "ЗАГРУЗКА... 100%";

            loadOp.allowSceneActivation = true;
        }
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
            brandingGroup.alpha = k;
            mapRoot.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, k);
            yield return null;
        }
        mapGroup.alpha = 1f; titleGroup.alpha = 1f; brandingGroup.alpha = 1f;
        mapRoot.localScale = Vector3.one;
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
    private static GameObject NewUI(string name, GameObject parent) => NewUI(name, parent.transform);
    private static GameObject NewUI(string name, RectTransform parent) => NewUI(name, (Transform)parent);

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