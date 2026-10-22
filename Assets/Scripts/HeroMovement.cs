using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class HeroMovement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5f;
    public float turnSpeed = 720f;
    public float gravity = -15f;

    [Tooltip("Centang kalau arah gerak harus relatif ke kamera")]
    public bool cameraRelative = false;

    private CharacterController controller;
    private Animator animator;
    private bool hasWalkParam = false;

    private Vector3 verticalVelocity;
    private Vector3 lookDirection;
    private bool hasLookDirection = false;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        if (animator != null)
        {
            animator.applyRootMotion = false;

            // Cek apakah parameter "isWalking" ada, supaya tidak muncul warning
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.name == "isWalking")
                {
                    hasWalkParam = true;
                    break;
                }
            }
        }
    }

    void Update()
    {
        // Read movement directly from the new Input System used by this project.
        // No movement is applied unless a movement key is held.
        Keyboard keyboard = Keyboard.current;
        float horizontal = keyboard == null ? 0f :
            (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) -
            (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
        float vertical = keyboard == null ? 0f :
            (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f) -
            (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);

        Vector3 input = new Vector3(horizontal, 0f, vertical).normalized;

        if (cameraRelative && Camera.main != null)
        {
            Vector3 camForward = Camera.main.transform.forward;
            Vector3 camRight = Camera.main.transform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            input = (camForward * vertical + camRight * horizontal).normalized;
        }

        bool isMoving = input.sqrMagnitude >= 0.01f;

        // Simpan arah hadap supaya hero berputar ke arah jalan
        if (isMoving)
        {
            lookDirection = input;
            hasLookDirection = true;
        }

        if (animator != null && hasWalkParam)
        {
            animator.SetBool("isWalking", isMoving);
        }

        // Gravitasi
        if (controller.isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = -2f;
        }
        verticalVelocity.y += gravity * Time.deltaTime;

        Vector3 move = input * walkSpeed + verticalVelocity;
        controller.Move(move * Time.deltaTime);

        // Putar badan hero ke arah gerak
        if (hasLookDirection)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime
            );
        }
    }
}
