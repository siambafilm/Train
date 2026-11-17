using System.Collections;
using UnityEngine;

namespace PauseMenu
{
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private GameObject pauseMenuRoot;
        [SerializeField] private PauseTrainMover trainMover;
        [SerializeField] private PauseMenuUIBuilder uiBuilder;
        [SerializeField] private Camera menuCamera;

        [Header("Main camera (опционально)")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private bool disableMainCameraWhilePaused = false;

        [Header("Input")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

        [Header("Exit")]
        [Tooltip("Страховка: если exit-анимация почему-то не уложилась, принудительно закрываем.")]
        [SerializeField] private float exitSafetyTimeout = 3f;

        public bool IsPaused { get; private set; }

        private Coroutine _openRoutine;
        private Coroutine _closeRoutine;
        private bool _isClosing;

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

            Time.timeScale = 0f;
            if (disableMainCameraWhilePaused && mainCamera != null) mainCamera.enabled = false;
            if (MenuCameraIsSeparate) menuCamera.gameObject.SetActive(true);

            pauseMenuRoot.SetActive(true);

            if (_openRoutine != null) StopCoroutine(_openRoutine);
            _openRoutine = StartCoroutine(OpenSequence());
        }

        public void CloseMenu()
        {
            if (!IsPaused || _isClosing) return;
            IsPaused = false;
            _isClosing = true; // блокируем повторное открытие до конца анимации

            if (_openRoutine != null) { StopCoroutine(_openRoutine); _openRoutine = null; }
            StopAllCoroutines();

            // Гасим внутренние корутины UI-билдера (AnimateItem), они крутятся на нём.
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

            // Всё уже улетело вниз — чистим и выключаем.
            if (trainMover != null) trainMover.ResetTrain();
            if (uiBuilder != null)  uiBuilder.HideAll();

            pauseMenuRoot.SetActive(false);
            if (MenuCameraIsSeparate) menuCamera.gameObject.SetActive(false);

            Time.timeScale = 1f;
            if (disableMainCameraWhilePaused && mainCamera != null) mainCamera.enabled = true;

            _isClosing = false;
            _closeRoutine = null;
        }

        private IEnumerator RunAndFlag(IEnumerator routine, System.Action onDone)
        {
            yield return StartCoroutine(routine);
            onDone?.Invoke();
        }
    }
}