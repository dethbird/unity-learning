using UnityEngine;

public class IsometricCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(-17.6f, 9.5f, -13.4f);
    [SerializeField] private float smoothTime = 0.2f;
    [SerializeField] private bool enableTargetInterpolation = true;

    private Vector3 velocity;
    private Rigidbody targetRigidbody;

    private void Awake()
    {
        CacheTargetRigidbody();
    }

    private void OnValidate()
    {
        CacheTargetRigidbody();
    }

    private void CacheTargetRigidbody()
    {
        targetRigidbody = target ? target.GetComponent<Rigidbody>() : null;

        if (enableTargetInterpolation && targetRigidbody != null && targetRigidbody.interpolation == RigidbodyInterpolation.None)
        {
            targetRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    private void LateUpdate()
    {
        if (!target)
        {
            return;
        }

        Vector3 targetPosition = targetRigidbody != null ? targetRigidbody.position : target.position;
        Vector3 desiredPosition = targetPosition + offset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref velocity,
            smoothTime);
    }
}
