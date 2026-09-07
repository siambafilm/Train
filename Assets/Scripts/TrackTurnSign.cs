using UnityEngine;

public class TrackTurnSign : MonoBehaviour
{
    [Header("Сила и направление поворота")]
    [Tooltip("Положительное число — вправо, отрицательное — влево. 0 — стоп поворот.")]
    [SerializeField] private float turnIntensity = 15f;
    [SerializeField] private bool isEndVector = false; // Если поставить true, этот триггер будет выпрямлять поезд

    private void OnTriggerEnter(Collider other)
    {
        // Проверяем, что сквозь триггер проехал именно поезд
        TrainMovement train = other.GetComponentInParent<TrainMovement>();
        if (train != null)
        {
            if (isEndVector)
            {
                train.StopTurning();
                Debug.Log("Поезд вышел на прямую траекторию.");
            }
            else
            {
                train.StartTurning(turnIntensity);
                Debug.Log($"Поезд зашел в поворот с интенсивностью: {turnIntensity}");
            }
        }
    }
}
