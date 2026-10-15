using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float turnSpeed = 720f;
    public float gravity = -15f;

    [Tooltip("Centang kalau arah gerak harus relatif ke kamera")]
    public bool cameraRelative = false;

    [Header("Door Interaction")]
    public float interactionRange = 2.5f;
    public Key openDoorKey = Key.E;

    [Tooltip("Tinggi titik awal Raycast dari posisi Hero")]
    public float rayHeight = 1f;

    private CharacterController controller;
    private Animator animator;

    // Pintu yang sedang terdeteksi di depan Hero
    private Door currentDoor = null;

    private Vector3 verticalVelocity;
    private Vector3 lookDirection;
    private bool hasLookDirection = false;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // Cari Animator pada Hero atau child model Hero
        animator = GetComponentInChildren<Animator>();

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
        else
        {
            Debug.LogWarning(
                "PlayerMovement: Animator tidak ditemukan."
            );
        }
    }

    void Update()
    {
        HandleMovement();
        HandleDoorInteraction();
    }

    // Rotasi Hero dilakukan di LateUpdate
    void LateUpdate()
    {
        if (!hasLookDirection)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(
            lookDirection,
            Vector3.up
        );

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime
        );
    }

    // ====================== MOVEMENT ======================
    void HandleMovement()
    {
        Keyboard kb = Keyboard.current;

        if (kb == null)
            return;

        float horizontal = 0f;
        float vertical = 0f;

        // Input gerakan keyboard
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)
            horizontal -= 1f;

        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed)
            horizontal += 1f;

        if (kb.sKey.isPressed || kb.downArrowKey.isPressed)
            vertical -= 1f;

        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)
            vertical += 1f;

        Vector3 input = new Vector3(
            horizontal,
            0f,
            vertical
        ).normalized;

        // Gerakan relatif terhadap kamera (opsional)
        if (cameraRelative && Camera.main != null)
        {
            Vector3 camForward = Camera.main.transform.forward;
            Vector3 camRight = Camera.main.transform.right;

            camForward.y = 0f;
            camRight.y = 0f;

            camForward.Normalize();
            camRight.Normalize();

            input = (
                camForward * vertical +
                camRight * horizontal
            ).normalized;
        }

        bool isMoving = input.sqrMagnitude >= 0.01f;

        // Simpan arah hadap Hero saat bergerak
        if (isMoving)
        {
            lookDirection = input;
            hasLookDirection = true;
        }

        // Animasi berjalan
        if (animator != null)
        {
            animator.SetBool("isWalking", isMoving);
        }

        // Gravitasi
        if (controller.isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = -2f;
        }

        verticalVelocity.y += gravity * Time.deltaTime;

        // Gerakan horizontal dan vertikal
        Vector3 move = input * walkSpeed + verticalVelocity;

        controller.Move(move * Time.deltaTime);
    }

    // ====================== DOOR INTERACTION ======================
    void HandleDoorInteraction()
    {
        // Titik awal Raycast berada di bagian depan tubuh Hero
        Vector3 rayOrigin = transform.position +
                            Vector3.up * rayHeight;

        // Arah deteksi mengikuti arah hadap Hero
        Vector3 rayDirection = transform.forward;

        Ray ray = new Ray(rayOrigin, rayDirection);

        // Deteksi hanya objek pertama yang terkena Raycast
        bool hitSomething = Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactionRange
        );

        Door detectedDoor = null;

        if (hitSomething)
        {
            // Cari komponen Door pada objek yang terkena
            detectedDoor =
                hit.collider.GetComponentInParent<Door>();
        }

        // Perbarui pintu yang sedang dihadapi
        if (detectedDoor != currentDoor)
        {
            // Lepaskan status pintu sebelumnya
            if (currentDoor != null)
            {
                currentDoor.SetPlayerNearby(false);
            }

            // Tandai pintu baru yang terdeteksi
            currentDoor = detectedDoor;

            if (currentDoor != null)
            {
                currentDoor.SetPlayerNearby(true);
            }
        }

        // Tekan E untuk membuka atau menutup pintu di depan Hero
        Keyboard kb = Keyboard.current;

        if (currentDoor != null &&
            kb != null &&
            kb[openDoorKey].wasPressedThisFrame)
        {
            currentDoor.ToggleDoor();
        }
    }

    // ====================== DEBUG RAYCAST ======================
    void OnDrawGizmosSelected()
    {
        // Tampilkan garis deteksi pintu di Scene View
        Gizmos.color = Color.yellow;

        Vector3 rayOrigin = transform.position +
                            Vector3.up * rayHeight;

        Gizmos.DrawRay(
            rayOrigin,
            transform.forward * interactionRange
        );
    }

    // Bersihkan status ketika Hero dinonaktifkan
    void OnDisable()
    {
        if (currentDoor != null)
        {
            currentDoor.SetPlayerNearby(false);
            currentDoor = null;
        }
    }
}