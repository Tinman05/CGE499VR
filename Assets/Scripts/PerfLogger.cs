using UnityEngine;

/// <summary>
/// Writes the average / worst frame rate to the log every few seconds, so performance on the headset
/// can be read with "adb logcat -s Unity". Costs almost nothing; remove it from the scene when not needed.
/// </summary>
public class PerfLogger : MonoBehaviour
{
    [SerializeField] float interval = 5f;

    float timer, worst;
    int frames;

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        timer += dt;
        frames++;
        if (dt > worst) worst = dt;

        if (timer < interval) return;

        float avgMs = timer / frames * 1000f;
        Debug.Log($"[PERF] avg {1000f / avgMs:F1} fps ({avgMs:F1} ms), worst frame {worst * 1000f:F1} ms, " +
                  $"gc {System.GC.GetTotalMemory(false) / 1048576} MB, refresh {Screen.currentResolution.refreshRateRatio.value:F0} Hz, " +
                  $"res {Screen.width}x{Screen.height}");
        timer = 0f; frames = 0; worst = 0f;
    }
}
