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

    private Coroutine resetFreeLookCoroutine;
    private float cameraBlendTime = 1.2f;


    // Public state variables for monitoring
    public float currentSpeed = 0f;
    public float targetSpeed = 0f;
    public Vector3 currentRotation;
    public float currentHealth;

    // Private state variables
    private float rollInput = 0f;
    private float yawInput = 0f;
    private float pitchInput = 0f;

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

    // Reference to the camera
    private Camera mainCamera;
    private Transform cameraTransform;

    // Repair variables
    private bool isRepairing = false;
    private float repairTimer = 0f;

    // Altitude resistance variables
    private float altitudeResistanceFactor = 0f;
    private Vector3 lastPosition;
    private float verticalSpeed = 0f;
    private bool isInAltitudeWarningZone = false;

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

        // Disable gravity - we're always flying
        rb.useGravity = false;

        // Set drag to zero for more consistent flight
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;

        // Initialize with normal flying speed
        currentSpeed = planeStats.airNormalSpeed;
        targetSpeed = planeStats.airNormalSpeed;

        // Get camera reference
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cameraTransform = mainCamera.transform;
        }

        // Initialize lastPosition for vertical speed calculation
        lastPosition = transform.position;

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

    }

    private void Update()
    {
        // Handle repair
        if (isRepairing)
        {
            HandleRepair();
            return; // Don't process other inputs while repairing
        }

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


        // Get throttle input
        float throttleInput = Input.GetKey(KeyCode.W) ? 1f : (Input.GetKey(KeyCode.S) ? -0.5f : 0f);

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

        // Smooth all inputs
        pitchInput = Mathf.SmoothDamp(pitchInput, targetPitchInput, ref smoothPitchVelocity, planeStats.inputSmoothTime);
        rollInput = Mathf.SmoothDamp(rollInput, targetRollInput, ref smoothRollVelocity, planeStats.inputSmoothTime);
        yawInput = Mathf.SmoothDamp(yawInput, targetYawInput, ref smoothYawVelocity, planeStats.keyboardSmoothTime);

        // Handle air movement
        HandleFlying(throttleInput);

        // Update current rotation for debugging
        currentRotation = transform.eulerAngles;

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

    private float CalculateAltitudeResistance()
    {
        // Get current Y position (altitude)
        float currentAltitude = transform.position.y;

        // Check if we're above the max comfortable altitude
        if (currentAltitude <= planeStats.maxAltitude)
        {
            return 0f; // No resistance below max altitude
        }

        // Calculate how far into the resistance zone we are (0 to 1)
        float distanceIntoZone = currentAltitude - planeStats.maxAltitude;
        float zoneProgress = Mathf.Clamp01(distanceIntoZone / planeStats.altitudeResistanceZone);

        // Apply non-linear curve to the resistance (makes it increase more dramatically)
        float resistance = Mathf.Pow(zoneProgress, planeStats.altitudeResistanceCurve);

        // Scale by the maximum resistance amount
        return resistance * planeStats.maxAltitudeResistance;
    }

    private void HandleFlying(float throttleInput)
    {
        // Calculate vertical speed
        if (Time.deltaTime > 0)
        {
            verticalSpeed = (transform.position.y - lastPosition.y) / Time.deltaTime;
        }
        lastPosition = transform.position;

        // Calculate altitude resistance
        altitudeResistanceFactor = CalculateAltitudeResistance();

        // Update warning zone state for feedback
        bool previousWarningState = isInAltitudeWarningZone;
        isInAltitudeWarningZone = transform.position.y > planeStats.maxAltitude;

        // Provide feedback when entering warning zone
        if (planeStats.enableAltitudeWarning && !previousWarningState && isInAltitudeWarningZone)
        {
            Debug.Log("Warning: Approaching maximum altitude. Reduced thrust.");
            // You could trigger audio or visual feedback here
        }

        // Set target speed based on throttle and altitude resistance
        if (throttleInput > 0)
        {
            float adjustedSpeed = Mathf.Lerp(planeStats.airNormalSpeed, planeStats.airBoostSpeed, throttleInput);
            // Apply altitude resistance to speed
            targetSpeed = adjustedSpeed * (1f - altitudeResistanceFactor);
        }
        else if (throttleInput < 0)
        {
            // Allow slowing down
            targetSpeed = Mathf.Lerp(planeStats.airNormalSpeed, planeStats.airNormalSpeed * 0.5f, -throttleInput);
        }
        else
        {
            // Cruise at normal speed when no input, with altitude resistance
            targetSpeed = planeStats.airNormalSpeed * (1f - altitudeResistanceFactor);
        }

        // Determine rate to use based on whether we're speeding up or slowing down
        float rate = currentSpeed < targetSpeed ? planeStats.accelerationRate : planeStats.decelerationRate;

        // Smoothly adjust current speed with proper acceleration/deceleration
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, 1f / rate);

        // Apply rotations
        if (!isFreeLookActive)
        {
            ApplyRotation();
        }

        // Calculate basic forward velocity
        Vector3 forwardVelocity = transform.forward * currentSpeed;

        // If climbing and in resistance zone, apply downward force proportional to vertical speed
        if (verticalSpeed > 0 && altitudeResistanceFactor > 0)
        {
            // Calculate a downward force that increases with altitude and vertical speed
            Vector3 downwardForce = Vector3.down * (verticalSpeed * planeStats.verticalSpeedReductionFactor * altitudeResistanceFactor);

            // Apply the combined velocity
            rb.linearVelocity = forwardVelocity + downwardForce;
        }
        else
        {
            // Normal velocity outside resistance zone
            rb.linearVelocity = forwardVelocity;
        }
    }

    private void ApplyRotation()
    {
        // Calculate target velocities based on inputs
        float targetRollVelocity = rollInput * planeStats.rollSpeed * 100f;
        float targetPitchVelocity = pitchInput * planeStats.pitchSpeed * 100f;
        float targetYawVelocity = yawInput * planeStats.yawSpeed * 100f;

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
}