using UnityEngine;

public class TrainMovement : MonoBehaviour
{
    [Header("Настройки направления движения")]
    [SerializeField] private Vector3 moveDirection = Vector3.forward; // По умолчанию едем по стрелке Forward (синяя стрелка в Unity)

    void Update()
    {
        // 1. Берем текущую скорость из нашего скрипта рычага
        float speedKmh = TrainLever.Speed;

        // 2. Переводим скорость из км/ч в метры в секунду (инженерная классика: делим на 3.6)
        float speedMs = speedKmh / 3.6f;

        // 3. Рассчитываем смещение за этот кадр (скорость * время кадра)
        float distanceThisFrame = speedMs * Time.deltaTime;

        // 4. Двигаем поезд в локальном пространстве вперед
        // Использование TransformDirection позволяет поезду ехать туда, куда направлен его "нос" (даже если рельсы повернут)
        Vector3 localMove = transform.TransformDirection(moveDirection) * distanceThisFrame;

        transform.position += localMove;
    }
}
