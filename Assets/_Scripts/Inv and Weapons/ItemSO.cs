using UnityEngine;

[CreateAssetMenu(
    fileName = "New Item",
    menuName = "Objects/Item",
    order = 0)]
public class itemSO : ScriptableObject
{
    [Header("Type")]
    public ItemType itemType;

    [Header("Stats")]
    public float weight;

    [Header("InvSystem")]
    public Vector2Int size;
    public Sprite icon;
}