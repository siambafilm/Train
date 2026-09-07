using UnityEngine;

public class TrainMovement : MonoBehaviour
{
    [Header("Управление поворотом")]
    public float currentYawSpeed = 0f; // Текущая скорость поворота (градусы в секунду)

    void Update()
    {
        float speedKmh = TrainLever.Speed;
        float speedMs = speedKmh / 3.6f;
        float distanceThisFrame = speedMs * Time.deltaTime;

        // 1. Двигаем поезд строго туда, куда смотрит его СОБСТВЕННЫЙ нос (transform.forward)
        transform.position += transform.forward * distanceThisFrame;

        // 2. Вращаем поезд по горизонтали, если мы находимся на дуге поворота
        // Скорость вращения умножается на скорость поезда, чтобы на стоянке поезд не крутился на месте
        if (speedKmh > 0.1f)
        {
            transform.Rotate(Vector3.up * currentYawSpeed * Time.deltaTime * (speedKmh / 40f));
        }
    }

    // Эти методы будут вызывать невидимые зоны-триггеры на рельсах
    public void StartTurning(float turnDirection)
    {
        currentYawSpeed = turnDirection; // Например, 15f для поворота вправо, -15f для поворота влево
    }

    public void StopTurning()
    {
        currentYawSpeed = 0f; // Снова едем прямо
    }
}
