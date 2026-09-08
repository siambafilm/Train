using UnityEngine;

// Убираем интерфейс IClickable, теперь управление идет через скролл
public class TrainReverser : MonoBehaviour
{
    [Header("Состояние реверса")]
    public int currentReverserPosition = 0; // -1 - Назад, 0 - Нейтраль, 1 - Вперед

    [Header("Настройки визуала")]
    [SerializeField] private float rotationAngle = 30f; // Угол поворота тумблера

    // Статические переменные для доступа из других скриптов
    public static int Direction = 0; 
    public static string ReverserMode = "НЕЙТРАЛЬ";

    // Этот метод теперь вызывается из скрипта мыши при прокрутке колесика
    public void ChangeReverserPosition(int direction)
    {
        // Ограничиваем позиции строго от -1 (Назад) до 1 (Вперед)
        int newPosition = Mathf.Clamp(currentReverserPosition + direction, -1, 1);
        
        if (newPosition != currentReverserPosition)
        {
            currentReverserPosition = newPosition;
            UpdateReverserLogic();
        }
    }

    private void UpdateReverserLogic()
    {
        Direction = currentReverserPosition;

        // Поворачиваем 3D-модель тумблера локально по оси Y в зависимости от режима
        transform.localRotation = Quaternion.Euler(0, currentReverserPosition * rotationAngle, 0);

        // Обновляем текст для электронного табло
        switch (currentReverserPosition)
        {
            case 1: ReverserMode = "ВПЕРЕД"; break;
            case 0: ReverserMode = "НЕЙТРАЛЬ"; break;
            case -1: ReverserMode = "НАЗАД"; break;
        }

        Debug.Log($"[РЕВЕРСОР] Переключен в режим: {ReverserMode}");
    }
}
