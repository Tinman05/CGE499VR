using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Throwable food. Becomes edible once it lands on the ground, so dinosaurs
/// ignore meat that is still in the player's hand or in the air.
/// Meat that ends up lying on the walkway (out of a seated player's reach)
/// disappears after a few seconds, and the spawner puts a fresh piece on the plate.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Meat : MonoBehaviour
{
    public static readonly List<Meat> All = new List<Meat>();

    /// <summary>Raised when a piece goes from lying around to being held by the player.</summary>
    public static event System.Action<Meat> Picked;

    /// <summary>Raised when the player lets go of a piece with enough speed to count as a throw.</summary>
    public static event System.Action<Meat> Thrown;

    /// <summary>Raised when a piece hits something hard enough to be heard (meat, impact speed in m/s).</summary>
    public static event System.Action<Meat, float> Landed;
    float nextLandedAt;

    public void NotifyThrown() => Thrown?.Invoke(this);

    [Tooltip("Layers that count as ground: only the pit floor, so meat landing on the walkway or table stays uneaten.")]
    [SerializeField] LayerMask groundMask;
    [Tooltip("Seconds before uneaten meat disappears.")]
    [SerializeField] float lifetime = 60f;
    [Tooltip("Meat that falls below this world Y is removed.")]
    [SerializeField] float killY = -20f;

    [Header("Stray meat")]
    [Tooltip("Seconds a piece may lie still, away from the plate and not on the pit floor, before it is removed.")]
    [SerializeField] float strayRemoveAfter = 3f;
    [Tooltip("Within this distance of the plate a piece counts as home and is never removed.")]
    [SerializeField] float homeRadius = 0.4f;
    [SerializeField] float restSpeed = 0.15f;

    Rigidbody body;
    float spawnTime;
    float restTime;

    public bool IsEdible { get; private set; }
    bool held;
    public bool IsHeld
    {
        get => held;
        set
        {
            if (value && !held) Picked?.Invoke(this);
            held = value;
        }
    }
    public Transform Home { get; set; }
    public Dinosaur ClaimedBy { get; set; }

    void Reset()
    {
        groundMask = LayerMask.GetMask("Ground");
    }

    void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        All.Add(this);
        spawnTime = Time.time;
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void Update()
    {
        if (IsHeld)
        {
            // Picked up again: it is no longer on the floor and the timers start over.
            IsEdible = false;
            spawnTime = Time.time;
            restTime = 0f;
            return;
        }

        if (transform.position.y < killY || Time.time - spawnTime > lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (!IsEdible && !IsNearHome() && body.linearVelocity.sqrMagnitude < restSpeed * restSpeed)
        {
            restTime += Time.deltaTime;
            if (restTime >= strayRemoveAfter) Destroy(gameObject);
        }
        else
        {
            restTime = 0f;
        }
    }

    bool IsNearHome()
    {
        return Home != null && (transform.position - Home.position).sqrMagnitude < homeRadius * homeRadius;
    }

    void OnCollisionEnter(Collision collision)
    {
        if ((groundMask.value & (1 << collision.gameObject.layer)) != 0)
            IsEdible = true;

        float impact = collision.relativeVelocity.magnitude;
        if (impact > 1.2f && Time.time >= nextLandedAt && !IsHeld)
        {
            nextLandedAt = Time.time + 0.25f;
            Landed?.Invoke(this, impact);
        }
    }

    /// <summary>Optional: call from a grab event if something other than MeatThrowBoost drives IsHeld.</summary>
    public void OnGrabbed()
    {
        IsEdible = false;
        spawnTime = Time.time;
    }
}
