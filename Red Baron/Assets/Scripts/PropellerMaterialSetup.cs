using UnityEngine;

[ExecuteInEditMode]
public class PropellerMaterialSetup : MonoBehaviour
{
    [Header("Color Settings")]
    [SerializeField] private Color mainColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
    [SerializeField] private Color shimmerColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
    
    [Header("Effect Settings")]
    [SerializeField] private float shimmerScale = 20f;
    [SerializeField] private float shimmerIntensity = 0.0f; // Changed to 0 to disable shimmer effect
    [SerializeField] private float rotationSpeed = 2f;
    [SerializeField] private int bladeCount = 4;
    
    [Header("References")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Material propellerBlurMaterial;
    
    private void OnValidate()
    {
        if (propellerBlurMaterial == null && targetRenderer != null)
        {
            // Get the material from the renderer
            propellerBlurMaterial = targetRenderer.sharedMaterial;
        }
        
        UpdateMaterialProperties();
    }
    
    private void UpdateMaterialProperties()
    {
        if (propellerBlurMaterial != null)
        {
            propellerBlurMaterial.SetColor("_MainColor", mainColor);
            propellerBlurMaterial.SetColor("_ShimmerColor", mainColor); // Use same color to avoid shimmer color change
            propellerBlurMaterial.SetFloat("_ShimmerScale", shimmerScale);
            propellerBlurMaterial.SetFloat("_ShimmerIntensity", 0.0f); // Set to 0 to disable shimmer effect
            propellerBlurMaterial.SetFloat("_RotationSpeed", rotationSpeed);
            propellerBlurMaterial.SetFloat("_BladeCount", bladeCount);
            propellerBlurMaterial.SetFloat("_EmissiveBlur", 1.0f); // Enable emissive for consistent appearance
        }
    }
    
    // This can be used to update material at runtime if needed
    public void SetMaterialProperties(Color mainColor, Color shimmerColor, float shimmerScale, 
                                       float shimmerIntensity, float rotationSpeed, int bladeCount)
    {
        this.mainColor = mainColor;
        this.shimmerColor = mainColor; // Use the same color for both
        this.shimmerScale = shimmerScale;
        this.shimmerIntensity = 0.0f; // Always set to zero to disable shimmer
        this.rotationSpeed = rotationSpeed;
        this.bladeCount = bladeCount;
        
        UpdateMaterialProperties();
    }
}