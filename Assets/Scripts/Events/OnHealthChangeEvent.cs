using UnityEngine;

public class OnHealthChangeEvent
{
    public HealthType healthType;
    public int amount;
    public GameObject target;
    public GameObject hitObject;
    public bool WeakPointHit;
}