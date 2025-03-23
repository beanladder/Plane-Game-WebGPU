using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class PropellerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform propellerBlades; // The actual propeller blades mesh
    [SerializeField] private GameObject blurCylinder; // The cylindrical blur effect
    [SerializeField] private PlaneController planeController; // Reference to the plane controller
    [SerializeField] private Material blurMaterial; // Reference to the blur material
    [SerializeField] private Renderer bladesRenderer; // Reference to the propeller blades renderer

    [Header("Propeller Settings")]
    [SerializeField] private float minRotationSpeed = 500f; // Minimum rotation speed in degrees per second
    [SerializeField] private float maxRotationSpeed = 3000f; // Maximum rotation speed in degrees per second
    [SerializeField] private float rotationSpeedMultiplier = 60f; // Multiplier to calculate rotation from plane speed
    [SerializeField] private float blurThresholdSpeed = 1500f; // Speed at which to switch to blur effect
    [SerializeField] private float transitionDuration = 0.3f; // Duration of transition between blades and blur

    [Header("Color Settings")]
    [SerializeField] private Color bladeColor = Color.white; // Color of the physical blades
    [SerializeField] private Color blurMainColor = Color.white; // Main color of the blur effect
    [SerializeField] private Color blurShimmerColor = Color.white; // Shimmer color of the blur effect
    [SerializeField] private bool synchronizeColors = true; // Whether to sync blur colors with blade color

    [Header("Shader Properties")]
    [SerializeField] private string opacityPropertyName = "_Opacity"; // Property name for opacity in shader
    [SerializeField] private string speedPropertyName = "_ShimmerSpeed"; // Property name for speed in shader
    [SerializeField] private float minOpacity = 0.3f; // Minimum opacity
    [SerializeField] private float maxOpacity = 0.8f; // Maximum opacity
    [SerializeField] private float shimmerSpeedMin = 1f; // Minimum shimmer speed
    [SerializeField] private float shimmerSpeedMax = 5f; // Maximum shimmer speed

    [Header("Audio Settings")]
    [SerializeField] private float maxVolume = 0.8f; // Maximum volume of the engine sound
    [SerializeField] private float minPitch = 0.8f; // Minimum pitch of the engine sound
    [SerializeField] private float maxPitch = 1.2f; // Maximum pitch of the engine sound

    [Header("Audio Variation")]
    [SerializeField][Range(0, 0.2f)] private float pitchVariation = 0.05f; // Range of pitch fluctuations
    [SerializeField] private float variationSpeed = 1.5f; // Speed of audio variations
    [SerializeField][Range(0, 0.1f)] private float volumeVariation = 0.03f; // Subtle volume changes

    // Internal state
    private float currentRotationSpeed;
    private bool usingBlurEffect = false;
    private MaterialPropertyBlock propBlock;
    private MaterialPropertyBlock bladePropBlock;
    private AudioSource audioSource;
    private float basePitch;
    private float baseVolume;
    private float variationSeed;

    private void Start()
    {
        // Get reference to PlaneController if not set
        if (planeController == null)
            planeController = GetComponentInParent<PlaneController>();

        // Initialize material property block for efficient shader property updates
        propBlock = new MaterialPropertyBlock();
        bladePropBlock = new MaterialPropertyBlock();

        // Initially hide blur cylinder and show blades
        if (blurCylinder != null)
            blurCylinder.SetActive(false);

        if (propellerBlades != null)
            propellerBlades.gameObject.SetActive(true);

        // Set initial colors
        UpdateBladeColor();
        SynchronizeBlurColors();

        // Get reference to AudioSource component
        audioSource = GetComponent<AudioSource>();

        // Error handling if somehow missing (though [RequireComponent] prevents this)
        if (audioSource == null)
        {
            Debug.LogError("PropellerController requires AudioSource component!", this);
            enabled = false;
            return;
        }

        // Configure audio source
        audioSource.loop = true;
        audioSource.playOnAwake = false;

        // Start playing if not already
        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }

        // Initialize audio variation seed
        variationSeed = Random.Range(0f, 100f); // Unique seed per instance
    }

    private void Update()
    {
        // Get the plane stats for proper values
        PlaneStats planeStats = GetPlaneStats();
        if (planeStats == null) return;

        // Calculate rotation speed based on plane's current speed
        float speedRatio = Mathf.InverseLerp(planeStats.airNormalSpeed, planeStats.airBoostSpeed, planeController.currentSpeed);
        currentRotationSpeed = Mathf.Lerp(minRotationSpeed, maxRotationSpeed, speedRatio);

        // Rotate the propeller blades if they're active
        if (propellerBlades != null && propellerBlades.gameObject.activeInHierarchy)
        {
            propellerBlades.Rotate(Vector3.forward * currentRotationSpeed * Time.deltaTime);
        }

        // Check if we need to switch between physical blades and blur effect
        if (!usingBlurEffect && currentRotationSpeed >= blurThresholdSpeed)
        {
            StartCoroutine(TransitionToBlur());
        }
        else if (usingBlurEffect && currentRotationSpeed < blurThresholdSpeed)
        {
            StartCoroutine(TransitionToBlades());
        }

        // Update blur shader properties
        if (blurCylinder != null && blurCylinder.activeInHierarchy)
        {
            UpdateBlurShader(planeStats);
        }

        // Update audio properties
        if (audioSource != null)
        {
            // Base values
            baseVolume = Mathf.Lerp(0.3f, maxVolume, speedRatio);
            basePitch = Mathf.Lerp(minPitch, maxPitch, speedRatio);

            // Add organic variation at max speed
            if (speedRatio >= 0.95f)
            {
                float time = Time.time * variationSpeed + variationSeed;

                // Smooth pitch variation using Perlin noise
                float pitchNoise = Mathf.PerlinNoise(time, 0) * 2 - 1;
                audioSource.pitch = Mathf.Clamp(
                    basePitch + pitchNoise * pitchVariation,
                    minPitch,
                    maxPitch + pitchVariation
                );

                // Subtle volume variation
                float volNoise = Mathf.PerlinNoise(time, 100) * 2 - 1;
                audioSource.volume = Mathf.Clamp(
                    baseVolume + volNoise * volumeVariation,
                    0.3f,
                    maxVolume + volumeVariation
                );
            }
            else
            {
                // Standard linear interpolation
                audioSource.pitch = basePitch;
                audioSource.volume = baseVolume;
            }
        }
    }

    // Helper method to safely get plane stats
    private PlaneStats GetPlaneStats()
    {
        if (planeController == null)
        {
            Debug.LogWarning("PlaneController reference is missing!", this);
            return null;
        }

        // Use reflection to access the private field
        System.Reflection.FieldInfo fieldInfo = typeof(PlaneController).GetField("planeStats",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (fieldInfo == null)
        {
            Debug.LogWarning("Cannot access planeStats field on PlaneController!", this);
            return null;
        }

        PlaneStats stats = fieldInfo.GetValue(planeController) as PlaneStats;
        if (stats == null)
        {
            Debug.LogWarning("PlaneStats is null on PlaneController!", this);
            return null;
        }

        return stats;
    }

    private void UpdateBlurShader(PlaneStats planeStats)
    {
        // Calculate opacity based on speed (faster = less opaque)
        float opacity = Mathf.Lerp(maxOpacity, minOpacity, Mathf.InverseLerp(blurThresholdSpeed, maxRotationSpeed, currentRotationSpeed));

        // Calculate shimmer speed based on rotation speed
        float shimmerSpeed = Mathf.Lerp(shimmerSpeedMin, shimmerSpeedMax, Mathf.InverseLerp(minRotationSpeed, maxRotationSpeed, currentRotationSpeed));

        // Calculate rotation speed for shader (2.5 to 7.5)
        float shaderRotationSpeed = Mathf.Lerp(2.5f, 7.5f, Mathf.InverseLerp(planeStats.airNormalSpeed, planeStats.airBoostSpeed, planeController.currentSpeed));

        // Update shader properties using property block
        Renderer renderer = blurCylinder.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.GetPropertyBlock(propBlock);
            propBlock.SetFloat(opacityPropertyName, opacity);
            propBlock.SetFloat(speedPropertyName, shimmerSpeed);
            propBlock.SetFloat("_RotationSpeed", shaderRotationSpeed); // Update rotation speed
            propBlock.SetFloat("_ShimmerIntensity", 0.0f); // Set shimmer intensity to 0 to disable color changing
            propBlock.SetColor("_MainColor", blurMainColor); // Force the main color
            propBlock.SetColor("_ShimmerColor", blurMainColor); // Use same color for shimmer
            renderer.SetPropertyBlock(propBlock);
        }
    }

    public void SetBladeColor(Color newColor)
    {
        bladeColor = newColor;
        UpdateBladeColor();

        if (synchronizeColors)
        {
            SynchronizeBlurColors();
        }
    }

    public void SetBlurColors(Color mainColor, Color shimmerColor)
    {
        blurMainColor = mainColor;
        blurShimmerColor = shimmerColor;
        synchronizeColors = false; // Disable sync when manually setting colors

        if (blurMaterial != null)
        {
            blurMaterial.SetColor("_MainColor", blurMainColor);
            blurMaterial.SetColor("_ShimmerColor", blurMainColor); // Use same color for shimmer to avoid color changes
        }
    }

    private void UpdateBladeColor()
    {
        if (bladesRenderer != null)
        {
            bladesRenderer.GetPropertyBlock(bladePropBlock);
            bladePropBlock.SetColor("_Color", bladeColor);
            bladesRenderer.SetPropertyBlock(bladePropBlock);
        }
    }

    private void SynchronizeBlurColors()
    {
        if (!synchronizeColors || blurMaterial == null) return;

        // Use blade color for both main and shimmer color to maintain consistency
        blurMainColor = bladeColor;
        blurShimmerColor = bladeColor; // Use same color instead of creating a lighter version

        // Apply to material
        blurMaterial.SetColor("_MainColor", blurMainColor);
        blurMaterial.SetColor("_ShimmerColor", blurMainColor);

        // Disable shimmer effect to prevent color variations
        blurMaterial.SetFloat("_ShimmerIntensity", 0.0f);
    }

    private IEnumerator TransitionToBlur()
    {
        usingBlurEffect = true;

        // Make sure blur colors match blade color if sync is enabled
        if (synchronizeColors)
        {
            SynchronizeBlurColors();
        }

        // Activate blur
        blurCylinder.SetActive(true);

        // Wait for transition
        yield return new WaitForSeconds(transitionDuration);

        // Deactivate blades
        propellerBlades.gameObject.SetActive(false);
    }

    private IEnumerator TransitionToBlades()
    {
        usingBlurEffect = false;

        // Activate blades
        propellerBlades.gameObject.SetActive(true);

        // Wait for transition
        yield return new WaitForSeconds(transitionDuration);

        // Deactivate blur
        blurCylinder.SetActive(false);
    }
}