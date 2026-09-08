using UnityEngine;

public class SwitchButton : MonoBehaviour, IClickable
{
    [SerializeField] private string buttonName = "TrackSwitch";

    // Теперь это просто намерение машиниста: true — хочу направо, false — хочу налево
    public static bool WantsToTurnRight = true; 

    public void OnClick()
    {
        transform.localPosition -= new Vector3(0, 0, 0.05f);
        Invoke(nameof(ResetButton), 0.2f);

        WantsToTurnRight = !WantsToTurnRight;
        
        Debug.Log($"[ПУЛЬТ] Машинист выбрал направление для следующей стрелки: {(WantsToTurnRight ? "НАПРАВО" : "НАЛЕВО")}");
    }

    private void ResetButton()
    {
        transform.localPosition += new Vector3(0, 0, 0.05f);
    }
}
