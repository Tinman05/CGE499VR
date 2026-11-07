using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;

/// <summary>
/// Big message floating in front of the player when a round is won or lost, so it cannot be missed
/// (the scoreboard by the table is easy to overlook while looking at the dinosaurs). It appears a
/// moment after the result, so the player first sees the dinosaur collapse, and goes away when the
/// next round is prepared. Built entirely in code; only the Thai font needs to be assigned.
/// </summary>
public class GameBanner : MonoBehaviour
{
    [SerializeField] TMP_FontAsset font;
    [Tooltip("Distance in front of the head (m).")]
    [SerializeField] float distance = 1.7f;
    [Tooltip("How far below eye level (m).")]
    [SerializeField] float drop = 0.12f;
    [SerializeField] float showDelay = 1.2f;
    [SerializeField] Vector2 panelSize = new Vector2(1000f, 430f);
    [SerializeField] float worldScale = 0.0015f;

    [Header("Messages ({0} is replaced; U+200B marks where Thai text may wrap)")]
    [SerializeField] string lostTitle = "แพ้​แล้ว!";
    [SerializeField] string lostLine = "{0} หิว​จน​ล้ม​ลง";
    [SerializeField] string wonTitle = "ชนะ​แล้ว!";
    [SerializeField] string wonLine = "ป้อน​อาหาร​ครบ {0} ครั้ง";
    [SerializeField] string hint = "ชี้​ที่ NPC แล้ว​กด​เพื่อ​เริ่ม​ใหม่";
    [SerializeField] Color lostColor = new Color(1f, 0.35f, 0.3f);
    [SerializeField] Color wonColor = new Color(0.45f, 1f, 0.5f);

    Transform panel;
    CanvasGroup group;
    TMP_Text title, line, hintText;
    Image back;
    bool wanted;
    float showAt;
    float shown;   // 0..1 fade
    Vector3 smoothedPosition;
    Transform viewer;

    static readonly List<OTL_FeatureTag> Features = new List<OTL_FeatureTag> { OTL_FeatureTag.kern, OTL_FeatureTag.mark, OTL_FeatureTag.mkmk };

    void Awake()
    {
        Build();
        panel.gameObject.SetActive(false);
    }

    void OnEnable() => HungerGame.StateChanged += OnState;
    void OnDisable() => HungerGame.StateChanged -= OnState;

    void OnDestroy()
    {
        if (panel != null) Destroy(panel.gameObject);
    }

    void OnState(HungerGame.State state)
    {
        var game = HungerGame.Current;
        switch (state)
        {
            case HungerGame.State.Lost:
                Set(lostTitle, string.Format(lostLine, game != null ? game.StarvedName : ""), lostColor);
                wanted = true; showAt = Time.time + showDelay;
                break;
            case HungerGame.State.Won:
                Set(wonTitle, string.Format(wonLine, game != null ? game.Meals : 0), wonColor);
                wanted = true; showAt = Time.time + 0.3f;
                break;
            default:
                wanted = false;
                break;
        }
    }

    void Set(string t, string l, Color c)
    {
        title.text = t;
        title.color = c;
        line.text = l;
    }

    void LateUpdate()
    {
        bool visible = wanted && Time.time >= showAt;
        shown = Mathf.MoveTowards(shown, visible ? 1f : 0f, Time.deltaTime * 3f);

        if (shown <= 0.001f)
        {
            if (panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
            return;
        }

        if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
        if (viewer == null) return;

        if (!panel.gameObject.activeSelf)
        {
            panel.gameObject.SetActive(true);
            smoothedPosition = Target();
        }

        // follows the head loosely: stays readable but does not stick to the face
        smoothedPosition = Vector3.Lerp(smoothedPosition, Target(), 1f - Mathf.Exp(-4f * Time.deltaTime));
        panel.position = smoothedPosition;
        Vector3 away = panel.position - viewer.position;
        if (away.sqrMagnitude > 0.0001f) panel.rotation = Quaternion.LookRotation(away, Vector3.up);
        panel.localScale = Vector3.one * worldScale * Mathf.Lerp(0.8f, 1f, shown);
        group.alpha = shown;
    }

    Vector3 Target()
    {
        Vector3 forward = viewer.forward; forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = viewer.forward;
        return viewer.position + forward.normalized * distance + Vector3.up * -drop;
    }

    // ---------------- building the panel ----------------

    void Build()
    {
        var root = new GameObject("GameBanner Panel", typeof(RectTransform));
        panel = root.transform;
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        group = root.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = panelSize;
        rt.localScale = Vector3.one * worldScale;

        back = NewImage("Back", root.transform, new Color(0.04f, 0.04f, 0.05f, 0.82f));
        Stretch(back.rectTransform, 0f, 0f, 1f, 1f, 0f);

        title = NewText("Title", root.transform, 150f, FontStyles.Bold);
        Place(title.rectTransform, 0.52f, 1f, 24f);
        line = NewText("Line", root.transform, 84f, FontStyles.Normal);
        Place(line.rectTransform, 0.27f, 0.52f, 24f);
        hintText = NewText("Hint", root.transform, 58f, FontStyles.Normal);
        hintText.color = new Color(0.85f, 0.85f, 0.85f);
        hintText.text = hint;
        Place(hintText.rectTransform, 0f, 0.27f, 24f);
    }

    TMP_Text NewText(string name, Transform parent, float size, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.color = Color.white;
        t.fontFeatures = Features;
        t.raycastTarget = false;
        return t;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static void Stretch(RectTransform r, float xMin, float yMin, float xMax, float yMax, float inset)
    {
        r.anchorMin = new Vector2(xMin, yMin);
        r.anchorMax = new Vector2(xMax, yMax);
        r.offsetMin = new Vector2(inset, inset);
        r.offsetMax = new Vector2(-inset, -inset);
    }

    static void Place(RectTransform r, float yMin, float yMax, float inset) => Stretch(r, 0f, yMin, 1f, yMax, inset);
}
