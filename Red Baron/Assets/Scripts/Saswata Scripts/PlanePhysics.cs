using UnityEngine;

[ExecuteAlways] // Enables it in Edit mode
[RequireComponent(typeof(Rigidbody))]
public class PlanePhysics : MonoBehaviour
{
    public Vector3 newCenterOfGravity = Vector3.zero;
    private Rigidbody rb;

    void OnValidate()
    {
        ApplyCoG(); // Updates when you change values in Inspector
    }

    void OnEnable()
    {
        ApplyCoG(); // Also reapply when script re-enables
    }

    void ApplyCoG()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        // Only applies if Rigidbody exists and is editable
        if (rb != null)
            rb.centerOfMass = newCenterOfGravity;
    }

    void OnDrawGizmos()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        Vector3 worldCoG = transform.TransformPoint(rb.centerOfMass);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(worldCoG, 0.2f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(worldCoG + Vector3.up * 0.2f, "Center of Gravity");
#endif
    }
}
