using UnityEngine;

// Klik kanan di Project window > Create > Inventory > Item
[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    [TextArea] public string description;
    public bool stackable = true;
    public int maxStack = 99;
}