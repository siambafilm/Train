using UnityEngine;

public class CouplingButton : MonoBehaviour, IClickable
{
    [SerializeField] private string couplingAction; // "Couple" или "Decouple"
    [SerializeField] private float maxCoupleDistance = 3.0f; // Максимальная дистанция для сцепки (3 метра)

    private TrainCar mainTrainCar;

    void Start()
    {
        // Ищем скрипт TrainCar на головном поезде
        // Предполагается, что камера находится внутри главного поезда
        mainTrainCar = GetComponentInParent<TrainCar>();
        if (mainTrainCar == null)
        {
            // Если камера на Main Camera, ищем объект поезда по тегу или типу
            mainTrainCar = FindFirstObjectByType<TrainMovement>().GetComponent<TrainCar>();
        }
    }

    public void OnClick()
    {
        // Анимация кнопки
        transform.localPosition -= new Vector3(0, 0, 0.05f);
        Invoke(nameof(ResetButton), 0.2f);

        if (mainTrainCar == null) return;

        if (couplingAction == "Couple")
        {
            // Пробуем прицепить вагон сзади головного
            mainTrainCar.TryCoupleRearCar(maxCoupleDistance);
        }
        else if (couplingAction == "Decouple")
        {
            // Отцепляем вагон сзади головного
            mainTrainCar.DecoupleRearCar();
        }
    }

    private void ResetButton()
    {
        transform.localPosition += new Vector3(0, 0, 0.05f);
    }
}
