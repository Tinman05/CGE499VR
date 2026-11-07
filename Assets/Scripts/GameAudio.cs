using UnityEngine;

/// <summary>
/// All sound effects of the feeding game in one place: picking up and throwing meat, meat landing,
/// dinosaurs eating, the "+1" ding, the NPC, and the win / lose jingles. It listens to the static
/// events of Meat, Dinosaur, HungerGame and NpcDialogue, so nothing else needs to know about audio.
/// Also gives the controllers a short buzz when meat is grabbed or thrown.
/// </summary>
public class GameAudio : MonoBehaviour
{
    [Header("Meat")]
    [SerializeField] AudioClip pickup;
    [SerializeField] AudioClip thrown;
    [SerializeField] AudioClip meatLanded;
    [SerializeField] AudioClip thud;

    [Header("Dinosaurs")]
    [SerializeField] AudioClip[] eat;
    [SerializeField] AudioClip burp;
    [Tooltip("Played where the scoreboard is, with the +1.")]
    [SerializeField] AudioClip ding;
    [SerializeField] Transform scoreboard;

    [Header("Round and NPC")]
    [SerializeField] AudioClip win;
    [SerializeField] AudioClip lose;
    [SerializeField] AudioClip npcPress;

    [Header("Volumes")]
    [SerializeField, Range(0f, 1f)] float master = 0.9f;
    [SerializeField] bool haptics = true;

    AudioSource[] pool;
    int next;

    void Awake()
    {
        pool = new AudioSource[10];
        for (int i = 0; i < pool.Length; i++)
        {
            var go = new GameObject("sfx " + i);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 2f;
            src.maxDistance = 45f;
            pool[i] = src;
        }
    }

    void OnEnable()
    {
        Meat.Picked += OnPicked;
        Meat.Thrown += OnThrown;
        Meat.Landed += OnLanded;
        Dinosaur.Ate += OnAte;
        HungerGame.StateChanged += OnState;
        NpcDialogue.Pressed += OnNpc;
    }

    void OnDisable()
    {
        Meat.Picked -= OnPicked;
        Meat.Thrown -= OnThrown;
        Meat.Landed -= OnLanded;
        Dinosaur.Ate -= OnAte;
        HungerGame.StateChanged -= OnState;
        NpcDialogue.Pressed -= OnNpc;
    }

    void OnPicked(Meat meat)
    {
        Play(pickup, meat.transform.position, 0.8f);
        if (haptics) Haptics.PulseHolding(0.45f, 0.08f);
    }

    void OnThrown(Meat meat)
    {
        Play(thrown, meat.transform.position, 0.9f);
        if (haptics) Haptics.PulseHolding(0.7f, 0.12f);
    }

    void OnLanded(Meat meat, float impact)
    {
        float v = Mathf.Clamp01(impact / 8f);
        var clip = meat.IsEdible ? thud : meatLanded;
        Play(clip != null ? clip : meatLanded, meat.transform.position, 0.35f + 0.65f * v);
    }

    void OnAte(Dinosaur dino)
    {
        if (dino == null) return;
        if (eat != null && eat.Length > 0) Play(eat[Random.Range(0, eat.Length)], dino.transform.position, 1f);
        if (burp != null && Random.value < 0.25f) Play(burp, dino.transform.position, 0.7f);
        Play(ding, scoreboard != null ? scoreboard.position : dino.transform.position, 0.7f);
    }

    void OnState(HungerGame.State state)
    {
        Vector3 at = scoreboard != null ? scoreboard.position : transform.position;
        if (state == HungerGame.State.Won) Play(win, at, 1f);
        else if (state == HungerGame.State.Lost) Play(lose, at, 1f);
    }

    void OnNpc() => Play(npcPress, scoreboard != null ? scoreboard.position : transform.position, 0.8f);

    void Play(AudioClip clip, Vector3 at, float volume)
    {
        if (clip == null || pool == null) return;
        var src = pool[next];
        next = (next + 1) % pool.Length;
        src.transform.position = at;
        src.clip = clip;
        src.volume = volume * master;
        src.pitch = Random.Range(0.94f, 1.06f);
        src.Play();
    }
}
