using UnityEngine;

public enum ItemType
{
    Consumable,   // bisa dipakai (potion, makanan, dll)
    Material,     // bahan, tidak bisa dipakai langsung
    Key           // item kunci/quest, dicek lewat script lain
}

// Klik kanan di Project window > Create > Inventory > Item
[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    [TextArea] public string description;

    [Header("Stack")]
    public bool stackable = true;
    public int maxStack = 99;

    [Header("Pemakaian")]
    public ItemType itemType = ItemType.Consumable;
    public bool consumeOnUse = true;   // berkurang 1 setelah dipakai
    public int effectValue = 20;       // misal jumlah HP yang dipulihkan
}