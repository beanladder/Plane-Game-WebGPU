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
    public bool isFreeLookActive = false;
    [SerializeField] private AnimationCurve fovTransitionCurve = AnimationCurve.Linear(0, 0, 1, 1);
    private float initialHorizontalAxisValue;
    private float initialVerticalAxisValue;

    [Header("Flight Physics")]
    [SerializeField] private float pitchSpeedInfluence = 0.3f;
    [SerializeField] private float diveSpeedBoost = 0.4f;
    [SerializeField] private float climbSpeedPenalty = 0.5f;
    [SerializeField] private float gravitationalForce = 9.8f;

    // Loop Prevention Variables
    [Header("Loop Prevention")]
    [SerializeField] private float loopPreventionStrength = 5f;
    [SerializeField] private float loopInputThreshold = 0.6f;
    [SerializeField] private float maxPitchVelocityLooping = 30f; // Maximum pitch velocity during loop attempts
    [SerializeField] private float recoveryBoostMultiplier = 2.5f; // Boost factor for recovery from extreme angles

    // Public state variables
    public float currentSpeed = 0f;
    public float previousSpeed = 0f;
    public float targetSpeed = 0f;
    public Vector3 currentRotation;
    public float currentHealth;
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

    // Repair variables
    private bool isRepairing = false;
    private float repairTimer = 0f;

    // Altitude control variables
    private float basePitchSensitivity;
    private float currentPitchSensitivityMultiplier = 1f;
    private float altitudeLimitFactor = 0f;

    // Variables for max altitude behavior
    private bool isAtMaxAltitude = false;
    private bool isForcedPitchActive = false;
    private float forcedPitchProgress = 0f;
    private const float FORCED_PITCH_DURATION = 4f;
    private float lastPitchInputTime;
    private float lastPitchInputMagnitude;
    private float forcedDescentStartTime = 0f;
    private float forcedDescentRecoveryBlend = 0f;

    // Loop detection and prevention
    private float continuousPitchInputTime = 0f;
    private float pitchInputDirection = 0f;
    private float lastRawPitchInput = 0f;
    private float pitchAngleAccumulator = 0f;
    private bool isInLoopingMotion = false;
    private float loopingPhase = 0f; // 0 to 1 representing how far through a loop we are

    private void Start()
    {
        if (planeStats == null)
        {
            Debug.LogError("PlaneStats not assigned to PlaneController!");
            return;
        }

        currentHealth = planeStats.maxHealth;

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
        throttleValue = 0.5f;

        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cameraTransform = mainCamera.transform;
        }

        basePitchSensitivity = planeStats.pitchSensitivityMultiplier;

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
        if (isRepairing)
        {
            HandleRepair();
            return;
        }

        // Check max altitude status
        if (currentAltitude >= planeStats.maxAltitude && !isAtMaxAltitude)
        {
            isAtMaxAltitude = true;
            StartCoroutine(ForcedPitchSequence());
        }
        else if (currentAltitude < planeStats.maxAltitude * 0.9f)
        {
            isAtMaxAltitude = false;
        }

        previousSpeed = currentSpeed;
        previousPitchAngle = currentPitchAngle;
        currentAltitude = transform.position.y;

        if (Input.GetKeyDown(KeyCode.R) && currentHealth < planeStats.maxHealth)
        {
            StartRepair();
            return;
        }

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
        else if (Input.GetKey(KeyCode.S))
            throttleValue = Mathf.Max(throttleValue - Time.deltaTime * 0.5f, 0.0f);

        float inversePitchFactor = planeStats.invertPitchAxis ? 1f : -1f;
        float inverseRollFactor = planeStats.invertRollAxis ? 1f : -1f;

        float rawPitchInput = Input.GetAxis("Mouse Y") * planeStats.mouseSensitivity * inversePitchFactor;
        float rawRollInput = Input.GetAxis("Mouse X") * planeStats.mouseSensitivity * inverseRollFactor;

        // Track pitch input timing and magnitude
        if (Mathf.Abs(rawPitchInput) > 0.01f)
        {
            lastPitchInputTime = Time.time;
            lastPitchInputMagnitude = Mathf.Abs(rawPitchInput);
        }

        // Handle looping detection and prevention
        ProcessLoopingDetection(rawPitchInput);

        // Apply loop prevention or recovery boost based on current state
        targetPitchInput = ApplyPitchControlScheme(rawPitchInput);
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

        // Manage smooth transition after forced descent
        if (isForcedPitchActive)
        {
            forcedDescentStartTime = Time.time;
            forcedDescentRecoveryBlend = 0f;
        }
        else if (Time.time - forcedDescentStartTime < 2.0f)
        {
            // Gradually restore control after forced descent
            forcedDescentRecoveryBlend = Mathf.Min(1f, (Time.time - forcedDescentStartTime) / 2.0f);
            pitchInput = Mathf.Lerp(0f, pitchInput, forcedDescentRecoveryBlend);
            rollInput = Mathf.Lerp(0f, rollInput, forcedDescentRecoveryBlend);
        }

        HandleFlying(throttleValue);

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

    private void ProcessLoopingDetection(float rawPitchInput)
    {
        // Detect continuous pitch input in the same direction
        if (Mathf.Sign(rawPitchInput) == Mathf.Sign(lastRawPitchInput) && Mathf.Abs(rawPitchInput) > loopInputThreshold)
        {
            if (pitchInputDirection == 0 || Mathf.Sign(rawPitchInput) == pitchInputDirection)
            {
                pitchInputDirection = Mathf.Sign(rawPitchInput);
                continuousPitchInputTime += Time.deltaTime;

                // Track accumulated pitch angle change in the same direction
                float pitchDelta = currentPitchAngle - previousPitchAngle;
                if (Mathf.Sign(pitchDelta) == pitchInputDirection)
                {
                    pitchAngleAccumulator += Mathf.Abs(pitchDelta);
                }

                // Detect if we're in a looping motion (accumulating significant pitch change)
                if (pitchAngleAccumulator > 45f && continuousPitchInputTime > 0.5f)
                {
                    isInLoopingMotion = true;
                    loopingPhase = Mathf.Clamp01(pitchAngleAccumulator / 360f);
                }
            }
            else
            {
                // Reset if direction changed
                pitchInputDirection = Mathf.Sign(rawPitchInput);
                continuousPitchInputTime = 0f;
                pitchAngleAccumulator = 0f;
            }
        }
        else if (Mathf.Abs(rawPitchInput) < 0.2f)
        {
            // Reset when no significant input
            continuousPitchInputTime = Mathf.Max(0, continuousPitchInputTime - Time.deltaTime * 2);

            if (continuousPitchInputTime < 0.2f)
            {
                pitchInputDirection = 0f;
                isInLoopingMotion = false;
                loopingPhase = 0f;
                pitchAngleAccumulator = 0f;
            }
        }

        lastRawPitchInput = rawPitchInput;
    }

    private float ApplyPitchControlScheme(float input)
    {
        if (isForcedPitchActive) return 0f;

        // Calculate angle factor (0 at level flight, 1 at 90° pitch)
        float angleFactor = Mathf.Clamp01(Mathf.Abs(currentPitchAngle) / 90f);

        // Calculate loop prevention strength
        float loopPreventionFactor = 0f;
        if (isInLoopingMotion)
        {
            // Progressively stronger prevention as you get further into the loop
            loopPreventionFactor = Mathf.Pow(loopingPhase, 2) * loopPreventionStrength;
        }

        // Recovery boost for extreme nose down angles
        float recoveryBoost = 1f;
        if (currentPitchAngle < -70f && input < 0f) // Trying to pull up from steep dive
        {
            // More negative pitch = stronger recovery boost
            float extremeAngleFactor = Mathf.Clamp01((Mathf.Abs(currentPitchAngle) - 70f) / 20f);
            recoveryBoost = 1f + (recoveryBoostMultiplier * extremeAngleFactor);
        }

        // Calculate altitude factor (0 below warning, 1 at max altitude)
        float altitudeFactor = Mathf.Clamp01(
            (currentAltitude - planeStats.altitudeWarningThreshold) /
            (planeStats.maxAltitude - planeStats.altitudeWarningThreshold)
        );

        // Base sensitivity
        float sensitivity = planeStats.pitchSensitivityMultiplier;

        // Apply angle-based adjustments (milder curve than before)
        sensitivity *= Mathf.Lerp(1f, 0.2f, Mathf.Pow(angleFactor, 2));

        // Apply altitude-based reduction
        sensitivity *= Mathf.Lerp(1f, 0.3f, altitudeFactor);

        // Apply input-based reduction for fast movements
        if (Time.time - lastPitchInputTime < 0.2f && lastPitchInputMagnitude > 1f)
        {
            sensitivity *= Mathf.Lerp(1f, 0.4f, lastPitchInputMagnitude / 2f);
        }

        // Apply loop prevention by reducing effective input
        float inputMagnitude = Mathf.Abs(input);
        float inputSign = Mathf.Sign(input);

        // Reduce input strength based on loop prevention factor if in same direction as loop
        if (inputSign == pitchInputDirection && isInLoopingMotion)
        {
            inputMagnitude = Mathf.Max(0, inputMagnitude - loopPreventionFactor);
        }

        // Boost recovery from extreme angles
        if (recoveryBoost > 1f)
        {
            inputMagnitude *= recoveryBoost;
        }

        // Reconstruct input with adjusted magnitude
        float modifiedInput = inputSign * inputMagnitude * sensitivity;

        // Apply exponential response if enabled
        if (planeStats.useExponentialPitchResponse)
        {
            float sign = Mathf.Sign(modifiedInput);
            float absValue = Mathf.Abs(modifiedInput);
            modifiedInput = sign * Mathf.Pow(absValue, planeStats.exponentialPitchFactor);
        }

        // Limit maximum pitch velocity during loop attempts
        if (isInLoopingMotion)
        {
            modifiedInput = Mathf.Clamp(modifiedInput, -maxPitchVelocityLooping, maxPitchVelocityLooping);
        }

        // Add general maximum limits
        float maxPitchRate = Mathf.Lerp(80f, 30f, angleFactor);
        return Mathf.Clamp(modifiedInput, -maxPitchRate, maxPitchRate);
    }

    private IEnumerator ForcedPitchSequence()
    {
        isForcedPitchActive = true;
        float startPitch = currentPitchAngle;
        float startRoll = transform.eulerAngles.z;
        float elapsedTime = 0f;

        while (elapsedTime < FORCED_PITCH_DURATION && isAtMaxAltitude)
        {
            forcedPitchProgress = elapsedTime / FORCED_PITCH_DURATION;

            // Smooth easing curve for more natural motion
            float easedProgress = 1f - Mathf.Pow(1f - forcedPitchProgress, 3); // Cubic ease out

            // Gradually level the roll while pitching down
            float targetPitch = Mathf.Lerp(startPitch, 50f, easedProgress);
            float targetRoll = Mathf.Lerp(startRoll, 0f, easedProgress * 0.7f);

            // Create a target rotation that combines current yaw with our desired pitch and roll
            Quaternion targetRotation = Quaternion.Euler(targetPitch, transform.eulerAngles.y, targetRoll);

            // Smoothly interpolate to the target rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 1.5f);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        isForcedPitchActive = false;
        forcedDescentStartTime = Time.time; // Mark the time when forced descent ends
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

    private void HandleFlying(float throttleInput)
    {
        float baseTargetSpeed = Mathf.Lerp(planeStats.airNormalSpeed * 0.5f, planeStats.airBoostSpeed, throttleInput);

        if (currentAltitude > planeStats.altitudeWarningThreshold)
        {
            float altitudePenalty = Mathf.Clamp01((currentAltitude - planeStats.altitudeWarningThreshold) /
                               (planeStats.maxAltitude - planeStats.altitudeWarningThreshold));
            baseTargetSpeed *= Mathf.Lerp(1f, 0.5f, altitudePenalty);
        }

        ApplyPitchBasedSpeedModifications(ref baseTargetSpeed);
        targetSpeed = baseTargetSpeed;

        float rate = currentSpeed < targetSpeed ? planeStats.accelerationRate : planeStats.decelerationRate;
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, 1f / rate);

        if (!isFreeLookActive && !isForcedPitchActive)
        {
            ApplyRotation();
        }

        Vector3 gravityVector = Vector3.down * gravitationalForce;
        Vector3 forwardVelocity = transform.forward * currentSpeed;
        forwardVelocity += gravityVector * Time.deltaTime * 3.0f;
        rb.linearVelocity = forwardVelocity;
    }

    private void ApplyPitchBasedSpeedModifications(ref float baseSpeed)
    {
        float pitchInfluence = Mathf.Sin(-currentPitchAngle * Mathf.Deg2Rad);

        if (pitchInfluence < 0)
        {
            float diveBoostFactor = Mathf.Abs(pitchInfluence) * diveSpeedBoost;
            baseSpeed += baseSpeed * diveBoostFactor;
        }
        else if (pitchInfluence > 0)
        {
            float climbPenaltyFactor = pitchInfluence * climbSpeedPenalty;
            baseSpeed -= baseSpeed * climbPenaltyFactor;
        }

        baseSpeed += baseSpeed * -pitchInfluence * pitchSpeedInfluence;
    }

    private void ApplyRotation()
    {
        float targetRollVelocity = rollInput * planeStats.rollSpeed * 100f;
        float targetPitchVelocity = pitchInput * planeStats.pitchSpeed * 100f;
        float targetYawVelocity = yawInput * planeStats.yawSpeed * 100f;

        float speedFactor = Mathf.Clamp01(currentSpeed / planeStats.airNormalSpeed);
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

    private void UpdateCameraFOV()
    {
        if (cam == null) return;

        float altitudeFactor = Mathf.Clamp01((currentAltitude - planeStats.altitudeWarningThreshold) /
                         (planeStats.maxAltitude - planeStats.altitudeWarningThreshold));

        float speedFactor = Mathf.InverseLerp(planeStats.airNormalSpeed, planeStats.airBoostSpeed, currentSpeed);
        float speedBasedFOV = Mathf.Lerp(planeStats.defaultFov, planeStats.maxFov, fovTransitionCurve.Evaluate(speedFactor));

        float targetFOV = Mathf.Lerp(speedBasedFOV, planeStats.highAltitudeFOV, altitudeFactor);

        cam.Lens.FieldOfView = Mathf.Lerp(cam.Lens.FieldOfView, targetFOV,
            Time.deltaTime * planeStats.fovSmoothSpeed);
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

    public void TakeDamage(float damageAmount)
    {
        currentHealth = Mathf.Max(0, currentHealth - damageAmount);

        if (currentHealth <= 0)
        {
            Debug.Log($"{planeStats.planeName} has been destroyed!");
        }
    }

    private void StartRepair()
    {
        isRepairing = true;
        repairTimer = 0f;
        Debug.Log($"Starting repair for {planeStats.planeName}");
    }

    private void HandleRepair()
    {
        repairTimer += Time.deltaTime;

        if (repairTimer >= planeStats.repairTime)
        {
            currentHealth = Mathf.Min(planeStats.maxHealth, currentHealth + planeStats.repairAmount);
            isRepairing = false;
            Debug.Log($"Repair complete. Health: {currentHealth}/{planeStats.maxHealth}");
        }
    }
}