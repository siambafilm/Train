using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace PauseMenu
{
    public class PauseMenuController : MonoBehaviour
    {
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

        public bool IsPaused { get; private set; }

        private Coroutine _openRoutine;
        private Coroutine _closeRoutine;
        private bool _isClosing;

        private RenderTexture _snapshotRT;
        private GameObject _snapshotQuad;
        private Material _snapshotMaterial;

        // Камера, снятая в момент открытия паузы (1-е или 3-е лицо — что было активно).
        private Camera _pausedGameplayCamera;

        // Сохранённое состояние TrainHUD на момент открытия паузы.
        private bool _hudStateSaved;
        private bool _savedDotVisible;
        private bool _savedMenuVisible;
        private bool _savedHotkeyActive;

        private bool MenuCameraIsSeparate =>
            menuCamera != null && !menuCamera.transform.IsChildOf(pauseMenuRoot.transform);

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
            IsPaused = true;

            // 1. Прячем HUD — до снимка/анимаций, чтобы ничего не мигало.
            HideHudForPause();

            // 2. Актуальная игровая камера на этот момент (1-е/3-е лицо, любая активная).
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

            // Возвращаем включённость той же камере, которую выключали (если она ещё жива).
            if (disableGameplayCameraWhilePaused && _pausedGameplayCamera != null)
                _pausedGameplayCamera.enabled = true;

            _pausedGameplayCamera = null;

            // Восстанавливаем HUD ровно в том виде, в котором он был до паузы.
            RestoreHudAfterPause();

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

        // ---------- Active gameplay camera lookup ----------

        /// <summary>
        /// Возвращает активную игровую камеру: любую включённую Camera в сцене, кроме menuCamera
        /// и камер, вложенных в pauseMenuRoot. Именно это позволяет работать с CameraManager
        /// без ручного назначения в инспекторе.
        /// </summary>
        private Camera GetActiveGameplayCamera()
        {
            Camera best = null;
            var cams = Camera.allCameras; // уже отфильтрованы по enabled + activeInHierarchy
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
                    Debug.LogWarning("[PauseMenuController] Активная игровая камера не найдена — snapshot-фон пропущен. " +
                                     "Проверь, что хотя бы одна игровая Camera сейчас активна.");
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

        private void OnDestroy()
        {
            if (_snapshotRT != null)       { _snapshotRT.Release(); Destroy(_snapshotRT); _snapshotRT = null; }
            if (_snapshotMaterial != null) { Destroy(_snapshotMaterial); _snapshotMaterial = null; }
            if (_snapshotQuad != null)     { Destroy(_snapshotQuad);     _snapshotQuad     = null; }
        }
    }
}