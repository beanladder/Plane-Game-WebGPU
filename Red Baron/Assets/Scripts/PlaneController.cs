using UnityEngine;

public class ArcadePlaneController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;

    [Header("Movement Settings")]
    [SerializeField] private float groundAcceleration = 5f;
    [SerializeField] private float airNormalSpeed = 30f;
    [SerializeField] private float airBoostSpeed = 50f;
    [SerializeField] private float speedDecayRate = 2f;
    [SerializeField] private float takeoffSpeed = 20f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxRollAngle = 60f;
    [SerializeField] private float rollSpeed = 2f;
    [SerializeField] private float pitchSpeed = 2f;
    [SerializeField] private float yawSpeed = 1f;
    [SerializeField] private float coordinatedTurnFactor = 2f;  // Controls how much roll is applied during yaw

    // Public state variables for monitoring
    public bool isGrounded = true;
    public float currentSpeed = 0f;
    public float targetSpeed = 0f;
    public Vector3 currentRotation;

    // Private state variables
    private float rollInput = 0f;
    private float yawInput = 0f;

    private void Start()
    {
        // Get reference to the Rigidbody if not set in the Inspector
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        // Lock cursor for flight controls
        Cursor.lockState = CursorLockMode.Locked;

        // Ensure gravity is on at start
        rb.useGravity = true;

        Debug.Log("Plane Controller initialized. Grounded: " + isGrounded);
    }

    private void Update()
    {
        // Get input
        float throttleInput = Input.GetKey(KeyCode.W) ? 1f : 0f;
        float pitchInput = -Input.GetAxis("Mouse Y") * mouseSensitivity;
        yawInput = Input.GetAxis("Mouse X") * mouseSensitivity;

        // Manual roll input based on A/D keys (additional to coordinated turns)
        if (Input.GetKey(KeyCode.A))
            rollInput = Mathf.Lerp(rollInput, -1f, Time.deltaTime * rollSpeed);
        else if (Input.GetKey(KeyCode.D))
            rollInput = Mathf.Lerp(rollInput, 1f, Time.deltaTime * rollSpeed);
        else
            rollInput = Mathf.Lerp(rollInput, 0f, Time.deltaTime * rollSpeed);

        // Handle ground movement
        if (isGrounded)
        {
            HandleGroundMovement(throttleInput);
        }
        else
        {
            HandleAirMovement(throttleInput, pitchInput, yawInput);
        }

        // Update current rotation for debugging
        currentRotation = transform.eulerAngles;
    }

    private void HandleGroundMovement(float throttleInput)
    {
        // Accelerate on ground
        if (throttleInput > 0)
        {
            currentSpeed += groundAcceleration * Time.deltaTime;
            Debug.Log("Accelerating on ground. Current Speed: " + currentSpeed);
        }
        else
        {
            currentSpeed = Mathf.Lerp(currentSpeed, 0f, Time.deltaTime);
        }

        // Apply    forward movement
        rb.linearVelocity = transform.forward * currentSpeed;

        // Check for takeoff
        if (currentSpeed >= takeoffSpeed)
        {
            isGrounded = false;
            // Turn off gravity once takeoff is achieved
            rb.useGravity = false;
            // Apply initial lift
            rb.AddForce(Vector3.up * takeoffSpeed, ForceMode.Impulse);
            Debug.Log("Taking off! Speed: " + currentSpeed + " - Gravity disabled");
        }
    }

    private void HandleAirMovement(float throttleInput, float pitchInput, float yawInput)
    {
        // Set target speed based on throttle
        targetSpeed = throttleInput > 0 ? airBoostSpeed : airNormalSpeed;

        // Smoothly adjust current speed
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * speedDecayRate);

        // Apply rotations
        ApplyRotation(pitchInput, yawInput);

        // Apply forward movement
        rb.linearVelocity = transform.forward * currentSpeed;
    }

    private void ApplyRotation(float pitchInput, float yawInput)
    {
        // Apply pitch (up/down)
        transform.Rotate(Vector3.right * pitchInput * pitchSpeed);

        // Apply yaw (left/right) - this will rotate the plane around its up axis
        transform.Rotate(Vector3.up * yawInput * yawSpeed);

        // Calculate roll based on both manual input and coordinated turn
        // This creates the banking effect during turns
        float coordinatedRoll = -yawInput * coordinatedTurnFactor;
        float totalRollInput = rollInput + coordinatedRoll;
        float effectiveRollAngle = totalRollInput * maxRollAngle;

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

        // Debug roll information for significant changes
        if (Mathf.Abs(yawInput) > 0.1f || Mathf.Abs(rollInput) > 0.1f)
        {
            Debug.Log($"Yaw Input: {yawInput}, Coordinated Roll: {coordinatedRoll}, Manual Roll: {rollInput}, Total Roll: {newRoll}");
        }
    }

    // Method to detect when the plane touches the ground again
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            // Turn gravity back on when landing
            rb.useGravity = true;
            currentSpeed = Mathf.Min(currentSpeed, takeoffSpeed * 0.8f); // Reduce speed on landing
            Debug.Log("Landed! Reduced speed to: " + currentSpeed + " - Gravity enabled");
        }
    }

    // Method when the plane leaves the ground
    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground") && isGrounded)
        {
            Debug.Log("No longer in contact with ground, but still in ground state until takeoff speed is reached");
        }
    }
}