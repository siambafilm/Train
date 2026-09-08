using UnityEngine;

public class TrainCar : MonoBehaviour
{
    [Header("Инженерные характеристики")]
    public float carMass = 25000f; // Масса одного вагона в кг (25 тонн)

    [Header("Точки сцепки")]
    [Tooltip("Пустой объект на задней части вагона, где находится сцепка")]
    [SerializeField] private Transform rearCouplingPoint;
    [Tooltip("Пустой объект на передней части вагона, где находится сцепка")]
    [SerializeField] private Transform frontCouplingPoint;

    [Header("Связи (Автоматически)")]
    public TrainCar attachedRearCar; // Вагон, прицепленный СЗАДИ
    private FixedJoint activeJoint;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        // Настраиваем базовую массу этого вагона
        rb.mass = carMass;
        
        // Отключаем гравитацию, если настраивали её ранее для движения, фиксируем углы
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
    }

    // Метод ПРИЦЕПЛЕНИЯ
    public bool TryCoupleRearCar(float maxDistance)
    {
        if (attachedRearCar != null || rearCouplingPoint == null) return false;

        // Ищем все вагоны поблизости
        TrainCar[] allCars = FindObjectsByType<TrainCar>(FindObjectsSortMode.None);
        TrainCar closestCar = null;
        float closestDistance = maxDistance;

        foreach (TrainCar otherCar in allCars)
        {
            if (otherCar == this || otherCar.frontCouplingPoint == null) continue;

            // Считаем расстояние между ЗАДНЕЙ сцепкой этого вагона и ПЕРЕДНЕЙ сцепкой другого
            float dist = Vector3.Distance(rearCouplingPoint.position, otherCar.frontCouplingPoint.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                closestCar = otherCar;
            }
        }

        // Если нашли вагон на допустимой дистанции — сцепляем!
        if (closestCar != null)
        {
            attachedRearCar = closestCar;

            // Создаем физический сустав FixedJoint (жесткая сцепка)
            activeJoint = gameObject.AddComponent<FixedJoint>();
            activeJoint.connectedBody = closestCar.GetComponent<Rigidbody>();
            
            // Важная инженерная фишка: физика Unity сама посчитает общую массу связанных тел,
            // но для нашего скрипта рычага/разгона мы тоже можем пересчитать поведение или логи
            Debug.Log($"[СЦЕПКА] Вагон {closestCar.name} успешно прицеплен к {gameObject.name}!");
            return true;
        }

        Debug.LogWarning("[СЦЕПКА] Рядом нет вагонов для прицепления!");
        return false;
    }

    // Метод ОТЦЕПЛЕНИЯ (работает и на ходу)
    public void DecoupleRearCar()
    {
        if (attachedRearCar == null)
        {
            Debug.Log("[СЦЕПКА] Сзади ничего не прицеплено.");
            return;
        }

        Debug.Log($"[СЦЕПКА] Отцепляем вагон {attachedRearCar.name} на ходу!");

        // Уничтожаем физическую связь
        if (activeJoint != null)
        {
            Destroy(activeJoint);
        }

        attachedRearCar = null;
    }
}
