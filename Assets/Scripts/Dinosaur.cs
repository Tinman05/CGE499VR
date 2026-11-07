using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>
/// Wanders around its enclosure, walks to meat that has landed nearby and eats it.
/// Needs a baked NavMesh (AI Navigation) on the enclosure floor.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Dinosaur : MonoBehaviour
{
    enum State { Idle, Wander, SeekMeat, Eat }

    [Header("Senses")]
    [SerializeField] float detectRadius = 15f;
    [SerializeField] float eatDistance = 1.5f;
    [SerializeField] float scanInterval = 0.25f;

    [Header("Wandering")]
    [SerializeField] float wanderRadius = 8f;
    [SerializeField] Vector2 idleTime = new Vector2(2f, 5f);

    [Header("Eating")]
    [SerializeField] float eatDuration = 3f;
    [SerializeField] float walkSpeed = 1.5f;
    [SerializeField] float runSpeed = 3.5f;
    [Tooltip("The dinosaur turns to face the meat and only starts eating once it is looking at it.")]
    [SerializeField] bool requireFacing = true;
    [Tooltip("How closely (degrees) it must face the meat before it starts eating.")]
    [SerializeField] float facingTolerance = 20f;
    [SerializeField] float turnSpeed = 120f;

    [Header("Animation (optional)")]
    [SerializeField] Animator animator;
    [SerializeField] string speedParam = "Speed";
    [SerializeField] string eatingBool = "Eating";

    [Header("Events")]
    public UnityEvent onAte;

    NavMeshAgent agent;
    Vector3 homePosition;
    State state = State.Idle;
    Meat target;
    NavMeshPath pathBuffer;
    DinoHunger hunger;
    float stateTimer;
    float nextScan;
    bool hasSpeedParam;
    bool hasEatingBool;

    /// <summary>Raised by every dinosaur when it finishes a piece of meat (score board, effects, sounds).</summary>
    public static event System.Action<Dinosaur> Ate;

    public int MealsEaten { get; private set; }

    /// <summary>True while walking to meat or eating it (a flying dinosaur must not take off now).</summary>
    public bool IsBusy => state == State.SeekMeat || state == State.Eat;

    /// <summary>A full dinosaur ignores meat (see DinoHunger).</summary>
    public bool WantsFood => hunger == null || hunger.WantsFood;

    /// <summary>Back to calm wandering, used after the component was switched off for a flight.</summary>
    public void ResumeIdle()
    {
        ReleaseTarget();
        EnterIdle();
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        pathBuffer = new NavMeshPath();
        hunger = GetComponent<DinoHunger>();
        homePosition = transform.position;

        if (animator != null)
        {
            foreach (var p in animator.parameters)
            {
                if (p.name == speedParam) hasSpeedParam = true;
                if (p.name == eatingBool) hasEatingBool = true;
            }
        }
    }

    void Start()
    {
        EnterIdle();
    }

    void Update()
    {
        switch (state)
        {
            case State.Idle:
                stateTimer -= Time.deltaTime;
                if (TryFindMeat()) break;
                if (stateTimer <= 0f) EnterWander();
                break;

            case State.Wander:
                if (TryFindMeat()) break;
                if (ReachedDestination()) EnterIdle();
                break;

            case State.SeekMeat:
                UpdateSeek();
                break;

            case State.Eat:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f) FinishEating();
                break;
        }

        if (hasSpeedParam)
            animator.SetFloat(speedParam, agent.velocity.magnitude, 0.1f, Time.deltaTime);
    }

    void SetEating(bool eating)
    {
        if (hasEatingBool) animator.SetBool(eatingBool, eating);
    }

    void EnterIdle()
    {
        SetEating(false);
        state = State.Idle;
        stateTimer = Random.Range(idleTime.x, idleTime.y);
        agent.ResetPath();
    }

    void EnterWander()
    {
        Vector3 point = homePosition + Random.insideUnitSphere * wanderRadius;
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            state = State.Wander;
            agent.speed = walkSpeed;
            agent.SetDestination(hit.position);
        }
        else
        {
            EnterIdle();
        }
    }

    bool TryFindMeat()
    {
        if (Time.time < nextScan) return false;
        nextScan = Time.time + scanInterval;

        if (!WantsFood) return false;

        Meat best = null;
        NavMeshHit bestHit = default;
        float bestSqr = detectRadius * detectRadius;
        Vector3 pos = transform.position;

        foreach (var meat in Meat.All)
        {
            if (!meat.IsEdible) continue;
            if (meat.ClaimedBy != null && meat.ClaimedBy != this) continue;

            float sqr = (meat.transform.position - pos).sqrMagnitude;
            if (sqr >= bestSqr) continue;
            if (!IsReachable(meat, out NavMeshHit hit)) continue;

            bestSqr = sqr;
            best = meat;
            bestHit = hit;
        }

        if (best == null) return false;

        target = best;
        target.ClaimedBy = this;
        state = State.SeekMeat;
        agent.speed = runSpeed;
        agent.SetDestination(bestHit.position);
        return true;
    }

    /// <summary>
    /// Meat in another pen or in a blocked blind spot has no complete path, so it is ignored
    /// instead of making the dinosaur press against a wall.
    /// </summary>
    bool IsReachable(Meat meat, out NavMeshHit hit)
    {
        if (!NavMesh.SamplePosition(meat.transform.position, out hit, 2f, NavMesh.AllAreas))
            return false; // landed off the NavMesh

        return agent.CalculatePath(hit.position, pathBuffer) && pathBuffer.status == NavMeshPathStatus.PathComplete;
    }

    void UpdateSeek()
    {
        // Meat can be destroyed, picked up again, or stolen while we walk.
        if (target == null || !target.IsEdible || target.ClaimedBy != this)
        {
            ReleaseTarget();
            EnterIdle();
            return;
        }

        float flatDist = Vector3.Distance(
            new Vector3(transform.position.x, 0f, transform.position.z),
            new Vector3(target.transform.position.x, 0f, target.transform.position.z));

        if (flatDist <= eatDistance)
        {
            agent.ResetPath();

            // In reach: stop and turn until the mouth points at the meat, then eat.
            if (requireFacing && !TurnToward(target.transform.position))
                return;

            state = State.Eat;
            stateTimer = eatDuration;
            SetEating(true);
            return;
        }

        // Meat may have rolled; keep the destination fresh.
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
            agent.SetDestination(target.transform.position);
    }

    /// <summary>Turns around the vertical axis toward a point; true once within the facing tolerance.</summary>
    bool TurnToward(Vector3 worldPoint)
    {
        Vector3 to = worldPoint - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return true;

        Quaternion want = Quaternion.LookRotation(to);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
        return Quaternion.Angle(transform.rotation, want) <= facingTolerance;
    }

    void FinishEating()
    {
        if (target != null) Destroy(target.gameObject);
        target = null;
        MealsEaten++;
        onAte.Invoke();
        Ate?.Invoke(this);
        EnterIdle();
    }

    void ReleaseTarget()
    {
        if (target != null && target.ClaimedBy == this) target.ClaimedBy = null;
        target = null;
    }

    bool ReachedDestination()
    {
        return !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;
    }

    void OnDisable()
    {
        ReleaseTarget();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(Application.isPlaying ? homePosition : transform.position, wanderRadius);
    }
}
