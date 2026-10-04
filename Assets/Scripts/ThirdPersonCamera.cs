using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Цель (обычно TrainMovement)")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 2f, 0f);

    [Header("Орбита")]
    [SerializeField] private float distance = 15f;
    [SerializeField] private float minDistance = 5f;
    [SerializeField] private float maxDistance = 30f;
    [SerializeField] private float zoomSpeed = 5f;

    [Header("Управление мышью")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minPitch = -15f;
    [SerializeField] private float maxPitch = 80f;

    private float yaw = 0f;
    private float pitch = 20f;

    void Start()
    {
        if (target == null)
        {
            var movement = FindFirstObjectByType<TrainMovement>();
            if (movement != null) target = movement.transform;
        }

        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
    }

    void LateUpdate()
    {
        if (target == null)
        {
            var movement = FindFirstObjectByType<TrainMovement>();
            if (movement != null) target = movement.transform;
            if (target == null) return;
        }

        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);
        }

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pos = target.position + targetOffset - rot * Vector3.forward * distance;

        transform.position = pos;
        transform.rotation = rot;
    }
}