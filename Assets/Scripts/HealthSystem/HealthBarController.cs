using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthBarController : MonoBehaviour
{
    public Slider healthBar;
    public Slider shieldBar;
    
    public TMP_Text healthValue;
    public TMP_Text shieldValue;


    public void onUpdate(OnChangeHealthUIEvent e)
    {
        
        healthBar.maxValue = e.maxHealth;
        shieldBar.maxValue = e.maxShield;
        
        healthBar.value = e.newHealth;
        shieldBar.value = e.newShield;
        
        healthValue.text = e.newHealth.ToString();
        shieldValue.text = e.newShield.ToString();

    }
    
}
