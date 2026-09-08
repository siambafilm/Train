using UnityEngine;

public class SwitchTurnPoint : MonoBehaviour
{
    private SmartTrackSwitch parentSwitch;

    void Start()
    {
        parentSwitch = GetComponentInParent<SmartTrackSwitch>();
    }

    private void OnTriggerEnter(Collider other)
    {
        TrainMovement train = other.GetComponentInParent<TrainMovement>();
        if (train != null)
        {
            parentSwitch.ExecuteTurn(train);
        }
    }
}
