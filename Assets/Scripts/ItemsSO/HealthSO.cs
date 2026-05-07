using UnityEngine;

public enum HealthType { Health, Shield }

[CreateAssetMenu(
    fileName = "New Health Item",
    menuName = "Objects/Health",
    order    = 2)]
public class HealthSO : itemSO
{
    [Header("Health")]
    public HealthType healthType;
    public float      restoreAmount;
}