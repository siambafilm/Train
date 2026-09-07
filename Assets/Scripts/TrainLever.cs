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
        switch (currentPosition)
        {
            case 2: // ХОД 2 (Интенсивный разгон тяжелого поезда)
                currentSpeed += 2.5f * Time.deltaTime; // Было 15, стало 2.5 — теперь разгон займет около 30 секунд
                LeverMode = "ХОД - 2";
                break;
            case 1: // ХОД 1 (Плавный маневровый разгон)
                currentSpeed += 1.2f * Time.deltaTime; // Было 7, стало 1.2
                LeverMode = "ХОД - 1";
                break;
            case 0: // ВЫБЕГ (Катимся почти без потери скорости, как на идеальных рельсах)
                currentSpeed -= 0.1f * Time.deltaTime; // Медленное сопротивление воздуха
                LeverMode = "ВЫБЕГ";
                break;
            case -1: // ТОРМОЗ (Уверенное торможение пневматикой)
                currentSpeed -= 4.0f * Time.deltaTime; // Замедение до полной остановки
                LeverMode = "ТОРМОЖЕНИЕ";
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
