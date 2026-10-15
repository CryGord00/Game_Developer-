using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Tooltip("Tinggi titik yang dilihat kamera dari kaki hero (sekitar kepala/bahu)")]
    public float targetHeight = 1.5f;

    [Header("Posisi Kamera")]
    public float distance = 4f;
    public float minDistance = 0.8f;

    [Header("Kontrol Mouse")]
    public float mouseSensitivity = 3f;
    public float minPitch = -20f;
    public float maxPitch = 60f;
    public bool lockCursor = true;

    [Header("Halus & Tabrakan")]
    public float followSmooth = 15f;
    public LayerMask collisionMask = ~0;
    public float collisionRadius = 0.25f;

    private float yaw;
    private float pitch = 15f;
    private float currentDistance;

    void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("ThirdPersonCamera: Target belum diisi. Drag hero ke field Target.");
            enabled = false;
            return;
        }

        // Mulai tepat di belakang hero
        yaw = target.eulerAngles.y;
        currentDistance = distance;

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        // Tekan Esc untuk melepas kursor, klik kiri untuk mengunci lagi
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (lockCursor && Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 focusPoint = target.position + Vector3.up * targetHeight;
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 direction = rotation * Vector3.back;

        // Dekatkan kamera kalau ada dinding di belakang hero
        float wantedDistance = distance;
        if (Physics.SphereCast(
                focusPoint,
                collisionRadius,
                direction,
                out RaycastHit hit,
                distance,
                collisionMask,
                QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.transform.IsChildOf(target))
            {
                wantedDistance = Mathf.Max(hit.distance, minDistance);
            }
        }

        currentDistance = Mathf.Lerp(
            currentDistance,
            wantedDistance,
            followSmooth * Time.deltaTime
        );

        transform.position = focusPoint + direction * currentDistance;
        transform.rotation = Quaternion.LookRotation(
            focusPoint - transform.position,
            Vector3.up
        );
    }
}