using UnityEngine;

/// <summary>
/// Keeps one piece of meat sitting at this transform (the "meat basket").
/// When the player takes it away, a new piece appears after a short delay.
/// </summary>
public class MeatSpawner : MonoBehaviour
{
    [SerializeField] Meat meatPrefab;
    [Tooltip("How far the meat must move from the spawn point before a new one is made.")]
    [SerializeField] float takenDistance = 0.25f;
    [SerializeField] float respawnDelay = 0.75f;

    [Tooltip("Optional piece already placed in the scene, so it is visible in edit mode. Spawned at Start if empty.")]
    [SerializeField] Meat current;

    float respawnAt = -1f;

    void Start()
    {
        if (current == null) Spawn();
        else current.Home = transform;
    }

    void Update()
    {
        if (current != null)
        {
            if ((current.transform.position - transform.position).sqrMagnitude > takenDistance * takenDistance)
            {
                current = null;
                respawnAt = Time.time + respawnDelay;
            }
        }
        else if (respawnAt < 0f)
        {
            // The meat we were holding for the player got destroyed.
            respawnAt = Time.time + respawnDelay;
        }
        else if (Time.time >= respawnAt)
        {
            Spawn();
        }
    }

    void Spawn()
    {
        current = Instantiate(meatPrefab, transform.position, transform.rotation);
        current.Home = transform;
        respawnAt = -1f;
    }
}
