using UnityEngine;

public class TrainMovement : MonoBehaviour
{
    [Header("���������� ���������")]
    public float currentYawSpeed = 0f; // ������� �������� �������� (������� � �������)

    void Update()
{
    float speedKmh = TrainLever.Speed;
    float speedMs = speedKmh / 3.6f;
    
    // Умножаем дистанцию на направление реверсора (1, 0 или -1)
    // Если реверсор в нейтрали (0), то даже при накате поезд физически заблокирует движение
    float distanceThisFrame = speedMs * Time.deltaTime * TrainReverser.Direction;

    // Двигаем поезд строго по его локальной оси Z (вперед или назад)
    transform.position += transform.forward * distanceThisFrame;

    // Вращение в поворотах (умножаем на направление, чтобы задом поезд поворачивал корректно)
    if (speedKmh > 0.1f)
    {
        transform.Rotate(Vector3.up * currentYawSpeed * Time.deltaTime * (speedKmh / 40f) * TrainReverser.Direction);
    }
}


    // ��� ������ ����� �������� ��������� ����-�������� �� �������
    public void StartTurning(float turnDirection)
    {
        currentYawSpeed = turnDirection; // ��������, 15f ��� �������� ������, -15f ��� �������� �����
    }

    public void StopTurning()
    {
        currentYawSpeed = 0f; // ����� ���� �����
    }
}
