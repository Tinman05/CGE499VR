using System.Collections;
using UnityEngine;

/// <summary>
/// Raises the tracking space so a seated player sees the world from standing eye height.
/// Measures the real head height at start (and whenever the headset is recentered) and
/// lifts the rig by the difference. Standing players get an offset of about zero.
/// Put on the OVRCameraRig root. Needs Tracking Origin Type = Floor Level.
/// </summary>
[RequireComponent(typeof(OVRCameraRig))]
public class SeatedHeightOffset : MonoBehaviour
{
    [Tooltip("Eye height the player should have in the game, in meters.")]
    [SerializeField] float targetEyeHeight = 1.65f;
    [Tooltip("Largest lift that will be applied, in meters.")]
    [SerializeField] float maxOffset = 0.9f;
    [Tooltip("Measure again whenever the headset is recentered (hold the Meta button).")]
    [SerializeField] bool recalibrateOnRecenter = true;
    [Tooltip("Used instead of measuring when Auto Calibrate is off.")]
    [SerializeField] float manualOffset;
    [SerializeField] bool autoCalibrate = true;

    OVRCameraRig rig;
    float appliedOffset;

    void Awake()
    {
        rig = GetComponent<OVRCameraRig>();
    }

    void OnEnable()
    {
        if (recalibrateOnRecenter && OVRManager.display != null)
            OVRManager.display.RecenteredPose += Calibrate;
    }

    void OnDisable()
    {
        if (OVRManager.display != null)
            OVRManager.display.RecenteredPose -= Calibrate;
        SetOffset(0f);
    }

    IEnumerator Start()
    {
        if (!autoCalibrate)
        {
            SetOffset(manualOffset);
            yield break;
        }

        // Editor test without a headset: the head sits at floor level, so pretend to stand at eye height.
        if (Application.isEditor && !OVRManager.isHmdPresent)
        {
            SetOffset(targetEyeHeight);
            yield break;
        }

        // Wait until the headset reports a real position, then give tracking a moment to settle.
        float timeout = 8f;
        while (timeout > 0f && rig.centerEyeAnchor.localPosition.y < 0.3f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(1f);
        Calibrate();
    }

    [ContextMenu("Calibrate now")]
    public void Calibrate()
    {
        // Head height in tracking space, so it is not affected by the offset applied below.
        float measured = rig.centerEyeAnchor.localPosition.y;
        if (measured < 0.3f) return; // no valid tracking yet

        SetOffset(Mathf.Clamp(targetEyeHeight - measured, 0f, maxOffset));
    }

    void SetOffset(float offset)
    {
        appliedOffset = offset;
        var p = rig.trackingSpace.localPosition;
        p.y = offset;
        rig.trackingSpace.localPosition = p;
    }

    void LateUpdate()
    {
        // Keep the offset if something else rewrites the tracking space position.
        if (Mathf.Abs(rig.trackingSpace.localPosition.y - appliedOffset) > 0.001f)
            SetOffset(appliedOffset);
    }
}
