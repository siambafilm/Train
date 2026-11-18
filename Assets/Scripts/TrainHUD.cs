using UnityEngine;

/// <summary>
/// Весь экранный UI: точка-прицел по центру + служебное меню.
/// Управление:
///   - Клавиша H (настраивается) — скрыть/показать меню
///   - Публичные методы — см. ниже
///
/// Примеры вызова из других скриптов:
///   TrainHUD.Instance.SetMenuVisible(false);   // выключить меню
///   TrainHUD.Instance.SetDotVisible(false);    // выключить точку
///   TrainHUD.Instance.ToggleAll();             // переключить всё разом
///   TrainHUD.Instance.ToggleMenu();            // переключить только меню
///   if (TrainHUD.Instance != null) TrainHUD.Instance.ToggleDot(); // безопасно
/// </summary>
public class TrainHUD : MonoBehaviour
{
    [Header("Прицел (точка в центре экрана)")]
    [SerializeField] private bool showDot = true;
    [SerializeField] private Color dotColor = Color.white;
    [SerializeField] private float dotSize = 4f;

    [Header("Служебное меню")]
    [SerializeField] private bool showMenu = true;

    [Header("Горячие клавиши")]
    [SerializeField] private bool useKeyboardShortcut = true;
    [SerializeField] private KeyCode toggleMenuKey = KeyCode.H;
    public static TrainHUD Instance { get; private set; }

    public bool IsDotVisible  => showDot;
    public bool IsMenuVisible => showMenu;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Debug.LogWarning("[TrainHUD] На сцене несколько TrainHUD — оставь один!");
    }


    // НОВОЕ: позволяет временно отключить реакцию на клавишу (например, во время паузы)
    private bool _hotkeyActive = true;
    public bool HotkeyEnabled => _hotkeyActive;
    public void SetHotkeyActive(bool active) => _hotkeyActive = active;

    void Update()
    {
        if (!useKeyboardShortcut || !_hotkeyActive) return;
        if (Input.GetKeyDown(toggleMenuKey))
            ToggleMenu();
    }

    // ---------- Публичное API ----------

    public void SetDotVisible(bool visible)  => showDot  = visible;
    public void SetMenuVisible(bool visible) => showMenu = visible;

    public void ToggleDot()  => showDot  = !showDot;
    public void ToggleMenu() => showMenu = !showMenu;

    /// <summary>Переключить всё разом (и точку, и меню).</summary>
    public void ToggleAll() { showDot = !showDot; showMenu = !showMenu; }

    /// <summary>Полностью скрыть весь UI.</summary>
    public void HideAll() { showDot = false; showMenu = false; }

    /// <summary>Показать весь UI.</summary>
    public void ShowAll() { showDot = true; showMenu = true; }

    // ---------- Отрисовка ----------

    private void OnGUI()
    {
        if (showDot)  DrawDot();
        if (showMenu) DrawMenu();
    }

    private void DrawDot()
    {
        float xMin = (Screen.width  / 2f) - (dotSize / 2f);
        float yMin = (Screen.height / 2f) - (dotSize / 2f);

        GUI.color = dotColor;
        GUI.DrawTexture(new Rect(xMin, yMin, dotSize, dotSize), Texture2D.whiteTexture);
    }

    private void DrawMenu()
    {
        // Полупрозрачная подложка (чуть выше, чтобы уместить подсказку снизу)
        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.DrawTexture(new Rect(20, 20, 300, 230), Texture2D.whiteTexture);

        // Базовый шрифт
        GUI.skin.label.fontSize = 16;
        GUI.skin.label.fontStyle = FontStyle.Bold;

        // Скорость
        GUI.color = Color.green;
        GUI.Label(new Rect(35, 30, 280, 30),
            $"СКОРОСТЬ: {TrainLever.Speed:F1} км/ч");

        // Режим тяги
        GUI.Label(new Rect(35, 60, 280, 30),
            $"РЕЖИМ: {TrainLever.LeverMode}");

        // Реверсор
        GUI.color = Color.white;
        GUI.Label(new Rect(35, 90, 280, 30),
            $"РЕВЕРСОР: {TrainReverser.ReverserMode}");

        // Двери
        GUI.color = TrainButton.LeftDoorsOpen ? Color.red : Color.green;
        GUI.Label(new Rect(35, 120, 280, 30),
            $"ЛЕВЫЕ ДВЕРИ: {(TrainButton.LeftDoorsOpen ? "ОТКРЫТЫ" : "ЗАКРЫТЫ")}");

        GUI.color = TrainButton.RightDoorsOpen ? Color.red : Color.green;
        GUI.Label(new Rect(35, 150, 280, 30),
            $"ПРАВЫЕ ДВЕРИ: {(TrainButton.RightDoorsOpen ? "ОТКРЫТЫ" : "ЗАКРЫТЫ")}");

        // Стрелка
        GUI.color = Color.cyan;
        GUI.Label(new Rect(35, 180, 280, 30),
            $"ВЫБОР СТРЕЛКИ: {(SwitchButton.WantsToTurnRight ? ">>> НАПРАВО" : "<<< НАЛЕВО")}");

        // Подсказка про H
        GUI.color = new Color(1f, 1f, 1f, 0.5f);
        GUI.skin.label.fontSize = 12;
        GUI.skin.label.fontStyle = FontStyle.Normal;
        GUI.Label(new Rect(35, 212, 280, 20),
            $"[{toggleMenuKey}] — скрыть/показать меню");
    }
}