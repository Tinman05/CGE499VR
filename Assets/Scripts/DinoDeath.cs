using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Lets a dinosaur collapse when it starves: the animation stops and the skeleton turns into a
/// limp ragdoll (a rigid body and capsule for every bone with a joint to its parent bone), so a
/// walking dinosaur topples over and a flying one tumbles out of the sky. The ragdoll is built when
/// needed and taken apart again by <see cref="Revive"/> when a new round starts.
/// </summary>
public class DinoDeath : MonoBehaviour
{
    [Header("Ragdoll")]
    [Tooltip("Total mass of the ragdoll (kg), shared between the bones by their length.")]
    [SerializeField] float totalMass = 300f;
    [Tooltip("Bones shorter than this (m) get no body of their own and just follow their parent.")]
    [SerializeField] float minBoneLength = 0.15f;
    [SerializeField, Range(0.15f, 0.5f)] float capsuleRadiusRatio = 0.28f;
    [SerializeField] float swingLimit = 20f;
    [SerializeField] float twistLimit = 8f;
    [SerializeField] float damping = 1.2f;

    [Header("Fall")]
    [Tooltip("Sideways push (m/s) so a standing dinosaur topples instead of settling upright.")]
    [SerializeField] float toppleSpeed = 2.6f;
    [Tooltip("The bodies are frozen in place after this many seconds (saves performance).")]
    [SerializeField] float freezeAfter = 10f;
    bool frozen;

    SkinnedMeshRenderer skin;
    Animator animator;
    NavMeshAgent agent;
    Dinosaur walker;
    FlyingDinosaur flyer;
    DinoHunger hunger;

    Vector3 homePosition;
    Quaternion homeRotation;
    readonly List<Rigidbody> bodies = new List<Rigidbody>();
    readonly List<Collider> colliders = new List<Collider>();
    readonly List<Joint> joints = new List<Joint>();
    readonly Dictionary<Transform, (Vector3 pos, Quaternion rot, Vector3 scale)> pose = new Dictionary<Transform, (Vector3, Quaternion, Vector3)>();
    bool updateWhenOffscreen;
    float collapsedAt;
    Vector3 lastPosition;
    Vector3 velocity;

    public bool IsDead { get; private set; }

    void Awake()
    {
        skin = GetComponentInChildren<SkinnedMeshRenderer>();
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        walker = GetComponent<Dinosaur>();
        flyer = GetComponent<FlyingDinosaur>();
        hunger = GetComponent<DinoHunger>();
        homePosition = transform.position;
        homeRotation = transform.rotation;
        lastPosition = homePosition;
    }

    void OnEnable() => HungerGame.StateChanged += OnGameState;
    void OnDisable() => HungerGame.StateChanged -= OnGameState;

    void OnGameState(HungerGame.State state)
    {
        if (state == HungerGame.State.Ready || state == HungerGame.State.Playing) Revive();
    }

    void Update()
    {
        if (!IsDead)
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            velocity = Vector3.Lerp(velocity, (transform.position - lastPosition) / dt, 0.3f);
            lastPosition = transform.position;
            return;
        }

