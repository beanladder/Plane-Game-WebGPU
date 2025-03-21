using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

public class CrosshairController : MonoBehaviour
{
    public CinemachineCamera cinemachineCam; // Assign your CinemachineCamera
    public RectTransform crosshairUI; // Assign your UI crosshair (RectTransform)
    public Camera mainCamera; // Assign your main camera

    private void Awake()
    {
        mainCamera = Camera.main;
        cinemachineCam = GetComponent<CinemachineCamera>();
    }
    void Update()
    {
        if (cinemachineCam == null || crosshairUI == null || mainCamera == null)
            return;

        // Get the world-space aim point (where the camera is looking)
        Vector3 aimTargetWorldPos = cinemachineCam.State.ReferenceLookAt;

        // Convert world position to screen-space
        Vector3 screenPos = mainCamera.WorldToScreenPoint(aimTargetWorldPos);

        // Update the UI crosshair position
        crosshairUI.position = screenPos;
    }
}
