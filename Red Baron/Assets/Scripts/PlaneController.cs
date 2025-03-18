using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlaneController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float maxSpeed = 150f;
    public float acceleration = 10f;
    public float turnSpeed = 80f;
    public float rollSpeed = 120f;

    private Rigidbody rb;
    private float currentSpeed;
    private float targetSpeed;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // Disable physics rotation
        currentSpeed = maxSpeed * 0.3f; 
    }

    void Update()
    {
        HandleThrottle();
        HandleRotation();
        MovePlane();
    }

    void HandleThrottle()
    {
        float throttle = Input.GetAxis("Vertical");
        targetSpeed = Mathf.Clamp(targetSpeed + throttle * acceleration * Time.deltaTime, 0, maxSpeed);
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * acceleration);
    }

    void HandleRotation()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        // Calculate rotation angles
        float pitch = -mouseY * turnSpeed * Time.deltaTime;
        float yaw = mouseX * turnSpeed * Time.deltaTime;
        float roll = mouseX * rollSpeed * Time.deltaTime;

        // Apply rotation with banking
        transform.Rotate(pitch, yaw, roll, Space.Self);
    }

    // In PlaneController.cs MovePlane() method:
    void MovePlane()
    {
        rb.linearVelocity = -transform.forward * currentSpeed; // Add negative sign
    }
}