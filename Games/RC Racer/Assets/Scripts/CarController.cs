using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CarInputReader))]
public class CarController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float motorForce = 2400f;
    [SerializeField] private float brakeForce = 1000f;
    [SerializeField] private float maxSpeed = 30f;
    [SerializeField] private float steerTorque = 140f;
    [SerializeField] private float maxAngularVelocity = 3.5f;
    [SerializeField] private float minSteerMultiplierAtMaxSpeed = 0.55f;

    [Header("Boost Settings")]
    [SerializeField] private float boostMultiplier = 2f;
    [SerializeField] private float boostDuration = 2f;
    [SerializeField] private float boostCooldown = 3f;
    [SerializeField] private float boostSteerMultiplier = 0.8f;
    private float boostTimer = 0f;
    private float boostCooldownTimer = 0f;
    private bool isBoosting = false;
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

    [Header("Reset Settings")]
    [SerializeField] private float resetHeight = 2f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool logInputValues = false;

    private Rigidbody rb;
    private CarInputReader input;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        input = GetComponent<CarInputReader>();

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

        if (input.BoostHeld && CanBoost && !isBoosting)
        {
            isBoosting = true;
            boostTimer = boostDuration;
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
        float currentSpeed = rb.linearVelocity.magnitude;
        float driveInput = input.Throttle - input.Brake;

        if (Mathf.Abs(driveInput) > 0.01f && currentSpeed < maxSpeed)
        {
            float force = motorForce * driveInput;

            if (isBoosting && driveInput > 0)
            {
                force *= boostMultiplier;
            }

            rb.AddForce(-transform.forward * force, ForceMode.Force);

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
            float speedRatio = Mathf.Clamp01(rb.linearVelocity.magnitude / maxSpeed);
            float steerMultiplier = Mathf.Lerp(1f, minSteerMultiplierAtMaxSpeed, speedRatio);

            if (isBoosting)
            {
                steerMultiplier *= boostSteerMultiplier;
            }

            if (input.HandbrakeHeld)
            {
                steerMultiplier *= handbrakeSteerMultiplier;
            }

            float turnAmount = input.Steer * steerTorque * steerMultiplier;
            rb.AddTorque(transform.up * turnAmount, ForceMode.Acceleration);
        }

        if (rb.angularVelocity.magnitude > maxAngularVelocity)
        {
            rb.angularVelocity = rb.angularVelocity.normalized * maxAngularVelocity;
        }
    }

    private void HandleHandbrake()
    {
        Vector3 forwardDir = -transform.forward;
        Vector3 rightDir = transform.right;

        Vector3 velocity = rb.linearVelocity;

        Vector3 forwardVelocity = Vector3.Project(velocity, forwardDir);
        Vector3 sidewaysVelocity = Vector3.Project(velocity, rightDir);

        if (input.HandbrakeHeld)
        {
            forwardVelocity *= handbrakeForwardBleed;
            sidewaysVelocity *= handbrakeSideGrip;

            rb.linearVelocity = (forwardVelocity + sidewaysVelocity) * handbrakeSpeedBleed;

            if (forwardVelocity.sqrMagnitude > 0.01f)
            {
                rb.AddForce(-forwardVelocity.normalized * handbrakeBrakeAcceleration, ForceMode.Acceleration);
            }

            Vector3 angularVelocity = rb.angularVelocity;
            angularVelocity.y *= handbrakeYawDamping;
            rb.angularVelocity = angularVelocity;

            return;
        }

        rb.linearVelocity = forwardVelocity + sidewaysVelocity * normalSideGrip;
    }

    private void ResetCar()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = spawnPosition + Vector3.up * resetHeight;
        transform.rotation = spawnRotation;
    }
}
