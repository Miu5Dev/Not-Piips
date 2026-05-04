using UnityEngine;

public class OnHealthChangeEvent
{
    public HealthType healthType;
    public int amount;
    public GameObject target;
    public bool WeakPointHit;
}

public enum HealthType
{
    Health,
    Shield
}