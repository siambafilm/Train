using UnityEngine;

public class TrackSwitchSign : MonoBehaviour
{
    [Header("Ссылки на указатели направления")]
    [SerializeField] private GameObject leftArrowMesh;
    [SerializeField] private GameObject rightArrowMesh;

    void Update()
    {
        if (leftArrowMesh == null || rightArrowMesh == null) return;

        // Включаем или выключаем 3D-модели стрелок на знаке в реальном времени
        bool isRight = SwitchButton.SwitchDirectionRight;
        
        rightArrowMesh.SetActive(isRight);
        leftArrowMesh.SetActive(!isRight);
    }
}
