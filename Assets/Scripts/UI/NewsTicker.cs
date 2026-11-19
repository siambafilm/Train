using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class NewsTicker : MonoBehaviour
{
    public static NewsTicker Instance { get; private set; }

    [Header("Настройки внешнего вида")]
    [SerializeField] private float barHeight = 60f;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.85f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private int fontSize = 32;
    [SerializeField] private Font customFont;

    [Header("Настройки анимации и управления")]
    [SerializeField] private float scrollSpeed = 250f;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private bool allowEscapeToClose = true;

    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private RectTransform _bgRect;
    private RectTransform _textRect;
    private Text _tickerText;
    private Coroutine _activeRoutine;
    private bool _isClosing;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        CreateUI();
    }

    private void Update()
    {
        if (allowEscapeToClose && _activeRoutine != null && !_isClosing && Input.GetKeyDown(KeyCode.Escape))
            Hide();
    }

    private void CreateUI()
    {
        GameObject canvasObj = new GameObject("TickerCanvas");
        canvasObj.transform.SetParent(transform);
        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 999;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject bgObj = new GameObject("TickerBackground");
        bgObj.transform.SetParent(canvasObj.transform, false);

        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = backgroundColor;

        _canvasGroup = bgObj.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;

        bgObj.AddComponent<RectMask2D>();

        _bgRect = bgObj.GetComponent<RectTransform>();
        _bgRect.anchorMin = new Vector2(0f, 0f);
        _bgRect.anchorMax = new Vector2(1f, 0f);
        _bgRect.pivot = new Vector2(0.5f, 0f);
        _bgRect.anchoredPosition = Vector2.zero;
        _bgRect.sizeDelta = new Vector2(0f, barHeight);

        GameObject textObj = new GameObject("TickerText");
        textObj.transform.SetParent(bgObj.transform, false);

        _tickerText = textObj.AddComponent<Text>();
        _tickerText.font = customFont != null ? customFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _tickerText.fontSize = fontSize;
        _tickerText.color = textColor;
        _tickerText.alignment = TextAnchor.MiddleLeft;
        _tickerText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _tickerText.verticalOverflow = VerticalWrapMode.Overflow;

        _textRect = textObj.GetComponent<RectTransform>();
        _textRect.anchorMin = new Vector2(0f, 0.5f);
        _textRect.anchorMax = new Vector2(0f, 0.5f);
        _textRect.pivot = new Vector2(0f, 0.5f);
        _textRect.anchoredPosition = Vector2.zero;
    }

    public static void Show(string message, int repeatCount = 1, float speed = -1f)
    {
        if (repeatCount <= 0 || string.IsNullOrEmpty(message)) return;

        if (Instance == null)
        {
            GameObject go = new GameObject("NewsTickerManager");
            go.AddComponent<NewsTicker>();
        }

        Instance.StartTicker(message, repeatCount, speed > 0 ? speed : Instance.scrollSpeed);
    }

    public static void Hide()
    {
        if (Instance != null && Instance._activeRoutine != null && !Instance._isClosing)
            Instance.StartClosing();
    }

    private void StartTicker(string message, int repeatCount, float speed)
    {
        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        _isClosing = false;
        _activeRoutine = StartCoroutine(TickerRoutine(message, repeatCount, speed));
    }

    private void StartClosing()
    {
        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        _isClosing = true;
        _activeRoutine = StartCoroutine(CloseRoutine());
    }

    private IEnumerator TickerRoutine(string message, int repeatCount, float speed)
    {
        _tickerText.text = message;
        Canvas.ForceUpdateCanvases();

        // Ждём кадр, если layout ещё не разрешился (Show() вызван сразу после создания Canvas)
        if (_bgRect.rect.width < 1f)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        float textWidth  = _tickerText.preferredWidth;
        float screenWidth = _bgRect.rect.width;
        if (screenWidth < 1f) screenWidth = Screen.width; // fallback

        _textRect.anchoredPosition = new Vector2(screenWidth, 0f);

        yield return StartCoroutine(FadeCanvasGroup(_canvasGroup.alpha, 1f, fadeDuration));

        for (int i = 0; i < repeatCount; i++)
        {
            float startX = _bgRect.rect.width;
            if (startX < 1f) startX = screenWidth;
            float endX = -textWidth;

            _textRect.anchoredPosition = new Vector2(startX, 0f);

            while (_textRect.anchoredPosition.x > endX)
            {
                float newX = _textRect.anchoredPosition.x - speed * Time.unscaledDeltaTime;
                _textRect.anchoredPosition = new Vector2(newX, 0f);
                yield return null;
            }
        }

        _isClosing = true;
        yield return StartCoroutine(FadeCanvasGroup(1f, 0f, fadeDuration));
        _isClosing = false;
        _activeRoutine = null;
    }

    private IEnumerator CloseRoutine()
    {
        float currentAlpha = _canvasGroup.alpha;
        float adjustedDuration = fadeDuration * currentAlpha;
        yield return StartCoroutine(FadeCanvasGroup(currentAlpha, 0f, adjustedDuration));
        _isClosing = false;
        _activeRoutine = null;
    }

    private IEnumerator FadeCanvasGroup(float startAlpha, float endAlpha, float duration)
    {
        if (duration <= 0f) { _canvasGroup.alpha = endAlpha; yield break; }

        float elapsed = 0f;
        _canvasGroup.alpha = startAlpha;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            yield return null;
        }
        _canvasGroup.alpha = endAlpha;
    }
}