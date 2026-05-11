using UnityEngine;

public class OnHealthChangedEvent
{
    public HealthType healthType;
    public int amount;
    public GameObject target;
    public GameObject hitObject;
    public bool WeakPointHit;
}