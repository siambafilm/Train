using UnityEngine;

public class TrainCar : MonoBehaviour
{
    [Header("Инженерные характеристики")]
    public float carMass = 25000f;

    [Header("Точки сцепки (для визуала/логики)")]
    [SerializeField] private Transform rearCouplingPoint;
    [SerializeField] private Transform frontCouplingPoint;

    [Header("Связи (заполняются TrainMovement)")]
    public TrainCar attachedRearCar;

    /// <summary>Ссылка на родительский TrainMovement (заполняется при старте).</summary>
    public TrainMovement ParentTrain { get; set; }

    void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Автоматически находим родителя-поезд
        ParentTrain = GetComponentInParent<TrainMovement>();
    }

    /// <summary>
    /// Пытается прицепить вагон сзади в радиусе maxDistance.
    /// Возвращает true, если сцепка успешна.
    /// </summary>
    public bool TryCoupleRearCar(float maxDistance)
    {
        if (attachedRearCar != null)
        {
            Debug.LogWarning($"[TrainCar] {name}: сзади уже прицеплен {attachedRearCar.name}");
            return false;
        }

        // Определяем точку поиска — rearCouplingPoint или позиция объекта
        Vector3 searchPoint = rearCouplingPoint != null
            ? rearCouplingPoint.position
            : transform.position;

        // Ищем все TrainCar на сцене
        var allCars = FindObjectsByType<TrainCar>(FindObjectsSortMode.None);
        TrainCar closest = null;
        float minD = float.MaxValue;

        foreach (var c in allCars)
        {
            if (c == this) continue;
            if (c.ParentTrain != null && c.ParentTrain == ParentTrain) continue; // уже в нашем поезде

            Vector3 otherPoint = c.frontCouplingPoint != null
                ? c.frontCouplingPoint.position
                : c.transform.position;

            float d = Vector3.Distance(searchPoint, otherPoint);
            if (d < minD && d <= maxDistance)
            {
                minD = d;
                closest = c;
            }
        }

        if (closest == null)
        {
            Debug.Log($"[TrainCar] {name}: нет вагонов в радиусе {maxDistance} м");
            return false;
        }

        // Прицепляем
        attachedRearCar = closest;
        if (ParentTrain != null)
        {
            ParentTrain.AttachCar(closest);
        }
        Debug.Log($"[TrainCar] {name}: прицеплен вагон {closest.name} (дистанция {minD:F2} м)");
        return true;
    }

    /// <summary>Отцепляет последний вагон сзади.</summary>
    public void DecoupleRearCar()
    {
        if (attachedRearCar == null)
        {
            Debug.LogWarning($"[TrainCar] {name}: сзади ничего не прицеплено");
            return;
        }

        TrainCar detached = attachedRearCar;
        attachedRearCar = null;

        if (ParentTrain != null)
        {
            ParentTrain.DetachCar(detached);
        }
        Debug.Log($"[TrainCar] {name}: отцеплен вагон {detached.name}");
    }
}