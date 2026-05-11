using UnityEngine;

/// <summary>
/// Place on a trigger collider that covers the boss room entrance.
/// Drag the BossController into the 'boss' field and set the Player layer mask.
/// When the player walks in, the boss activates once (ambient starts, 10-second countdown begins).
/// </summary>
public class BossRoomTrigger : MonoBehaviour
{
    [SerializeField] private BossController boss;
    [SerializeField] private LayerMask      playerLayer;

    private void OnTriggerEnter(Collider other)
    {
        if ((playerLayer.value & (1 << other.gameObject.layer)) == 0) return;

        boss.ActivateBoss();
        // Disable so the trigger fires exactly once.
        GetComponent<Collider>().enabled = false;
    }
}
