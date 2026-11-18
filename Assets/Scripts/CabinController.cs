using UnityEngine;

public class CabinController : MonoBehaviour
{
    private Camera cam;

    [Header("Настройки обзора")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float verticalLookLimit = 60f;

    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        cam = GetComponent<Camera>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        rotationX = transform.localEulerAngles.x;
        rotationY = transform.localEulerAngles.y;
    }

    void Update()
    {
        // 1. Вращение камеры
        rotationY += Input.GetAxis("Mouse X") * mouseSensitivity;
        rotationX -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        rotationX = Mathf.Clamp(rotationX, -verticalLookLimit, verticalLookLimit);
        transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0f);

        // 2. Клик по IClickable
        if (Input.GetMouseButtonDown(0))
            ProcessClick();

        // 3. Скролл по рычагу / реверсору
        ProcessLeverScroll();
    }

    private void ProcessClick()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        Debug.DrawRay(ray.origin, ray.direction * 2.0f, Color.red, 1f);

        if (Physics.Raycast(ray, out hit, 5.0f))
        {
            Debug.Log($"Наведение на объект: {hit.collider.gameObject.name}");

            IClickable clickable = hit.collider.GetComponent<IClickable>();
            if (clickable != null)
                clickable.OnClick();
            else
                Debug.LogWarning($"На объекте {hit.collider.gameObject.name} нет IClickable!");
        }
        else
        {
            Debug.Log("Клик мимо цели (Raycast не попал).");
        }
    }

    private void ProcessLeverScroll()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 5.0f))
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll == 0f) return;

            TrainLever lever = hit.collider.GetComponent<TrainLever>();
            if (lever != null)
            {
                if (scroll > 0f) lever.ChangePosition(1);
                if (scroll < 0f) lever.ChangePosition(-1);
                return;
            }

            TrainReverser reverser = hit.collider.GetComponent<TrainReverser>();
            if (reverser != null)
            {
                if (scroll > 0f) reverser.ChangeReverserPosition(1);
                if (scroll < 0f) reverser.ChangeReverserPosition(-1);
            }
        }
    }
}