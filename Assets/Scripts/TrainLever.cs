using UnityEngine;

public class TrainLever : MonoBehaviour
{
    [Header("Настройки позиций рычага")]
    public int currentPosition = 0;
    public float angleStep = 20f;

    [Header("Физика поезда (Реалистичная)")]
    public float currentSpeed = 0f;
    public float maxSpeed = 80f;

    public static float Speed = 0f;
    public static string LeverMode = "ВЫБЕГ";

    void Update()
    {
        // --- ИНЖЕНЕРНАЯ БЕЗОПАСНОСТЬ ---
        // Проверяем, открыты ли какие-либо двери
        bool anyDoorOpen = TrainButton.LeftDoorsOpen || TrainButton.RightDoorsOpen;

        // Если двери открыты, а машинист пытается ехать (Ход 1 или Ход 2)
        if (anyDoorOpen && currentPosition > 0)
        {
            currentPosition = 0; // Принудительно сбрасываем рычаг в Выбег
            UpdateLeverVisual(); // Обновляем наклон рычага на пульте
            Debug.LogWarning("БЛОКИРОВКА ТЯГИ: Двери открыты! Разгон невозможен.");
        }

        // Изменяем текстовый режим для табло, если сработала блокировка
        if (anyDoorOpen && currentPosition == 0)
        {
            LeverMode = "БЛОК. ТЯГИ (ДВЕРИ)";
        }
        else
        {
            // Обычная логика отображения режимов
            switch (currentPosition)
            {
                case 2: LeverMode = "ХОД - 2"; break;
                case 1: LeverMode = "ХОД - 1"; break;
                case 0: LeverMode = "ВЫБЕГ"; break;
                case -1: LeverMode = "ТОРМОЖЕНИЕ"; break;
            }
        }

        // Симуляция изменения скорости (с учетом блокировки)
        switch (currentPosition)
        {
            case 2:
                currentSpeed += 2.5f * Time.deltaTime;
                break;
            case 1:
                currentSpeed += 1.2f * Time.deltaTime;
                break;
            case 0:
                currentSpeed -= 0.1f * Time.deltaTime;
                break;
            case -1:
                currentSpeed -= 4.0f * Time.deltaTime;
                break;
        }

        currentSpeed = Mathf.Clamp(currentSpeed, 0f, maxSpeed);
        Speed = currentSpeed;
    }


    public void ChangePosition(int direction)
    {
        int newPosition = Mathf.Clamp(currentPosition + direction, -1, 2);

        if (newPosition != currentPosition)
        {
            currentPosition = newPosition;
            UpdateLeverVisual();
        }
    }

    private void UpdateLeverVisual()
    {
        float targetAngle = currentPosition * angleStep;
        transform.localRotation = Quaternion.Euler(targetAngle, 0, 0);
    }
}
