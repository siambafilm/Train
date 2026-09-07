using UnityEngine;

public class DoorAnimator : MonoBehaviour
{
    [Header("Ссылки на 3D-модели створок")]
    [SerializeField] private Transform leftDoorMesh;
    [SerializeField] private Transform rightDoorMesh;

    [Header("Настройки анимации")]
    [SerializeField] private float openDistance = 1.2f; // На сколько метров отъезжает дверь по оси X
    [SerializeField] private float animationSpeed = 3f;  // Скорость открытия

    private Vector3 leftDoorClosedPos;
    private Vector3 rightDoorClosedPos;

    private Vector3 leftDoorOpenPos;
    private Vector3 rightDoorOpenPos;

    void Start()
    {
        if (leftDoorMesh == null || rightDoorMesh == null)
        {
            Debug.LogError("Забудьте! Вы не привязали створки дверей в скрипт DoorAnimator!");
            return;
        }

        // Запоминаем начальные (закрытые) позиции створок
        leftDoorClosedPos = leftDoorMesh.localPosition;
        rightDoorClosedPos = rightDoorMesh.localPosition;

        // Рассчитываем открытые позиции (смещаем по локальной оси X влево и вправо)
        leftDoorOpenPos = leftDoorClosedPos - new Vector3(openDistance, 0, 0);
        rightDoorOpenPos = rightDoorClosedPos + new Vector3(openDistance, 0, 0);
    }

    void Update()
    {
        // Плавно двигаем левую створку в зависимости от статической переменной из TrainButton
        Vector3 targetLeftPos = TrainButton.LeftDoorsOpen ? leftDoorOpenPos : leftDoorClosedPos;
        leftDoorMesh.localPosition = Vector3.Lerp(leftDoorMesh.localPosition, targetLeftPos, Time.deltaTime * animationSpeed);

        // Плавно двигаем правую створку
        Vector3 targetRightPos = TrainButton.RightDoorsOpen ? rightDoorOpenPos : rightDoorClosedPos;
        rightDoorMesh.localPosition = Vector3.Lerp(rightDoorMesh.localPosition, targetRightPos, Time.deltaTime * animationSpeed);
    }
}
