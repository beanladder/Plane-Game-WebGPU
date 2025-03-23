using UnityEngine;
using UnityEngine.UI;

public class CrosshairController : MonoBehaviour
{
    public Transform plane;
    public RectTransform crosshairUI;
    public float crosshairDistance = 500f;
    public float smoothing = 10f;

    private Camera mainCam;
    private Vector3 crosshairWorldPosition; // Stores world position

    

    void Start()
    {
        mainCam = Camera.main;

        if (crosshairUI == null)
            Debug.LogError("❌ Crosshair UI not assigned!");

        if (plane == null)
            Debug.LogError("❌ Plane reference missing!");
    }

    void Update()
    {
        if (plane == null || crosshairUI == null) return;

        // Calculate crosshair position in world space
        Vector3 projectedPoint = plane.position + (plane.forward * crosshairDistance) + (plane.up * -50f);

        // Convert world point to screen space
        Vector3 screenPos = mainCam.WorldToScreenPoint(projectedPoint);

        // Ensure crosshair remains visible on screen
        if (screenPos.z > 0)
        {
            Vector3 smoothPos = Vector3.Lerp(crosshairUI.position, screenPos, Time.deltaTime * smoothing);
            crosshairUI.position = smoothPos;
        }

        // Store world position for bullets to use
        crosshairWorldPosition = projectedPoint;
    }
    public Vector3 GetCrosshairWorldPosition()
    {
        if (crosshairUI == null || mainCam == null)
        {
            Debug.LogError("❌ Crosshair UI or Camera not assigned!");
            return Vector3.zero;
        }

        // Convert UI crosshair screen position to world space
        Vector3 screenPos = crosshairUI.position;
        screenPos.z = crosshairDistance; // Maintain the projected distance

        Vector3 worldPos = mainCam.ScreenToWorldPoint(screenPos);

        Debug.Log($"🎯 Crosshair World Position: {worldPos}");
        return worldPos;
    }

}
