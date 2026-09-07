using UnityEngine;

public class TrainButton : MonoBehaviour, IClickable
{
    [SerializeField] private string buttonName;

    // Статические переменные, чтобы к ним легко было достучаться из любого скрипта
    public static bool LeftDoorsOpen = false;
    public static bool RightDoorsOpen = false;

    public void OnClick()
    {
        // Анимация нажатия кнопки
        transform.localPosition -= new Vector3(0, 0, 0.05f);
        Invoke(nameof(ResetButton), 0.2f);

        // Переключаем состояние дверей (если были открыты — закроются, и наоборот)
        if (buttonName == "B_Left")
        {
            LeftDoorsOpen = !LeftDoorsOpen;
            Debug.Log($"Левые двери теперь: {(LeftDoorsOpen ? "ОТКРЫТЫ" : "ЗАКРЫТЫ")}");
        }
        else if (buttonName == "B_Right")
        {
            RightDoorsOpen = !RightDoorsOpen;
            Debug.Log($"Правые двери теперь: {(RightDoorsOpen ? "ОТКРЫТЫ" : "ЗАКРЫТЫ")}");
        }
    }

    private void ResetButton()
    {
        transform.localPosition += new Vector3(0, 0, 0.05f);
    }
}
