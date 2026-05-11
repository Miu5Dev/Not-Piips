using UnityEngine;

/// <summary>
/// Auto-added by BossController at runtime to whichever Collider is dragged into
/// the "Room Trigger" field. Do not add this manually.
/// </summary>
public class BossRoomTrigger : MonoBehaviour
{
    private BossController _boss;
    private LayerMask      _playerLayer;

    public void Init(BossController boss, LayerMask playerLayer)
    {
        _boss        = boss;
        _playerLayer = playerLayer;
    }

    private void OnTriggerEnter(Collider other)
    {
        if ((_playerLayer.value & (1 << other.gameObject.layer)) == 0) return;

        _boss.StartBoss();
        GetComponent<Collider>().enabled = false;
    }
}
