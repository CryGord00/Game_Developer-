using UnityEngine;
using UnityEngine.InputSystem;

public class ToggleLight : MonoBehaviour
{
    [Header("Light Settings")]
    public Light targetLight;               // Drag Light component ke sini
    public float interactDistance = 3f;     // Jarak maksimal bisa nyalakan
    public Key interactKey = Key.F;         // Tombol

    [Header("Optional")]
    public Transform player;                // Drag object hero ke sini

    private bool isOn;

    void Start()
    {
        if (targetLight != null)
            isOn = targetLight.enabled;
    }

    void Update()
    {
        if (player == null || targetLight == null) return;

        float dist = Vector3.Distance(player.position, transform.position);

        if (dist <= interactDistance && Keyboard.current[interactKey].wasPressedThisFrame)
        {
            isOn = !isOn;
            targetLight.enabled = isOn;

            Debug.Log(isOn ? "Lampu NYALA" : "Lampu MATI");
        }
    }

    // Tampilkan radius di Scene view
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}