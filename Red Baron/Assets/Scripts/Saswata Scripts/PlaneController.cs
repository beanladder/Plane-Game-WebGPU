using UnityEngine;

public class PlaneController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;

    [Header("Movement Settings")]
    [SerializeField] public float airNormalSpeed = 30f;
    [SerializeField] public float airBoostSpeed = 50f;
    [SerializeField] private float speedDecayRate = 2f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxRollAngle = 60f;
    [SerializeField] private float rollSpeed = 2f;
    [SerializeField] private float pitchSpeed = 2f;
    [SerializeField] private float yawSpeed = 1f;

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
    [SerializeField] private float inputSmoothTime = 0.1f; // Smoothing time for mouse input
    [SerializeField] private float keyboardSmoothTime = 0.15f; // Smoothing time for keyboard input

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

    private void Start()
    {
        // Get reference to the Rigidbody if not set in the Inspector
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        // Lock cursor for flight controls
        Cursor.lockState = CursorLockMode.Locked;

        // Disable gravity - we're always flying
        rb.useGravity = false;

        // Initialize with normal flying speed
        currentSpeed = airNormalSpeed;
        targetSpeed = airNormalSpeed;

        Debug.Log("Plane Controller initialized in flying mode");
    }

    private void Update()
    {
        // Get throttle input
        float throttleInput = Input.GetKey(KeyCode.W) ? 1f : 0f;

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
        targetSpeed = throttleInput > 0 ? airBoostSpeed : airNormalSpeed;

        // Smoothly adjust current speed
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * speedDecayRate);

        // Apply rotations
        ApplyRotation();

        // Apply forward movement
        rb.linearVelocity = transform.forward * currentSpeed;
    }

    private void ApplyRotation()
    {
        // Apply pitch (up/down)
        transform.Rotate(Vector3.right * pitchInput * pitchSpeed);

        // Apply yaw (left/right) - controlled by A/D keys
        transform.Rotate(Vector3.up * yawInput * yawSpeed);

        // Apply roll (banking) - controlled by mouse X
        float effectiveRollAngle = rollInput * maxRollAngle;

        // Get current roll angle
        Vector3 currentEulerAngles = transform.eulerAngles;
        float currentRoll = currentEulerAngles.z;

        // Normalize the angle to -180 to 180
        if (currentRoll > 180f)
            currentRoll -= 360f;

        // Smoothly interpolate to the target roll
        float newRoll = Mathf.Lerp(currentRoll, effectiveRollAngle, Time.deltaTime * rollSpeed);

        // Apply the new roll angle
        transform.eulerAngles = new Vector3(currentEulerAngles.x, currentEulerAngles.y, newRoll);

        // Debug rotation information for significant changes
        if (Mathf.Abs(rollInput) > 0.1f || Mathf.Abs(yawInput) > 0.1f)
        {
            Debug.Log($"Roll Input: {rollInput}, Yaw Input: {yawInput}, Current Roll: {newRoll}");
        }
    }
}