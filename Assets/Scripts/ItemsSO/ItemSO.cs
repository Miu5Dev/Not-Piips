using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(
    fileName = "New Item",
    menuName = "Objects/Item",
    order = 0)]
public class itemSO : ScriptableObject
{
    [Header("Type")]
    ItemType itemType;
    
    [Header("Stacking")]
    public bool isStackable  = false;
    public int  maxStackSize = 30;
    
    [Header("Stats")]
    public float weight;
    
    [Header("InvSystem")] 
    public Vector2Int size;
    public Sprite icon;
}