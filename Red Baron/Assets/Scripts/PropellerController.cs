using UnityEngine;

public class PropellerController : MonoBehaviour
{
    [Header("Propeller Settings")]
    [Tooltip("RPM of the propeller")]
    [Range(0, 10000)]
    public float rpm = 1000f;
    
    [Tooltip("Maximum allowed RPM")]
    public float maxRpm = 10000f;
    
    [Header("Animation Settings")]
    [Tooltip("Time to reach target RPM")]
    public float accelerationTime = 2.0f;
    
    [Header("Shader Control")]
    [Tooltip("Material with the propeller shader")]
    public Material propellerMaterial;
    
    // Internal variables
    private float currentRpm = 0f;
    private float degreesPerSecond;
    private int tweenId = -1;
    
    void Start()
    {
        // Set initial RPM
        SetRpm(rpm);
    }
    
    void Update()
    {
        // Convert RPM to degrees per second
        degreesPerSecond = currentRpm * 360f / 60f;
        
        // Rotate the propeller along the X-axis
        transform.Rotate(Vector3.up * degreesPerSecond * Time.deltaTime);
        
        // Update shader properties if material exists
        if (propellerMaterial != null)
        {
            // Calculate normalized RPM (0-1 range)
            float normalizedRpm = Mathf.Clamp01(currentRpm / maxRpm);
            
            // Update shader properties
            propellerMaterial.SetFloat("_RotationSpeed", normalizedRpm);
            propellerMaterial.SetFloat("_BlurAmount", normalizedRpm);
            propellerMaterial.SetFloat("_Transparency", normalizedRpm);
            propellerMaterial.SetFloat("_RotationDirection", 1f); // 1 for clockwise, -1 for counter-clockwise
        }
    }
    
    // Method to set RPM with animation
    public void SetRpm(float targetRpm)
    {
        // Clamp target RPM
        targetRpm = Mathf.Clamp(targetRpm, 0, maxRpm);
        
        // Cancel any existing tween
        if (tweenId != -1)
        {
            LeanTween.cancel(tweenId);
        }
        
        // Create new tween to target RPM
        tweenId = LeanTween.value(gameObject, currentRpm, targetRpm, accelerationTime)
            .setEase(LeanTweenType.easeInOutQuad)
            .setOnUpdate((float value) => {
                currentRpm = value;
                // Update the public rpm value to match
                rpm = value;
            })
            .id;
    }
    
    // Editor-only method to update RPM when slider changes
    void OnValidate()
    {
        // Only respond to changes during play mode
        if (Application.isPlaying)
        {
            SetRpm(rpm);
        }
    }
}