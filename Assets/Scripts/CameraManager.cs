using UnityEngine;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [Header("Камеры (GameObject-ы целиком, чтобы отключались и скрипты)")]
    [SerializeField] private GameObject firstPersonCamera;
    [SerializeField] private GameObject thirdPersonCamera;

    [Header("Управление")]
    [SerializeField] private KeyCode toggleKey = KeyCode.C;

    private bool isFirstPerson = true;
    private bool lockedToFirstPerson = false;
    private bool viewBeforeLock = true; // какой вид был ДО входа в FirstPersonOnly-зону

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        ApplyView();
    }

    void Update()
    {
        // Переключение доступно только если нет активного FirstPersonOnly-блока
        if (Input.GetKeyDown(toggleKey) && !lockedToFirstPerson)
        {
            isFirstPerson = !isFirstPerson;
            ApplyView();
            Debug.Log($"[CameraManager] Вид: {(isFirstPerson ? "1-е лицо" : "3-е лицо")}");
        }
    }

    /// <summary>
    /// Вызывается из TrainTrackMarker.OnTriggerEnter, когда поезд проезжает маркер.
    /// </summary>
    public void OnMarkerPassed(TrainTrackMarker marker)
    {
        if (marker == null) return;

        switch (marker.cameraMode)
        {
            case MarkerCameraMode.FirstPersonOnly:
                // Уже заблокировано? Ничего не делаем, чтобы не перезаписывать сохранённый вид
                if (lockedToFirstPerson) return;

                viewBeforeLock = isFirstPerson; // запоминаем текущий вид
                isFirstPerson = true;
                lockedToFirstPerson = true;
                ApplyView();
                Debug.Log($"[CameraManager] Маркер '{marker.name}' (FirstPersonOnly): 1-е лицо, переключение ЗАБЛОКИРОВАНО.");
                break;

            case MarkerCameraMode.Both:
                // Если блок не активен — нечего восстанавливать
                if (!lockedToFirstPerson) return;

                lockedToFirstPerson = false;
                isFirstPerson = viewBeforeLock; // возвращаем вид, который был до FirstPersonOnly
                ApplyView();
                Debug.Log($"[CameraManager] Маркер '{marker.name}' (Both): блок снят, вид восстановлен на {(isFirstPerson ? "1-е" : "3-е")} лицо.");
                break;
        }
    }

    private void ApplyView()
    {
        if (firstPersonCamera != null) firstPersonCamera.SetActive(isFirstPerson);
        if (thirdPersonCamera != null) thirdPersonCamera.SetActive(!isFirstPerson);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public bool IsFirstPerson => isFirstPerson;
    public bool IsLocked => lockedToFirstPerson;
}