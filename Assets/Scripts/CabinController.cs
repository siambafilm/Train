using UnityEngine;

public class CabinController : MonoBehaviour
{
    private Camera cam;

    [Header("Настройки камеры")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float verticalLookLimit = 60f; // Ограничение взгляда вверх/вниз

    [Header("Настройки прицела (Точки)")]
    [SerializeField] private Color dotColor = Color.white;
    [SerializeField] private float dotSize = 4f;

    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        cam = GetComponent<Camera>();

        // Блокируем курсор
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Запоминаем стартовый поворот камеры, чтобы не было резкого прыжка при старте
        rotationX = transform.localEulerAngles.x;
        rotationY = transform.localEulerAngles.y;
    }

    void Update()
    {
        // 1. Полноценный обзор мышью во все стороны
        rotationY += Input.GetAxis("Mouse X") * mouseSensitivity;
        rotationX -= Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Ограничиваем взгляд по вертикали, чтобы нормально смотреть на пульт под ногами
        rotationX = Mathf.Clamp(rotationX, -verticalLookLimit, verticalLookLimit);

        // Применяем вращение
        transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0f);

        // 2. Взаимодействие с кабиной (ЛКМ)
        if (Input.GetMouseButtonDown(0))
        {
            ProcessClick();
        }

        // 3. Взаимодействие с рычагом через колесико мыши
        ProcessLeverScroll();
    }

    private void ProcessClick()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        // Тестовый луч в окне Scene (виден во время игры, если включить Gizmos)
        Debug.DrawRay(ray.origin, ray.direction * 2.0f, Color.red, 1f);

        if (Physics.Raycast(ray, out hit, 5.0f))
        {
            // ЭТА СТРОКА НАПИШЕТ, КУДА МЫ ПОПАЛИ:
            Debug.Log($"Рейкаст попал в объект: {hit.collider.gameObject.name}");

            IClickable clickable = hit.collider.GetComponent<IClickable>();
            if (clickable != null)
            {
                clickable.OnClick();
            }
            else
            {
                Debug.LogWarning($"На объекте {hit.collider.gameObject.name} НЕТ скрипта TrainButton!");
            }
        }
        else
        {
            Debug.Log("Кликнули, но луч ни во что не попал (слишком далеко или нет коллайдера)");
        }
    }

    private void ProcessLeverScroll()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 5.0f)) // Дистанция 5 метров, как у кнопок
        {
            // 1. Проверяем, крутится ли колесико вообще
            float scroll = Input.GetAxis("Mouse ScrollWheel");

            if (scroll != 0f)
            {
                // Этот лог покажет, видит ли Unity скролл мыши над объектом
                Debug.Log($"Колесико крутится над объектом: {hit.collider.gameObject.name}. Скролл = {scroll}");

                // 2. Ищем скрипт рычага
                TrainLever lever = hit.collider.GetComponent<TrainLever>();

                if (lever != null)
                {
                    if (scroll > 0f) lever.ChangePosition(1);
                    if (scroll < 0f) lever.ChangePosition(-1);
                }
                else
                {
                    // Этот лог ругнется, если коллайдер есть, а скрипта TrainLever на нем НЕТ
                    Debug.LogWarning($"Колесико крутится над {hit.collider.gameObject.name}, но на нем НЕТ скрипта TrainLever!");
                }
            }
        }
    }



    // Рисуем точку строго по центру экрана
    private void OnGUI()
    {
        float xMin = (Screen.width / 2f) - (dotSize / 2f);
        float yMin = (Screen.height / 2f) - (dotSize / 2f);

        Texture2D dotTexture = Texture2D.whiteTexture;
        GUI.color = dotColor;
        GUI.DrawTexture(new Rect(xMin, yMin, dotSize, dotSize), dotTexture);

        // --- НОВЫЙ КОД: ЭЛЕКТРОННОЕ ТАБЛО МАШИНИСТА ---

        // Создаем подложку (черное полупрозрачное окошко слева сверху)
        GUI.color = new Color(0, 0, 0, 0.6f); // Черный цвет с альфой 60%
        GUI.DrawTexture(new Rect(20, 20, 300, 150), Texture2D.whiteTexture);

        // Настройки шрифта
        GUI.color = Color.green; // Текст будет ядовито-зеленым, как на старых дисплеях метро
        GUI.skin.label.fontSize = 16;
        GUI.skin.label.fontStyle = FontStyle.Bold;

        // Выводим данные на табло
        string speedText = $"СКОРОСТЬ: {TrainLever.Speed:F1} км/ч";
        string modeText = $"РЕЖИМ: {TrainLever.LeverMode}";
        string leftDoorsText = $"ЛЕВЫЕ ДВЕРИ: {(TrainButton.LeftDoorsOpen ? "ОТКРЫТЫ" : "ЗАКРЫТЫ")}";
        string rightDoorsText = $"ПРАВЫЕ ДВЕРИ: {(TrainButton.RightDoorsOpen ? "ОТКРЫТЫ" : "ЗАКРЫТЫ")}";

        // Рисуем строчки друг под другом
        GUI.Label(new Rect(35, 30, 280, 30), speedText);
        GUI.Label(new Rect(35, 60, 280, 30), modeText);

        // Для дверей сделаем подсветку: если открыты — пусть текст горит оранжевым/красным
        GUI.color = TrainButton.LeftDoorsOpen ? Color.red : Color.green;
        GUI.Label(new Rect(35, 90, 280, 30), leftDoorsText);

        GUI.color = TrainButton.RightDoorsOpen ? Color.red : Color.green;
        GUI.Label(new Rect(35, 120, 280, 30), rightDoorsText);
    }
}
