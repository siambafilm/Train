using UnityEngine;

public class TrainTrackMarker : MonoBehaviour
{
    public float reachRadius = 1.5f;

    [Header("Стрелка")]
    public bool isSwitchPoint = false;
    public SmartTrackSwitch associatedSwitch;

    [Header("Связи")]
    [Tooltip("Обычный маркер — один следующий. Для стрелки оставь пустым и заполни Right/Left.")]
    public TrainTrackMarker nextForward;

    [Tooltip("Только для isSwitchPoint: ветка НАПРАВО")]
    public TrainTrackMarker nextForwardRight;

    [Tooltip("Только для isSwitchPoint: ветка НАЛЕВО")]
    public TrainTrackMarker nextForwardLeft;

    [HideInInspector] public TrainTrackMarker nextBackward;

    /// <summary>Возвращает следующий маркер с учётом выбранной стрелком ветки.</summary>
    public TrainTrackMarker GetNextForward(bool switchRight)
    {
        if (isSwitchPoint)
            return switchRight ? nextForwardRight : nextForwardLeft;
        return nextForward;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Color baseColor = isSwitchPoint ? new Color(1f, 0.5f, 0f, 0.9f) : new Color(0f, 1f, 0f, 0.7f);
        Gizmos.color = baseColor;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
        if (nextForward != null)      Gizmos.DrawLine(transform.position, nextForward.transform.position);
        if (nextForwardRight != null) Gizmos.DrawLine(transform.position, nextForwardRight.transform.position);
        if (nextForwardLeft != null)  Gizmos.DrawLine(transform.position, nextForwardLeft.transform.position);

        Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, reachRadius);

        string label = isSwitchPoint ? "[SWITCH]" : "[Marker]";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.8f, label);
    }
#endif
}