using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace PauseMenu
{
    public class PauseMenuController : MonoBehaviour
    {
        public static PauseMenuController Instance { get; private set; }

        [Header("Refs")]
        [SerializeField] private GameObject pauseMenuRoot;
        [SerializeField] private PauseTrainMover trainMover;
        [SerializeField] private PauseMenuUIBuilder uiBuilder;
        [SerializeField] private Camera menuCamera;

        [Header("Gameplay camera (авто-поиск)")]
        [Tooltip("Отключать игровую камеру на время паузы (экономия GPU). " +
                 "Камера берётся из Camera.allCameras в момент нажатия Esc — назначать ничего не нужно.")]
        [SerializeField] private bool disableGameplayCameraWhilePaused = false;

        [Header("Input")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

        [Header("Exit")]
        [Tooltip("Страховка: если exit-анимация почему-то не уложилась, принудительно закрываем.")]
        [SerializeField] private float exitSafetyTimeout = 3f;

        [Header("Snapshot Background")]
        [Tooltip("Если включено — фоном паузы становится последний кадр активной игровой камеры (с затемнением).")]
        [SerializeField] private bool useSnapshotBackground = true;
        [Tooltip("На каком расстоянии от камеры паузы ставится полноэкранный квад со снимком.")]
        [SerializeField] private float snapshotQuadDistance = 1f;
        [Tooltip("Тинт снимка. RGB — затемнение (0.35 = тёмный), A — прозрачность (оставь 1).")]
        [SerializeField] private Color snapshotTint = new Color(0.35f, 0.35f, 0.35f, 1f);

        [Header("HUD")]
        [Tooltip("Скрывать TrainHUD (точку и меню) на время паузы и восстанавливать после.")]
        [SerializeField] private bool hideHudDuringPause = true;

        [Header("Cursor")]
        [Tooltip("Показывать и разлочивать курсор во время паузы, восстанавливать после.")]
        [SerializeField] private bool manageCursorDuringPause = true;

        public bool IsPaused { get; private set; }

        private Coroutine _openRoutine;
        private Coroutine _closeRoutine;
        private bool _isClosing;

        private RenderTexture _snapshotRT;
        private GameObject _snapshotQuad;
        private Material _snapshotMaterial;

        private Camera _pausedGameplayCamera;

        // Сохранённое состояние TrainHUD
        private bool _hudStateSaved;
        private bool _savedDotVisible;
        private bool _savedMenuVisible;
        private bool _savedHotkeyActive;

        // Сохранённое состояние курсора
        private CursorLockMode _savedCursorLockMode;
        private bool _savedCursorVisible;

        private bool MenuCameraIsSeparate =>
            menuCamera != null && !menuCamera.transform.IsChildOf(pauseMenuRoot.transform);

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Debug.LogWarning("[PauseMenuController] На сцене несколько PauseMenuController — оставь один!");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (_snapshotRT != null)       { _snapshotRT.Release(); Destroy(_snapshotRT); _snapshotRT = null; }
            if (_snapshotMaterial != null) { Destroy(_snapshotMaterial); _snapshotMaterial = null; }
            if (_snapshotQuad != null)     { Destroy(_snapshotQuad);     _snapshotQuad     = null; }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) Toggle();
        }

        public void Toggle()
        {
            if (IsPaused) CloseMenu();
            else          OpenMenu();
        }

        public void OpenMenu()
        {
            if (IsPaused || _isClosing) return;
            
            // НОВОЕ: не открываем паузу поверх большой карты навигатора
            var nav = MetroNavigator.Instance;
            if (nav != null && nav.IsFullMapOpen) return;

            IsPaused = true;

            // 1. Курсор — сразу, чтобы игрок мог работать с меню.
            if (manageCursorDuringPause) ApplyPauseCursor();

            // 2. Прячем HUD — до снимка/анимаций.
            HideHudForPause();

            // 3. Актуальная игровая камера.
            _pausedGameplayCamera = GetActiveGameplayCamera();

            if (useSnapshotBackground) CaptureSnapshot(_pausedGameplayCamera);

            Time.timeScale = 0f;

            if (disableGameplayCameraWhilePaused && _pausedGameplayCamera != null)
                _pausedGameplayCamera.enabled = false;

            if (MenuCameraIsSeparate) menuCamera.gameObject.SetActive(true);

            pauseMenuRoot.SetActive(true);

            if (_openRoutine != null) StopCoroutine(_openRoutine);
            _openRoutine = StartCoroutine(OpenSequence());
        }

        public void CloseMenu()
        {
            if (!IsPaused || _isClosing) return;
            IsPaused = false;
            _isClosing = true;

            if (_openRoutine != null) { StopCoroutine(_openRoutine); _openRoutine = null; }
            StopAllCoroutines();

            if (uiBuilder != null) uiBuilder.CancelRunningAnimations();

            _closeRoutine = StartCoroutine(CloseSequence());
        }

        private IEnumerator OpenSequence()
        {
            if (trainMover != null) yield return trainMover.PlayTrainRide();
            if (uiBuilder != null)  yield return uiBuilder.RevealFan();
            _openRoutine = null;
        }

        private IEnumerator CloseSequence()
        {
            bool trainDone = trainMover == null;
            bool uiDone    = uiBuilder == null;

            if (trainMover != null) StartCoroutine(RunAndFlag(trainMover.FallDown(),   () => trainDone = true));
            if (uiBuilder != null)  StartCoroutine(RunAndFlag(uiBuilder.FallDownAll(), () => uiDone    = true));

            float elapsed = 0f;
            while ((!trainDone || !uiDone) && elapsed < exitSafetyTimeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (trainMover != null) trainMover.ResetTrain();
            if (uiBuilder != null)  uiBuilder.HideAll();

            pauseMenuRoot.SetActive(false);
            if (MenuCameraIsSeparate) menuCamera.gameObject.SetActive(false);

            Time.timeScale = 1f;

            if (disableGameplayCameraWhilePaused && _pausedGameplayCamera != null)
                _pausedGameplayCamera.enabled = true;

            _pausedGameplayCamera = null;

            RestoreHudAfterPause();

            if (manageCursorDuringPause) RestoreCursorAfterPause();

            _isClosing = false;
            _closeRoutine = null;
        }

        private IEnumerator RunAndFlag(IEnumerator routine, System.Action onDone)
        {
            yield return StartCoroutine(routine);
            onDone?.Invoke();
        }

        // ---------- HUD save / restore ----------

        private void HideHudForPause()
        {
            if (!hideHudDuringPause) return;
            if (TrainHUD.Instance == null) return;

            _savedDotVisible   = TrainHUD.Instance.IsDotVisible;
            _savedMenuVisible  = TrainHUD.Instance.IsMenuVisible;
            _savedHotkeyActive = TrainHUD.Instance.HotkeyEnabled;
            _hudStateSaved     = true;

            TrainHUD.Instance.SetHotkeyActive(false);
            TrainHUD.Instance.HideAll();
        }

        private void RestoreHudAfterPause()
        {
            if (!_hudStateSaved) return;
            _hudStateSaved = false;

            if (TrainHUD.Instance == null) return;

            TrainHUD.Instance.SetDotVisible(_savedDotVisible);
            TrainHUD.Instance.SetMenuVisible(_savedMenuVisible);
            TrainHUD.Instance.SetHotkeyActive(_savedHotkeyActive);
        }

        // ---------- Cursor ----------

        private void ApplyPauseCursor()
        {
            _savedCursorLockMode = Cursor.lockState;
            _savedCursorVisible  = Cursor.visible;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        private void RestoreCursorAfterPause()
        {
            Cursor.lockState = _savedCursorLockMode;
            Cursor.visible   = _savedCursorVisible;
        }

        // ---------- Active gameplay camera lookup ----------

        private Camera GetActiveGameplayCamera()
        {
            Camera best = null;
            var cams = Camera.allCameras;
            foreach (var cam in cams)
            {
                if (cam == null) continue;
                if (cam == menuCamera) continue;
                if (pauseMenuRoot != null && cam.transform.IsChildOf(pauseMenuRoot.transform)) continue;
                if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) continue;

                if (best == null || cam.depth > best.depth)
                    best = cam;
            }
            return best;
        }

        // ---------- Snapshot background ----------

        private void CaptureSnapshot(Camera sourceCamera)
        {
            if (sourceCamera == null || menuCamera == null)
            {
                if (useSnapshotBackground)
                    Debug.LogWarning("[PauseMenuController] Активная игровая камера не найдена — snapshot-фон пропущен.");
                return;
            }

            int w = Mathf.Max(1, Screen.width);
            int h = Mathf.Max(1, Screen.height);
            var format = (sourceCamera.allowHDR && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR))
                ? RenderTextureFormat.DefaultHDR
                : RenderTextureFormat.ARGB32;

            if (_snapshotRT == null ||
                _snapshotRT.width != w ||
                _snapshotRT.height != h ||
                _snapshotRT.format != format)
            {
                if (_snapshotRT != null)
                {
                    _snapshotRT.Release();
                    Destroy(_snapshotRT);
                }
                _snapshotRT = new RenderTexture(w, h, 24, format) { name = "PauseSnapshotRT" };
                _snapshotRT.Create();
            }

            if (_snapshotMaterial != null)
                _snapshotMaterial.mainTexture = null;

            bool wasEnabled = sourceCamera.enabled;
            sourceCamera.enabled = true;

            var prevTarget = sourceCamera.targetTexture;
            sourceCamera.targetTexture = _snapshotRT;
            sourceCamera.Render();
            sourceCamera.targetTexture = prevTarget;

            sourceCamera.enabled = wasEnabled;

            EnsureSnapshotQuad();
            UpdateSnapshotMaterial();
            PositionQuadInFrontOfCamera(_snapshotQuad, menuCamera, snapshotQuadDistance);
            _snapshotQuad.SetActive(true);
        }

        private void EnsureSnapshotQuad()
        {
            if (_snapshotQuad != null) return;

            _snapshotQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _snapshotQuad.name = "PauseSnapshotBackground";

            var col = _snapshotQuad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            _snapshotMaterial = new Material(shader) { name = "PauseSnapshotMat" };
            _snapshotMaterial.renderQueue = 1000;

            var mr = _snapshotQuad.GetComponent<MeshRenderer>();
            mr.material = _snapshotMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            int layer = LayerMask.NameToLayer("PauseMenu");
            if (layer != -1) _snapshotQuad.layer = layer;
            else Debug.LogWarning("[PauseMenuController] Слой 'PauseMenu' не найден. Создай его в Project Settings → Tags and Layers.");

            _snapshotQuad.transform.SetParent(pauseMenuRoot.transform, false);
        }

        private void UpdateSnapshotMaterial()
        {
            if (_snapshotMaterial == null || _snapshotRT == null) return;
            _snapshotMaterial.mainTexture = _snapshotRT;
            if (_snapshotMaterial.HasProperty("_Color"))
                _snapshotMaterial.color = snapshotTint;
        }

        private void PositionQuadInFrontOfCamera(GameObject quad, Camera cam, float distance)
        {
            float dist = Mathf.Max(distance, cam.nearClipPlane + 0.01f);

            var t = quad.transform;
            t.SetPositionAndRotation(
                cam.transform.position + cam.transform.forward * dist,
                cam.transform.rotation);

            float height, width;
            if (cam.orthographic)
            {
                height = cam.orthographicSize * 2f;
                width  = height * cam.aspect;
            }
            else
            {
                height = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                width  = height * cam.aspect;
            }

            t.localScale = new Vector3(width * 1.05f, height * 1.05f, 1f);
        }
    }
}