using UnityEngine;

public class PlaneController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;

    [Header("Movement Settings")]
    [SerializeField] public float airNormalSpeed = 30f;
    [SerializeField] public float airBoostSpeed = 50f;
    [SerializeField] private float accelerationRate = 1f;
    [SerializeField] private float decelerationRate = 0.5f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxRollAngle = 360f;
    [SerializeField] private float rollSpeed = 2f;
    [SerializeField] private float pitchSpeed = 2f;
    [SerializeField] private float yawSpeed = 1f;

    [Header("Inertia Settings")]
    [SerializeField] private float rotationalDamping = 0.97f; // Higher values (closer to 1) mean slower deceleration
    [SerializeField] private float rollDamping = 0.98f; // Specific damping for roll to make it even smoother

    [Header("Sensitivity Schemes")]
    [Range(0, 2)]
    [SerializeField] private float pitchSensitivityMultiplier = 1f;
    [Range(0, 2)]
    [SerializeField] private float rollSensitivityMultiplier = 1f;
    [SerializeField] private bool useExponentialPitchResponse = false;
    [SerializeField] private bool useProgressiveRollResponse = false;
    [SerializeField] private float exponentialPitchFactor = 1.5f;
    [SerializeField] private float progressiveRollThreshold = 0.3f;
    [SerializeField] private float progressiveRollMultiplier = 1.5f;

    [Header("Input Smoothing")]
    [SerializeField] private float inputSmoothTime = 0.1f;
    [SerializeField] private float keyboardSmoothTime = 0.15f;

    [Header("Camera Settings")]
    [SerializeField] private bool lockCameraToHorizon = true;

    // Public state variables for monitoring
    public float currentSpeed = 0f;
    public float targetSpeed = 0f;
    public Vector3 currentRotation;

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

    // Reference to the camera (if needed)
    private Camera mainCamera;
    private Transform cameraTransform;

    private void Start()
    {
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
        currentSpeed = airNormalSpeed;
        targetSpeed = airNormalSpeed;

        // Get camera reference
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cameraTransform = mainCamera.transform;
        }

        Debug.Log("Plane Controller initialized in flying mode");
    }

    private void Update()
    {
        // Get throttle input
        float throttleInput = Input.GetKey(KeyCode.W) ? 1f : (Input.GetKey(KeyCode.S) ? -0.5f : 0f);

        // Get base pitch input from mouse Y-axis
        float rawPitchInput = -Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Get base roll input from mouse X-axis
        float rawRollInput = -Input.GetAxis("Mouse X") * mouseSensitivity;

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
        pitchInput = Mathf.SmoothDamp(pitchInput, targetPitchInput, ref smoothPitchVelocity, inputSmoothTime);
        rollInput = Mathf.SmoothDamp(rollInput, targetRollInput, ref smoothRollVelocity, inputSmoothTime);
        yawInput = Mathf.SmoothDamp(yawInput, targetYawInput, ref smoothYawVelocity, keyboardSmoothTime);

        // Handle air movement
        HandleFlying(throttleInput);

        // Update current rotation for debugging
        currentRotation = transform.eulerAngles;

        // Update camera if needed
        if (lockCameraToHorizon && cameraTransform != null && cameraTransform.parent == transform)
        {
            // This will keep the camera upright regardless of the plane's roll
            Vector3 cameraEuler = cameraTransform.localEulerAngles;
            cameraTransform.localEulerAngles = new Vector3(cameraEuler.x, cameraEuler.y, -transform.eulerAngles.z);
        }
    }

    private float ApplyPitchSensitivityScheme(float input)
    {
        // Apply base sensitivity multiplier
        float modifiedInput = input * pitchSensitivityMultiplier;

        // Apply exponential response if enabled
        if (useExponentialPitchResponse)
        {
            // Preserve the sign while applying exponential curve
            float sign = Mathf.Sign(modifiedInput);
            float absValue = Mathf.Abs(modifiedInput);

            // Apply exponential response for more precision at center, more response at extremes
            modifiedInput = sign * Mathf.Pow(absValue, exponentialPitchFactor);
        }

        return modifiedInput;
    }

    private float ApplyRollSensitivityScheme(float input)
    {
        // Apply base sensitivity multiplier
        float modifiedInput = input * rollSensitivityMultiplier;

        // Apply progressive response if enabled
        if (useProgressiveRollResponse)
        {
            // Apply progressive response - faster response after threshold
            if (Mathf.Abs(modifiedInput) > progressiveRollThreshold)
            {
                float exceededAmount = Mathf.Abs(modifiedInput) - progressiveRollThreshold;
                float extraResponse = exceededAmount * progressiveRollMultiplier;

                modifiedInput = Mathf.Sign(modifiedInput) *
                    (progressiveRollThreshold + extraResponse);
            }
        }

        return modifiedInput;
    }

    private void HandleFlying(float throttleInput)
    {
        // Set target speed based on throttle
        if (throttleInput > 0)
        {
            targetSpeed = Mathf.Lerp(airNormalSpeed, airBoostSpeed, throttleInput);
        }
        else if (throttleInput < 0)
        {
            // Allow slowing down
            targetSpeed = Mathf.Lerp(airNormalSpeed, airNormalSpeed * 0.5f, -throttleInput);
        }
        else
        {
            // Cruise at normal speed when no input
            targetSpeed = airNormalSpeed;
        }

        // Determine rate to use based on whether we're speeding up or slowing down
        float rate = currentSpeed < targetSpeed ? accelerationRate : decelerationRate;

        // Smoothly adjust current speed with proper acceleration/deceleration
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, 1f / rate);

        // Apply rotations
        ApplyRotation();

        // Apply forward movement
        rb.linearVelocity = transform.forward * currentSpeed;
    }

    private void ApplyRotation()
    {
        // Calculate target velocities based on inputs
        float targetRollVelocity = rollInput * rollSpeed * 100f;
        float targetPitchVelocity = pitchInput * pitchSpeed * 100f;
        float targetYawVelocity = yawInput * yawSpeed * 100f;

        // Smoothly transition between current velocity and target velocity
        if (Mathf.Abs(rollInput) > 0.01f)
        {
            // When there's input, accelerate toward that input
            rollVelocity = Mathf.Lerp(rollVelocity, targetRollVelocity, Time.deltaTime * 5f);
        }
        else
        {
            // When no input, apply damping to gradually reduce velocity
            rollVelocity *= rollDamping;
        }

        // Same for pitch
        if (Mathf.Abs(pitchInput) > 0.01f)
        {
            pitchVelocity = Mathf.Lerp(pitchVelocity, targetPitchVelocity, Time.deltaTime * 5f);
        }
        else
        {
            pitchVelocity *= rotationalDamping;
        }

        // Same for yaw
        if (Mathf.Abs(yawInput) > 0.01f)
        {
            yawVelocity = Mathf.Lerp(yawVelocity, targetYawVelocity, Time.deltaTime * 5f);
        }
        else
        {
            yawVelocity *= rotationalDamping;
        }

        // Apply the actual rotations based on the velocities
        transform.Rotate(Vector3.right * pitchVelocity * Time.deltaTime, Space.Self);
        transform.Rotate(Vector3.up * yawVelocity * Time.deltaTime, Space.Self);
        transform.Rotate(Vector3.forward * rollVelocity * Time.deltaTime, Space.Self);

        // Debug logging when there are significant velocity changes
        if (Mathf.Abs(rollVelocity) > 5f)
        {
            Debug.Log($"Roll Velocity: {rollVelocity}, Input: {rollInput}");
        }
    }
}