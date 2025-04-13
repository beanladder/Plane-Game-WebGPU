using UnityEngine;

public class PlaneHUD : MonoBehaviour
{
    private PlaneController planeController;
    private GUIStyle guiStyle;

    private void Start()
    {
        planeController = GetComponent<PlaneController>();
        guiStyle = new GUIStyle();
        guiStyle.normal.textColor = Color.white;
        guiStyle.fontSize = 24;
    }

    private void OnGUI()
    {
        if (planeController == null || planeController.planeStats == null) return;

        // Calculate the current pitch sensitivity
        float pitchSensitivity = CalculateCurrentPitchSensitivity();

        GUI.Label(new Rect(20, 20, 300, 30),
            $"Speed: {planeController.currentSpeed:0} km/h", guiStyle);
        GUI.Label(new Rect(20, 50, 300, 30),
            $"Health: {planeController.currentHealth:0}/{planeController.planeStats.maxHealth}", guiStyle);
        GUI.Label(new Rect(20, 80, 300, 30),
            $"Pitch: {planeController.currentPitchAngle:0}°", guiStyle);
        GUI.Label(new Rect(20, 110, 300, 30),
            $"Altitude: {planeController.currentAltitude:0}m", guiStyle);
        GUI.Label(new Rect(20, 140, 300, 30),
            $"Pitch Sensitivity: {pitchSensitivity:0.00}", guiStyle);
    }

    private float CalculateCurrentPitchSensitivity()
    {
        // Access the private fields we need via reflection to show the actual pitch sensitivity
        // This uses the same formula as in ApplyPitchControlScheme() method

        // Base value
        float sensitivity = planeController.planeStats.pitchSensitivityMultiplier;

        // Calculate angle factor (0 at level flight, 1 at 90° pitch)
        float angleFactor = Mathf.Clamp01(Mathf.Abs(planeController.currentPitchAngle) / 90f);

        // Apply angle-based adjustments
        sensitivity *= Mathf.Lerp(1f, 0.2f, Mathf.Pow(angleFactor, 2));

        // Calculate altitude factor (0 below warning, 1 at max altitude)
        float altitudeFactor = Mathf.Clamp01(
            (planeController.currentAltitude - planeController.planeStats.altitudeWarningThreshold) /
            (planeController.planeStats.maxAltitude - planeController.planeStats.altitudeWarningThreshold)
        );

        // Apply altitude-based reduction
        sensitivity *= Mathf.Lerp(1f, 0.3f, altitudeFactor);

        // Check if in looping motion
        bool isInLoopingMotion = false;
        float loopingFactor = 0f;

        // Approximate the looping state based on publicly visible properties
        // Note: This is an approximation since we can't directly access private fields
        if (Mathf.Abs(planeController.currentPitchAngle - planeController.previousPitchAngle) > 5f)
        {
            isInLoopingMotion = true;
            loopingFactor = 0.5f; // Approximate value
        }

        // Apply loop prevention factor if applicable
        if (isInLoopingMotion)
        {
            sensitivity *= Mathf.Lerp(1f, 0.5f, loopingFactor);
        }

        return sensitivity;
    }
}