using UnityEngine;

public class TrainReverser : MonoBehaviour
{
    [Header("Состояние реверса")]
    public int currentReverserPosition = 0; // -1 - Назад, 0 - Нейтраль, 1 - Вперед

    [Header("Настройки визуала")]
    [SerializeField] private float rotationAngle = 30f;

    [Header("Защита от переключения на ходу")]
    [Tooltip("Минимальная скорость (км/ч), при которой нельзя переключить реверсор")]
    [SerializeField] private float minSpeedToSwitch = 1.5f;

    // Статические переменные для доступа из других скриптов
    public static int Direction = 0;
    public static string ReverserMode = "НЕЙТРАЛЬ";

    public void ChangeReverserPosition(int direction)
    {
        int newPosition = Mathf.Clamp(currentReverserPosition + direction, -1, 1);
        if (newPosition == currentReverserPosition) return;

        // ЗАЩИТА: нельзя переключить реверсор на ходу
        float speed = Mathf.Abs(TrainLever.Speed);
        if (speed > minSpeedToSwitch)
        {
            Debug.LogWarning($"[РЕВЕРСОР] Нельзя переключить на ходу! Скорость: {speed:F1} км/ч. " +
                             $"Сначала остановитесь (порог {minSpeedToSwitch} км/ч).");
            return;
        }

        currentReverserPosition = newPosition;
        UpdateReverserLogic();
    }

    private void UpdateReverserLogic()
    {
        Direction = currentReverserPosition;
        transform.localRotation = Quaternion.Euler(0, currentReverserPosition * rotationAngle, 0);

        switch (currentReverserPosition)
        {
            case 1:  ReverserMode = "ВПЕРЕД";  break;
            case 0:  ReverserMode = "НЕЙТРАЛЬ"; break;
            case -1: ReverserMode = "НАЗАД";    break;
        }
        Debug.Log($"[РЕВЕРСОР] Переключен в режим: {ReverserMode}");
    }
}