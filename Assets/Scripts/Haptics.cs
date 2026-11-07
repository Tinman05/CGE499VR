using UnityEngine;

/// <summary>
/// Short controller buzzes. Call <see cref="Pulse"/> from anywhere; a hidden driver object stops
/// the vibration again when the time is up. Does nothing when no Quest controller is active.
/// </summary>
public class Haptics : MonoBehaviour
{
    static Haptics instance;

    float stopLeft = -1f;
    float stopRight = -1f;

    /// <param name="amplitude">0..1</param>
    /// <param name="seconds">How long the buzz lasts.</param>
    /// <param name="left">Buzz the left controller.</param>
    /// <param name="right">Buzz the right controller.</param>
    public static void Pulse(float amplitude, float seconds, bool left = true, bool right = true)
    {
        if (!Application.isPlaying) return;

        if (instance == null)
        {
            var go = new GameObject("[Haptics]");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Haptics>();
        }
        instance.Begin(amplitude, seconds, left, right);
    }

    /// <summary>Buzz the controller(s) whose trigger or grip is held right now (both if neither is).</summary>
    public static void PulseHolding(float amplitude, float seconds)
    {
        bool left = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch)
                    || OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch);
        bool right = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch)
                     || OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);
        if (!left && !right) { left = true; right = true; }
        Pulse(amplitude, seconds, left, right);
    }

    void Begin(float amplitude, float seconds, bool left, bool right)
    {
        amplitude = Mathf.Clamp01(amplitude);
        if (left) { OVRInput.SetControllerVibration(1f, amplitude, OVRInput.Controller.LTouch); stopLeft = Time.unscaledTime + seconds; }
        if (right) { OVRInput.SetControllerVibration(1f, amplitude, OVRInput.Controller.RTouch); stopRight = Time.unscaledTime + seconds; }
    }

    void Update()
    {
        if (stopLeft >= 0f && Time.unscaledTime >= stopLeft)
        {
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
            stopLeft = -1f;
        }
        if (stopRight >= 0f && Time.unscaledTime >= stopRight)
        {
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
            stopRight = -1f;
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
    }
}
