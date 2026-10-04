using UnityEngine;

public class TrainLever : MonoBehaviour
{
    [Header("Параметры позиции рычага")]
    public int currentPosition = 0;
    public float angleStep = 20f;

    [Header("Параметры скорости (локальные)")]
    public float currentSpeed = 0f;
    public float maxSpeed = 80f;

    // Статические поля — доступны из других скриптов
    public static float Speed = 0f;
    public static string LeverMode = "ВЫБЕГ";

    // Статическая ссылка на единственный экземпляр рычага
    public static TrainLever Instance { get; private set; }

    void Awake()
    {
        // Регистрируем себя как единственный Instance
        if (Instance == null)
            Instance = this;
        else
            Debug.LogWarning("[TrainLever] Обнаружено несколько TrainLever на сцене!");
    }

    // Находим общее количество прицепленных вагонов в цепочке
    int GetTotalCarsCount()
    {
        int count = 1;
        var movement = FindFirstObjectByType<TrainMovement>();
        if (movement == null) return count;
        TrainCar current = movement.GetComponent<TrainCar>();
        while (current != null && current.attachedRearCar != null)
        {
            count++;
            current = current.attachedRearCar;
        }
        return count;
    }

    /// <summary>
    /// Принудительно обнуляет скорость и возвращает рычаг в нейтраль.
    /// Используется при упоре в тупик. Вызывается статически из TrainMovement.
    /// </summary>
    public static void ForceStop()
    {
        if (Instance == null)
        {
            Debug.LogWarning("[TrainLever] ForceStop: Instance не найден!");
            Speed = 0f;
            LeverMode = "ХОД 0";
            return;
        }

        // Сбрасываем нестатические поля через Instance
        Instance.currentPosition = 0;
        Instance.currentSpeed = 0f;
        Instance.UpdateLeverVisual(); // чтобы рычаг визуально вернулся в нейтраль

        // Сбрасываем статические поля
        Speed = 0f;
        LeverMode = "ХОД 0";

        Debug.Log("[TrainLever] Принудительная остановка — рычаг в нуле.");
    }

    void Update()
    {
        bool anyDoorOpen = TrainButton.LeftDoorsOpen || TrainButton.RightDoorsOpen;

        // --- ИНЖЕНЕРНАЯ БЕЗОПАСНОСТЬ: ДВЕРИ ---
        if (anyDoorOpen && currentPosition > 0)
        {
            currentPosition = 0;
            UpdateLeverVisual();
            Debug.LogWarning("БЛОКИРОВКА ТЯГИ: Двери открыты! Разгон невозможен.");
        }

        // --- ИНЖЕНЕРНАЯ БЕЗОПАСНОСТЬ: РЕВЕРСОР ---
        if (TrainReverser.Direction == 0 && currentPosition > 0)
        {
            currentPosition = 0;
            UpdateLeverVisual();
            Debug.LogWarning("БЛОКИРОВКА ТЯГИ: Реверсор установлен в НЕЙТРАЛЬ!");
        }

        // Настройка текста для табло
        if (anyDoorOpen && currentPosition == 0)
        {
            LeverMode = "БЛОК. ТЯГИ (ДВЕРИ)";
        }
        else if (TrainReverser.Direction == 0 && currentPosition > 0)
        {
            LeverMode = "БЛОК. ТЯГИ (РЕВЕРС)";
        }
        else
        {
            switch (currentPosition)
            {
                case 2: LeverMode = "ХОД - 2"; break;
                case 1: LeverMode = "ХОД - 1"; break;
                case 0: LeverMode = "ВЫБЕГ"; break;
                case -1: LeverMode = "ТОРМОЖЕНИЕ"; break;
            }
        }

        // Симуляция изменения скорости
        switch (currentPosition)
        {
            case 2:  currentSpeed += 2.5f * Time.deltaTime; break;
            case 1:  currentSpeed += 1.2f * Time.deltaTime; break;
            case 0:  currentSpeed -= 0.1f * Time.deltaTime; break;
            case -1: currentSpeed -= 4.0f * Time.deltaTime; break;
        }

        currentSpeed = Mathf.Clamp(currentSpeed, 0f, maxSpeed);
        if (currentSpeed < 0.01f) currentSpeed = 0f;
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