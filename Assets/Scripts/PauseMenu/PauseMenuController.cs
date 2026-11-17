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

        public bool IsPaused { get; private set; }

        private Coroutine _openRoutine;

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
            if (IsPaused) return;
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
            if (!IsPaused) return;
            IsPaused = false;

            if (_openRoutine != null) { StopCoroutine(_openRoutine); _openRoutine = null; }
            StopAllCoroutines();

            if (trainMover != null) trainMover.ResetTrain();
            if (uiBuilder != null) uiBuilder.HideAll();

            pauseMenuRoot.SetActive(false);
            if (MenuCameraIsSeparate) menuCamera.gameObject.SetActive(false);

            Time.timeScale = 1f;
            if (disableMainCameraWhilePaused && mainCamera != null) mainCamera.enabled = true;
        }

        private IEnumerator OpenSequence()
        {
            if (trainMover != null) yield return trainMover.PlayTrainRide();
            if (uiBuilder != null)  yield return uiBuilder.RevealFan();
            _openRoutine = null;
        }
    }
}