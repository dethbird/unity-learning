using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CarInputReader))]
public class CarController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float motorForce = 400f;
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float maxReverseSpeed = 10f;
    [SerializeField] private float steerTorque = 140f;
    [SerializeField] private float maxAngularVelocity = 3.5f;

    [Header("Boost Settings")]
    [SerializeField] private float boostForceMultiplier = 3f;
    [SerializeField] private float boostMaxSpeedMultiplier = 1.4f;
    [SerializeField] private float boostImpulse = 12f;
    [SerializeField] private float boostDuration = 0.75f;
    [SerializeField] private float boostCooldown = 3f;
    private float boostTimer = 0f;
    private float boostCooldownTimer = 0f;
    private bool isBoosting = false;
    public bool IsBoosting => isBoosting;
    public bool CanBoost => boostCooldownTimer <= 0f;
    public float BoostChargePercent => CanBoost ? 1f : 1f - (boostCooldownTimer / boostCooldown);

    [Header("Handbrake Settings")]
    [SerializeField] private float normalSideGrip = 0.65f;
    [SerializeField] private float handbrakeSideGrip = 0.99f;
    [SerializeField] private float handbrakeForwardBleed = 0.994f;
    [SerializeField] private float handbrakeSpeedBleed = 0.994f;
    [SerializeField] private float handbrakeYawDamping = 0.9f;
    [SerializeField] private float handbrakeBrakeAcceleration = 1.12f;

    [Header("Ground / Air")]
    [SerializeField] private float groundCheckDistance = 0.45f;
    [SerializeField] private LayerMask groundLayerMask = ~0;
    [SerializeField] private float airborneExtraGravity = 12f;
    [SerializeField] private float throttleVelocityAlignRate = 8f;
    [SerializeField] private float maxFallSpeed = 25f;
    [SerializeField] private float maxLaunchSpeed = 6f;

    [SerializeField] private float airbornePitchDamping = 0.85f;
    [SerializeField] private float pitchLevelStartDistance = 1.8f;
    [SerializeField] private float preLandingFloatStrength = 10f;

    [Header("Reset Settings")]
    [SerializeField] private float resetHeight = 2f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool logInputValues = false;

    private Rigidbody rb;
    private Collider carCollider;
    private CarInputReader input;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private bool isGrounded;
    private bool wasGrounded;
    private Vector3 groundNormal = Vector3.up;
    private float landingSteeringResetTimer;
    private float groundedDrag;

    public bool IsGrounded => isGrounded;
    public bool IsAirborne => !isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        carCollider = GetComponent<Collider>();
        input = GetComponent<CarInputReader>();

        rb.constraints = RigidbodyConstraints.FreezeRotationZ;

        groundedDrag = rb.linearDamping;

        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    private void OnDrawGizmos()
    {
        if (showDebugGizmos)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 3f);
            Gizmos.DrawSphere(transform.position + transform.forward * 3f, 0.2f);
        }
    }

    private void FixedUpdate()
    {
        wasGrounded = isGrounded;
        UpdateGrounded();

        if (!wasGrounded && isGrounded)
        {
            rb.linearDamping = groundedDrag;
            AbsorbLandingImpact();
        }
        else if (wasGrounded && !isGrounded)
        {
            rb.linearDamping = 0f;
            Vector3 v = rb.linearVelocity;
            if (v.y > maxLaunchSpeed)
            {
                v.y = maxLaunchSpeed;
                rb.linearVelocity = v;
            }
        }

        ApplyAirborneGravity();
        UpdateBoost();
        HandleThrottle();
        AlignVelocityToFacingWhenAccelerating();
        HandleSteering();
        HandleHandbrake();
    }

    private void Update()
    {
        if (input.ResetPressedThisFrame)
        {
            ResetCar();
        }

        if (input.PausePressedThisFrame)
        {
            Debug.Log("Pause pressed - implement pause menu here");
        }

        if (input.BoostPressedThisFrame && CanBoost && !isBoosting)
        {
            isBoosting = true;
            boostTimer = boostDuration;
            rb.AddForce(-transform.forward * boostImpulse, ForceMode.Impulse);
        }
    }

    private void UpdateBoost()
    {
        if (isBoosting)
        {
            boostTimer -= Time.fixedDeltaTime;
            if (boostTimer <= 0f)
            {
                isBoosting = false;
                boostCooldownTimer = boostCooldown;
            }
        }

        if (boostCooldownTimer > 0f)
        {
            boostCooldownTimer -= Time.fixedDeltaTime;
        }
    }

    private void HandleThrottle()
    {
        if (!isGrounded)
        {
            return;
        }

        Vector3 planarVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        Vector3 driveDirection = Vector3.ProjectOnPlane(-transform.forward, Vector3.up).normalized;
        if (driveDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float currentSpeed = Vector3.Dot(planarVelocity, driveDirection);
        float driveInput = input.Throttle - input.Brake;
        float effectiveMaxForwardSpeed = isBoosting
            ? maxSpeed * boostMaxSpeedMultiplier
            : maxSpeed;
        float effectiveMaxReverseSpeed = maxReverseSpeed;

        bool canApplyDriveForce = driveInput > 0f
            ? currentSpeed < effectiveMaxForwardSpeed
            : driveInput < 0f && currentSpeed > -effectiveMaxReverseSpeed;

        if (Mathf.Abs(driveInput) > 0.01f && canApplyDriveForce)
        {
            float force = motorForce * driveInput;

            if (isBoosting && driveInput > 0)
            {
                force *= boostForceMultiplier;
            }

            rb.AddForce(driveDirection * force, ForceMode.Force);

            if (logInputValues)
            {
                Debug.Log($"DRIVE - Input: {driveInput:F2}, Force: {force}, Boost: {isBoosting}, Speed: {currentSpeed:F2}");
            }
        }
    }

    private void HandleSteering()
    {
        if (Mathf.Abs(input.Steer) > 0.01f)
        {
            float turnAmount = input.Steer * steerTorque;
            rb.AddTorque(Vector3.up * turnAmount, ForceMode.Acceleration);
        }

        Vector3 angularVelocity = rb.angularVelocity;
        angularVelocity.y = Mathf.Clamp(angularVelocity.y, -maxAngularVelocity, maxAngularVelocity);
        rb.angularVelocity = angularVelocity;
    }

    private void HandleHandbrake()
    {
        if (!isGrounded) return;

        Vector3 forwardDir = -transform.forward;
        Vector3 rightDir = transform.right;

        Vector3 velocity = rb.linearVelocity;
        Vector3 verticalVelocity = Vector3.Project(velocity, Vector3.up);

        Vector3 forwardVelocity = Vector3.Project(velocity, forwardDir);
        Vector3 sidewaysVelocity = Vector3.Project(velocity, rightDir);

        if (input.HandbrakeHeld)
        {
            forwardVelocity *= handbrakeForwardBleed;
            sidewaysVelocity *= handbrakeSideGrip;

            rb.linearVelocity = (forwardVelocity + sidewaysVelocity) * handbrakeSpeedBleed + verticalVelocity;

            if (forwardVelocity.sqrMagnitude > 0.01f)
            {
                rb.AddForce(-forwardVelocity.normalized * handbrakeBrakeAcceleration, ForceMode.Acceleration);
            }

            Vector3 angularVelocity = rb.angularVelocity;
            angularVelocity.y *= handbrakeYawDamping;
            rb.angularVelocity = angularVelocity;

            return;
        }

        rb.linearVelocity = forwardVelocity + sidewaysVelocity * normalSideGrip + verticalVelocity;
    }

    private void UpdateGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        float rayDistance = groundCheckDistance;

        if (carCollider != null)
        {
            origin = carCollider.bounds.center;
            rayDistance = carCollider.bounds.extents.y + groundCheckDistance;
        }

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            isGrounded = true;
            groundNormal = hit.normal;
        }
        else
        {
            isGrounded = false;
            groundNormal = Vector3.up;
        }
    }

    private float GetHeightAboveGround()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 50f, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            return hit.distance;
        }

        return float.MaxValue;
    }

    private void ApplyAirborneGravity()
    {
        if (isGrounded) return;

        rb.AddForce(Vector3.down * airborneExtraGravity, ForceMode.Acceleration);

        Vector3 velocity = rb.linearVelocity;
        if (velocity.y < -maxFallSpeed)
        {
            velocity.y = -maxFallSpeed;
            rb.linearVelocity = velocity;
        }

        // Pre-landing phase: smoothly level and cushion fall when close to ground
        float heightAboveGround = GetHeightAboveGround();
        if (heightAboveGround < pitchLevelStartDistance)
        {
            float t = 1f - Mathf.Clamp01(heightAboveGround / pitchLevelStartDistance);

            // Steer angular velocity toward level pitch/roll — don't use MoveRotation
            // as it fights gravity integration on non-kinematic rigidbodies
            Vector3 angVel = rb.angularVelocity;
            angVel.x = Mathf.MoveTowards(angVel.x, 0f, preLandingFloatStrength * t * Time.fixedDeltaTime);
            angVel.z = Mathf.MoveTowards(angVel.z, 0f, preLandingFloatStrength * t * Time.fixedDeltaTime);
            rb.angularVelocity = angVel;

            // Once mostly level, add extra pull-down so the car snaps to the ground quickly
            float pitchAngle = transform.eulerAngles.x;
            if (pitchAngle > 180f) pitchAngle -= 360f;
            if (Mathf.Abs(pitchAngle) < 15f)
            {
                rb.AddForce(Vector3.down * airborneExtraGravity * 3f * t, ForceMode.Acceleration);
            }
        }
    }

    private void AlignVelocityToFacingWhenAccelerating()
    {
        if (!isGrounded || input.Throttle <= 0.01f)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        Vector3 verticalVelocity = Vector3.Project(velocity, Vector3.up);
        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);

        float speed = planarVelocity.magnitude;
        if (speed < 0.01f)
        {
            return;
        }

        Vector3 facingDirection = Vector3.ProjectOnPlane(-transform.forward, Vector3.up).normalized;
        if (facingDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 desiredDirection = facingDirection.normalized;
        Vector3 desiredPlanarVelocity = desiredDirection * speed;

        float alignmentAmount = 1f - Mathf.Abs(Vector3.Dot(planarVelocity.normalized, desiredDirection));
        float alignRate = throttleVelocityAlignRate * Mathf.Lerp(1f, 1.75f, alignmentAmount);

        Vector3 alignedPlanarVelocity = Vector3.RotateTowards(
            planarVelocity,
            desiredPlanarVelocity,
            alignRate * Time.fixedDeltaTime,
            0f);

        rb.linearVelocity = alignedPlanarVelocity + verticalVelocity;
    }

    private void AbsorbLandingImpact()
    {
        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;
        rb.angularVelocity = Vector3.zero;

        Vector3 euler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
    }

    private void ResetCar()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = spawnPosition + Vector3.up * resetHeight;
        transform.rotation = spawnRotation;
    }
}
