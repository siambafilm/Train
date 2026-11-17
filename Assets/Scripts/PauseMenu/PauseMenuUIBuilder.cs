using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace PauseMenu
{
    public class PauseMenuUIBuilder : MonoBehaviour
    {
        [Header("Items (заглушки)")]
        [SerializeField] private string[] itemLabels = { "Продолжить", "Настройки", "Управление", "Заново", "Выход" };
        [SerializeField] private Vector2 buttonSize = new Vector2(260f, 70f);

        [Header("Fan Layout (в референсных пикселях)")]
        [SerializeField] private float fanRadius = 700f;
        [Range(0f, 180f)]
        [SerializeField] private float fanAngle = 90f;
        [SerializeField] private Vector2 fanCenterOffset = new Vector2(0f, -250f);

        [Header("Fan Animation")]
        [SerializeField] private float itemRevealDuration = 0.35f;
        [SerializeField] private float delayBetweenItems = 0.08f;
        [SerializeField] private AnimationCurve revealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Style")]
        [SerializeField] private Font font;
        [SerializeField] private int fontSize = 28;
        [SerializeField] private Color panelColor = new Color(0.06f, 0.07f, 0.09f, 0.9f);
        [SerializeField] private Color textColor = Color.white;

        [Header("Canvas")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private int canvasSortingOrder = 100;

        [Header("Fan Direction")]
        [Tooltip("0 — вверх, 90 — вправо, -90 — влево, 180 — вниз. Промежуточные — по вкусу.")]
        [SerializeField] private float fanBaseAngle = -90f;

        [Header("Fall Down Exit")]
        [SerializeField] private float fallDuration = 0.6f;
        [Tooltip("На сколько пикселей вниз падают пункты. Должно хватать, чтобы уйти за нижний край экрана.")]
        [SerializeField] private float fallDistance = 1500f;
        [SerializeField] private float fallTilt = 18f;
        [SerializeField] private float fallStagger = 0.04f;
        [SerializeField] private AnimationCurve fallCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private bool fadeWhileFalling = true;

        private RectTransform _fanRoot;
        private readonly List<RectTransform> _items = new List<RectTransform>();

        public int ItemCount => _items.Count;

        private void Awake()
        {
            EnsureEventSystem();
            BuildUI();
            HideAll();
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("PauseMenuCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var fanGO = new GameObject("FanRoot", typeof(RectTransform));
            fanGO.transform.SetParent(canvasGO.transform, false);
            _fanRoot = (RectTransform)fanGO.transform;
            _fanRoot.anchorMin = _fanRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _fanRoot.pivot = new Vector2(0.5f, 0.5f);
            _fanRoot.anchoredPosition = fanCenterOffset;
            _fanRoot.sizeDelta = Vector2.zero;

            _items.Clear();
            for (int i = 0; i < itemLabels.Length; i++)
                _items.Add(CreateItem(i));
        }

        private RectTransform CreateItem(int index)
        {
            var go = new GameObject($"PauseItem_{index}",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
            go.transform.SetParent(_fanRoot, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = buttonSize;

            var img = go.GetComponent<Image>();
            img.color = panelColor;
            img.raycastTarget = true;

            var textGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(go.transform, false);
            var trt = (RectTransform)textGO.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            var txt = textGO.GetComponent<Text>();
            txt.font = ResolveFont();
            txt.text = itemLabels[index];
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = textColor;
            txt.fontSize = fontSize;
            txt.raycastTarget = false;

            var btn = go.GetComponent<Button>();
            int captured = index;
            btn.onClick.AddListener(() => OnItemClicked(captured));

            return rt;
        }

        private Font ResolveFont()
        {
            if (font != null) return font;
#if UNITY_2022_2_OR_NEWER
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }

        private void OnItemClicked(int index)
        {
            Debug.Log($"[PauseMenu] Клик по пункту '{itemLabels[index]}' (index {index}). " +
                      "Привяжи здесь реальную логику.");
        }

        /// <summary>Мгновенно сбрасывает все элементы в «невидимое» состояние перед открытием.</summary>
        public void HideAll()
        {
            foreach (var it in _items)
            {
                it.localScale = Vector3.zero;
                it.anchoredPosition = Vector2.zero;
                it.localRotation = Quaternion.identity;
                var cg = it.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
            }
        }

        /// <summary>Останавливает все внутренние анимации (AnimateItem), запущенные на этом билдере.</summary>
        public void CancelRunningAnimations()
        {
            StopAllCoroutines();
        }

        public IEnumerator RevealFan()
        {
            int n = _items.Count;
            if (n == 0) yield break;

            for (int i = 0; i < n; i++)
            {
                StartCoroutine(AnimateItem(i, n));
                if (i < n - 1)
                    yield return new WaitForSecondsRealtime(delayBetweenItems);
            }

            yield return new WaitForSecondsRealtime(itemRevealDuration);
        }

        /// <summary>Плавно роняет все пункты вниз и завершается, когда все уже вне кадра.</summary>
        public IEnumerator FallDownAll()
        {
            int n = _items.Count;
            if (n == 0) yield break;

            int remaining = n;
            for (int i = 0; i < n; i++)
            {
                StartCoroutine(FallItem(i, () => remaining--));
                if (i < n - 1 && fallStagger > 0f)
                    yield return new WaitForSecondsRealtime(fallStagger);
            }

            while (remaining > 0) yield return null;
        }

        private IEnumerator AnimateItem(int index, int total)
        {
            var rt = _items[index];

            float t01 = total <= 1 ? 0.5f : (float)index / (total - 1);
            float spread = Mathf.Lerp(-fanAngle * 0.5f, fanAngle * 0.5f, t01);
            float angle  = fanBaseAngle + spread;
            float rad    = angle * Mathf.Deg2Rad;

            Vector2 target = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * fanRadius;
            Quaternion startRot = Quaternion.Euler(0f, 0f, -angle);
            Quaternion endRot   = Quaternion.identity;

            AnimationCurve curve = (revealCurve == null || revealCurve.length == 0)
                ? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
                : revealCurve;

            float dur = Mathf.Max(0.0001f, itemRevealDuration);
            float elapsed = 0f;

            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float e = curve.Evaluate(t);

                rt.anchoredPosition = Vector2.LerpUnclamped(Vector2.zero, target, e);
                rt.localScale      = Vector3.one * e;
                rt.localRotation   = Quaternion.Slerp(startRot, endRot, e);
                yield return null;
            }

            rt.anchoredPosition = target;
            rt.localScale      = Vector3.one;
            rt.localRotation   = Quaternion.identity;
        }

        private IEnumerator FallItem(int index, System.Action onDone)
        {
            var rt = _items[index];
            var cg = rt.GetComponent<CanvasGroup>();

            Vector2 startPos = rt.anchoredPosition;
            Vector2 endPos   = startPos + Vector2.down * fallDistance;

            Quaternion startRot = rt.localRotation;
            float dirSign = (index % 2 == 0) ? 1f : -1f;
            Quaternion endRot = startRot * Quaternion.Euler(0f, 0f, fallTilt * dirSign);

            Vector3 startScale = rt.localScale;
            Vector3 endScale   = startScale * 0.65f;

            float startAlpha = cg != null ? cg.alpha : 1f;

            AnimationCurve curve = (fallCurve == null || fallCurve.length == 0)
                ? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
                : fallCurve;

            float dur = Mathf.Max(0.0001f, fallDuration);
            float elapsed = 0f;

            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float e = curve.Evaluate(t);

                rt.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, e);
                rt.localRotation    = Quaternion.Slerp(startRot, endRot, e);
                rt.localScale       = Vector3.LerpUnclamped(startScale, endScale, e);
                if (fadeWhileFalling && cg != null)
                    cg.alpha = Mathf.Lerp(startAlpha, 0f, e);

                yield return null;
            }

            rt.anchoredPosition = endPos;
            rt.localRotation    = endRot;
            rt.localScale       = endScale;
            if (cg != null) cg.alpha = 0f;

            onDone?.Invoke();
        }
    }
}