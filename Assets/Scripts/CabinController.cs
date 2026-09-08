using UnityEngine;

public class CabinController : MonoBehaviour
{
    private Camera cam;

    [Header("��������� ������")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float verticalLookLimit = 60f; // ����������� ������� �����/����

    [Header("��������� ������� (�����)")]
    [SerializeField] private Color dotColor = Color.white;
    [SerializeField] private float dotSize = 4f;

    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        cam = GetComponent<Camera>();

        // ��������� ������
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // ���������� ��������� ������� ������, ����� �� ���� ������� ������ ��� ������
        rotationX = transform.localEulerAngles.x;
        rotationY = transform.localEulerAngles.y;
    }

    void Update()
    {
        // 1. ����������� ����� ����� �� ��� �������
        rotationY += Input.GetAxis("Mouse X") * mouseSensitivity;
        rotationX -= Input.GetAxis("Mouse Y") * mouseSensitivity;

        // ������������ ������ �� ���������, ����� ��������� �������� �� ����� ��� ������
        rotationX = Mathf.Clamp(rotationX, -verticalLookLimit, verticalLookLimit);

        // ��������� ��������
        transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0f);

        // 2. �������������� � ������� (���)
        if (Input.GetMouseButtonDown(0))
        {
            ProcessClick();
        }

        // 3. �������������� � ������� ����� �������� ����
        ProcessLeverScroll();
    }

    private void ProcessClick()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        // �������� ��� � ���� Scene (����� �� ����� ����, ���� �������� Gizmos)
        Debug.DrawRay(ray.origin, ray.direction * 2.0f, Color.red, 1f);

        if (Physics.Raycast(ray, out hit, 5.0f))
        {
            // ��� ������ �������, ���� �� ������:
            Debug.Log($"������� ����� � ������: {hit.collider.gameObject.name}");

            IClickable clickable = hit.collider.GetComponent<IClickable>();
            if (clickable != null)
            {
                clickable.OnClick();
            }
            else
            {
                Debug.LogWarning($"�� ������� {hit.collider.gameObject.name} ��� ������� TrainButton!");
            }
        }
        else
        {
            Debug.Log("��������, �� ��� �� �� ��� �� ����� (������� ������ ��� ��� ����������)");
        }
    }

    private void ProcessLeverScroll()
    {
        // Дистанция взаимодействия — 5 метров
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 5.0f))
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            
            if (scroll != 0f)
            {
                // 1. Проверяем, смотрим ли мы на главный РЫЧАГ ГАЗА
                TrainLever lever = hit.collider.GetComponent<TrainLever>();
                if (lever != null)
                {
                    if (scroll > 0f) lever.ChangePosition(1);  // Колесико вверх -> добавить ход
                    if (scroll < 0f) lever.ChangePosition(-1); // Колесико вниз -> тормоз
                    return; // Выходим из метода, так как объект найден
                }

                // 2. Проверяем, смотрим ли мы на ТУМБЛЕР РЕВЕРСОРА
                TrainReverser reverser = hit.collider.GetComponent<TrainReverser>();
                if (reverser != null)
                {
                    if (scroll > 0f) reverser.ChangeReverserPosition(1);  // Колесико вверх -> Вперед
                    if (scroll < 0f) reverser.ChangeReverserPosition(-1); // Колесико вниз -> Назад
                    return;
                }
            }
        }
    }




    // ������ ����� ������ �� ������ ������
    private void OnGUI()
    {
        float xMin = (Screen.width / 2f) - (dotSize / 2f);
        float yMin = (Screen.height / 2f) - (dotSize / 2f);

        Texture2D dotTexture = Texture2D.whiteTexture;
        GUI.color = dotColor;
        GUI.DrawTexture(new Rect(xMin, yMin, dotSize, dotSize), dotTexture);

        // --- ����� ���: ����������� ����� ��������� ---

        // ������� �������� (������ �������������� ������ ����� ������)
        GUI.color = new Color(0, 0, 0, 0.6f); // ������ ���� � ������ 60%
        GUI.DrawTexture(new Rect(20, 20, 300, 200), Texture2D.whiteTexture);

        // ��������� ������
        GUI.color = Color.green; // ����� ����� �������-�������, ��� �� ������ �������� �����
        GUI.skin.label.fontSize = 16;
        GUI.skin.label.fontStyle = FontStyle.Bold;

        // ������� ������ �� �����
        string speedText = $"СКОРОСТЬ: {TrainLever.Speed:F1} км/ч"; 
        string modeText = $"РЕЖИМ: {TrainLever.LeverMode}";
        // НОВАЯ СТРОЧКА:
        string reverserText = $"РЕВЕРСОР: {TrainReverser.ReverserMode}"; 
        string leftDoorsText = $"ЛЕВЫЕ ДВЕРИ: {(TrainButton.LeftDoorsOpen ? "ОТКРЫТЫ" : "ЗАКРЫТЫ")}";
        string rightDoorsText = $"ПРАВЫЕ ДВЕРИ: {(TrainButton.RightDoorsOpen ? "ОТКРЫТЫ" : "ЗАКРЫТЫ")}";

        GUI.Label(new Rect(35, 30, 280, 30), speedText);
        GUI.Label(new Rect(35, 60, 280, 30), modeText);

        // Нарисуем реверсор своим цветом для красоты (например, желтым/белым)
        GUI.color = Color.white;
        GUI.Label(new Rect(35, 90, 280, 30), reverserText); // Сдвигаем двери чуть ниже

        GUI.color = TrainButton.LeftDoorsOpen ? Color.red : Color.green;
        GUI.Label(new Rect(35, 120, 280, 30), leftDoorsText); // Сдвинули по высоте на 120

        GUI.color = TrainButton.RightDoorsOpen ? Color.red : Color.green;
        GUI.Label(new Rect(35, 150, 280, 30), rightDoorsText); // Сдвинули по высоте на 150

        // Добавьте эту строку к остальным текстам в OnGUI()
        string trackSwitchText = $"СТРЕЛКА: {(SwitchButton.SwitchDirectionRight ? ">>> НАПРАВО" : "<<< НАЛЕВО")}";

        // Отрисуем её чуть ниже дверей (на высоте 180)
        GUI.color = Color.cyan; // Сделаем текст стрелки красивым голубым цветом
        GUI.Label(new Rect(35, 180, 280, 30), trackSwitchText);


    }
}
