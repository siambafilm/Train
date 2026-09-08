using UnityEngine;

public class TrainLever : MonoBehaviour
{
    [Header("��������� ������� ������")]
    public int currentPosition = 0;
    public float angleStep = 20f;

    [Header("������ ������ (������������)")]
    public float currentSpeed = 0f;
    public float maxSpeed = 80f;

    public static float Speed = 0f;
    public static string LeverMode = "�����";

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
    // Если реверсор в нейтрали, а мы пытаемся ехать — сбрасываем ход
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

    // Симуляция изменения скорости с учетом направления реверсора
    switch (currentPosition)
    {
        case 2:
            currentSpeed += 2.5f * Time.deltaTime;
            break;
        case 1:
            currentSpeed += 1.2f * Time.deltaTime;
            break;
        case 0:
            // На выбеге скорость плавно падает до нуля
            currentSpeed -= 0.1f * Time.deltaTime;
            break;
        case -1:
            currentSpeed -= 4.0f * Time.deltaTime;
            break;
    }

    currentSpeed = Mathf.Clamp(currentSpeed, 0f, maxSpeed);
    
    // Если скорость упала до 0 на выбеге или тормозе, а реверсор в нейтрали, жестко держим 0
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
