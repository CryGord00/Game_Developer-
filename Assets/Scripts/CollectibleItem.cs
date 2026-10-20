using UnityEngine;
using UnityEngine.InputSystem;

public class CollectibleItem : MonoBehaviour
{
    [SerializeField] private ItemData item;          // seret asset ItemData ke sini
    [SerializeField] private int amount = 1;
    [SerializeField] private float interactDistance = 2f;

    private Inventory inventory;
    private Transform hero;
    private bool heroNearby;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("hero"))
            return;

        heroNearby = true;
        hero = other.transform;
        inventory = other.GetComponentInParent<Inventory>();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("hero"))
            return;

        heroNearby = false;
        hero = null;
        inventory = null;
    }

    private void Update()
    {
        if (!heroNearby || Keyboard.current == null || inventory == null || hero == null)
            return;

        // Sekarang interactDistance benar-benar dipakai
        if (Vector3.Distance(transform.position, hero.position) > interactDistance)
            return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (inventory.AddItem(item, amount))
                Destroy(gameObject);

            return;
        }

        if (Keyboard.current.qKey.wasPressedThisFrame)
            inventory.RemoveItem(item);
    }
}