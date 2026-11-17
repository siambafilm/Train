using System.Collections;
using UnityEngine;

namespace PauseMenu
{
    public class PauseTrainMover : MonoBehaviour
    {
        [Header("Rails")]
        [SerializeField] private GameObject railsPrefab;
        [SerializeField] private bool stretchRailsToDistance = true; // если рельса — 1-unit сегмент вдоль +Z

        [Header("Train")]
        [SerializeField] private GameObject trainPrefab;

        [Header("Path (World Space)")]
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform stopPoint;

        [Header("Movement")]
        [SerializeField] private float baseSpeed = 25f;
        [Tooltip("Скорость как множитель от baseSpeed вдоль дистанции (0 — старт, 1 — стоп).")]
        [SerializeField] private AnimationCurve brakeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        [Tooltip("Доп. локальный угол наклона/поворота вагона.")]
        [SerializeField] private Vector3 tiltEuler = Vector3.zero;
        [SerializeField] private float safetyTimeout = 15f;

        [Header("Fall Down Exit")]
        [SerializeField] private float fallDuration = 0.7f;
        [Tooltip("На сколько метров вниз уронить поезд и рельсы. Должно хватить, чтобы скрылось из вида камеры.")]
        [SerializeField] private float fallDistance = 30f;
        [SerializeField] private float fallTilt = 25f;
        [SerializeField] private AnimationCurve fallCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private GameObject _trainInstance;
        private GameObject _railsInstance;

        public IEnumerator FallDown()
        {
            var targets = new System.Collections.Generic.List<Transform>(2);
            if (_trainInstance != null) targets.Add(_trainInstance.transform);
            if (_railsInstance != null) targets.Add(_railsInstance.transform);
            if (targets.Count == 0) yield break;

            int n = targets.Count;
            var startPos = new Vector3[n];
            var startRot = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                startPos[i] = targets[i].position;
                startRot[i] = targets[i].rotation;
            }

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

                for (int i = 0; i < n; i++)
                {
                    if (targets[i] == null) continue;
                    targets[i].position = startPos[i] + Vector3.down * (fallDistance * e);
                    targets[i].rotation = startRot[i] * Quaternion.Euler(fallTilt * e, 0f, 0f);
                }
                yield return null;
            }
        }
        public void ResetTrain()
        {
            if (_trainInstance != null) Destroy(_trainInstance);
            if (_railsInstance != null) Destroy(_railsInstance);
            _trainInstance = null;
            _railsInstance = null;
        }

        public IEnumerator PlayTrainRide()
        {
            if (trainPrefab == null || startPoint == null || stopPoint == null)
            {
                Debug.LogError("[PauseTrainMover] Не назначены trainPrefab / startPoint / stopPoint.");
                yield break;
            }

            SpawnRails();
            SpawnTrain();

            Vector3 a = startPoint.position;
            Vector3 b = stopPoint.position;
            float distance = Vector3.Distance(a, b);
            if (distance < 0.001f) yield break;

            Vector3 dir = (b - a).normalized;
            Quaternion baseRot = Quaternion.LookRotation(dir, Vector3.up);
            _trainInstance.transform.rotation = baseRot * Quaternion.Euler(tiltEuler);

            AnimationCurve curve = (brakeCurve == null || brakeCurve.length == 0)
                ? AnimationCurve.Linear(0f, 1f, 1f, 0f)
                : brakeCurve;

            float traveled = 0f;
            float elapsed = 0f;

            while (traveled < distance && elapsed < safetyTimeout)
            {
                elapsed += Time.unscaledDeltaTime;

                float progress = traveled / distance;
                float speedMul = Mathf.Max(0f, curve.Evaluate(progress));
                traveled += baseSpeed * speedMul * Time.unscaledDeltaTime;

                float p = Mathf.Clamp01(traveled / distance);
                _trainInstance.transform.position = Vector3.LerpUnclamped(a, b, p);
                _trainInstance.transform.rotation = baseRot * Quaternion.Euler(tiltEuler);

                yield return null;
            }

            _trainInstance.transform.position = b;
        }

        private void SpawnRails()
        {
            if (railsPrefab == null) return;

            Vector3 a = startPoint.position;
            Vector3 b = stopPoint.position;
            Vector3 delta = b - a;
            float len = delta.magnitude;
            if (len < 0.001f) return;

            Vector3 mid = a + delta * 0.5f;
            Quaternion rot = Quaternion.LookRotation(delta.normalized, Vector3.up);

            _railsInstance = Instantiate(railsPrefab, mid, rot);

            if (stretchRailsToDistance)
            {
                var s = _railsInstance.transform.localScale;
                s.z = len;
                _railsInstance.transform.localScale = s;
            }
        }

        private void SpawnTrain()
        {
            _trainInstance = Instantiate(trainPrefab, startPoint.position, Quaternion.identity);
        }
    }
}