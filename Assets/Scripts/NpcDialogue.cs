using TMPro;
using UnityEngine;

/// <summary>
/// Speech bubble for the guide NPC. Each press (ray + trigger) shows the next line;
/// after the last line, or after a while, the bubble closes. The bubble stays fixed in the
/// world so it does not follow the player's head.
/// Wire Interact / SetHover to the NPC's InteractableUnityEventWrapper.
/// </summary>
public class NpcDialogue : MonoBehaviour
{
    [SerializeField] GameObject bubble;
    [SerializeField] TMP_Text label;
    [SerializeField, TextArea(2, 4)] string[] lines;
    [Tooltip("The bubble closes by itself after this many seconds without a press.")]
    [SerializeField] float autoHideSeconds = 15f;

    [Header("Hover highlight")]
    [SerializeField] Renderer highlightRenderer;
    [Tooltip("Emission added to every part of the NPC while the ray points at it.")]
    [SerializeField] Color hoverGlow = new Color(0.22f, 0.22f, 0.22f);

    /// <summary>Raised on every press of the NPC (used for the click sound).</summary>
    public static event System.Action Pressed;

    int index = -1;
    float hideAt;
    MaterialPropertyBlock block;

    // Emission works for every material of the NPC at once, so the multi-colour paint is kept.
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    void Start()
    {
        if (bubble != null) bubble.SetActive(false);

        if (highlightRenderer != null) block = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (bubble != null && bubble.activeSelf && Time.time >= hideAt)
            Close();
    }

    /// <summary>Show the next line, or close after the last one.</summary>
    public void Interact()
    {
        Pressed?.Invoke();

        // After a round has ended, pressing the NPC starts over instead of talking.
        if (HungerGame.Current != null && HungerGame.Current.IsEnded)
        {
            HungerGame.Current.Restart();
            return;
        }

        if (lines == null || lines.Length == 0) return;

        index++;
        if (index >= lines.Length)
        {
            Close();
            return;
        }

        label.text = lines[index];
        bubble.SetActive(true);
        hideAt = Time.time + autoHideSeconds;
    }

    public void SetHover(bool hovering)
    {
        if (highlightRenderer == null) return;

        highlightRenderer.GetPropertyBlock(block);
        block.SetColor(EmissionColorId, hovering ? hoverGlow : Color.black);
        highlightRenderer.SetPropertyBlock(block);
    }

    public void HoverOn() => SetHover(true);
    public void HoverOff() => SetHover(false);

    void Close()
    {
        index = -1;
        if (bubble != null) bubble.SetActive(false);
    }
}
