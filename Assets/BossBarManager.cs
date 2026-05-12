using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class BossBarManager : MonoBehaviour
{
    
    public GameObject bossBar;
    
    public LayerMask BossLayer;
    public Slider BossSlider;
    public TMP_Text BossName;
    public TMP_Text BossHealth;

    private int lastHealth;

    private BossController controller;
    private HealthController hp;

    public void onPingEventReceived(OnPingEvent e)
    {
        if ((BossLayer.value & (1 << e.sender.layer)) != 0)
        {
            controller = e.sender?.GetComponent<BossController>();
            hp = controller._health;
            BossName.text = controller.bossData.bossName;
            BossSlider.maxValue = controller.bossData.maxHealth;
            BossSlider.minValue = 0; 
            BossSlider.value = controller.bossData.maxHealth;
            lastHealth = controller.bossData.maxHealth;

            EventBus.Raise(new OnPingEvent()
            {
                sender = transform.root.gameObject,
            });
        }
    }

    public void LateUpdate()
    {
        
        if (controller == null || hp == null || hp.isDead)
        {
            if(bossBar.active)
                bossBar.SetActive(false);
            return;
        }

        if (!bossBar.active)
        {
            bossBar.SetActive(true);
            BossName.text = controller.bossData.bossName;
            BossSlider.maxValue = controller.bossData.maxHealth;
            BossSlider.minValue = 0; 
            BossSlider.value = controller.bossData.maxHealth;
        }
        
        if (lastHealth != controller._health.health)
        {
            lastHealth = controller._health.health;
            BossHealth.text = $"{hp.health} / {hp.maxHealth}";
            BossSlider.value = hp.health;
        }
        
    }
}
