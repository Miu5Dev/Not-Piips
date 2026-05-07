using System;
using UnityEngine;

public class HealthController : MonoBehaviour
{
    public int health;
    public int shield;
    public int maxHealth = 100;
    public int maxShield = 0;
    public int weakPointMultiplier = 2;

    public bool isDead = false;
    public bool isPlayer = false;

    private int lastHealth;
    private int lastShield;

    public void OnHealthChange(OnHealthChangeEvent e)
    {
        if (e.target != gameObject || isDead) return;

        int finalAmount = e.amount;

        if (e.WeakPointHit && e.amount < 0)
            finalAmount *= weakPointMultiplier;

        switch (e.healthType)
        {
            case HealthType.Health:
                health = Mathf.Clamp(health + finalAmount, 0, maxHealth);
                break;

            case HealthType.Shield:
                if (finalAmount < 0)
                {
                    int damage = -finalAmount;

                    int damageToShield = Mathf.Min(shield, damage);
                    shield -= damageToShield;
                    damage -= damageToShield;

                    if (damage > 0)
                        health = Mathf.Clamp(health - damage, 0, maxHealth);
                }
                else
                {
                    shield = Mathf.Clamp(shield + finalAmount, 0, maxShield);
                }
                break;
        }

        if (health <= 0 && !isDead)
        {
            isDead = true;
            OnDie();
        }
    }

    public void Start()
    {
        health = maxHealth;
        shield = maxShield;
        ForceUIUpdate();
    }

    public void LateUpdate()
    {
        if (!isPlayer) return;

        if (health != lastHealth || shield != lastShield)
        {
            RaiseUIEvent();
            lastHealth = health;
            lastShield = shield;
        }
    }

    public void ForceUIUpdate()
    {
        if (!isPlayer) return;

        RaiseUIEvent();
        lastHealth = health;
        lastShield = shield;
    }

    private void RaiseUIEvent()
    {
        EventBus.Raise(new OnChangeHealthUIEvent()
        {
            newHealth = health,
            newShield = shield,
            maxHealth = this.maxHealth,
            maxShield = this.maxShield,
        });
    }

    public void OnDie()
    {
        EventBus.Raise(new OnDieEvent()
        {
            murderedObject = gameObject
        });
    }
}