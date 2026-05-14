using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CarInputReader))]
public class CarController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float motorForce = 400f;
    [SerializeField] private float brakeForce = 400f;
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float maxReverseSpeed = 10f;
    [SerializeField] private float steerTorque = 140f;
    [SerializeField] private float maxAngularVelocity = 3.5f;
    [SerializeField] private float minSteerMultiplierAtMaxSpeed = 0.55f;

    [Header("Boost Settings")]
    [SerializeField] private float boostForceMultiplier = 3f;
    [SerializeField] private float boostMaxSpeedMultiplier = 1.4f;
    [SerializeField] private float boostImpulse = 12f;
    [SerializeField] private float boostDuration = 0.75f;
    [SerializeField] private float boostCooldown = 3f;
    [SerializeField] private float boostSteerMultiplier = 1.15f;
    private float boostTimer = 0f;
    private float boostCooldownTimer = 0f;
    private bool isBoosting = false;
    public bool IsBoosting => isBoosting;
    public bool CanBoost => boostCooldownTimer <= 0f;
    public float BoostChargePercent => CanBoost ? 1f : 1f - (boostCooldownTimer / boostCooldown);

    [Header("Handbrake Settings")]
    [SerializeField] private float handbrakeSteerMultiplier = 1.15f;
    [SerializeField] private float normalSideGrip = 0.65f;
    [SerializeField] private float handbrakeSideGrip = 0.99f;
    [SerializeField] private float handbrakeForwardBleed = 0.994f;
    [SerializeField] private float handbrakeSpeedBleed = 0.994f;
    [SerializeField] private float handbrakeYawDamping = 0.9f;
    [SerializeField] private float handbrakeBrakeAcceleration = 1.12f;

    [Header("Ground / Air")]
    [SerializeField] private float groundCheckDistance = 0.45f;
    [SerializeField] private LayerMask groundLayerMask = ~0;
    [SerializeField] private float airborneExtraGravity = 35f;
    [SerializeField] private float airborneSteerMultiplier = 0.35f;
    [SerializeField] private float airborneYawResetSpeed = 8f;
    [SerializeField] private float maxFallSpeed = 25f;
    [SerializeField] private float landingAngularDamping = 0.5f;
    [SerializeField] private float minLandingAlignSpeed = 2f;

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

    public bool IsGrounded => isGrounded;
    public bool IsAirborne => !isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        carCollider = GetComponent<Collider>();
        input = GetComponent<CarInputReader>();

        rb.constraints = RigidbodyConstraints.FreezeRotationX;

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
            AbsorbLandingImpact();
        }

        ApplyAirborneGravity();
        UpdateBoost();
        HandleThrottle();
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

        Vector3 planarVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, groundNormal);
        Vector3 driveDirection = Vector3.ProjectOnPlane(-transform.forward, groundNormal).normalized;
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
        float steerInput = input.Steer;

        if (!isGrounded)
        {
            Vector3 airborneAngularVelocity = rb.angularVelocity;

            if (Mathf.Abs(steerInput) <= 0.01f)
            {
                airborneAngularVelocity.y = Mathf.MoveTowards(airborneAngularVelocity.y, 0f, airborneYawResetSpeed * Time.fixedDeltaTime);
            }
            else
            {
                float airborneTurnAmount = steerInput * steerTorque * airborneSteerMultiplier;
                rb.AddTorque(Vector3.up * airborneTurnAmount, ForceMode.Acceleration);
                airborneAngularVelocity.y = Mathf.Clamp(airborneAngularVelocity.y, -maxAngularVelocity, maxAngularVelocity);
            }

            rb.angularVelocity = airborneAngularVelocity;
            return;
        }

        Vector3 planarVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, groundNormal);
        Vector3 driveDirection = Vector3.ProjectOnPlane(-transform.forward, groundNormal).normalized;
        float forwardSpeed = Mathf.Abs(Vector3.Dot(planarVelocity, driveDirection));

        if (Mathf.Abs(steerInput) > 0.01f)
        {
            float speedRatio = Mathf.Clamp01(forwardSpeed / maxSpeed);
            float steerMultiplier = Mathf.Lerp(1f, minSteerMultiplierAtMaxSpeed, speedRatio);

            if (isBoosting)
            {
                steerMultiplier *= boostSteerMultiplier;
            }

            if (input.HandbrakeHeld)
            {
                steerMultiplier *= handbrakeSteerMultiplier;
            }

            float turnAmount = steerInput * steerTorque * steerMultiplier;
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
    }

    private void AbsorbLandingImpact()
    {
        Vector3 velocity = rb.linearVelocity;
        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);

        velocity.y = 0f;

        if (planarVelocity.sqrMagnitude > minLandingAlignSpeed * minLandingAlignSpeed)
        {
            transform.rotation = Quaternion.LookRotation(-planarVelocity.normalized, Vector3.up);
        }

        rb.linearVelocity = velocity;

        Vector3 angularVelocity = rb.angularVelocity;
        angularVelocity.y = 0f;
        rb.angularVelocity = angularVelocity;
    }

    private void ResetCar()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = spawnPosition + Vector3.up * resetHeight;
        transform.rotation = spawnRotation;
    }
}
