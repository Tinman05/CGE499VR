using TMPro;
using UnityEngine;

/// <summary>
/// The round: keep every dinosaur fed until the timer runs out. The round starts when the player
/// first picks up meat; if any dinosaur reaches zero fullness the round is lost. After the result
/// is shown for a while (or when the player presses the NPC) everything resets.
/// </summary>
public class HungerGame : MonoBehaviour
{
    public enum State { Ready, Playing, Won, Lost }

    public static HungerGame Current { get; private set; }

    /// <summary>Raised whenever the round changes state (started, won, lost, back to ready).</summary>
    public static event System.Action<State> StateChanged;

    [SerializeField] TMP_Text statusLabel;
    [SerializeField] FeedingFeedback feedback;

    [Header("Rules")]
    [Tooltip("Length of a round in seconds. Surviving it wins.")]
    [SerializeField] float roundSeconds = 240f;
    [Tooltip("Hunger drains this many times faster at the end of the round than at the start.")]
    [SerializeField] float difficultyRamp = 1.5f;
    [Tooltip("Seconds the result stays on the board before a new round is prepared. 0 = wait for the NPC press.")]
    [SerializeField] float autoRestartSeconds = 25f;

    [Header("Messages ({0} is replaced; U+200B marks where Thai text may wrap)")]
    [SerializeField] string readyText = "หยิบ​เนื้อ​เพื่อ​เริ่ม​เกม";
    [SerializeField] string playingText = "เวลา​ที่​เหลือ {0}";
    [SerializeField] string wonText = "ชนะ​แล้ว! ป้อน​ครบ {0} ครั้ง";
    [SerializeField] string lostText = "แพ้​แล้ว {0} หิว​เกิน​ไป";

    State state = State.Ready;
    float elapsed;
    float endedAt;
    int meals;
    int shownSecond = -1;
    string starvedName = "";

    public string StarvedName => starvedName;
    public int Meals => meals;

    public bool IsPlaying => state == State.Playing;
    public bool IsEnded => state == State.Won || state == State.Lost;
    public State Phase => state;
    public float TimeLeft => Mathf.Max(0f, roundSeconds - elapsed);

    /// <summary>1 at the start of the round, up to difficultyRamp at its end.</summary>
    public float DifficultyFactor => Mathf.Lerp(1f, difficultyRamp, Mathf.Clamp01(elapsed / roundSeconds));

    void Awake()
    {
        Current = this;
    }

    void OnEnable()
    {
        Meat.Picked += OnMeatPicked;
        Dinosaur.Ate += OnAte;
    }

    void OnDisable()
    {
        Meat.Picked -= OnMeatPicked;
        Dinosaur.Ate -= OnAte;
        if (Current == this) Current = null;
    }

    void Start()
    {
        ShowStatus();
    }

    void Update()
    {
        switch (state)
        {
            case State.Playing:
                elapsed += Time.deltaTime;
                if (elapsed >= roundSeconds) { Finish(State.Won); break; }

                // Only rewrite the label when the shown second changes (no per-frame string garbage).
                int second = Mathf.CeilToInt(TimeLeft);
                if (second != shownSecond) { shownSecond = second; ShowStatus(); }
                break;

            case State.Won:
            case State.Lost:
                if (autoRestartSeconds > 0f && Time.time - endedAt >= autoRestartSeconds) Restart();
                break;
        }
    }

    void OnMeatPicked(Meat meat)
    {
        if (state == State.Ready) StartRound();
    }

    void OnAte(Dinosaur dino)
    {
        if (state == State.Playing) meals++;
    }

    void StartRound()
    {
        elapsed = 0f;
        meals = 0;
        shownSecond = -1;
        starvedName = "";
        state = State.Playing;
        ShowStatus();
        StateChanged?.Invoke(state);
    }

    /// <summary>Ends the round as a win right now (demo shortcut, so a whole round can be shown quickly).</summary>
    public void ForceWin()
    {
        if (state == State.Playing) Finish(State.Won);
    }

    public void OnStarved(DinoHunger who)
    {
        if (state != State.Playing) return;
        starvedName = who.DisplayName;
        var death = who.GetComponent<DinoDeath>();
        if (death != null) death.Collapse();
        Finish(State.Lost);
    }

    void Finish(State result)
    {
        state = result;
        endedAt = Time.time;
        ShowStatus();
        StateChanged?.Invoke(state);
    }

    /// <summary>Back to the waiting state with every dinosaur reset.</summary>
    public void Restart()
    {
        foreach (var hunger in DinoHunger.All) hunger.ResetHunger();
        if (feedback != null) feedback.ResetScore();
        elapsed = 0f;
        meals = 0;
        starvedName = "";
        state = State.Ready;
        ShowStatus();
        StateChanged?.Invoke(state);
    }

    void ShowStatus()
    {
        if (statusLabel == null) return;

        switch (state)
        {
            case State.Ready: statusLabel.text = readyText; break;
            case State.Playing: statusLabel.text = string.Format(playingText, FormatTime(TimeLeft)); break;
            case State.Won: statusLabel.text = string.Format(wonText, meals); break;
            case State.Lost: statusLabel.text = string.Format(lostText, starvedName); break;
        }
    }

    static string FormatTime(float seconds)
    {
        int s = Mathf.CeilToInt(seconds);
        return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    }
}
