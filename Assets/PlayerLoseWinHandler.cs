using UnityEngine;

public class PlayerLoseWinHandler : MonoBehaviour
{
    public GameObject LoseGO;
    public GameObject WinGo;
    public LayerMask BossLayer;
    
    
    public void onLose(OnDieEvent e)
    {
        if (e.murderedObject == this.gameObject)
        {
            LoseGO.SetActive(true);
            FreeMouse();
        }
    }

    public void onWin(OnDieEvent e)
    {
        if ((BossLayer.value & (1 << e.murderedObject.layer)) != 0)
        {
            WinGo.SetActive(true);
            FreeMouse();
        }
    }
    
    public void FreeMouse()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
    
}
