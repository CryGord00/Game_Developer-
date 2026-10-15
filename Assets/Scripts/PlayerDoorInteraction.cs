using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerDoorInteraction : MonoBehaviour
{
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private float rayHeight = 1f;
    [SerializeField] private LayerMask doorLayer = Physics.DefaultRaycastLayers;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.eKey.wasPressedThisFrame)
            return;

        Vector3 origin = transform.position + Vector3.up * rayHeight;

        if (Physics.Raycast(
                origin,
                transform.forward,
                out RaycastHit hit,
                interactDistance,
                doorLayer,
                QueryTriggerInteraction.Ignore))
        {
            SojaExiles.opencloseDoor door =
                hit.collider.GetComponentInParent<SojaExiles.opencloseDoor>();

            if (door != null)
                door.Toggle();
        }
    }
}