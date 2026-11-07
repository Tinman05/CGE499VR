using UnityEngine;

/// <summary>
/// Invisible walls that stop the VR player's character controller but nothing else,
/// so thrown meat still flies over the walkway edge into the pit.
/// Put on the parent of the barrier colliders; every child must use the barrier layer.
/// </summary>
public class PlayerOnlyBarrier : MonoBehaviour
{
    void Awake()
    {
        if (transform.childCount == 0) return;

        int barrierLayer = transform.GetChild(0).gameObject.layer;
        if (barrierLayer == 0) return;

        for (int layer = 0; layer < 32; layer++)
            Physics.IgnoreLayerCollision(barrierLayer, layer, true);
    }
}
