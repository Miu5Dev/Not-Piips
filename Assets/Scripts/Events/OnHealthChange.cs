using UnityEngine;

public class OnHealthChange
{
    public HealthType healthType;
    public int amount;
    public GameObject target;
}

public enum HealthType
{
    Health,
    Shield
}