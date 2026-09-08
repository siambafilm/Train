using UnityEngine;

public class SwitchTrigger : MonoBehaviour
{
    [Header("Настройки интенсивности поворотов")]
    [Tooltip("Сила поворота, если стрелка переведена НАПРАВО")]
    [SerializeField] private float rightTurnIntensity = 15f;

    [Tooltip("Сила поворота, если стрелка переведена НАЛЕВО (обычно отрицательное число)")]
    [SerializeField] private float leftTurnIntensity = -15f;

    private void OnTriggerEnter(Collider other)
    {
        // Проверяем, что сквозь стрелку едет поезд
        TrainMovement train = other.GetComponentInParent<TrainMovement>();
        
        if (train != null)
        {
            // Считываем положение стрелки из кнопки в кабине прямо в момент наезда!
            if (SwitchButton.SwitchDirectionRight)
            {
                train.StartTurning(rightTurnIntensity);
                Debug.Log($"[РАЗВИЛКА] Поезд ушел НАПРАВО с силой {rightTurnIntensity}");
            }
            else
            {
                train.StartTurning(leftTurnIntensity);
                Debug.Log($"[РАЗВИЛКА] Поезд ушел НАЛЕВО с силой {leftTurnIntensity}");
            }
        }
    }
}
