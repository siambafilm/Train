using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrainMovement : MonoBehaviour
{
    [Header("Состав поезда (голова -> хвост)")]
    [Tooltip("Первый элемент — локомотив (голова). Последний — хвостовой вагон. " +
             "Если пусто — соберётся автоматически из TrainCar в детях.")]
    [SerializeField] private List<TrainCar> cars = new List<TrainCar>();

    [Header("Настройки")]
    [SerializeField] private string markerTag = "TrainTrackMarker";
    [SerializeField] private float rotationLerpSpeed = 8f;

    [Header("Тряска при упоре в конец пути")]
    [SerializeField] private float shakeDuration = 0.5f;
    [SerializeField] private float shakeAmplitude = 0.15f;
    [SerializeField] private float shakeFrequency = 35f;

    // Смещения вагонов относительно головы (в метрах вдоль поезда)
    private float[] carOffsets;
    private float trainLength;

    // Трейл — пройденные точки пути (от старта к текущему положению головы)
    private readonly List<Vector3> trailPoints = new List<Vector3>();
    private readonly List<float>   trailDistances = new List<float>();

    // Текущее положение головы вдоль цепочки маркеров
    private TrainTrackMarker currentMarker;
    private TrainTrackMarker nextMarker;
    private float segmentLength;
    private float progress;
    private bool initialized = false;

    // Тряска
    private bool isShaking = false;
    private float shakeTimer = 0f;
    private Vector3 shakeBasePosition;
    private Quaternion shakeBaseRotation;

    public int CarsCount => cars.Count;

    void Start()
    {
        if (cars.Count == 0)
        {
            cars.AddRange(GetComponentsInChildren<TrainCar>());
            if (cars.Count == 0)
            {
                Debug.LogError("[TrainMovement] Не найден ни один TrainCar в детях!");
                return;
            }
        }

        // Прописываем каждому вагону ссылку на этот поезд
        foreach (var c in cars) c.ParentTrain = this;

        RecomputeCarOffsets();
        WireBackwardsLinks();
        if (!SnapToMarkers()) return;
        initialized = true;
    }

    void Update()
    {
        if (!initialized) return;

        // Обработка тряски
        if (isShaking)
        {
            shakeTimer += Time.deltaTime;
            if (shakeTimer >= shakeDuration)
            {
                isShaking = false;
                transform.localPosition = shakeBasePosition;
                transform.localRotation = shakeBaseRotation;
            }
            else
            {
                float t = shakeTimer / shakeDuration;
                float decay = 1f - t; // затухание
                float offsetX = Mathf.Sin(shakeTimer * shakeFrequency) * shakeAmplitude * decay;
                float offsetY = Mathf.Sin(shakeTimer * shakeFrequency * 1.3f) * shakeAmplitude * 0.5f * decay;
                float rotZ = Mathf.Sin(shakeTimer * shakeFrequency * 0.8f) * 2f * decay;

                transform.localPosition = shakeBasePosition + new Vector3(offsetX, offsetY, 0f);
                transform.localRotation = shakeBaseRotation * Quaternion.Euler(0f, 0f, rotZ);
            }
            // Во время тряски движение отключено (isShaking = true), но вагоны всё равно обновляем
            UpdateCars();
            return;
        }

        float speedMs = TrainLever.Speed / 3.6f;
        float ds = speedMs * Time.deltaTime * TrainReverser.Direction;
        if (Mathf.Abs(ds) > 0.00001f) Advance(ds);
        UpdateCars();
    }

    // -------------------------------------------------------------------
    //  Публичные методы для сцепки/расцепки на лету
    // -------------------------------------------------------------------
    public void AttachCar(TrainCar car)
    {
        if (car == null || cars.Contains(car)) return;
        cars.Add(car);
        car.ParentTrain = this;
        RecomputeCarOffsets();
        Debug.Log($"[TrainMovement] Вагон {car.name} добавлен в состав. Всего вагонов: {cars.Count}");
    }

    public void DetachCar(TrainCar car)
    {
        if (car == null) return;
        if (!cars.Contains(car)) return;

        // Нельзя отцепить голову (локомотив)
        if (cars.IndexOf(car) == 0)
        {
            Debug.LogWarning("[TrainMovement] Нельзя отцепить голову поезда!");
            return;
        }

        cars.Remove(car);
        car.ParentTrain = null;
        RecomputeCarOffsets();
        Debug.Log($"[TrainMovement] Вагон {car.name} удалён из состава. Всего вагонов: {cars.Count}");
    }

    // -------------------------------------------------------------------
    //  Инициализация
    // -------------------------------------------------------------------
    void RecomputeCarOffsets()
    {
        carOffsets = new float[cars.Count];
        carOffsets[0] = 0f;
        for (int i = 1; i < cars.Count; i++)
        {
            carOffsets[i] = carOffsets[i - 1] +
                Vector3.Distance(cars[i - 1].transform.position, cars[i].transform.position);
        }
        trainLength = cars.Count > 0 ? carOffsets[cars.Count - 1] : 0f;
    }

    void WireBackwardsLinks()
    {
        var all = FindObjectsByType<TrainTrackMarker>(FindObjectsSortMode.None);
        foreach (var m in all)
        {
            if (m.nextForward != null && m.nextForward.nextBackward == null)
                m.nextForward.nextBackward = m;
            if (m.nextForwardRight != null && m.nextForwardRight.nextBackward == null)
                m.nextForwardRight.nextBackward = m;
            if (m.nextForwardLeft != null && m.nextForwardLeft.nextBackward == null)
                m.nextForwardLeft.nextBackward = m;
        }
    }

    bool SnapToMarkers()
    {
        var all = FindObjectsByType<TrainTrackMarker>(FindObjectsSortMode.None);
        if (all.Length == 0)
        {
            Debug.LogError("[TrainMovement] Нет маркеров с тегом " + markerTag);
            return false;
        }

        Vector3 headPos = cars[0].transform.position;
        TrainTrackMarker closest = null;
        float minD = float.MaxValue;
        foreach (var m in all)
        {
            float d = Vector3.Distance(headPos, m.transform.position);
            if (d < minD) { minD = d; closest = m; }
        }
        currentMarker = closest;
        nextMarker = ChooseNext(currentMarker);

        if (nextMarker == null)
        {
            Debug.LogError($"[TrainMovement] У стартового маркера '{currentMarker.name}' нет следующего!");
            return false;
        }

        segmentLength = Vector3.Distance(currentMarker.transform.position, nextMarker.transform.position);
        progress = 0f;

        // Строим трейл назад длиной trainLength
        List<TrainTrackMarker> chain = new List<TrainTrackMarker> { currentMarker };
        float cumBack = 0f;
        TrainTrackMarker cursor = currentMarker;
        int guard = 0;
        while (cumBack < trainLength && cursor.nextBackward != null && guard++ < 500)
        {
            TrainTrackMarker prev = cursor.nextBackward;
            cumBack += Vector3.Distance(prev.transform.position, cursor.transform.position);
            chain.Insert(0, prev);
            cursor = prev;
        }

        trailPoints.Clear();
        trailDistances.Clear();
        float cum = 0f;
        for (int i = 0; i < chain.Count; i++)
        {
            if (i > 0)
                cum += Vector3.Distance(chain[i - 1].transform.position, chain[i].transform.position);
            trailPoints.Add(chain[i].transform.position);
            trailDistances.Add(cum);
        }

        Debug.Log($"[TrainMovement] Длина поезда: {trainLength:F1} м, длина трейла: {cum:F1} м");
        return true;
    }

    // -------------------------------------------------------------------
    //  Движение головы
    // -------------------------------------------------------------------
    void Advance(float ds)
    {
        if (ds > 0f) AdvanceForward(ds);
        else         AdvanceBackward(-ds);
    }

    void AdvanceForward(float amount)
    {
        int guard = 0;
        while (amount > 0f && guard++ < 200)
        {
            // Если впереди нет пути — УПОРА! Трясём и останавливаем.
            if (nextMarker == null)
            {
                TriggerShake();
                return;
            }

            float distToNext = (1f - progress) * segmentLength;
            if (amount < distToNext)
            {
                progress += amount / segmentLength;
                amount = 0f;
            }
            else
            {
                amount -= distToNext;
                trailPoints.Add(nextMarker.transform.position);
                trailDistances.Add(trailDistances[trailDistances.Count - 1] + segmentLength);

                currentMarker = nextMarker;
                nextMarker = ChooseNext(currentMarker);
                if (nextMarker == null)
                {
                    progress = 1f; // дошли до упора
                    TriggerShake();
                    return;
                }
                segmentLength = Vector3.Distance(currentMarker.transform.position, nextMarker.transform.position);
                progress = 0f;
            }
        }
    }

    void AdvanceBackward(float amount)
{
    int guard = 0;
    while (amount > 0f && guard++ < 200)
    {
        float distToCurrent = progress * segmentLength;
        if (amount < distToCurrent)
        {
            progress -= amount / segmentLength;
            amount = 0f;
        }
        else
        {
            amount -= distToCurrent;

            // Нельзя уехать назад дальше самого первого маркера — УПОР
            if (currentMarker == null || currentMarker.nextBackward == null)
            {
                progress = 0f;
                TriggerShake();
                return;
            }

            // «Откатываем» трейл: убираем точку, которую покидаем.
            // Без этой строки lastTrailDist перестаёт соответствовать currentMarker,
            // и вагоны уезжают на один сегмент вперёд.
            if (trailPoints.Count > 1)
            {
                trailPoints.RemoveAt(trailPoints.Count - 1);
                trailDistances.RemoveAt(trailDistances.Count - 1);
            }

            nextMarker = currentMarker;
            currentMarker = currentMarker.nextBackward;
            segmentLength = Vector3.Distance(currentMarker.transform.position,
                                             nextMarker.transform.position);
            progress = 1f;
        }
    }
}

    TrainTrackMarker ChooseNext(TrainTrackMarker from)
    {
        if (from == null) return null;
        return from.GetNextForward(SwitchButton.WantsToTurnRight);
    }

    // -------------------------------------------------------------------
    //  Тряска при упоре в конец пути
    // -------------------------------------------------------------------
    void TriggerShake()
    {
        if (isShaking) return;
        isShaking = true;
        shakeTimer = 0f;
        shakeBasePosition = transform.localPosition;
        shakeBaseRotation = transform.localRotation;

        // Обнуляем скорость через рычаг — поезд реально останавливается
        TrainLever.ForceStop();
        Debug.Log("[TrainMovement] УПОР! Путь закончился. Поезд остановлен с тряской.");
    }

    // -------------------------------------------------------------------
    //  Раскладка вагонов вдоль трейла
    // -------------------------------------------------------------------
    void UpdateCars()
    {
        if (trailDistances.Count == 0) return;

        float lastTrailDist = trailDistances[trailDistances.Count - 1];
        float headPathDist = lastTrailDist + progress * segmentLength;

        for (int i = 0; i < cars.Count; i++)
        {
            float targetDist = headPathDist - carOffsets[i];
            GetPosAndDir(targetDist, out Vector3 pos, out Vector3 fwd);

            cars[i].transform.position = pos;

            if (fwd.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(fwd, Vector3.up);
                cars[i].transform.rotation = Quaternion.Slerp(
                    cars[i].transform.rotation, targetRot, Time.deltaTime * rotationLerpSpeed);
            }
        }
    }

    void GetPosAndDir(float d, out Vector3 pos, out Vector3 fwd)
    {
        float lastTrailDist = trailDistances[trailDistances.Count - 1];

        // 1. Впереди последней точки трейла — интерполируем на текущем сегменте
        if (d >= lastTrailDist && nextMarker != null && currentMarker != null && segmentLength > 0.0001f)
        {
            float t = Mathf.Clamp01((d - lastTrailDist) / segmentLength);
            Vector3 a = currentMarker.transform.position;
            Vector3 b = nextMarker.transform.position;
            pos = Vector3.Lerp(a, b, t);
            fwd = b - a;
            if (fwd.sqrMagnitude < 0.0001f) fwd = cars[0].transform.forward;
            fwd.Normalize();
            return;
        }

        // 2. Перед началом трейла — экстраполируем назад (страховка)
        if (d <= 0f)
        {
            if (trailPoints.Count >= 2)
            {
                Vector3 dir = (trailPoints[0] - trailPoints[1]).normalized;
                pos = trailPoints[0] + dir * (-d);
                fwd = -dir;
            }
            else
            {
                pos = trailPoints[0];
                fwd = cars[0].transform.forward;
            }
            return;
        }

        // 3. Ищем сегмент внутри трейла
        int segIdx = 0;
        for (int k = 0; k < trailDistances.Count - 1; k++)
        {
            if (d >= trailDistances[k] && d <= trailDistances[k + 1]) { segIdx = k; break; }
        }

        float segLen = trailDistances[segIdx + 1] - trailDistances[segIdx];
        float segT = segLen > 0.0001f ? (d - trailDistances[segIdx]) / segLen : 0f;
        Vector3 p0 = trailPoints[segIdx];
        Vector3 p1 = trailPoints[segIdx + 1];
        pos = Vector3.Lerp(p0, p1, segT);
        Vector3 v = p1 - p0;
        if (v.sqrMagnitude > 0.0001f) { v.Normalize(); fwd = v; }
        else fwd = cars[0].transform.forward;
    }

    public void StartTurning(float direction) { }
    public void StopTurning() { }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        for (int i = 1; i < trailPoints.Count; i++)
            Gizmos.DrawLine(trailPoints[i - 1], trailPoints[i]);

        if (currentMarker != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(currentMarker.transform.position, 0.5f);
        }
        if (nextMarker != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(nextMarker.transform.position, 0.5f);
        }
    }
#endif
}