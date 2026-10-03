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

    // Смещения вагонов относительно головы (в метрах вдоль поезда)
    private float[] carOffsets;
    private float trainLength;

    // Трейл — пройденные точки пути (от старта к текущему положению головы)
    private readonly List<Vector3> trailPoints = new List<Vector3>();
    private readonly List<float>   trailDistances = new List<float>();

    // Текущее положение головы вдоль цепочки маркеров
    private TrainTrackMarker currentMarker; // маркер, который голова только что прошла
    private TrainTrackMarker nextMarker;    // маркер, к которому голова едет
    private float segmentLength;            // длина текущего сегмента current -> next
    private float progress;                 // 0..1 по сегменту

    private bool initialized = false;

    // Публичный доступ к составу (используется TrainLever)
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

        ComputeCarOffsets();
        WireBackwardsLinks();

        if (!SnapToMarkers()) return;

        initialized = true;
    }

    void Update()
    {
        if (!initialized) return;

        // Скорость положительная, реверсор задаёт знак
        float speedMs = TrainLever.Speed / 3.6f;
        float ds = speedMs * Time.deltaTime * TrainReverser.Direction;

        if (Mathf.Abs(ds) > 0.00001f) Advance(ds);
        UpdateCars();
    }

    // -------------------------------------------------------------------
    //  Инициализация
    // -------------------------------------------------------------------

    void ComputeCarOffsets()
    {
        carOffsets = new float[cars.Count];
        carOffsets[0] = 0f;
        for (int i = 1; i < cars.Count; i++)
        {
            carOffsets[i] = carOffsets[i - 1] +
                Vector3.Distance(cars[i - 1].transform.position, cars[i].transform.position);
        }
        trainLength = carOffsets[cars.Count - 1];
    }

    /// <summary>Прописываем каждой стрелке ветки назад (для движения задним ходом).</summary>
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

    /// <summary>Ставим голову на ближайший маркер и строим трейл назад длиной trainLength.</summary>
    bool SnapToMarkers()
    {
        var all = FindObjectsByType<TrainTrackMarker>(FindObjectsSortMode.None);
        if (all.Length == 0)
        {
            Debug.LogError("[TrainMovement] Нет маркеров с тегом " + markerTag);
            return false;
        }

        // 1. Ближайший к голове маркер
        Vector3 headPos = cars[0].transform.position;
        TrainTrackMarker closest = null;
        float minD = float.MaxValue;
        foreach (var m in all)
        {
            float d = Vector3.Distance(headPos, m.transform.position);
            if (d < minD) { minD = d; closest = m; }
        }
        currentMarker = closest;

        // 2. Следующий маркер с учётом стрелок
        nextMarker = ChooseNext(currentMarker);
        if (nextMarker == null)
        {
            Debug.LogError($"[TrainMovement] У стартового маркера '{currentMarker.name}' нет следующего!");
            return false;
        }
        segmentLength = Vector3.Distance(currentMarker.transform.position, nextMarker.transform.position);
        progress = 0f;

        // 3. Строим трейл назад — от currentMarker по nextBackward пока не наберём trainLength
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
            float distToNext = (1f - progress) * segmentLength;

            if (amount < distToNext)
            {
                progress += amount / segmentLength;
                amount = 0f;
            }
            else
            {
                // Дошли до nextMarker — пришпиливаем его в трейл
                amount -= distToNext;
                trailPoints.Add(nextMarker.transform.position);
                trailDistances.Add(trailDistances[trailDistances.Count - 1] + segmentLength);

                currentMarker = nextMarker;
                nextMarker = ChooseNext(currentMarker);
                if (nextMarker == null) { progress = 0f; return; }
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

                // Откатываем последний маркер из трейла
                if (trailPoints.Count > 1)
                {
                    trailPoints.RemoveAt(trailPoints.Count - 1);
                    trailDistances.RemoveAt(trailDistances.Count - 1);
                }

                nextMarker = currentMarker;
                currentMarker = currentMarker.nextBackward;
                if (currentMarker == null) { progress = 0f; return; }
                segmentLength = Vector3.Distance(currentMarker.transform.position, nextMarker.transform.position);
                progress = 1f;
            }
        }
    }

    TrainTrackMarker ChooseNext(TrainTrackMarker from)
    {
        if (from == null) return null;
        // Намерение машиниста, выбранное кнопкой в кабине
        return from.GetNextForward(SwitchButton.WantsToTurnRight);
    }

    // -------------------------------------------------------------------
    //  Раскладка вагонов вдоль трейла
    // -------------------------------------------------------------------

    void UpdateCars()
    {
        float lastTrailDist = trailDistances[trailDistances.Count - 1];
        float headPathDist  = lastTrailDist + progress * segmentLength;

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

    /// <summary>Позиция и направление на расстоянии d от начала трейла.</summary>
    void GetPosAndDir(float d, out Vector3 pos, out Vector3 fwd)
    {
        float lastTrailDist = trailDistances[trailDistances.Count - 1];

        // 1. Впереди последней точки трейла — интерполируем на текущем сегменте
        if (d >= lastTrailDist && nextMarker != null && segmentLength > 0.0001f)
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
        float segT   = segLen > 0.0001f ? (d - trailDistances[segIdx]) / segLen : 0f;

        Vector3 p0 = trailPoints[segIdx];
        Vector3 p1 = trailPoints[segIdx + 1];
        pos = Vector3.Lerp(p0, p1, segT);

        Vector3 v = p1 - p0;
        if (v.sqrMagnitude > 0.0001f) { v.Normalize(); fwd = v; }
        else fwd = cars[0].transform.forward;
    }

    // Заглушки — оставлены на случай, если какие-то скрипты ещё их вызывают
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