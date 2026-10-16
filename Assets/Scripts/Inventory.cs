using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InventorySlot
{
    public ItemData item;
    public int amount;
    public bool IsEmpty => item == null;
}

public class Inventory : MonoBehaviour
{
    public int slotCount = 20;
    public List<InventorySlot> slots = new List<InventorySlot>();

    // UI akan subscribe ke event ini supaya otomatis refresh
    public event Action OnInventoryChanged;
    public event Action<ItemData> OnItemUsed;   // didengarkan script efek (mis. PlayerHealth)

    void Awake()
    {
        while (slots.Count < slotCount)
            slots.Add(new InventorySlot());
    }

    // Return true kalau semua item berhasil masuk, false kalau inventory penuh
    public bool AddItem(ItemData item, int amount = 1)
    {
        // 1) Isi dulu slot yang sudah berisi item sama (stacking)
        if (item.stackable)
        {
            foreach (var slot in slots)
            {
                if (slot.item == item && slot.amount < item.maxStack)
                {
                    int add = Mathf.Min(item.maxStack - slot.amount, amount);
                    slot.amount += add;
                    amount -= add;
                    if (amount <= 0) { OnInventoryChanged?.Invoke(); return true; }
                }
            }
        }

        // 2) Sisanya taruh di slot kosong
        foreach (var slot in slots)
        {
            if (!slot.IsEmpty) continue;

            int add = item.stackable ? Mathf.Min(item.maxStack, amount) : 1;
            slot.item = item;
            slot.amount = add;
            amount -= add;
            if (amount <= 0) { OnInventoryChanged?.Invoke(); return true; }
        }

        OnInventoryChanged?.Invoke();
        Debug.Log("Inventory penuh!");
        return false;
    }

    public void RemoveItem(int slotIndex, int amount = 1)
    {
        var slot = slots[slotIndex];
        if (slot.IsEmpty) return;

        slot.amount -= amount;
        if (slot.amount <= 0)
        {
            slot.item = null;
            slot.amount = 0;
        }
        OnInventoryChanged?.Invoke();
    }

    // Hapus berdasarkan item (cari slot pertama yang berisi item ini)
    public bool RemoveItem(ItemData item, int amount = 1)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].item == item)
            {
                RemoveItem(i, amount);
                return true;
            }
        }
        return false;
    }

    // Tukar isi dua slot (atau gabungkan stack kalau itemnya sama)
    public void SwapSlots(int a, int b)
    {
        if (a == b) return;
        var sa = slots[a];
        var sb = slots[b];

        if (!sa.IsEmpty && !sb.IsEmpty && sa.item == sb.item && sa.item.stackable)
        {
            int move = Mathf.Min(sa.item.maxStack - sb.amount, sa.amount);
            sb.amount += move;
            sa.amount -= move;
            if (sa.amount <= 0) { sa.item = null; sa.amount = 0; }
        }
        else
        {
            (sa.item, sb.item) = (sb.item, sa.item);
            (sa.amount, sb.amount) = (sb.amount, sa.amount);
        }
        OnInventoryChanged?.Invoke();
    }

    // Hapus seluruh isi satu slot
    public void DeleteSlot(int slotIndex)
    {
        slots[slotIndex].item = null;
        slots[slotIndex].amount = 0;
        OnInventoryChanged?.Invoke();
    }

    public void UseItem(int slotIndex)
    {
        var slot = slots[slotIndex];
        if (slot.IsEmpty) return;

        ItemData item = slot.item;

        if (item.itemType != ItemType.Consumable)
        {
            Debug.Log(item.itemName + " tidak bisa dipakai langsung.");
            return;
        }

        OnItemUsed?.Invoke(item);

        if (item.consumeOnUse)
            RemoveItem(slotIndex, 1);
    }

    // Cek apakah inventory punya item (berguna untuk pintu terkunci, quest, dll)
    public bool HasItem(ItemData item, int amount = 1)
    {
        int total = 0;
        foreach (var slot in slots)
            if (slot.item == item) total += slot.amount;
        return total >= amount;
    }
}