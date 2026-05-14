using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CarInputReader))]
public class CarController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float motorForce = 500f;
    [SerializeField] private float brakeForce = 1000f;
    [SerializeField] private float maxSpeed = 15f;
    [SerializeField] private float steerTorque = 100f;
    [SerializeField] private float maxAngularVelocity = 3f;

    [Header("Boost Settings")]
    [SerializeField] private float boostMultiplier = 1.8f;
    [SerializeField] private float boostDuration = 2f;
    [SerializeField] private float boostCooldown = 3f;
    private float boostTimer = 0f;
    private float boostCooldownTimer = 0f;
    private bool isBoosting = false;
    public bool CanBoost => boostCooldownTimer <= 0f;
    public float BoostChargePercent => CanBoost ? 1f : 1f - (boostCooldownTimer / boostCooldown);

    [Header("Handbrake Settings")]
    [SerializeField] private float handbrakeDriftTorqueMultiplier = 2f;
    [SerializeField] private float handbrakeSidewaysDampening = 0.5f;
    private float normalDrag;

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

        normalDrag = rb.linearDamping;
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
            float turnMultiplier = input.HandbrakeHeld ? handbrakeDriftTorqueMultiplier : 1f;
            float turnAmount = input.Steer * steerTorque * turnMultiplier;
            rb.AddTorque(transform.up * turnAmount, ForceMode.Acceleration);
        }

        float maxAngularVel = input.HandbrakeHeld ? maxAngularVelocity * 1.5f : maxAngularVelocity;
        if (rb.angularVelocity.magnitude > maxAngularVel)
        {
            rb.angularVelocity = rb.angularVelocity.normalized * maxAngularVel;
        }
    }

    private void HandleHandbrake()
    {
        if (input.HandbrakeHeld)
        {
            Vector3 forwardVelocity = Vector3.Project(rb.linearVelocity, -transform.forward);
            Vector3 sidewaysVelocity = rb.linearVelocity - forwardVelocity;

            rb.linearVelocity = forwardVelocity + (sidewaysVelocity * handbrakeSidewaysDampening);
        }
    }

    private void ResetCar()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = spawnPosition + Vector3.up * resetHeight;
        transform.rotation = spawnRotation;
    }
}
