using UnityEngine;

public class SwitchPreZone : MonoBehaviour
{
    private SmartTrackSwitch parentSwitch;

    void Start()
    {
        parentSwitch = GetComponentInParent<SmartTrackSwitch>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<TrainMovement>() != null)
        {
            parentSwitch.PlayerEnteredPreZone();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<TrainMovement>() != null)
        {
            parentSwitch.PlayerExitedPreZone();
        }
    }
}
