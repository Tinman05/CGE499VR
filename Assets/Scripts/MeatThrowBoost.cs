using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Makes throws travel farther (a seated player cannot swing as hard as a standing one)
/// and tells <see cref="Meat"/> whether it is currently held.
/// </summary>
[RequireComponent(typeof(Grabbable), typeof(Rigidbody), typeof(Meat))]
public class MeatThrowBoost : MonoBehaviour
{
    [Tooltip("Multiplier on the release velocity measured from the hand.")]
    [SerializeField] float boost = 2f;
    [Tooltip("Upper limit for the release speed in m/s (about 14 m of range at 45 degrees).")]
    [SerializeField] float maxSpeed = 12f;
    [Tooltip("Slower releases are treated as a drop, not a throw.")]
    [SerializeField] float minThrowSpeed = 0.8f;

    Grabbable grabbable;
    Rigidbody body;
    Meat meat;
    ThrowWhenUnselected subscribed;

    void Awake()
    {
        grabbable = GetComponent<Grabbable>();
        body = GetComponent<Rigidbody>();
        meat = GetComponent<Meat>();
    }

    void Start()
    {
        subscribed = grabbable.VelocityThrow;
        subscribed.WhenThrown += OnThrown;
    }

    void OnDestroy()
    {
        if (subscribed != null) subscribed.WhenThrown -= OnThrown;
    }

    void Update()
    {
        meat.IsHeld = grabbable.SelectingPointsCount > 0;
    }

    public void OnThrown(Vector3 velocity, Vector3 torque)
    {
        if (body.isKinematic || velocity.magnitude < minThrowSpeed) return;
        body.linearVelocity = Vector3.ClampMagnitude(velocity * boost, maxSpeed);
        meat.NotifyThrown();
    }
}
