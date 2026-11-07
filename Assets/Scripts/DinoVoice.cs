using UnityEngine;

/// <summary>
/// Gives a dinosaur its own voice: every so often it roars, grunts or screeches from where it
/// stands (3D sound, so the player hears which pen it is in). Hungry dinosaurs call more often.
/// Silent while the round is over.
/// </summary>
public class DinoVoice : MonoBehaviour
{
    [SerializeField] AudioClip[] calls;
    [SerializeField] Vector2 secondsBetweenCalls = new Vector2(18f, 40f);
    [SerializeField, Range(0f, 1f)] float volume = 0.8f;
    [SerializeField] float maxDistance = 45f;
    [Tooltip("Pitch range: lower for big dinosaurs, higher for small ones.")]
    [SerializeField] Vector2 pitch = new Vector2(0.9f, 1.05f);

    AudioSource source;
    DinoHunger hunger;
    float nextCall;

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = 4f;
        source.maxDistance = maxDistance;
        hunger = GetComponent<DinoHunger>();
        Schedule();
    }

    void Update()
    {
        if (calls == null || calls.Length == 0 || Time.time < nextCall) return;

        var game = HungerGame.Current;
        if (game == null || !game.IsEnded)
        {
            source.pitch = Random.Range(pitch.x, pitch.y);
            source.PlayOneShot(calls[Random.Range(0, calls.Length)], volume);
        }
        Schedule();
    }

    void Schedule()
    {
        float wait = Random.Range(secondsBetweenCalls.x, secondsBetweenCalls.y);
        if (hunger != null && hunger.Fullness < 0.35f) wait *= 0.5f;   // hungry: calls more often
        nextCall = Time.time + wait;
    }
}
