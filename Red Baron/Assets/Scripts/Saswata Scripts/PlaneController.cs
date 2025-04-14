using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

public class PlaneController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] public PlaneStats planeStats;

    [Header("Camera Settings")]
    [SerializeField] private CinemachineCamera cam;
    [SerializeField] private CinemachineCamera freeLookCam;
    [SerializeField] private int defaultCamPriority = 10;
    [SerializeField] private int freeLookCamPriority = 20;
    [SerializeField] private AnimationCurve fovTransitionCurve = AnimationCurve.Linear(0, 0, 1, 1);
    public bool isFreeLookActive = false;
    private float initialHorizontalAxisValue;
    private float initialVerticalAxisValue;

    [Header("Flight Physics")]
    [SerializeField] private float pitchSpeedInfluence = 0.3f;
    [SerializeField] private float diveSpeedBoost = 0.4f;
    [SerializeField] private float climbSpeedPenalty = 0.5f;
    [SerializeField] private float gravitationalForce = 9.8f;
    [SerializeField] private float extremeClimbAngle = 45f;

    [Header("Auto-Pitch Settings")]
    [SerializeField] private float autoPitchFadeOutTime = 0.5f;
    private float currentAutoPitchForce = 1f;
    private float autoPitchFadeOutVelocity;

    // Public state variables
    public float currentSpeed = 0f;
    public float previousSpeed = 0f;
    public float targetSpeed = 0f;
    public Vector3 currentRotation;
    public float currentPitchAngle = 0f;
    public float previousPitchAngle = 0f;
    public float currentAltitude = 0f;

    // Private state variables
    private float rollInput = 0f;
    private float yawInput = 0f;
    private float pitchInput = 0f;
    private float throttleValue = 0f;

    // Smoothing variables
    private float smoothPitchVelocity;
    private float smoothRollVelocity;
    private float smoothYawVelocity;
    private float targetPitchInput = 0f;
    private float targetRollInput = 0f;
    private float targetYawInput = 0f;

    // Inertia and momentum variables
    private float rollVelocity = 0f;
    private float pitchVelocity = 0f;
    private float yawVelocity = 0f;
    private float speedSmoothVelocity;

    // Camera references
    private Camera mainCamera;
    private Transform cameraTransform;

    // Auto-pitch variables
    private float zeroSpeedTimer = 0f;
    private bool isAutoPitching = false;
    private const float AutoPitchDuration = 2.5f;
    private const float AutoPitchSpeed = 30f;
    private const float TargetAutoPitchAngle = 90f;

    private void Start()
    {
        if (planeStats == null)
        {
            Debug.LogError("PlaneStats not assigned to PlaneController!");
            return;
        }

        if (cam != null)
        {
            cam.Lens.FieldOfView = planeStats.defaultFov;
        }

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        Cursor.lockState = CursorLockMode.Locked;
        rb.useGravity = true;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;

        currentSpeed = planeStats.airNormalSpeed;
        previousSpeed = currentSpeed;
        targetSpeed = planeStats.airNormalSpeed;
        throttleValue = 0f;

        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cameraTransform = mainCamera.transform;
        }

        if (freeLookCam != null)
        {
            var orbitalFollow = freeLookCam.GetComponent<CinemachineOrbitalFollow>();
            if (orbitalFollow != null)
            {
                initialHorizontalAxisValue = orbitalFollow.HorizontalAxis.Value;
                initialVerticalAxisValue = orbitalFollow.VerticalAxis.Value;
            }
        }

        currentPitchAngle = NormalizePitchAngle(transform.eulerAngles.x);
        previousPitchAngle = currentPitchAngle;
    }

    private void Update()
    {
        previousSpeed = currentSpeed;
        previousPitchAngle = currentPitchAngle;
        currentAltitude = transform.position.y;

        UpdateAutoPitchTimer();

        if (Input.GetMouseButton(2))
        {
            freeLookCam.Priority = freeLookCamPriority;
            cam.Priority = 0;
            isFreeLookActive = true;
        }
        else
        {
            if (isFreeLookActive)
            {
                StartCoroutine(ResetFreeLookCameraAfterBlend());
            }
            freeLookCam.Priority = 0;
            cam.Priority = defaultCamPriority;
            isFreeLookActive = false;
        }

        if (Input.GetKey(KeyCode.W))
            throttleValue = Mathf.Min(throttleValue + Time.deltaTime, 1.0f);
        else
            throttleValue = Mathf.Max(throttleValue - Time.deltaTime * 2f, 0.0f);

        float inversePitchFactor = planeStats.invertPitchAxis ? 1f : -1f;
        float inverseRollFactor = planeStats.invertRollAxis ? 1f : -1f;

        float rawPitchInput = Input.GetAxis("Mouse Y") * planeStats.mouseSensitivity * inversePitchFactor;
        float rawRollInput = Input.GetAxis("Mouse X") * planeStats.mouseSensitivity * inverseRollFactor;

        targetPitchInput = ApplyPitchSensitivityScheme(rawPitchInput);
        targetRollInput = ApplyRollSensitivityScheme(rawRollInput);

        if (Input.GetKey(KeyCode.A))
            targetYawInput = -1f;
        else if (Input.GetKey(KeyCode.D))
            targetYawInput = 1f;
        else
            targetYawInput = 0f;

        pitchInput = Mathf.SmoothDamp(pitchInput, targetPitchInput, ref smoothPitchVelocity, planeStats.inputSmoothTime);
        rollInput = Mathf.SmoothDamp(rollInput, targetRollInput, ref smoothRollVelocity, planeStats.inputSmoothTime);
        yawInput = Mathf.SmoothDamp(yawInput, targetYawInput, ref smoothYawVelocity, planeStats.keyboardSmoothTime);

        currentPitchAngle = NormalizePitchAngle(transform.eulerAngles.x);
        float effectiveThrottle = AdjustThrottleForClimb(throttleValue);

        HandleFlying(effectiveThrottle);
        currentRotation = transform.eulerAngles;
        currentPitchAngle = NormalizePitchAngle(transform.eulerAngles.x);

        if (planeStats.lockCameraToHorizon && cameraTransform != null && cameraTransform.parent == transform)
        {
            Vector3 cameraEuler = cameraTransform.localEulerAngles;
            cameraTransform.localEulerAngles = new Vector3(cameraEuler.x, cameraEuler.y, -transform.eulerAngles.z);
        }

        if (!isFreeLookActive)
        {
            UpdateCameraFOV();
        }
    }

    private void UpdateAutoPitchTimer()
    {
        if (currentSpeed <= 1.5f)
        {
            if (Mathf.Abs(targetPitchInput) < 0.1f)
            {
                zeroSpeedTimer += Time.deltaTime;
                if (zeroSpeedTimer >= AutoPitchDuration && !isAutoPitching)
                {
                    isAutoPitching = true;
                    currentAutoPitchForce = 1f;
                }
            }
        }
        else
        {
            zeroSpeedTimer = 0f;
        }

        if (isAutoPitching && Mathf.Abs(targetPitchInput) > 0.05f)
        {
            currentAutoPitchForce = Mathf.SmoothDamp(currentAutoPitchForce, 0f, ref autoPitchFadeOutVelocity, autoPitchFadeOutTime);
            if (currentAutoPitchForce < 0.01f)
            {
                isAutoPitching = false;
                zeroSpeedTimer = 0f;
            }
        }
    }

    private void UpdateCameraFOV()
    {
        if (cam == null) return;

        float targetFOV;
        float transitionSpeed = planeStats.fovSmoothSpeed;

        if (currentSpeed < 15f)
        {
            float slowFactor = 1f - Mathf.InverseLerp(0f, 15f, currentSpeed);
            targetFOV = Mathf.Lerp(planeStats.defaultFov, 30f, slowFactor);
            transitionSpeed *= 1.5f;
        }
        else if (currentSpeed > planeStats.airNormalSpeed)
        {
            float speedFactor = Mathf.InverseLerp(
                planeStats.airNormalSpeed,
                planeStats.airBoostSpeed,
                currentSpeed
            );
            float curveFactor = fovTransitionCurve.Evaluate(speedFactor);
            targetFOV = Mathf.Lerp(planeStats.defaultFov, planeStats.maxFov, curveFactor);
        }
        else
        {
            targetFOV = planeStats.defaultFov;
        }

        cam.Lens.FieldOfView = Mathf.Lerp(
            cam.Lens.FieldOfView,
            targetFOV,
            Time.deltaTime * transitionSpeed
        );
    }

    private float AdjustThrottleForClimb(float rawThrottle)
    {
        float pitchFactor = -Mathf.Sin(currentPitchAngle * Mathf.Deg2Rad);

        if (pitchFactor <= 0)
            return rawThrottle;

        float climbSteepness = Mathf.Clamp01(pitchFactor);

        if (climbSteepness > 0.9f)
            return 0f;

        float throttleEffectiveness = 1f - (climbSteepness * climbSteepness * 1.5f);
        throttleEffectiveness = Mathf.Clamp01(throttleEffectiveness);

        return rawThrottle * throttleEffectiveness;
    }

    private void HandleFlying(float throttleInput)
    {
        float baseTargetSpeed = Mathf.Lerp(planeStats.airNormalSpeed, planeStats.airBoostSpeed, throttleInput);

        ApplyPitchBasedSpeedModifications(ref baseTargetSpeed, throttleInput);

        targetSpeed = baseTargetSpeed;

        float rate = currentSpeed < targetSpeed ? planeStats.accelerationRate : planeStats.decelerationRate;
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, 1f / rate);

        if (!isFreeLookActive)
        {
            ApplyRotation();
        }

        Vector3 gravityVector = Vector3.down * gravitationalForce;
        Vector3 forwardVelocity = transform.forward * currentSpeed;
        forwardVelocity += gravityVector * Time.deltaTime * 3.0f;
        rb.linearVelocity = forwardVelocity;
    }

    private void ApplyPitchBasedSpeedModifications(ref float baseSpeed, float throttle)
    {
        float pitchInfluence = Mathf.Sin(-currentPitchAngle * Mathf.Deg2Rad);

        if (pitchInfluence < 0)
        {
            float diveBoostFactor = Mathf.Abs(pitchInfluence) * diveSpeedBoost * 2.0f;
            baseSpeed += baseSpeed * diveBoostFactor;
        }
        else if (pitchInfluence > 0)
        {
            float climbSteepness = pitchInfluence;
            float climbPenaltyFactor = climbSteepness * climbSpeedPenalty * 1.5f;
            baseSpeed -= baseSpeed * climbPenaltyFactor;

            if (climbSteepness > 0.7f)
            {
                float extremeClimbFactor = Mathf.InverseLerp(0.7f, 1.0f, climbSteepness);
                baseSpeed *= (1.0f - extremeClimbFactor);
            }

            if (baseSpeed < 0.1f)
            {
                baseSpeed = 0f;
            }
        }

        baseSpeed += baseSpeed * -pitchInfluence * pitchSpeedInfluence;
    }

    private void ApplyRotation()
    {
        if (isAutoPitching)
        {
            HandleAutoPitchRotation();
            HandleRollAndYaw();

            if (currentAutoPitchForce < 1f)
            {
                HandleAllRotations();
            }
        }
        else
        {
            HandleAllRotations();
        }
    }

    private void HandleAutoPitchRotation()
    {
        float currentPitch = NormalizePitchAngle(transform.eulerAngles.x);
        float effectiveAutoPitchSpeed = AutoPitchSpeed * currentAutoPitchForce;
        float newPitch = Mathf.MoveTowards(currentPitch, TargetAutoPitchAngle, effectiveAutoPitchSpeed * Time.deltaTime);
        float pitchDelta = newPitch - currentPitch;
        transform.Rotate(Vector3.right * pitchDelta, Space.Self);

        if (Mathf.Abs(newPitch - TargetAutoPitchAngle) < 0.1f)
        {
            isAutoPitching = false;
            zeroSpeedTimer = 0f;
        }
    }

    private void HandleRollAndYaw()
    {
        float targetRollVelocity = rollInput * planeStats.rollSpeed * 100f;
        float targetYawVelocity = yawInput * planeStats.yawSpeed * 100f;

        float speedFactor = Mathf.Max(0.2f, Mathf.Clamp01(currentSpeed / planeStats.airNormalSpeed));
        targetRollVelocity *= speedFactor;
        targetYawVelocity *= speedFactor;

        if (Mathf.Abs(rollInput) > 0.01f)
        {
            rollVelocity = Mathf.Lerp(rollVelocity, targetRollVelocity, Time.deltaTime * 5f);
        }
        else
        {
            rollVelocity *= planeStats.rollDamping;
        }

        if (Mathf.Abs(yawInput) > 0.01f)
        {
            yawVelocity = Mathf.Lerp(yawVelocity, targetYawVelocity, Time.deltaTime * 5f);
        }
        else
        {
            yawVelocity *= planeStats.rotationalDamping;
        }

        transform.Rotate(Vector3.up * yawVelocity * Time.deltaTime, Space.Self);
        transform.Rotate(Vector3.forward * rollVelocity * Time.deltaTime, Space.Self);
    }

    private void HandleAllRotations()
    {
        float targetRollVelocity = rollInput * planeStats.rollSpeed * 100f;
        float targetPitchVelocity = pitchInput * planeStats.pitchSpeed * 100f;
        float targetYawVelocity = yawInput * planeStats.yawSpeed * 100f;

        float speedFactor = Mathf.Max(0.2f, Mathf.Clamp01(currentSpeed / planeStats.airNormalSpeed));
        targetPitchVelocity *= speedFactor;
        targetRollVelocity *= speedFactor;
        targetYawVelocity *= speedFactor;

        if (Mathf.Abs(rollInput) > 0.01f)
        {
            rollVelocity = Mathf.Lerp(rollVelocity, targetRollVelocity, Time.deltaTime * 5f);
        }
        else
        {
            rollVelocity *= planeStats.rollDamping;
        }

        if (Mathf.Abs(pitchInput) > 0.01f)
        {
            pitchVelocity = Mathf.Lerp(pitchVelocity, targetPitchVelocity, Time.deltaTime * 5f);
        }
        else
        {
            pitchVelocity *= planeStats.rotationalDamping;
        }

        if (Mathf.Abs(yawInput) > 0.01f)
        {
            yawVelocity = Mathf.Lerp(yawVelocity, targetYawVelocity, Time.deltaTime * 5f);
        }
        else
        {
            yawVelocity *= planeStats.rotationalDamping;
        }

        transform.Rotate(Vector3.right * pitchVelocity * Time.deltaTime, Space.Self);
        transform.Rotate(Vector3.up * yawVelocity * Time.deltaTime, Space.Self);
        transform.Rotate(Vector3.forward * rollVelocity * Time.deltaTime, Space.Self);
    }

    private float NormalizePitchAngle(float angle)
    {
        if (angle > 180)
            angle -= 360;
        return angle;
    }

    private IEnumerator ResetFreeLookCameraAfterBlend()
    {
        yield return new WaitForSeconds(1.2f);
        if (freeLookCam != null)
        {
            var orbitalFollow = freeLookCam.GetComponent<CinemachineOrbitalFollow>();
            if (orbitalFollow != null)
            {
                orbitalFollow.HorizontalAxis.Value = initialHorizontalAxisValue;
                orbitalFollow.VerticalAxis.Value = initialVerticalAxisValue;
            }
        }
    }

    private float ApplyPitchSensitivityScheme(float input)
    {
        float modifiedInput = input * planeStats.pitchSensitivityMultiplier;
        if (planeStats.useExponentialPitchResponse)
        {
            float sign = Mathf.Sign(modifiedInput);
            float absValue = Mathf.Abs(modifiedInput);
            modifiedInput = sign * Mathf.Pow(absValue, planeStats.exponentialPitchFactor);
        }
        return modifiedInput;
    }

    private float ApplyRollSensitivityScheme(float input)
    {
        float modifiedInput = input * planeStats.rollSensitivityMultiplier;
        if (planeStats.useProgressiveRollResponse)
        {
            if (Mathf.Abs(modifiedInput) > planeStats.progressiveRollThreshold)
            {
                float exceededAmount = Mathf.Abs(modifiedInput) - planeStats.progressiveRollThreshold;
                float extraResponse = exceededAmount * planeStats.progressiveRollMultiplier;
                modifiedInput = Mathf.Sign(modifiedInput) * (planeStats.progressiveRollThreshold + extraResponse);
            }
        }
        return modifiedInput;
    }
}