using UnityEngine;

public class IsometricCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothTime = 0.2f;
    [SerializeField] private bool enableTargetInterpolation = true;

    [Header("Startup")]
    [SerializeField] private bool centerOnTargetAtStartup = true;
    [SerializeField] private Vector3 startupRotationEuler = new Vector3(35f, 50f, 0f);
    [SerializeField] private float startupFollowDistance = 11f;
    [SerializeField] private Vector3 startupFramingOffset = Vector3.zero;

    [Header("Look Ahead")]
    [SerializeField] private bool invertTargetForward = true;
    [SerializeField] private float forwardLookAheadDistance = 4f;
    [SerializeField] private float velocityLookAheadDistance = 6f;
    [SerializeField] private float maxLookAheadSpeed = 20f;
    [SerializeField] private float lookAheadSmoothTime = 0.15f;

    [Header("Framing Effects")]
    [SerializeField] private float boostPullbackDistance = 2.5f;
    [SerializeField] private float boostVelocityLookAheadMultiplier = 1.75f;
    [SerializeField] private float handbrakeLookAheadMultiplier = 0.4f;
    [SerializeField] private float boostZoomOutAmount = 0.4f;
    [SerializeField] private float handbrakeZoomInAmount = 0.75f;
    [SerializeField] private float orthographicSizeSmoothTime = 0.12f;

    private Vector3 offset;
    private Vector3 followVelocity;
    private Vector3 currentLookAhead;
    private Vector3 lookAheadVelocity;
    private Camera targetCamera;
    private float baseOrthographicSize;
    private float orthographicSizeVelocity;
    private Rigidbody targetRigidbody;
    private CarController targetCarController;
    private CarInputReader targetInputReader;
    private Transform cachedTarget;

    private void Start()
    {
        targetCamera = GetComponent<Camera>();
        if (targetCamera != null)
        {
            baseOrthographicSize = targetCamera.orthographicSize;
        }

        CacheTargetRigidbody();
        InitializeOffset();
    }

    private void OnValidate()
    {
        CacheTargetRigidbody();
    }

    private void CacheTargetRigidbody()
    {
        cachedTarget = target;
        targetRigidbody = target ? target.GetComponent<Rigidbody>() : null;
        targetCarController = target ? target.GetComponent<CarController>() : null;
        targetInputReader = target ? target.GetComponent<CarInputReader>() : null;

        if (enableTargetInterpolation && targetRigidbody != null && targetRigidbody.interpolation == RigidbodyInterpolation.None)
        {
            targetRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    private void InitializeOffset()
    {
        if (!target)
        {
            return;
        }

        Vector3 targetPosition = targetRigidbody != null ? targetRigidbody.position : target.position;

        if (centerOnTargetAtStartup)
        {
            transform.rotation = Quaternion.Euler(startupRotationEuler);

            Vector3 viewDirection = -transform.forward;
            if (viewDirection.sqrMagnitude > 0.001f)
            {
                viewDirection.Normalize();
            }

            Vector3 framingOffset = transform.right * startupFramingOffset.x
                + Vector3.up * startupFramingOffset.y
                + transform.forward * startupFramingOffset.z;

            offset = viewDirection * startupFollowDistance + framingOffset;
        }
        else
        {
            offset = transform.position - targetPosition;
        }

        transform.position = targetPosition + offset;
        currentLookAhead = Vector3.zero;
        lookAheadVelocity = Vector3.zero;
        followVelocity = Vector3.zero;
    }

    private Vector3 GetDesiredLookAhead()
    {
        Vector3 targetForward = invertTargetForward ? -target.forward : target.forward;
        targetForward.y = 0f;
        if (targetForward.sqrMagnitude > 0.001f)
        {
            targetForward.Normalize();
        }

        Vector3 desiredLookAhead = targetForward * forwardLookAheadDistance;

        if (targetRigidbody != null)
        {
            Vector3 planarVelocity = targetRigidbody.linearVelocity;
            planarVelocity.y = 0f;

            if (planarVelocity.sqrMagnitude > 0.001f)
            {
                float speedFactor = Mathf.Clamp01(planarVelocity.magnitude / maxLookAheadSpeed);
                float velocityLookAhead = velocityLookAheadDistance;

                if (targetCarController != null && targetCarController.IsBoosting)
                {
                    velocityLookAhead *= boostVelocityLookAheadMultiplier;
                }

                desiredLookAhead += planarVelocity.normalized * (velocityLookAhead * speedFactor);
            }
        }

        if (targetCarController != null && targetCarController.IsBoosting)
        {
            desiredLookAhead -= targetForward * boostPullbackDistance;
        }

        if (targetInputReader != null && targetInputReader.HandbrakeHeld)
        {
            desiredLookAhead *= handbrakeLookAheadMultiplier;
        }

        return desiredLookAhead;
    }

    private void LateUpdate()
    {
        if (!target)
        {
            return;
        }

        if (target != cachedTarget)
        {
            CacheTargetRigidbody();
            InitializeOffset();
        }

        Vector3 targetPosition = targetRigidbody != null ? targetRigidbody.position : target.position;
        Vector3 desiredLookAhead = GetDesiredLookAhead();

        currentLookAhead = Vector3.SmoothDamp(
            currentLookAhead,
            desiredLookAhead,
            ref lookAheadVelocity,
            lookAheadSmoothTime);

        Vector3 desiredPosition = targetPosition + offset + currentLookAhead;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            smoothTime);

        if (targetCamera != null && targetCamera.orthographic)
        {
            float desiredOrthographicSize = baseOrthographicSize;

            if (targetCarController != null && targetCarController.IsBoosting)
            {
                desiredOrthographicSize += boostZoomOutAmount;
            }

            if (targetInputReader != null && targetInputReader.HandbrakeHeld)
            {
                desiredOrthographicSize = Mathf.Max(0.1f, baseOrthographicSize - handbrakeZoomInAmount);
            }

            targetCamera.orthographicSize = Mathf.SmoothDamp(
                targetCamera.orthographicSize,
                desiredOrthographicSize,
                ref orthographicSizeVelocity,
                orthographicSizeSmoothTime);
        }
    }
}
