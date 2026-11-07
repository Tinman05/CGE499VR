using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Lets a Dinosaur also fly. On the ground it behaves like any other dinosaur (wander, walk to
/// meat, turn, eat). From time to time it takes off, circles high above the pit, and lands again.
/// If meat lands in its pen while it is in the air, it swoops down next to the meat and eats it.
/// </summary>
[RequireComponent(typeof(Dinosaur), typeof(NavMeshAgent))]
public class FlyingDinosaur : MonoBehaviour
{
    enum Mode { Ground, TakeOff, Cruise, Land }

    [Header("Flight area")]
    [Tooltip("World position of the middle of the pit; the bird circles around it.")]
    [SerializeField] Vector3 orbitCenter;
    [SerializeField] Vector2 orbitRadius = new Vector2(7f, 10f);
    [Tooltip("Cruise height above the ground it took off from.")]
    [SerializeField] float cruiseHeight = 9f;
    [SerializeField] float heightWobble = 1f;

    [Header("Movement")]
    [SerializeField] float flySpeed = 5f;
    [Tooltip("Degrees per second.")]
    [SerializeField] float turnRate = 110f;
    [SerializeField] float climbRate = 3.5f;
    [SerializeField] float takeOffSeconds = 2.5f;
    [Tooltip("Height lost per meter flown while landing.")]
    [SerializeField] float glideSlope = 0.55f;
    [Tooltip("Height above the pen floor it must reach before leaving the pen sideways, and before dropping into the pen. Must clear the walkway deck, its barriers and the wings (walkway rail top is about 5.6 m above the pen floor).")]
    [SerializeField] float safeHeight = 7f;
    [Tooltip("Half size (m) of the box the bird's body needs, wings spread. Landing and take-off spots must have this much free space all the way up to Safe Height.")]
    [SerializeField] Vector3 bodyHalfExtents = new Vector3(2.0f, 1.1f, 2.0f);
    [Tooltip("It drops straight down once it is this close (m, horizontally) to the landing spot.")]
    [SerializeField] float dropRadius = 1.2f;
    [Tooltip("Horizontal speed (m/s) while climbing out of the pen.")]
    [SerializeField] float climbOutSpeed = 0f;

    [Header("Timing")]
    [SerializeField] Vector2 groundSeconds = new Vector2(8f, 16f);
    [SerializeField] Vector2 flightSeconds = new Vector2(14f, 24f);
    [Tooltip("How far from its home it looks for meat to swoop down to.")]
    [SerializeField] float meatSearchRadius = 14f;

    [Header("Animation")]
    [SerializeField] Animator animator;
    [SerializeField] string speedParam = "Speed";
    [SerializeField] float groundAnimSpeed = 0.7f;
    [SerializeField] float flightAnimSpeed = 1.6f;

    Dinosaur ground;
    NavMeshAgent agent;
    NavMeshPath path;
    Mode mode = Mode.Ground;
    Vector3 homeNav;
    float timer;
    float modeTime;
    float flightEnd;
    float groundY;
    float radius;
    float orbitDir = 1f;
    float yaw;
    float pitch;
    float roll;
    Vector3 landPoint;
    Meat landMeat;
    bool landingForMeat;
    bool dropping;
    float nextMeatCheck;
    float nextClearCheck;

    public bool IsFlying => mode != Mode.Ground;

    /// <summary>Back to walking after a collapse and revive: the owner places the dinosaur and re-enables the components.</summary>
    public void ResetToGround()
    {
        mode = Mode.Ground;
        modeTime = 0f;
        pitch = 0f;
        roll = 0f;
        dropping = false;
        landMeat = null;
        landingForMeat = false;
        timer = Random.Range(groundSeconds.x, groundSeconds.y);
    }

    void Awake()
    {
        ground = GetComponent<Dinosaur>();
        agent = GetComponent<NavMeshAgent>();
        path = new NavMeshPath();
        if (animator == null) animator = GetComponent<Animator>();
    }

    void Start()
    {
        homeNav = NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas)
            ? hit.position
            : transform.position;
        timer = Random.Range(groundSeconds.x, groundSeconds.y);
    }

    void Update()
    {
        if (mode == Mode.Ground)
        {
            SetAnimSpeed(groundAnimSpeed);
            timer -= Time.deltaTime;
            // Only take off from a spot with open sky above (it wanders until it stands on one).
            if (timer <= 0f && !ground.IsBusy && Time.time >= nextClearCheck)
            {
                nextClearCheck = Time.time + 0.3f;
                if (HasClearance(transform.position)) BeginTakeOff();
            }
            return;
        }

        modeTime += Time.deltaTime;
        SetAnimSpeed(flightAnimSpeed);
        if (animator != null) animator.SetFloat(speedParam, 3f);

        switch (mode)
        {
            case Mode.TakeOff:
            {
                // Climb almost straight up out of the pen: leaving sideways while still low would
                // carry the wings through the pen walls and the walkway.
                float climbed = transform.position.y - groundY;
                if (climbed < safeHeight)
                {
                    Vector3 up = new Vector3(transform.position.x, groundY + cruiseHeight, transform.position.z);
                    Fly(up, climbOutSpeed, climbRate);
                }
                else
                {
                    Fly(TargetOnCircle(), flySpeed * Mathf.SmoothStep(0.3f, 1f, (climbed - safeHeight) / 2f), climbRate);
                    if (modeTime >= takeOffSeconds) { mode = Mode.Cruise; modeTime = 0f; }
                }
                break;
            }

            case Mode.Cruise:
                Fly(TargetOnCircle(), flySpeed, climbRate);
                if (Time.time >= nextMeatCheck)
                {
                    nextMeatCheck = Time.time + 0.5f;
                    var meat = FindMeat();
                    if (meat != null) { BeginLand(meat); break; }
                }
                if (Time.time >= flightEnd) BeginLand(null);
                break;

            case Mode.Land:
                UpdateLanding();
                break;
        }
    }

    // ---------------- take off ----------------

    void BeginTakeOff()
    {
        ground.enabled = false;
        agent.enabled = false;

        groundY = transform.position.y;
        yaw = transform.eulerAngles.y;
        radius = Random.Range(orbitRadius.x, orbitRadius.y);
        orbitDir = Random.value < 0.5f ? -1f : 1f;
        flightEnd = Time.time + Random.Range(flightSeconds.x, flightSeconds.y);
        nextMeatCheck = Time.time + 2f;
        mode = Mode.TakeOff;
        modeTime = 0f;
    }

    // ---------------- flying ----------------

    Vector3 TargetOnCircle()
    {
        Vector3 p = transform.position;
        float angle = Mathf.Atan2(p.z - orbitCenter.z, p.x - orbitCenter.x) + orbitDir * 0.6f;
        float y = groundY + cruiseHeight + Mathf.Sin(Time.time * 0.6f) * heightWobble;
        return new Vector3(orbitCenter.x + Mathf.Cos(angle) * radius, y, orbitCenter.z + Mathf.Sin(angle) * radius);
    }

    /// <summary>Steers toward a point: limited turn rate, limited climb rate, banks into turns.</summary>
    void Fly(Vector3 target, float speed, float vertical)
    {
        float dt = Time.deltaTime;
        Vector3 p = transform.position;

        Vector3 flat = new Vector3(target.x - p.x, 0f, target.z - p.z);
        float want = flat.sqrMagnitude > 0.0001f ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg : yaw;
        float newYaw = Mathf.MoveTowardsAngle(yaw, want, turnRate * dt);
        float yawRate = Mathf.DeltaAngle(yaw, newYaw) / Mathf.Max(dt, 0.0001f);
        yaw = newYaw;

        float yawRad = yaw * Mathf.Deg2Rad;
        p.x += Mathf.Sin(yawRad) * speed * dt;
        p.z += Mathf.Cos(yawRad) * speed * dt;

        float oldY = p.y;
        p.y = Mathf.MoveTowards(p.y, target.y, vertical * dt);
        float climbSpeed = (p.y - oldY) / Mathf.Max(dt, 0.0001f);

        float targetPitch = -Mathf.Clamp(Mathf.Atan2(climbSpeed, Mathf.Max(speed, 0.5f)) * Mathf.Rad2Deg, -25f, 25f);
        float targetRoll = Mathf.Clamp(-yawRate * 0.3f, -35f, 35f);
        pitch = Mathf.Lerp(pitch, targetPitch, 5f * dt);
        roll = Mathf.Lerp(roll, targetRoll, 4f * dt);

        transform.SetPositionAndRotation(p, Quaternion.Euler(pitch, yaw, roll));
    }

    // ---------------- landing ----------------

    void BeginLand(Meat meat)
    {
        landMeat = meat;
        landingForMeat = meat != null;
        dropping = false;
        landPoint = ChooseLandingPoint(meat);
        mode = Mode.Land;
        modeTime = 0f;
    }

    Vector3 ChooseLandingPoint(Meat meat)
    {
        if (meat != null && NavMesh.SamplePosition(meat.transform.position, out NavMeshHit meatNav, 2f, NavMesh.AllAreas))
        {
            // Come down near the meat but in open space (wings must not touch a wall), then walk
            // the last few meters like any other dinosaur. Candidates are tried from close to far,
            // on the pen side first.
            Vector3 toHome = homeNav - meatNav.position;
            toHome.y = 0f;
            Vector3 baseDir = toHome.sqrMagnitude > 0.01f ? toHome.normalized : Vector3.forward;
            float[] distances = { 1.5f, 3f, 4.5f };
            float[] angles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };
            foreach (float d in distances)
                foreach (float a in angles)
                {
                    Vector3 spot = meatNav.position + Quaternion.Euler(0f, a, 0f) * baseDir * d;
                    if (NavMesh.SamplePosition(spot, out NavMeshHit spotNav, 1.5f, NavMesh.AllAreas) && HasClearance(spotNav.position))
                        return spotNav.position;
                }
            // no open space next to the meat: land at home and walk over
        }

        // Pick a spot at home with open space around it, so the descent never touches a wall.
        for (int attempt = 0; attempt < 30; attempt++)
        {
            float reach = attempt < 12 ? 3.5f : 7f;
            Vector3 around = homeNav + new Vector3(Random.Range(-reach, reach), 0f, Random.Range(-reach, reach));
            if (NavMesh.SamplePosition(around, out NavMeshHit homeHit, 4f, NavMesh.AllAreas) && HasClearance(homeHit.position))
                return homeHit.position;
        }
        return homeNav;
    }

    /// <summary>
    /// True when the whole column above the point, from the floor up to the safe height, is free for
    /// the bird's body (wings spread): no wall, no walkway deck. Used for landing spots, which are
    /// also the take-off spots, so the bird always flies straight up and down through open sky.
    /// </summary>
    bool HasClearance(Vector3 p)
    {
        int mask = ~(1 << LayerMask.NameToLayer("PlayerBarrier"));
        Vector3 half = bodyHalfExtents;
        for (float y = p.y + half.y + 0.15f; y <= p.y + safeHeight + half.y; y += 0.6f)
        {
            if (Physics.CheckBox(new Vector3(p.x, y, p.z), half, Quaternion.identity, mask, QueryTriggerInteraction.Ignore))
                return false;
        }
        return true;
    }

    void UpdateLanding()
    {
        // The meat may be taken or eaten by someone else on the way down: land at home instead.
        if (landingForMeat && (landMeat == null || !landMeat.IsEdible || (landMeat.ClaimedBy != null && landMeat.ClaimedBy != ground)))
        {
            landMeat = null;
            landingForMeat = false;
            landPoint = ChooseLandingPoint(null);
        }

        Vector3 p = transform.position;
        float distance = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(landPoint.x, 0f, landPoint.z));

        // Fly high over the walkway to a point straight above the landing spot, then drop down
        // vertically. A sloping approach would carry the bird through the pen wall and the deck.
        float cruiseY = landPoint.y + cruiseHeight;
        if (!dropping && distance > dropRadius)
        {
            float speed = flySpeed * Mathf.Clamp(0.3f + distance / 6f, 0.3f, 1f);
            Fly(new Vector3(landPoint.x, cruiseY, landPoint.z), speed, climbRate);
            if (modeTime > 60f) dropping = true;   // safety: never circle forever
            return;
        }

        dropping = true;
        float dropSpeed = Mathf.Clamp(distance * 1.2f, 0.35f, 2f);
        Fly(new Vector3(landPoint.x, landPoint.y, landPoint.z), dropSpeed, climbRate * 1.1f);

        bool arrived = distance < 0.9f && transform.position.y - landPoint.y < 0.4f;
        if (arrived || modeTime > 90f) TouchDown();
    }

    void TouchDown()
    {
        transform.SetPositionAndRotation(landPoint, Quaternion.Euler(0f, yaw, 0f));
        pitch = 0f;
        roll = 0f;

        agent.enabled = true;
        agent.Warp(landPoint);
        ground.enabled = true;
        ground.ResumeIdle();

        mode = Mode.Ground;
        timer = Random.Range(groundSeconds.x, groundSeconds.y);
        landMeat = null;
        landingForMeat = false;
    }

    // ---------------- meat ----------------

    Meat FindMeat()
    {
        if (!ground.WantsFood) return null;

        Meat best = null;
        float bestSqr = float.MaxValue;
        Vector3 me = transform.position;

        foreach (var meat in Meat.All)
        {
            if (!meat.IsEdible || meat.ClaimedBy != null) continue;

            Vector3 mp = meat.transform.position;
            if ((new Vector3(mp.x, 0f, mp.z) - new Vector3(homeNav.x, 0f, homeNav.z)).sqrMagnitude > meatSearchRadius * meatSearchRadius) continue;

            float sqr = (mp - me).sqrMagnitude;
            if (sqr >= bestSqr) continue;

            // Only meat in this bird's own pen: there must be a walkable route from its home.
            if (!NavMesh.SamplePosition(mp, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;
            if (!NavMesh.CalculatePath(homeNav, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;

            best = meat;
            bestSqr = sqr;
        }
        return best;
    }

    void SetAnimSpeed(float value)
    {
        if (animator != null) animator.speed = value;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 c = orbitCenter + Vector3.up * (cruiseHeight);
        Gizmos.DrawWireSphere(c, orbitRadius.x);
        Gizmos.DrawWireSphere(c, orbitRadius.y);
    }
}
