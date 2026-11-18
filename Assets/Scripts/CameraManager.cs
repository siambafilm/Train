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

        ResolveFirstPersonCamera();
    }

    void Start()
    {
        ApplyView();
    }

    void Update()
    {
        // НОВОЕ: пока открыта большая карта навигатора — не переключаем камеру
        var nav = MetroNavigator.Instance;
        if (nav != null && nav.IsFullMapOpen) return;

        if (Input.GetKeyDown(toggleKey) && !lockedToFirstPerson)
        {
            isFirstPerson = !isFirstPerson;
            ApplyView();
            Debug.Log($"[CameraManager] Вид: {(isFirstPerson ? "1-е лицо" : "3-е лицо")}");
        }
    }

    /// <summary>
    /// Если в инспекторе не назначена камера от первого лица — подхватываем активную камеру сцены
    /// (обычно это MainCamera). Вызывается один раз в Awake.
    /// </summary>
    private void ResolveFirstPersonCamera()
    {
        if (firstPersonCamera != null) return;

        Camera activeCam = Camera.main;

        // Camera.main ищет только объекты с тегом "MainCamera".
        // Если её нет — берём первую попавшуюся активную камеру в сцене.
        if (activeCam == null)
        {
            Camera[] allCams = FindObjectsOfType<Camera>();
            foreach (var cam in allCams)
            {
                if (cam != null && cam.isActiveAndEnabled)
                {
                    activeCam = cam;
                    break;
                }
            }
        }

        if (activeCam != null)
        {
            firstPersonCamera = activeCam.gameObject;
            Debug.Log($"[CameraManager] firstPersonCamera не задана — использую активную камеру '{activeCam.name}'.");
        }
        else
        {
            Debug.LogWarning("[CameraManager] firstPersonCamera не задана, и в сцене не найдено ни одной активной камеры!");
        }

        // Защита от случая, когда обе ссылки указывают на один и тот же GameObject
        if (firstPersonCamera != null && firstPersonCamera == thirdPersonCamera)
        {
            Debug.LogWarning($"[CameraManager] firstPersonCamera и thirdPersonCamera ссылаются на один объект ('{firstPersonCamera.name}'). " +
                             "Камера будет выключаться при переключении на вид от третьего лица.");
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
                if (lockedToFirstPerson) return;

                viewBeforeLock = isFirstPerson;
                isFirstPerson = true;
                lockedToFirstPerson = true;
                ApplyView();
                Debug.Log($"[CameraManager] Маркер '{marker.name}' (FirstPersonOnly): 1-е лицо, переключение ЗАБЛОКИРОВАНО.");
                break;

            case MarkerCameraMode.Both:
                if (!lockedToFirstPerson) return;

                lockedToFirstPerson = false;
                isFirstPerson = viewBeforeLock;
                ApplyView();
                Debug.Log($"[CameraManager] Маркер '{marker.name}' (Both): блок снят, вид восстановлен на {(isFirstPerson ? "1-е" : "3-е")} лицо.");
                break;
        }
    }

    private void ApplyView()
    {
        if (firstPersonCamera != null) firstPersonCamera.SetActive(isFirstPerson);
        if (thirdPersonCamera != null && thirdPersonCamera != firstPersonCamera)
            thirdPersonCamera.SetActive(!isFirstPerson);

        // НОВОЕ: если открыта карта — не перебиваем курсор
        var nav = MetroNavigator.Instance;
        if (nav != null && nav.IsFullMapOpen) return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public bool IsFirstPerson => isFirstPerson;
    public bool IsLocked => lockedToFirstPerson;
}