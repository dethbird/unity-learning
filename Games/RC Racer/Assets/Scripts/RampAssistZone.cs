using UnityEngine;
using System.Collections.Generic;

public class RampAssistZone : MonoBehaviour
{
    [SerializeField] private float assistAcceleration = 12f;
    [SerializeField] private float minRampSpeed = 10f;
    [SerializeField] private float launchUpImpulse = 8f;
    [SerializeField] private float launchForwardImpulse = 10f;
    [SerializeField] private float rampLinearDamping = 0.2f;
    [SerializeField] private float rampAngularDamping = 0.5f;

    private readonly Dictionary<Rigidbody, DampingState> dampingStates = new();

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        Rigidbody rb = other.attachedRigidbody;
        if (rb == null)
        {
            return;
        }

        if (!dampingStates.ContainsKey(rb))
        {
            dampingStates[rb] = new DampingState(rb.linearDamping, rb.angularDamping);
        }

        rb.linearDamping = Mathf.Min(rb.linearDamping, rampLinearDamping);
        rb.angularDamping = Mathf.Min(rb.angularDamping, rampAngularDamping);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        Rigidbody rb = other.attachedRigidbody;
        if (rb == null)
        {
            return;
        }

        Vector3 rampDirection = transform.forward.normalized;

        rb.AddForce(rampDirection * assistAcceleration, ForceMode.Acceleration);

        float speedAlongRamp = Vector3.Dot(rb.linearVelocity, rampDirection);
        if (speedAlongRamp < minRampSpeed)
        {
            float neededSpeed = minRampSpeed - speedAlongRamp;
            rb.AddForce(rampDirection * neededSpeed, ForceMode.VelocityChange);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        Rigidbody rb = other.attachedRigidbody;
        if (rb == null)
        {
            return;
        }

        RestoreDamping(rb);

        Vector3 rampDirection = transform.forward.normalized;
        rb.AddForce(Vector3.up * launchUpImpulse, ForceMode.Impulse);
        rb.AddForce(rampDirection * launchForwardImpulse, ForceMode.Impulse);
    }

    private void OnDisable()
    {
        foreach (KeyValuePair<Rigidbody, DampingState> pair in dampingStates)
        {
            if (pair.Key == null)
            {
                continue;
            }

            pair.Key.linearDamping = pair.Value.LinearDamping;
            pair.Key.angularDamping = pair.Value.AngularDamping;
        }

        dampingStates.Clear();
    }

    private void RestoreDamping(Rigidbody rb)
    {
        if (!dampingStates.TryGetValue(rb, out DampingState dampingState))
        {
            return;
        }

        rb.linearDamping = dampingState.LinearDamping;
        rb.angularDamping = dampingState.AngularDamping;
        dampingStates.Remove(rb);
    }

    private readonly struct DampingState
    {
        public DampingState(float linearDamping, float angularDamping)
        {
            LinearDamping = linearDamping;
            AngularDamping = angularDamping;
        }

        public float LinearDamping { get; }

        public float AngularDamping { get; }
    }
}
