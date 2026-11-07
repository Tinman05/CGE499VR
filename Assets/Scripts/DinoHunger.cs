using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hunger of one dinosaur: 1 = full, 0 = starving. It drains while the round is running,
/// a meal refills it, and a full dinosaur ignores meat. Draws its own bar above the dinosaur.
/// </summary>
[RequireComponent(typeof(Dinosaur))]
public class DinoHunger : MonoBehaviour
{
    public static readonly List<DinoHunger> All = new List<DinoHunger>();

    [SerializeField] string displayName = "Dinosaur";
    [Tooltip("Fullness lost per second at the start of a round (1 = full).")]
    [SerializeField, Range(0.002f, 0.05f)] float decayPerSecond = 0.009f;
    [Tooltip("How much one piece of meat refills.")]
    [SerializeField] float mealValue = 0.45f;
    [Tooltip("The dinosaur ignores meat while its fullness is at or above this.")]
    [SerializeField] float hungryBelow = 0.8f;
    [SerializeField] float warnBelow = 0.3f;
    [SerializeField] Vector2 startFullness = new Vector2(0.55f, 0.8f);

    [Header("Activity")]
    [Tooltip("Hunger only drains at full speed while the dinosaur is moving (walking, running, flying). Standing still or eating drains this fraction of the normal rate.")]
    [SerializeField, Range(0f, 1f)] float restingDrain = 0.15f;
    [Tooltip("Speed in m/s at which the dinosaur counts as fully active.")]
    [SerializeField] float fullActivitySpeed = 0.8f;

    [Header("Bar")]
    [SerializeField] float barHeight = 2.5f;
    [SerializeField] Vector2 barSize = new Vector2(1.2f, 0.2f);

    Dinosaur dino;
    float fullness = 1f;
    Transform bar;
    RectTransform fillRect;
    Image fillImage;
    Transform viewer;
    Vector3 lastPosition;
    float smoothSpeed;

    /// <summary>0..1: how much the dinosaur is moving right now (smoothed), used to scale hunger.</summary>
    public float Activity => Mathf.Lerp(restingDrain, 1f, Mathf.Clamp01(smoothSpeed / Mathf.Max(0.01f, fullActivitySpeed)));

    /// <summary>Hide or show the bar above the head (hidden while the dinosaur lies collapsed).</summary>
    public void SetBarVisible(bool visible)
    {
        if (bar != null) bar.gameObject.SetActive(visible);
    }

    public string DisplayName => displayName;
    public float Fullness => fullness;
    public bool WantsFood => fullness < hungryBelow;

    void Awake()
    {
        dino = GetComponent<Dinosaur>();
        lastPosition = transform.position;
        BuildBar();
        ResetHunger();
    }

    void OnEnable()
    {
        All.Add(this);
        Dinosaur.Ate += OnAte;
    }

    void OnDisable()
    {
        All.Remove(this);
        Dinosaur.Ate -= OnAte;
    }

    void OnDestroy()
    {
        if (bar != null) Destroy(bar.gameObject);
    }

    /// <summary>Sets how full the dinosaur is (0 = starving, 1 = full). Used by the demo shortcuts.</summary>
    public void SetFullness(float value)
    {
        fullness = Mathf.Clamp01(value);
        Refresh();
    }

    public void ResetHunger()
    {
        fullness = Random.Range(startFullness.x, startFullness.y);
        Refresh();
    }

    void OnAte(Dinosaur who)
    {
        if (who != dino) return;
        fullness = Mathf.Min(1f, fullness + mealValue);
    }

    void Update()
    {
        // Measure how fast it really moves (works for walking and flying alike).
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed = (transform.position - lastPosition).magnitude / dt;
        lastPosition = transform.position;
        smoothSpeed = Mathf.Lerp(smoothSpeed, Mathf.Min(speed, 20f), 1f - Mathf.Exp(-4f * dt));

        var game = HungerGame.Current;
        if (game != null && game.IsPlaying)
        {
            fullness = Mathf.Max(0f, fullness - decayPerSecond * Activity * game.DifficultyFactor * Time.deltaTime);
            if (fullness <= 0f) game.OnStarved(this);
        }
        Refresh();
    }

    void LateUpdate()
    {
        if (bar == null) return;

        bar.position = transform.position + Vector3.up * barHeight;

        if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
        if (viewer != null) bar.rotation = Quaternion.LookRotation(bar.position - viewer.position, Vector3.up);
    }

    // ---------------- bar ----------------

    void BuildBar()
    {
        var root = new GameObject(displayName + " HungerBar", typeof(RectTransform));
        bar = root.transform;
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1000f, 1000f * barSize.y / barSize.x);
        rt.localScale = Vector3.one * (barSize.x / 1000f);

        var back = NewImage("Back", root.transform, new Color(0.05f, 0.05f, 0.05f, 0.75f));
        Stretch(back.rectTransform, 0f, 1f, 0f);

        var fill = NewImage("Fill", root.transform, Color.green);
        fillImage = fill;
        fillRect = fill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(10f, 10f);
        fillRect.offsetMax = new Vector2(-10f, -10f);
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static void Stretch(RectTransform r, float min, float max, float inset)
    {
        r.anchorMin = new Vector2(min, 0f);
        r.anchorMax = new Vector2(max, 1f);
        r.offsetMin = r.offsetMax = new Vector2(inset, 0f);
    }

    void Refresh()
    {
        if (fillRect == null) return;

        fillRect.anchorMax = new Vector2(Mathf.Clamp01(fullness), 1f);

        Color c;
        if (fullness > 0.5f) c = new Color(0.3f, 0.85f, 0.3f);
        else if (fullness > warnBelow) c = new Color(0.95f, 0.85f, 0.2f);
        else
        {
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 8f);
            c = new Color(0.95f * pulse + 0.05f, 0.15f, 0.15f);
        }
        fillImage.color = c;
    }
}
