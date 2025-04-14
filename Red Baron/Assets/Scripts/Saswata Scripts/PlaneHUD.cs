using UnityEngine;

public class PlaneHUD : MonoBehaviour
{
    private PlaneController planeController;
    private GUIStyle guiStyle;
    private PlaneHealth planeHealth;
    private void Start()
    {
        planeController = GetComponent<PlaneController>();
        planeHealth = GetComponent<PlaneHealth>();
        guiStyle = new GUIStyle();
        guiStyle.normal.textColor = Color.white;
        guiStyle.fontSize = 24;
    }

    private void OnGUI()
    {
        if (planeController == null || planeController.planeStats == null) return;

        GUI.Label(new Rect(20, 20, 300, 30),
            $"Speed: {planeController.currentSpeed:0} km/h", guiStyle);
        GUI.Label(new Rect(20, 50, 300, 30),
            $"Health: {planeHealth.currentHealth:0}/{planeController.planeStats.maxHealth}", guiStyle);
        GUI.Label(new Rect(20, 80, 300, 30),
            $"Pitch: {planeController.currentPitchAngle:0}°", guiStyle);
        GUI.Label(new Rect(20, 110, 300, 30),
            $"Altitude: {planeController.currentAltitude:0}m", guiStyle);
        
    }

    
}