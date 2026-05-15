using System.Collections.Generic;
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
    [SerializeField] private float maxLaunchSpeed = 8f;
    [SerializeField] private float groundProbeForwardOffset = 0.6f;

    [SerializeField] private float boosterImpulse = 4f;

    [Header("Reset Settings")]
    [SerializeField] private float resetHeight = 2f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool logInputValues = false;

    private Rigidbody rb;
    private Collider[] carColliders;
    private CarInputReader input;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private bool isGrounded;
    private bool wasGrounded;
    private Vector3 groundNormal = Vector3.up;
    private bool hasFrontGroundHit;
    private Vector3 frontGroundNormal = Vector3.up;
    private float landingSteeringResetTimer;
    private float groundedDrag;
    private bool isOnAccelerator;
    private readonly HashSet<Collider> boosterContacts = new HashSet<Collider>();
    private readonly HashSet<Collider> selfColliders = new HashSet<Collider>();

    public bool IsGrounded => isGrounded;
    public bool IsAirborne => !isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        carColliders = GetComponentsInChildren<Collider>();
        input = GetComponent<CarInputReader>();

        selfColliders.Clear();
        foreach (Collider collider in carColliders)
        {
            if (collider != null && !collider.isTrigger)
            {
                selfColliders.Add(collider);
            }
        }

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

            if (TryGetGroundProbeData(out Vector3 centerOrigin, out Vector3 frontOrigin, out Vector3 rearOrigin, out float rayDistance))
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(centerOrigin, centerOrigin + Vector3.down * rayDistance);
                Gizmos.DrawSphere(centerOrigin, 0.05f);

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(frontOrigin, frontOrigin + Vector3.down * rayDistance);
                Gizmos.DrawSphere(frontOrigin, 0.05f);

                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(rearOrigin, rearOrigin + Vector3.down * rayDistance);
                Gizmos.DrawSphere(rearOrigin, 0.05f);
            }
        }
    }

    private void FixedUpdate()
    {
        bool wasOnAccelerator = isOnAccelerator;
        wasGrounded = isGrounded;
        UpdateGrounded();

        isOnAccelerator = boosterContacts.Count > 0;

        if (!wasOnAccelerator && isOnAccelerator)
        {
            ApplyAcceleratorBoost();
        }

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

        Vector3 surfaceNormal = GetDriveSurfaceNormal();
        Vector3 planarVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, surfaceNormal);
        Vector3 driveDirection = Vector3.ProjectOnPlane(-transform.forward, surfaceNormal).normalized;
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

        Vector3 surfaceNormal = groundNormal.sqrMagnitude > 0.01f ? groundNormal : Vector3.up;
        Vector3 forwardDir = -transform.forward;

        Vector3 velocity = rb.linearVelocity;

        // Decompose into three orthogonal parts: along surface normal, along forward, along side.
        // This avoids double-counting Y on ramps where forwardDir has a world-Y component.
        Vector3 normalVelocity = Vector3.Project(velocity, surfaceNormal);
        Vector3 planarVelocity = velocity - normalVelocity;
        Vector3 forwardVelocity = Vector3.Project(planarVelocity, forwardDir);
        Vector3 sidewaysVelocity = planarVelocity - forwardVelocity;

        if (input.HandbrakeHeld)
        {
            forwardVelocity *= handbrakeForwardBleed;
            sidewaysVelocity *= handbrakeSideGrip;

            rb.linearVelocity = (forwardVelocity + sidewaysVelocity) * handbrakeSpeedBleed + normalVelocity;

            if (forwardVelocity.sqrMagnitude > 0.01f)
            {
                rb.AddForce(-forwardVelocity.normalized * handbrakeBrakeAcceleration, ForceMode.Acceleration);
            }

            Vector3 angularVelocity = rb.angularVelocity;
            angularVelocity.y *= handbrakeYawDamping;
            rb.angularVelocity = angularVelocity;

            return;
        }

        rb.linearVelocity = forwardVelocity + sidewaysVelocity * normalSideGrip + normalVelocity;
    }

    private void ApplyAcceleratorBoost()
    {
        Vector3 surfaceNormal = GetDriveSurfaceNormal();
        Vector3 boostDirection = Vector3.ProjectOnPlane(-transform.forward, surfaceNormal).normalized;

        if (boostDirection.sqrMagnitude < 0.0001f)
        {
            Vector3 planarVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, surfaceNormal);
            boostDirection = planarVelocity.sqrMagnitude > 0.0001f
                ? planarVelocity.normalized
                : Vector3.ProjectOnPlane(-transform.forward, Vector3.up).normalized;
        }

        if (boostDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        rb.AddForce(boostDirection * boosterImpulse, ForceMode.VelocityChange);
    }

    private void UpdateGrounded()
    {
        if (!TryGetGroundProbeData(out Vector3 centerOrigin, out Vector3 frontOrigin, out Vector3 rearOrigin, out float rayDistance))
        {
            isGrounded = false;
            groundNormal = Vector3.up;
            hasFrontGroundHit = false;
            frontGroundNormal = Vector3.up;
            return;
        }

        Vector3 normalSum = Vector3.zero;
        int hitCount = 0;
        hasFrontGroundHit = false;
        frontGroundNormal = Vector3.up;

        if (TryGetGroundHit(centerOrigin, rayDistance, out RaycastHit centerHit))
        {
            normalSum += centerHit.normal;
            hitCount++;
        }

        if (TryGetGroundHit(frontOrigin, rayDistance, out RaycastHit frontHit))
        {
            normalSum += frontHit.normal;
            hitCount++;
            hasFrontGroundHit = true;
            frontGroundNormal = frontHit.normal.sqrMagnitude > 0.01f ? frontHit.normal.normalized : Vector3.up;
        }

        if (TryGetGroundHit(rearOrigin, rayDistance, out RaycastHit rearHit))
        {
            normalSum += rearHit.normal;
            hitCount++;
        }

        if (hitCount > 0)
        {
            isGrounded = true;
            groundNormal = (normalSum / hitCount).normalized;
            if (groundNormal.sqrMagnitude < 0.01f)
            {
                groundNormal = Vector3.up;
            }
        }
        else
        {
            isGrounded = false;
            groundNormal = Vector3.up;
        }
    }

    private bool TryGetGroundProbeData(out Vector3 centerOrigin, out Vector3 frontOrigin, out Vector3 rearOrigin, out float rayDistance)
    {
        centerOrigin = transform.position + Vector3.up * 0.1f;
        frontOrigin = centerOrigin;
        rearOrigin = centerOrigin;
        rayDistance = groundCheckDistance;

        if (!TryGetCombinedColliderBounds(out Bounds bounds))
        {
            return false;
        }

        Vector3 probeForward = Vector3.ProjectOnPlane(-transform.forward, Vector3.up);
        if (probeForward.sqrMagnitude < 0.0001f)
        {
            probeForward = -transform.forward;
        }

        probeForward.Normalize();

        centerOrigin = bounds.center + Vector3.up * 0.1f;
        float forwardOffset = Mathf.Max(bounds.extents.z * groundProbeForwardOffset, 0.1f);
        frontOrigin = centerOrigin + probeForward * forwardOffset;
        rearOrigin = centerOrigin - probeForward * forwardOffset;
        rayDistance = bounds.extents.y + groundCheckDistance + 0.1f;
        return true;
    }

    private bool TryGetCombinedColliderBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (carColliders == null || carColliders.Length == 0)
        {
            carColliders = GetComponentsInChildren<Collider>();
        }

        foreach (Collider collider in carColliders)
        {
            if (collider == null || collider.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    private bool TryGetGroundHit(Vector3 origin, float rayDistance, out RaycastHit groundHit)
    {
        groundHit = default;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, rayDistance, groundLayerMask, QueryTriggerInteraction.Ignore);
        float closestDistance = float.MaxValue;
        bool foundHit = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || selfColliders.Contains(hit.collider))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                groundHit = hit;
                foundHit = true;
            }
        }

        return foundHit;
    }

    private Vector3 GetDriveSurfaceNormal()
    {
        Vector3 surfaceNormal = groundNormal.sqrMagnitude > 0.01f ? groundNormal : Vector3.up;

        if (hasFrontGroundHit && frontGroundNormal.y < surfaceNormal.y - 0.01f)
        {
            surfaceNormal = Vector3.Slerp(surfaceNormal, frontGroundNormal, 0.75f).normalized;
        }

        return surfaceNormal.sqrMagnitude > 0.01f ? surfaceNormal : Vector3.up;
    }

    private void OnCollisionEnter(Collision collision)
    {
        TrackBoosterContact(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        TrackBoosterContact(collision.collider);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider != null && collision.collider.CompareTag("Booster"))
        {
            boosterContacts.Remove(collision.collider);
        }
    }

    private void TrackBoosterContact(Collider collider)
    {
        if (collider != null && collider.CompareTag("Booster"))
        {
            boosterContacts.Add(collider);
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

    private void AlignVelocityToFacingWhenAccelerating()
    {
        if (!isGrounded || input.Throttle <= 0.01f)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        Vector3 surfaceNormal = GetDriveSurfaceNormal();
        Vector3 verticalVelocity = Vector3.Project(velocity, surfaceNormal);
        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, surfaceNormal);

        float speed = planarVelocity.magnitude;
        if (speed < 0.01f)
        {
            return;
        }

        Vector3 facingDirection = Vector3.ProjectOnPlane(-transform.forward, surfaceNormal).normalized;
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
