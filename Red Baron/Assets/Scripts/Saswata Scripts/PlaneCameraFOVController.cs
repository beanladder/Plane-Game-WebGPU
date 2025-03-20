using UnityEngine;

using Unity.Cinemachine;

public class PlaneCameraFOVController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlaneController planeController;
    [SerializeField] private CinemachineCamera cam;

    [Header("FOV Settings")]
    [SerializeField] private float normalFOV = 50f;
    [SerializeField] private float boostFOV = 40f;
    [SerializeField] private float fovChangeSpeed = 2f;

    private void Start()
    {
        // Auto-find references if not set
        if (planeController == null)
            planeController = FindFirstObjectByType<PlaneController>();

        if (cam == null)
            cam = GetComponent<CinemachineCamera>();

        // Initialize FOV
        if (cam != null)
            cam.Lens.FieldOfView = normalFOV;
    } 

    private void Update()
    {
        if (planeController == null || cam == null)
            return;

        // Determine target FOV based on speed
        float targetFOV = normalFOV;

        // Check if we're at boost speed
        bool isBoosting = planeController.currentSpeed > (planeController.targetSpeed * 0.9f) &&
                         planeController.targetSpeed > planeController.airNormalSpeed + 5f;

        if (isBoosting)
        {
            targetFOV = boostFOV;
        }

        // Smoothly adjust the FOV
        float newFOV = Mathf.Lerp(
            cam.Lens.FieldOfView,
            targetFOV,
            Time.deltaTime * fovChangeSpeed
        );

        // Apply the new FOV value
        cam.Lens.FieldOfView = newFOV;
    }
}