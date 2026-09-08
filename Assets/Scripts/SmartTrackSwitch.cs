using UnityEngine;

public class SmartTrackSwitch : MonoBehaviour
{
    [Header("Ссылки на 3D-стрелки ЭТОГО знака")]
    [SerializeField] private GameObject leftArrowMesh;
    [SerializeField] private GameObject rightArrowMesh;

    [Header("Настройки поворота")]
    [SerializeField] private float rightTurnIntensity = 15f;
    [SerializeField] private float leftTurnIntensity = -15f;

    private bool isThisSwitchSetRight = true;
    private bool isPlayerInsidePreZone = false;

    void Update()
    {
        // Если игрок в зоне видимости, обновляем стрелки на знаке от пульта
        if (isPlayerInsidePreZone)
        {
            isThisSwitchSetRight = SwitchButton.WantsToTurnRight;
        }

        if (leftArrowMesh != null && rightArrowMesh != null)
        {
            rightArrowMesh.SetActive(isThisSwitchSetRight);
            leftArrowMesh.SetActive(!isThisSwitchSetRight);
        }
    }

    // --- МЕТОДЫ ДЛЯ ЗОНЫ ПРЕДВАРИТЕЛЬНОГО ВКЛЮЧЕНИЯ ЗНАКА ---
    public void PlayerEnteredPreZone()
    {
        isPlayerInsidePreZone = true;
        Debug.Log($"[{gameObject.name}] Поезд на подходе. Включаем индикацию знака.");
    }

    public void PlayerExitedPreZone()
    {
        isPlayerInsidePreZone = false;
        Debug.Log($"[{gameObject.name}] Поезд уехал. Выключаем индикацию.");
    }

    // --- МЕТОД ДЛЯ ФИНАЛЬНОГО ПОВОРОТА (Вызывается только на развилке) ---
    public void ExecuteTurn(TrainMovement train)
    {
        if (isThisSwitchSetRight)
        {
            train.StartTurning(rightTurnIntensity);
            Debug.Log($"[{gameObject.name}] ФИЗИЧЕСКИЙ ПОВОРОТ: Ушли НАПРАВО");
        }
        else
        {
            train.StartTurning(leftTurnIntensity);
            Debug.Log($"[{gameObject.name}] ФИЗИЧЕСКИЙ ПОВОРОТ: Ушли НАЛЕВО");
        }
    }
}
