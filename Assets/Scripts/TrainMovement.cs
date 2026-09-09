using UnityEngine;

public class TrainMovement : MonoBehaviour
{
    [Header("Параметры поворота")]
    public float currentYawSpeed = 0f;          // Текущая скорость поворота (градусы/сек)
    private float targetYawSpeed = 0f;          // Целевая скорость поворота
    private float turnVelocity = 0f;            // Вспомогательная переменная для сглаживания

    [Header("Настройки плавности")]
    [Tooltip("Время сглаживания поворота (сек). Больше = медленнее и плавнее")]
    public float turnSmoothingTime = 1.5f;
    
    [Tooltip("Максимальная скорость поворота в градусах/сек")]
    public float maxTurnRate = 30f;
    
    [Tooltip("Скорость (км/ч), при которой поезд поворачивается с полной скоростью")]
    public float referenceSpeed = 40f;

    void Update()
    {
        float speedKmh = TrainLever.Speed;
        float speedMs = speedKmh / 3.6f;
        
        // Двигаем поезд строго по его локальной оси Z (вперёд или назад)
        float distanceThisFrame = speedMs * Time.deltaTime * TrainReverser.Direction;
        transform.position += transform.forward * distanceThisFrame;

        // Плавное изменение текущей скорости поворота к целевой
        // Это создаёт эффект инерции тяжёлого поезда
        currentYawSpeed = Mathf.SmoothDamp(
            currentYawSpeed, 
            targetYawSpeed, 
            ref turnVelocity, 
            turnSmoothingTime, 
            maxTurnRate
        );

        // Вращение в поворотах — пропорционально скорости движения
        // Чем медленнее едет поезд, тем медленнее он поворачивается
        if (speedKmh > 0.1f && Mathf.Abs(currentYawSpeed) > 0.01f)
        {
            // Влияние скорости на скорость поворота
            float turnInfluence = Mathf.Clamp01(speedKmh / referenceSpeed);
            float yawAmount = currentYawSpeed * turnInfluence * Time.deltaTime * TrainReverser.Direction;
            transform.Rotate(Vector3.up * yawAmount);
        }
    }

    /// <summary>
    /// Начать поворот. turnDirection — желаемая скорость поворота (градусы/сек),
    /// положительная = вправо, отрицательная = влево.
    /// Поезд плавно войдёт в поворот благодаря SmoothDamp.
    /// </summary>
    public void StartTurning(float turnDirection)
    {
        targetYawSpeed = Mathf.Clamp(turnDirection, -maxTurnRate, maxTurnRate);
    }

    /// <summary>
    /// Плавно прекратить поворот. Поезд вернётся к прямолинейному движению.
    /// </summary>
    public void StopTurning()
    {
        targetYawSpeed = 0f;
    }
}
