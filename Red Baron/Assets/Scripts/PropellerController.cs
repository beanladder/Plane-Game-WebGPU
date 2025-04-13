using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class PropellerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform propellerBlades;
    [SerializeField] private GameObject blurCylinder;
    [SerializeField] private PlaneController planeController;
    [SerializeField] private Material blurMaterial;
    [SerializeField] private Renderer bladesRenderer;

    [Header("Propeller Settings")]
    [SerializeField] private float minRotationSpeed = 500f;
    [SerializeField] private float maxRotationSpeed = 3000f;
    [SerializeField] private float rotationSpeedMultiplier = 60f;
    [SerializeField] private float blurThresholdSpeed = 1500f;
    [SerializeField] private float transitionDuration = 0.3f;

    [Header("Color Settings")]
    [SerializeField] private Color bladeColor = Color.white;
    [SerializeField] private Color blurMainColor = Color.white;
    [SerializeField] private Color blurShimmerColor = Color.white;
    [SerializeField] private bool synchronizeColors = true;

    [Header("Shader Properties")]
    [SerializeField] private string opacityPropertyName = "_Opacity";
    [SerializeField] private string speedPropertyName = "_ShimmerSpeed";
    [SerializeField] private float minOpacity = 0.3f;
    [SerializeField] private float maxOpacity = 0.8f;
    [SerializeField] private float shimmerSpeedMin = 1f;
    [SerializeField] private float shimmerSpeedMax = 5f;

    [Header("Audio Settings")]
    [SerializeField] private float maxVolume = 0.8f;
    [SerializeField] private float minPitch = 0.8f;
    [SerializeField] private float maxPitch = 1.2f;

    [Header("Audio Variation")]
    [SerializeField][Range(0, 0.2f)] private float pitchVariation = 0.05f;
    [SerializeField] private float variationSpeed = 1.5f;
    [SerializeField][Range(0, 0.1f)] private float volumeVariation = 0.03f;

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
        if (planeController == null)
            planeController = GetComponentInParent<PlaneController>();

        propBlock = new MaterialPropertyBlock();
        bladePropBlock = new MaterialPropertyBlock();

        if (blurCylinder != null)
            blurCylinder.SetActive(false);

        if (propellerBlades != null)
            propellerBlades.gameObject.SetActive(true);

        UpdateBladeColor();
        SynchronizeBlurColors();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Debug.LogError("PropellerController requires AudioSource component!", this);
            enabled = false;
            return;
        }

        audioSource.loop = true;
        audioSource.playOnAwake = false;

        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }

        variationSeed = Random.Range(0f, 100f);
    }

    private void Update()
    {
        if (planeController == null || planeController.planeStats == null) return;
        PlaneStats planeStats = planeController.planeStats;

        float speedRatio = Mathf.InverseLerp(planeStats.airNormalSpeed, planeStats.airBoostSpeed, planeController.currentSpeed);
        currentRotationSpeed = Mathf.Lerp(minRotationSpeed, maxRotationSpeed, speedRatio);

        if (propellerBlades != null && propellerBlades.gameObject.activeInHierarchy)
        {
            propellerBlades.Rotate(Vector3.forward * currentRotationSpeed * Time.deltaTime);
        }

        if (!usingBlurEffect && currentRotationSpeed >= blurThresholdSpeed)
        {
            StartCoroutine(TransitionToBlur());
        }
        else if (usingBlurEffect && currentRotationSpeed < blurThresholdSpeed)
        {
            StartCoroutine(TransitionToBlades());
        }

        if (blurCylinder != null && blurCylinder.activeInHierarchy)
        {
            UpdateBlurShader(planeStats);
        }

        if (audioSource != null)
        {
            baseVolume = Mathf.Lerp(0.3f, maxVolume, speedRatio);
            basePitch = Mathf.Lerp(minPitch, maxPitch, speedRatio);

            if (speedRatio >= 0.95f)
            {
                float time = Time.time * variationSpeed + variationSeed;
                float pitchNoise = Mathf.PerlinNoise(time, 0) * 2 - 1;
                audioSource.pitch = Mathf.Clamp(
                    basePitch + pitchNoise * pitchVariation,
                    minPitch,
                    maxPitch + pitchVariation
                );

                float volNoise = Mathf.PerlinNoise(time, 100) * 2 - 1;
                audioSource.volume = Mathf.Clamp(
                    baseVolume + volNoise * volumeVariation,
                    0.3f,
                    maxVolume + volumeVariation
                );
            }
            else
            {
                audioSource.pitch = basePitch;
                audioSource.volume = baseVolume;
            }
        }
    }

    private void UpdateBlurShader(PlaneStats planeStats)
    {
        float opacity = Mathf.Lerp(maxOpacity, minOpacity, Mathf.InverseLerp(blurThresholdSpeed, maxRotationSpeed, currentRotationSpeed));
        float shimmerSpeed = Mathf.Lerp(shimmerSpeedMin, shimmerSpeedMax, Mathf.InverseLerp(minRotationSpeed, maxRotationSpeed, currentRotationSpeed));
        float shaderRotationSpeed = Mathf.Lerp(2.5f, 7.5f, Mathf.InverseLerp(planeStats.airNormalSpeed, planeStats.airBoostSpeed, planeController.currentSpeed));

        Renderer renderer = blurCylinder.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.GetPropertyBlock(propBlock);
            propBlock.SetFloat(opacityPropertyName, opacity);
            propBlock.SetFloat(speedPropertyName, shimmerSpeed);
            propBlock.SetFloat("_RotationSpeed", shaderRotationSpeed);
            propBlock.SetFloat("_ShimmerIntensity", 0.0f);
            propBlock.SetColor("_MainColor", blurMainColor);
            propBlock.SetColor("_ShimmerColor", blurMainColor);
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
        synchronizeColors = false;

        if (blurMaterial != null)
        {
            blurMaterial.SetColor("_MainColor", blurMainColor);
            blurMaterial.SetColor("_ShimmerColor", blurMainColor);
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

        blurMainColor = bladeColor;
        blurShimmerColor = bladeColor;

        blurMaterial.SetColor("_MainColor", blurMainColor);
        blurMaterial.SetColor("_ShimmerColor", blurMainColor);
        blurMaterial.SetFloat("_ShimmerIntensity", 0.0f);
    }

    private IEnumerator TransitionToBlur()
    {
        usingBlurEffect = true;

        if (synchronizeColors)
        {
            SynchronizeBlurColors();
        }

        blurCylinder.SetActive(true);
        yield return new WaitForSeconds(transitionDuration);
        propellerBlades.gameObject.SetActive(false);
    }

    private IEnumerator TransitionToBlades()
    {
        usingBlurEffect = false;
        propellerBlades.gameObject.SetActive(true);
        yield return new WaitForSeconds(transitionDuration);
        blurCylinder.SetActive(false);
    }
}