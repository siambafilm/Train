using UnityEngine;

public class SwitchButton : MonoBehaviour, IClickable
{
    [SerializeField] private string buttonName = "TrackSwitch";

    // Статическая переменная: true — направо, false — налево
    public static bool SwitchDirectionRight = true; 

    public void OnClick()
    {
        // Анимация кнопки
        transform.localPosition -= new Vector3(0, 0, 0.05f);
        Invoke(nameof(ResetButton), 0.2f);

        // Инвертируем направление стрелки
        SwitchDirectionRight = !SwitchDirectionRight;
        
        Debug.Log($"[СТРЕЛКА] Направление переключено: {(SwitchDirectionRight ? "НАПРАВО" : "НАЛЕВО")}");
    }

    private void ResetButton()
    {
        transform.localPosition += new Vector3(0, 0, 0.05f);
    }
}