        if (!frozen && freezeAfter > 0f && Time.time - collapsedAt > freezeAfter)
        {
            foreach (var body in bodies) if (body != null) body.isKinematic = true;
            frozen = true;
        }
    }

    // ---------------- collapse ----------------

    public void Collapse()
    {
        if (IsDead || skin == null) return;
        IsDead = true;
        collapsedAt = Time.time;

        if (walker != null) walker.enabled = false;
        if (flyer != null) flyer.enabled = false;
        Vector3 carried = flyer != null && flyer.IsFlying ? velocity : (agent != null && agent.enabled ? agent.velocity : Vector3.zero);
        if (agent != null) agent.enabled = false;
        if (animator != null) animator.enabled = false;
        if (hunger != null) hunger.SetBarVisible(false);

        updateWhenOffscreen = skin.updateWhenOffscreen;
        skin.updateWhenOffscreen = true;   // bones leave the original bounds: never cull the mesh

        BuildRagdoll(carried);
    }

    void BuildRagdoll(Vector3 carried)
    {
        var bones = new HashSet<Transform>();
        foreach (var b in skin.bones) if (b != null) bones.Add(b);
        if (bones.Count == 0) return;

        // remember the pose so Revive can put every bone back
        var all = new HashSet<Transform>(bones);
        foreach (var b in bones) { var p = b.parent; while (p != null && p != transform) { all.Add(p); p = p.parent; } }
        foreach (var t in all) pose[t] = (t.localPosition, t.localRotation, t.localScale);

        // bones that get a body: they have at least one child bone and are long enough
        var lengths = new Dictionary<Transform, float>();
        var targets = new Dictionary<Transform, Vector3>();   // world point the capsule reaches to
        float sum = 0f;
        foreach (var b in bones)
        {
            Vector3 reach = Vector3.zero; int n = 0; float len = 0f;
            foreach (Transform c in b)
            {
                if (!bones.Contains(c)) continue;
                reach += c.position; len += Vector3.Distance(b.position, c.position); n++;
            }
            if (n == 0) continue;
            len /= n;
            if (len < minBoneLength) continue;
            lengths[b] = len; targets[b] = reach / n; sum += len;
        }
        if (lengths.Count == 0) return;

        var bodyOf = new Dictionary<Transform, Rigidbody>();
        foreach (var kv in lengths)
        {
            var b = kv.Key; float len = kv.Value;
            var rb = b.gameObject.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.5f, totalMass * len / sum);
            rb.linearDamping = damping * 0.5f;
            rb.angularDamping = damping + 0.4f;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.linearVelocity = carried;
            bodies.Add(rb); bodyOf[b] = rb;

            // capsule from the bone's own pivot toward its child bones
            Vector3 local = b.InverseTransformPoint(targets[b]);
            int axis = Mathf.Abs(local.x) >= Mathf.Abs(local.y) && Mathf.Abs(local.x) >= Mathf.Abs(local.z) ? 0 : (Mathf.Abs(local.y) >= Mathf.Abs(local.z) ? 1 : 2);
            var cap = b.gameObject.AddComponent<CapsuleCollider>();
            float worldScale = Mathf.Max(0.0001f, (Mathf.Abs(b.lossyScale.x) + Mathf.Abs(b.lossyScale.y) + Mathf.Abs(b.lossyScale.z)) / 3f);
            cap.direction = axis;
            cap.center = local * 0.5f;
            cap.height = local.magnitude * 1.05f;
            cap.radius = Mathf.Clamp(len * capsuleRadiusRatio, 0.05f, 0.45f) / worldScale;
            colliders.Add(cap);
        }

        // joints: each body hangs from the nearest ancestor that has one
        foreach (var kv in bodyOf)
        {
            Transform p = kv.Key.parent; Rigidbody parentBody = null;
            while (p != null && p != transform) { if (bodyOf.TryGetValue(p, out parentBody)) break; p = p.parent; }
            if (parentBody == null) continue;

            var j = kv.Key.gameObject.AddComponent<CharacterJoint>();
            j.connectedBody = parentBody;
            j.anchor = Vector3.zero;
            j.autoConfigureConnectedAnchor = true;
            j.enablePreprocessing = false;
            j.enableProjection = true;
            j.lowTwistLimit = new SoftJointLimit { limit = -twistLimit };
            j.highTwistLimit = new SoftJointLimit { limit = twistLimit };
            j.swing1Limit = new SoftJointLimit { limit = swingLimit };
            j.swing2Limit = new SoftJointLimit { limit = swingLimit };
            joints.Add(j);
        }

        // limbs of the same dinosaur never collide with each other
        for (int i = 0; i < colliders.Count; i++)
            for (int k = i + 1; k < colliders.Count; k++)
                Physics.IgnoreCollision(colliders[i], colliders[k], true);

        // Topple: the higher a body is, the harder it is pushed sideways, so a standing dinosaur
        // falls over instead of settling on its legs; a flying one gets a tumbling spin.
        Vector3 side = Random.insideUnitSphere; side.y = 0f; if (side.sqrMagnitude < 0.01f) side = Vector3.right;
        side.Normalize();
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var rb in bodies) { minY = Mathf.Min(minY, rb.position.y); maxY = Mathf.Max(maxY, rb.position.y); }
        float range = Mathf.Max(0.5f, maxY - minY);
        foreach (var rb in bodies)
        {
            float high = (rb.position.y - minY) / range;
            rb.AddForce(side * toppleSpeed * (0.4f + 1.6f * high) + Vector3.up * 0.6f, ForceMode.VelocityChange);
            rb.AddTorque(Random.onUnitSphere * 0.5f, ForceMode.VelocityChange);
        }
    }

    // ---------------- revive ----------------

    public void Revive()
    {
        if (!IsDead) return;
        IsDead = false;

        foreach (var j in joints) if (j != null) DestroyImmediate(j);
        foreach (var rb in bodies) if (rb != null) DestroyImmediate(rb);
        foreach (var c in colliders) if (c != null) DestroyImmediate(c);
        joints.Clear(); bodies.Clear(); colliders.Clear();

        foreach (var kv in pose)
        {
            if (kv.Key == null) continue;
            kv.Key.localPosition = kv.Value.pos;
            kv.Key.localRotation = kv.Value.rot;
            kv.Key.localScale = kv.Value.scale;
        }
        pose.Clear();
        if (skin != null) skin.updateWhenOffscreen = updateWhenOffscreen;

        transform.SetPositionAndRotation(homePosition, homeRotation);
        lastPosition = homePosition;
        velocity = Vector3.zero;
        frozen = false;

        if (animator != null) animator.enabled = true;
        if (flyer != null) { flyer.ResetToGround(); flyer.enabled = true; }
        if (agent != null) { agent.enabled = true; agent.Warp(homePosition); }
        if (walker != null) { walker.enabled = true; walker.ResumeIdle(); }
        if (hunger != null) hunger.SetBarVisible(true);
    }
}
