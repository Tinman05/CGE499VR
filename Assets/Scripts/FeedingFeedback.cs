using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Shows the player that feeding worked: a counter board and a "+1" that floats up
/// above the dinosaur that just finished eating.
/// </summary>
public class FeedingFeedback : MonoBehaviour
{
    [Header("Score board")]
    [SerializeField] TMP_Text scoreLabel;
    [Tooltip("{0} is replaced by the number of pieces eaten.")]
    [SerializeField] string scoreFormat = "Fed {0}";

    [Header("Floating popup")]
    [SerializeField] TMP_FontAsset popupFont;
    [SerializeField] string popupText = "+1";
    [SerializeField] Color popupColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] float popupSize = 9f;
    [SerializeField] float popupRise = 1.2f;
    [SerializeField] float popupSeconds = 1.8f;

    int total;
    Transform viewer;

    void OnEnable()
    {
        Dinosaur.Ate += OnAte;
        UpdateLabel();
    }

    void OnDisable()
    {
        Dinosaur.Ate -= OnAte;
    }

    /// <summary>Back to zero for a new round.</summary>
    public void ResetScore()
    {
        total = 0;
        UpdateLabel();
    }

    void OnAte(Dinosaur dino)
    {
        total++;
        UpdateLabel();
        StartCoroutine(Popup(dino));
    }

    void UpdateLabel()
    {
        if (scoreLabel != null) scoreLabel.text = string.Format(scoreFormat, total);
    }

    IEnumerator Popup(Dinosaur dino)
    {
        var agent = dino.GetComponent<NavMeshAgent>();
        float height = agent != null ? agent.height : 2f;
        Vector3 start = dino.transform.position + dino.transform.forward * 0.8f + Vector3.up * (height + 0.4f);

        var go = new GameObject("FeedPopup");
        go.transform.position = start;
        var text = go.AddComponent<TextMeshPro>();
        if (popupFont != null) text.font = popupFont;
        text.text = popupText;
        text.fontSize = popupSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = popupColor;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        for (float t = 0f; t < popupSeconds; t += Time.deltaTime)
        {
            float k = t / popupSeconds;
            go.transform.position = start + Vector3.up * (popupRise * k);

            var view = GetViewer();
            if (view != null) go.transform.rotation = Quaternion.LookRotation(go.transform.position - view.position, Vector3.up);

            var c = popupColor; c.a = 1f - k * k;
            text.color = c;
            yield return null;
        }

        Destroy(go);
    }

    Transform GetViewer()
    {
        if (viewer == null)
        {
            var cam = Camera.main;
            viewer = cam != null ? cam.transform : null;
        }
        return viewer;
    }
}
