using Ashsvp;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Jump Settings")][Space(10)]
    
    [Tooltip("Standard jump height (m) at zero speed")]
    public float minJumpHeight = 1f;

    [Tooltip("Max jump height (m) at $(fullHeightSpeed) speed")]
    public float maxJumpHeight = 3f;

    [Tooltip("Speed when we rich max jump height")]
    public float fullHeightSpeed = 10f;

    [Tooltip("Gravity multiplier while airborne. Higher - faster jump, less - jumping time")]
    public float airGravityMultiplier = 2f;

    private Rigidbody rb;
    private SimcadeVehicleController vehicle;

    private bool IsGrounded => vehicle == null || vehicle.vehicleIsGrounded;

    private float AirGravity => Mathf.Abs(Physics.gravity.y) * airGravityMultiplier;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        vehicle = GetComponent<SimcadeVehicleController>();
    }

    private void FixedUpdate()
    {
        // Extra gravity in the air only, so the car snaps back down instead of floating.
        if (!IsGrounded)
        {
            rb.AddForce(Physics.gravity * (airGravityMultiplier - 1f), ForceMode.Acceleration);
        }
    }

    public void OnJump(InputValue value)
    {
        if (!value.isPressed || !IsGrounded)
        {
            return;
        }

        float speed = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up).magnitude;
        float height = Mathf.Lerp(minJumpHeight, maxJumpHeight, Mathf.Clamp01(speed / fullHeightSpeed));

        // v = sqrt(2 * g * h) - the launch speed that reaches exactly that height.
        Vector3 velocity = rb.linearVelocity;
        velocity.y = Mathf.Sqrt(2f * AirGravity * height);
        rb.linearVelocity = velocity;
    }
}
