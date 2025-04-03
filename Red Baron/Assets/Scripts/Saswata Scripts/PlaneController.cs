using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

public class PlaneController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private PlaneStats planeStats;

    [Header("Camera Settings")]
    [SerializeField] private CinemachineCamera cam;
    [SerializeField] private CinemachineCamera freeLookCam;
    [SerializeField] private int defaultCamPriority = 10;
    [SerializeField] private int freeLookCamPriority = 20;
    public bool isFreeLookActive = false;
    [SerializeField] private AnimationCurve fovTransitionCurve = AnimationCurve.Linear(0, 0, 1, 1);
    private float initialHorizontalAxisValue;
    private float initialVerticalAxisValue;

    [Header("Stalling & Flight Physics")]
    [SerializeField] private float stallAngleThreshold = 40f;
    [SerializeField] private float stallRecoveryTime = 3f;
    [SerializeField] private float stallSpeedPenalty = 0.6f;
    [SerializeField] private float pitchSpeedInfluence = 0.3f;
    [SerializeField] private float diveSpeedBoost = 0.4f;
    [SerializeField] private float climbSpeedPenalty = 0.5f;
    [SerializeField] private ParticleSystem stallWarningEffect;
    [SerializeField] private AudioSource stallWarningSound;
    [SerializeField] private float gravitationalForce = 9.8f;

    [Header("Debug Settings")]
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color pitchUpColor = Color.yellow;
    [SerializeField] private Color pitchDownColor = Color.cyan;
    [SerializeField] private Color speedIncreasingColor = Color.green;
    [SerializeField] private Color speedDecreasingColor = Color.red;

    private Coroutine resetFreeLookCoroutine;
    private float cameraBlendTime = 1.2f;

    // Public state variables for monitoring
    public float currentSpeed = 0f;
    public float previousSpeed = 0f;
    public float targetSpeed = 0f;
    public Vector3 currentRotation;
    public float currentHealth;
    public bool isStalling = false;
    public float currentPitchAngle = 0f;
    public float previousPitchAngle = 0f;
    public float stallWarningLevel = 0f;

    // Debug state flags
    public bool isPitchingUp = false;
    public bool isPitchingDown = false;
    public bool isSpeedIncreasing = false;
    public bool isSpeedDecreasing = false;

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

    // Stalling variables
    private float stallTimer = 0f;
    private float stallRecoveryCounter = 0f;
    private Vector3 stallRotation = Vector3.zero;
    private float controlReductionFactor = 1f;

    // Reference to the camera
    private Camera mainCamera;
    private Transform cameraTransform;

    // Repair variables
    private bool isRepairing = false;
    private float repairTimer = 0f;

    // GUI Style cache
    private GUIStyle labelStyle;
    private GUIStyle warningStyle;

    private void Start()
    {
        if (planeStats == null)
        {
            Debug.LogError("PlaneStats not assigned to PlaneController!");
            return;
        }

        // Initialize health
        currentHealth = planeStats.maxHealth;

        if (cam != null)
        {
            cam.Lens.FieldOfView = planeStats.defaultFov;
        }

        // Get reference to the Rigidbody if not set in the Inspector
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        // Lock cursor for flight controls
        Cursor.lockState = CursorLockMode.Locked;

        // Enable gravity for realistic flight
        rb.useGravity = true;

        // Set drag to zero for more consistent flight
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;

        // Initialize with normal flying speed
        currentSpeed = planeStats.airNormalSpeed;
        previousSpeed = currentSpeed;
        targetSpeed = planeStats.airNormalSpeed;
        throttleValue = 0.5f;  // Default throttle at 50%

        // Get camera reference
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cameraTransform = mainCamera.transform;
        }

        Debug.Log($"Plane Controller initialized for {planeStats.planeName}");

        if (freeLookCam != null)
        {
            var orbitalFollow = freeLookCam.GetComponent<CinemachineOrbitalFollow>();
            if (orbitalFollow != null)
            {
                initialHorizontalAxisValue = orbitalFollow.HorizontalAxis.Value;
                initialVerticalAxisValue = orbitalFollow.VerticalAxis.Value;
            }
        }

        // Disable stall effects initially
        if (stallWarningEffect != null)
            stallWarningEffect.Stop();
        if (stallWarningSound != null)
            stallWarningSound.Stop();

        // Initialize pitch angle
        currentPitchAngle = NormalizePitchAngle(transform.eulerAngles.x);
        previousPitchAngle = currentPitchAngle;
    }

    private void Update()
    {
        // Handle repair
        if (isRepairing)
        {
            HandleRepair();
            return; // Don't process other inputs while repairing
        }

        // Store previous values for comparison
        previousSpeed = currentSpeed;
        previousPitchAngle = currentPitchAngle;

        // Check for repair initiation
        if (Input.GetKeyDown(KeyCode.R) && currentHealth < planeStats.maxHealth)
        {
            StartRepair();
            return;
        }

        //Switch between default cam and freeLook cam
        if (Input.GetMouseButton(2))
        {
            freeLookCam.Priority = freeLookCamPriority;
            cam.Priority = 0;
            isFreeLookActive = true;
        }
        else
        {
            if (isFreeLookActive) // Only reset when switching back
            {
                StartCoroutine(ResetFreeLookCameraAfterBlend());
            }

            freeLookCam.Priority = 0;
            cam.Priority = defaultCamPriority;
            isFreeLookActive = false;
        }

        // Get throttle input - more granular throttle control
        if (Input.GetKey(KeyCode.W))
            throttleValue = Mathf.Min(throttleValue + Time.deltaTime, 1.0f);
        else if (Input.GetKey(KeyCode.S))
            throttleValue = Mathf.Max(throttleValue - Time.deltaTime * 0.5f, 0.0f);

        // Get base pitch input from mouse Y-axis
        float inversePitchFactor = planeStats.invertPitchAxis ? 1f : -1f;
        float inverseRollFactor = planeStats.invertRollAxis ? 1f : -1f;

        float rawPitchInput = Input.GetAxis("Mouse Y") * planeStats.mouseSensitivity * inversePitchFactor;

        // Get base roll input from mouse X-axis
        float rawRollInput = Input.GetAxis("Mouse X") * planeStats.mouseSensitivity * inverseRollFactor;

        // Apply sensitivity schemes
        targetPitchInput = ApplyPitchSensitivityScheme(rawPitchInput);
        targetRollInput = ApplyRollSensitivityScheme(rawRollInput);

        // Get yaw input from A/D keys
        if (Input.GetKey(KeyCode.A))
            targetYawInput = -1f;
        else if (Input.GetKey(KeyCode.D))
            targetYawInput = 1f;
        else
            targetYawInput = 0f;

        // Apply control reduction during stall recovery
        if (isStalling || stallRecoveryCounter > 0)
        {
            targetPitchInput *= controlReductionFactor;
            targetRollInput *= controlReductionFactor;
            targetYawInput *= controlReductionFactor;
        }

        // Smooth all inputs
        pitchInput = Mathf.SmoothDamp(pitchInput, targetPitchInput, ref smoothPitchVelocity, planeStats.inputSmoothTime);
        rollInput = Mathf.SmoothDamp(rollInput, targetRollInput, ref smoothRollVelocity, planeStats.inputSmoothTime);
        yawInput = Mathf.SmoothDamp(yawInput, targetYawInput, ref smoothYawVelocity, planeStats.keyboardSmoothTime);

        // Handle air movement
        HandleFlying(throttleValue);

        // Update current rotation for debugging
        currentRotation = transform.eulerAngles;

        // Update current pitch angle (normalized to -180 to 180)
        currentPitchAngle = NormalizePitchAngle(transform.eulerAngles.x);

        // Update debug state flags
        UpdateDebugFlags();

        // Check for stalling condition
        CheckStallCondition();

        // Update camera if needed
        if (planeStats.lockCameraToHorizon && cameraTransform != null && cameraTransform.parent == transform)
        {
            // This will keep the camera upright regardless of the plane's roll
            Vector3 cameraEuler = cameraTransform.localEulerAngles;
            cameraTransform.localEulerAngles = new Vector3(cameraEuler.x, cameraEuler.y, -transform.eulerAngles.z);
        }

        if (!isFreeLookActive)
        {
            UpdateCameraFOV();
        }
    }

    private void UpdateDebugFlags()
    {
        // Check if pitching up or down (comparing current to previous)
        float pitchDelta = currentPitchAngle - previousPitchAngle;
        isPitchingUp = pitchDelta < -0.5f;    // Negative delta means nose going up in Unity
        isPitchingDown = pitchDelta > 0.5f;   // Positive delta means nose going down in Unity

        // Check if speed increasing or decreasing
        float speedDelta = currentSpeed - previousSpeed;
        isSpeedIncreasing = speedDelta > 0.2f;
        isSpeedDecreasing = speedDelta < -0.2f;
    }

    private float NormalizePitchAngle(float angle)
    {
        // Convert 0-360 angle to -180 to 180 range
        if (angle > 180)
            angle -= 360;
        return angle;
    }

    private void CheckStallCondition()
    {
        // Calculate the absolute pitch angle
        float absPitchAngle = Mathf.Abs(currentPitchAngle);

        // In Unity, positive pitch actually means nose down, negative means nose up
        // (this is the core fix - the original logic was inverted)
        bool stallRisk = absPitchAngle > stallAngleThreshold &&
                          currentPitchAngle < 0 && // Only stall on negative pitch (climbing)
                          currentSpeed < planeStats.airNormalSpeed * 0.8f;

        // Calculate stall warning level (0-1) for effects and sound
        stallWarningLevel = Mathf.Clamp01((absPitchAngle - stallAngleThreshold + 10) / 20) *
                           (1 - (currentSpeed / planeStats.airNormalSpeed));

        // Manage stall warning effects
        ManageStallWarningEffects();

        // If we're already stalling, handle the stall state
        if (isStalling)
        {
            HandleStallingState();
            return;
        }

        // Check if we should enter a stall
        if (stallRisk)
        {
            stallTimer += Time.deltaTime;

            // If we've been at risk of stalling for too long, initiate stall
            if (stallTimer > 1.5f)
            {
                EnterStall();
            }
        }
        else
        {
            stallTimer = Mathf.Max(0, stallTimer - Time.deltaTime * 2);
        }
    }

    private void ManageStallWarningEffects()
    {
        // Handle stall warning effects
        if (stallWarningLevel > 0.3f && !isStalling)
        {
            // Visual effect intensity
            if (stallWarningEffect != null && !stallWarningEffect.isPlaying)
            {
                stallWarningEffect.Play();
            }

            // Sound effect intensity
            if (stallWarningSound != null)
            {
                if (!stallWarningSound.isPlaying)
                    stallWarningSound.Play();

                stallWarningSound.volume = stallWarningLevel;
                stallWarningSound.pitch = 0.8f + stallWarningLevel * 0.4f;
            }
        }
        else
        {
            if (stallWarningEffect != null && stallWarningEffect.isPlaying && !isStalling)
            {
                stallWarningEffect.Stop();
            }

            if (stallWarningSound != null && stallWarningSound.isPlaying && !isStalling)
            {
                stallWarningSound.Stop();
            }
        }
    }

    private void EnterStall()
    {
        isStalling = true;
        stallRecoveryCounter = 0;

        // Store the random rotation to apply during stall
        stallRotation = new Vector3(
            Random.Range(15f, 30f),  // Pitch down (positive in Unity)
            Random.Range(-15f, 15f),   // Random yaw
            Random.Range(-180f, 180f)  // Random roll
        );

        // Drastically reduce speed
        targetSpeed *= stallSpeedPenalty;

        // Reduce control effectiveness
        controlReductionFactor = 0.2f;

        Debug.Log("STALL! NOSE DOWN TO RECOVER.");
    }

    private void HandleStallingState()
    {
        // Apply stall rotation (nose drops, random roll)
        transform.Rotate(stallRotation * Time.deltaTime * 0.5f, Space.Self);

        // Gradually increase control as recovery progresses
        stallRecoveryCounter += Time.deltaTime;

        // Check if we're recovering (pointing nose down)
        bool isRecovering = currentPitchAngle > 0 && rb.linearVelocity.magnitude > planeStats.airNormalSpeed * 0.6f;

        if (isRecovering)
        {
            // Improve control gradually
            controlReductionFactor = Mathf.Lerp(0.2f, 1.0f, stallRecoveryCounter / stallRecoveryTime);

            // If we've recovered long enough, exit stall
            if (stallRecoveryCounter >= stallRecoveryTime)
            {
                ExitStall();
            }
        }
        else
        {
            // Reset recovery counter if not actively recovering
            stallRecoveryCounter = Mathf.Max(0, stallRecoveryCounter - Time.deltaTime * 0.5f);

            // Keep controls limited
            controlReductionFactor = 0.2f;
        }
    }

    private void ExitStall()
    {
        isStalling = false;
        stallTimer = 0;
        controlReductionFactor = 1.0f;

        // Set a recovery period where controls gradually return to full
        StartCoroutine(StallRecoveryPeriod());

        Debug.Log("Stall recovered!");
    }

    private IEnumerator StallRecoveryPeriod()
    {
        float recoveryTime = 2.0f;
        float timer = 0;

        while (timer < recoveryTime)
        {
            timer += Time.deltaTime;
            controlReductionFactor = Mathf.Lerp(0.5f, 1.0f, timer / recoveryTime);
            yield return null;
        }

        controlReductionFactor = 1.0f;
        stallRecoveryCounter = 0;
    }

    private IEnumerator ResetFreeLookCameraAfterBlend()
    {
        yield return new WaitForSeconds(1.2f); // Wait for the blend to finish

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
        // Apply base sensitivity multiplier
        float modifiedInput = input * planeStats.pitchSensitivityMultiplier;

        // Apply exponential response if enabled
        if (planeStats.useExponentialPitchResponse)
        {
            // Preserve the sign while applying exponential curve
            float sign = Mathf.Sign(modifiedInput);
            float absValue = Mathf.Abs(modifiedInput);

            // Apply exponential response for more precision at center, more response at extremes
            modifiedInput = sign * Mathf.Pow(absValue, planeStats.exponentialPitchFactor);
        }

        return modifiedInput;
    }

    private float ApplyRollSensitivityScheme(float input)
    {
        // Apply base sensitivity multiplier
        float modifiedInput = input * planeStats.rollSensitivityMultiplier;

        // Apply progressive response if enabled
        if (planeStats.useProgressiveRollResponse)
        {
            // Apply progressive response - faster response after threshold
            if (Mathf.Abs(modifiedInput) > planeStats.progressiveRollThreshold)
            {
                float exceededAmount = Mathf.Abs(modifiedInput) - planeStats.progressiveRollThreshold;
                float extraResponse = exceededAmount * planeStats.progressiveRollMultiplier;

                modifiedInput = Mathf.Sign(modifiedInput) *
                    (planeStats.progressiveRollThreshold + extraResponse);
            }
        }

        return modifiedInput;
    }

    private void HandleFlying(float throttleInput)
    {
        // Calculate base target speed from throttle
        float baseTargetSpeed = Mathf.Lerp(planeStats.airNormalSpeed * 0.5f, planeStats.airBoostSpeed, throttleInput);

        // Apply pitch-based speed modifications
        ApplyPitchBasedSpeedModifications(ref baseTargetSpeed);

        // Set the final target speed
        targetSpeed = baseTargetSpeed;

        // Determine rate to use based on whether we're speeding up or slowing down
        float rate = currentSpeed < targetSpeed ? planeStats.accelerationRate : planeStats.decelerationRate;

        // Smoothly adjust current speed with proper acceleration/deceleration
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, 1f / rate);

        // Apply rotations
        if (!isFreeLookActive)
        {
            ApplyRotation();
        }

        // Apply gravity - stronger when stalling
        Vector3 gravityVector = Vector3.down * gravitationalForce * (isStalling ? 2.0f : 1.0f);

        // Calculate basic forward velocity
        Vector3 forwardVelocity = transform.forward * currentSpeed;

        // Blend in gravity influence based on pitch
        if (!isStalling)
        {
            forwardVelocity += gravityVector * Time.deltaTime * 3.0f;
        }
        else
        {
            // When stalling, gravity takes over more dramatically
            forwardVelocity = Vector3.Lerp(forwardVelocity, gravityVector.normalized * currentSpeed * 0.7f,
                                           Time.deltaTime * 2.0f);
        }

        // Apply the velocity
        rb.linearVelocity = forwardVelocity;
    }

    private void ApplyPitchBasedSpeedModifications(ref float baseSpeed)
    {
        // Don't modify speed if stalling (handled separately)
        if (isStalling)
            return;

        // In Unity's coordinate system:
        // Positive X rotation = nose down (diving)
        // Negative X rotation = nose up (climbing)

        // Calculate pitch angle influence (positive = nose down, negative = nose up)
        float pitchInfluence = Mathf.Sin(-currentPitchAngle * Mathf.Deg2Rad);

        // Diving (nose down) adds speed
        if (pitchInfluence < 0)
        {
            // More speed boost during steeper dives
            float diveBoostFactor = Mathf.Abs(pitchInfluence) * diveSpeedBoost;
            baseSpeed += baseSpeed * diveBoostFactor;
        }
        // Climbing (nose up) reduces speed
        else if (pitchInfluence > 0)
        {
            // More speed penalty during steeper climbs
            float climbPenaltyFactor = pitchInfluence * climbSpeedPenalty;
            baseSpeed -= baseSpeed * climbPenaltyFactor;
        }

        // Apply overall pitch influence to make sure we're not climbing too fast on low throttle
        baseSpeed += baseSpeed * -pitchInfluence * pitchSpeedInfluence;
    }

    private void ApplyRotation()
    {
        // Calculate target velocities based on inputs
        float targetRollVelocity = rollInput * planeStats.rollSpeed * 100f;
        float targetPitchVelocity = pitchInput * planeStats.pitchSpeed * 100f;
        float targetYawVelocity = yawInput * planeStats.yawSpeed * 100f;

        // Modify control effectiveness based on speed
        float speedFactor = Mathf.Clamp01(currentSpeed / planeStats.airNormalSpeed);
        targetPitchVelocity *= speedFactor;
        targetRollVelocity *= speedFactor;
        targetYawVelocity *= speedFactor;

        // Smoothly transition between current velocity and target velocity
        if (Mathf.Abs(rollInput) > 0.01f)
        {
            // When there's input, accelerate toward that input
            rollVelocity = Mathf.Lerp(rollVelocity, targetRollVelocity, Time.deltaTime * 5f);
        }
        else
        {
            // When no input, apply damping to gradually reduce velocity
            rollVelocity *= planeStats.rollDamping;
        }

        // Same for pitch
        if (Mathf.Abs(pitchInput) > 0.01f)
        {
            pitchVelocity = Mathf.Lerp(pitchVelocity, targetPitchVelocity, Time.deltaTime * 5f);
        }
        else
        {
            pitchVelocity *= planeStats.rotationalDamping;
        }

        // Same for yaw
        if (Mathf.Abs(yawInput) > 0.01f)
        {
            yawVelocity = Mathf.Lerp(yawVelocity, targetYawVelocity, Time.deltaTime * 5f);
        }
        else
        {
            yawVelocity *= planeStats.rotationalDamping;
        }

        // Apply the actual rotations based on the velocities
        transform.Rotate(Vector3.right * pitchVelocity * Time.deltaTime, Space.Self);
        transform.Rotate(Vector3.up * yawVelocity * Time.deltaTime, Space.Self);
        transform.Rotate(Vector3.forward * rollVelocity * Time.deltaTime, Space.Self);
    }

    private void UpdateCameraFOV()
    {
        if (cam == null) return;

        // Normalize speed between 0 (normal speed) and 1 (boost speed)
        float speedFactor = Mathf.InverseLerp(planeStats.airNormalSpeed, planeStats.airBoostSpeed, currentSpeed);

        // Get transition effect from curve (affects how quickly it moves towards maxFOV)
        float curveFactor = fovTransitionCurve.Evaluate(speedFactor);

        // Calculate target FOV based on curve influence
        float targetFOV = Mathf.Lerp(planeStats.defaultFov, planeStats.maxFov, curveFactor);

        // Smooth transition
        cam.Lens.FieldOfView = Mathf.Lerp(cam.Lens.FieldOfView, targetFOV, Time.deltaTime * planeStats.fovSmoothSpeed);
    }

    // Health and repair methods
    public void TakeDamage(float damageAmount)
    {
        currentHealth = Mathf.Max(0, currentHealth - damageAmount);

        if (currentHealth <= 0)
        {
            // Implement plane destruction logic here
            Debug.Log($"{planeStats.planeName} has been destroyed!");
        }
    }

    private void StartRepair()
    {
        isRepairing = true;
        repairTimer = 0f;

        // Optional: disable movement controls, play repair animation, etc.
        Debug.Log($"Starting repair for {planeStats.planeName}");
    }

    private void HandleRepair()
    {
        repairTimer += Time.deltaTime;

        if (repairTimer >= planeStats.repairTime)
        {
            // Repair complete
            currentHealth = Mathf.Min(planeStats.maxHealth, currentHealth + planeStats.repairAmount);
            isRepairing = false;
            Debug.Log($"Repair complete. Health: {currentHealth}/{planeStats.maxHealth}");
        }
    }

    // Initialize GUI styles
    private void InitGUIStyles()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 16;
            labelStyle.fontStyle = FontStyle.Bold;
        }

        if (warningStyle == null)
        {
            warningStyle = new GUIStyle(GUI.skin.label);
            warningStyle.fontSize = 20;
            warningStyle.fontStyle = FontStyle.Bold;
            warningStyle.alignment = TextAnchor.MiddleCenter;
        }
    }

    // Debug visualization
    private void OnGUI()
    {
        InitGUIStyles();

        // Basic flight info
        GUI.color = normalTextColor;
        GUI.Label(new Rect(10, 10, 300, 20), $"Speed: {currentSpeed:F1} / {targetSpeed:F1}", labelStyle);
        GUI.Label(new Rect(10, 30, 300, 20), $"Pitch Angle: {currentPitchAngle:F1}°", labelStyle);

        // Pitch indicators
        if (isPitchingUp)
        {
            GUI.color = pitchUpColor;
            GUI.Label(new Rect(10, 50, 300, 20), "? PITCHING UP", labelStyle);
        }
        else if (isPitchingDown)
        {
            GUI.color = pitchDownColor;
            GUI.Label(new Rect(10, 50, 300, 20), "? PITCHING DOWN", labelStyle);
        }

        // Speed change indicators
        if (isSpeedIncreasing)
        {
            GUI.color = speedIncreasingColor;
            GUI.Label(new Rect(10, 70, 300, 20), "? SPEED INCREASING", labelStyle);
        }
        else if (isSpeedDecreasing)
        {
            GUI.color = speedDecreasingColor;
            GUI.Label(new Rect(10, 70, 300, 20), "? SPEED DECREASING", labelStyle);
        }

        // Health indicator
        GUI.color = Color.Lerp(Color.red, Color.green, currentHealth / planeStats.maxHealth);
        GUI.Label(new Rect(10, 90, 300, 20), $"Health: {currentHealth:F0}/{planeStats.maxHealth}", labelStyle);

        // Show stall indicators
        if (isStalling)
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 - 25, 300, 50), "STALL! NOSE DOWN TO RECOVER", warningStyle);
        }
        else if (stallWarningLevel > 0.3f)
        {
            GUI.color = Color.yellow;
            GUI.Label(new Rect(Screen.width / 2 - 100, Screen.height / 2 - 25, 200, 50), "STALL WARNING", warningStyle);
        }

        // Reset color
        GUI.color = Color.white;
    }
}