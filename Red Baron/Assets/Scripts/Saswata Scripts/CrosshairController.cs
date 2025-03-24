using UnityEngine;
using UnityEngine.UI;

public class CrosshairController : MonoBehaviour
{
    [Header("References")]
    public Transform plane;
    public RectTransform crosshairUI;
    public float crosshairDistance = 500f;
    public float smoothing = 10f;

    [Header("Drift Settings")]
    public float maxDriftDistance = 40f;
    public float driftSpeed = 8f;
    public float returnSpeed = 5f;

    private Camera mainCam;
    private Vector3 basePosition;
    private Vector3 currentDrift;
    private float currentAccuracy = 1f;
    private float noiseOffsetX;
    private float noiseOffsetY;

    void Start()
    {
        mainCam = Camera.main;
        noiseOffsetX = Random.Range(0f, 100f);
        noiseOffsetY = Random.Range(0f, 100f);
    }

    void Update()
    {
        if (!plane || !crosshairUI) return;

        UpdateBasePosition();
        ApplyAccuracyDrift();
        UpdateCrosshairVisual();
    }

    void UpdateBasePosition()
    {
        basePosition = plane.position +
                      (plane.forward * crosshairDistance) +
                      (plane.up * -50f);
    }

    void ApplyAccuracyDrift()
    {
        float inaccuracy = 1 - currentAccuracy;
        float xNoise = Mathf.PerlinNoise(Time.time * driftSpeed, noiseOffsetX) * 2 - 1;
        float yNoise = Mathf.PerlinNoise(noiseOffsetY, Time.time * driftSpeed) * 2 - 1;

        Vector3 targetDrift = new Vector3(
            xNoise * inaccuracy * maxDriftDistance,
            yNoise * inaccuracy * maxDriftDistance,
            0
        );

        currentDrift = Vector3.Lerp(currentDrift, targetDrift, Time.deltaTime * returnSpeed);
    }

    void UpdateCrosshairVisual()
    {
        Vector3 finalPosition = mainCam.WorldToScreenPoint(basePosition + currentDrift);

        if (finalPosition.z > 0)
        {
            crosshairUI.position = Vector3.Lerp(
                crosshairUI.position,
                finalPosition,
                Time.deltaTime * smoothing
            );
        }
    }

    public void UpdateAccuracy(float accuracy)
    {
        currentAccuracy = Mathf.Clamp01(accuracy);
    }

    public Vector3 GetCrosshairWorldPosition()
    {
        return basePosition + currentDrift;
    }
}