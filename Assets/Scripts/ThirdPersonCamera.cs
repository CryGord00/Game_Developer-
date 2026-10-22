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
    private readonly RaycastHit[] cameraHits = new RaycastHit[32];

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

        // Cari penghalang terdekat di belakang hero. SphereCastAll/NonAlloc
        // diperlukan karena collider hero sendiri bisa menjadi hit pertama;
        // jika hanya memeriksa satu hit, kamera dapat menembus furnitur.
        float wantedDistance = distance;
        int hitCount = Physics.SphereCastNonAlloc(
                focusPoint,
                collisionRadius,
                direction,
                cameraHits,
                distance,
                collisionMask,
                QueryTriggerInteraction.Ignore);
        float closestObstacleDistance = distance;
        bool foundObstacle = false;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = cameraHits[i].collider;
            if (hitCollider == null || hitCollider.transform == target ||
                hitCollider.transform.IsChildOf(target))
            {
                continue;
            }

            if (cameraHits[i].distance < closestObstacleDistance)
            {
                closestObstacleDistance = cameraHits[i].distance;
                foundObstacle = true;
            }
        }

        if (foundObstacle)
        {
            // Jangan biarkan kamera masuk ke tubuh Hero saat collider furnitur
            // berada sangat dekat. Batas minimum juga mencegah tampilan close-up
            // ekstrem ketika dialog laci menghentikan waktu permainan.
            float safeMinimumDistance = Mathf.Clamp(minDistance, 0.1f, distance);
            wantedDistance = Mathf.Max(
                safeMinimumDistance,
                Mathf.Min(distance, closestObstacleDistance - 0.05f));
        }

        // Masuk mendekati hero langsung saat ada penghalang, lalu mundur halus
        // setelah jalurnya kembali lapang agar kamera tidak sempat menembus objek.
        currentDistance = wantedDistance < currentDistance
            ? wantedDistance
            : Mathf.Lerp(currentDistance, wantedDistance, followSmooth * Time.unscaledDeltaTime);

        transform.position = focusPoint + direction * currentDistance;
        transform.rotation = Quaternion.LookRotation(
            focusPoint - transform.position,
            Vector3.up
        );
    }
}
