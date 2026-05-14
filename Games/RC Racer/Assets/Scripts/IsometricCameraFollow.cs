using UnityEngine;

public class IsometricCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothTime = 0.2f;
    [SerializeField] private bool enableTargetInterpolation = true;

    [Header("Look Ahead")]
    [SerializeField] private bool invertTargetForward = true;
    [SerializeField] private float forwardLookAheadDistance = 4f;
    [SerializeField] private float velocityLookAheadDistance = 6f;
    [SerializeField] private float maxLookAheadSpeed = 20f;
    [SerializeField] private float lookAheadSmoothTime = 0.15f;

    private Vector3 offset;
    private Vector3 followVelocity;
    private Vector3 currentLookAhead;
    private Vector3 lookAheadVelocity;
    private Rigidbody targetRigidbody;
    private Transform cachedTarget;

    private void Start()
    {
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
        offset = transform.position - targetPosition;
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
                desiredLookAhead += planarVelocity.normalized * (velocityLookAheadDistance * speedFactor);
            }
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
    }
}
