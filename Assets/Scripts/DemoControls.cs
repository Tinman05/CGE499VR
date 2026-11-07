using UnityEngine;

/// <summary>
/// Controller shortcuts for showing the whole game in a short video ("God mode" for the demo):
///   Left X  - feed every dinosaur to full
///   Left Y  - starve one dinosaur right now (shows the collapse and the defeat banner)
///   Right A - win the round right now
///   Right B - back to the start of a new round
/// Switch it off with the Enabled checkbox when the game is handed over for normal play.
/// </summary>
public class DemoControls : MonoBehaviour
{
    [SerializeField] bool enabledShortcuts = true;

    int starveIndex;

    void Update()
    {
        if (!enabledShortcuts) return;

        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch)) FeedAll();
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch)) StarveOne();
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch)) Win();
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch)) Restart();
    }

    static void FeedAll()
    {
        foreach (var hunger in DinoHunger.All) hunger.SetFullness(1f);
    }

    void StarveOne()
    {
        var game = HungerGame.Current;
        if (game == null || DinoHunger.All.Count == 0) return;
        if (game.Phase == HungerGame.State.Ready) return;   // the round has not started yet
        var victim = DinoHunger.All[starveIndex++ % DinoHunger.All.Count];
        victim.SetFullness(0f);                              // the normal rules take over: lose + collapse
    }

    static void Win()
    {
        if (HungerGame.Current != null) HungerGame.Current.ForceWin();
    }

    static void Restart()
    {
        if (HungerGame.Current != null) HungerGame.Current.Restart();
    }
}
