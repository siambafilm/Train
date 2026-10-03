using UnityEngine;

public class TrainCar : MonoBehaviour
{
    [Header("Инженерные характеристики")]
    public float carMass = 25000f;

    [Header("Точки сцепки (для совместимости)")]
    [SerializeField] private Transform rearCouplingPoint;
    [SerializeField] private Transform frontCouplingPoint;

    [Header("Связи (заполняются TrainMovement)")]
    public TrainCar attachedRearCar;

    // Больше НЕ создаём Rigidbody и FixedJoint — вагоны двигаются кинематически
    // через TrainMovement. Если Rigidbody уже висит на объекте — делаем его кинематическим,
    // чтобы физика не боролась с transform.position.

    void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }
    }

    // Оставляем методы для совместимости с CouplingButton,
    // но они больше не используют физику.
    public bool TryCoupleRearCar(float maxDistance)   { return false; }
    public void DecoupleRearCar()                     { }
}