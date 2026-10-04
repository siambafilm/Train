using UnityEngine;
using System.Collections.Generic;

public enum MarkerCameraMode
{
    Both,
    FirstPersonOnly
}

[RequireComponent(typeof(Collider))]
public class TrainTrackMarker : MonoBehaviour
{
    public static readonly List<TrainTrackMarker> All = new List<TrainTrackMarker>();

    public float reachRadius = 1.5f;

    [Header("Камера")]
    [Tooltip("FirstPersonOnly — при проезде включается 1-е лицо и блокируется переключение до следующего Both-маркера.")]
    public MarkerCameraMode cameraMode = MarkerCameraMode.Both;

    [Header("Стрелка")]
    public bool isSwitchPoint = false;
    public SmartTrackSwitch associatedSwitch;

    [Header("Связи")]
    public TrainTrackMarker nextForward;
    public TrainTrackMarker nextForwardRight;
    public TrainTrackMarker nextForwardLeft;
    [HideInInspector] public TrainTrackMarker nextBackward;

    void Reset()
    {
        // При добавлении скрипта в редакторе — настроить коллайдер как триггер
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);

        // На всякий случай: коллайдер должен быть триггером
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;
    }

    void OnDisable() => All.Remove(this);

    public TrainTrackMarker GetNextForward(bool switchRight)
    {
        if (isSwitchPoint)
            return switchRight ? nextForwardRight : nextForwardLeft;
        return nextForward;
    }

    // --- Событие проезда маркера ---
    private void OnTriggerEnter(Collider other)
    {
        // Реагируем только на поезд
        if (other.GetComponentInParent<TrainMovement>() == null) return;

        if (CameraManager.Instance != null)
            CameraManager.Instance.OnMarkerPassed(this);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Color baseColor = cameraMode == MarkerCameraMode.FirstPersonOnly
            ? new Color(1f, 0.2f, 0.2f, 0.9f)
            : (isSwitchPoint ? new Color(1f, 0.5f, 0f, 0.9f) : new Color(0f, 1f, 0f, 0.7f));

        Gizmos.color = baseColor;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        if (nextForward != null)      Gizmos.DrawLine(transform.position, nextForward.transform.position);
        if (nextForwardRight != null) Gizmos.DrawLine(transform.position, nextForwardRight.transform.position);
        if (nextForwardLeft != null)  Gizmos.DrawLine(transform.position, nextForwardLeft.transform.position);

        Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, reachRadius);

        string label = isSwitchPoint ? "[SWITCH]" : "[Marker]";
        label += cameraMode == MarkerCameraMode.FirstPersonOnly ? " [1ST ONLY]" : " [BOTH]";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.8f, label);
    }
#endif
}