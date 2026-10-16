using UnityEngine;

// Pasang di object item di dunia game. Collider harus "Is Trigger".
public class ItemPickup : MonoBehaviour
{
    public ItemData item;
    public int amount = 1;

    // Untuk game 3D. Kalau game 2D, ganti jadi OnTriggerEnter2D(Collider2D other)
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        Inventory inv = other.GetComponent<Inventory>();
        if (inv != null && inv.AddItem(item, amount))
            Destroy(gameObject);
    }
}