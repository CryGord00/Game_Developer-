using UnityEngine;

public class Door : MonoBehaviour
{
    [Header("Settings")]
    public float openAngle = 90f;
    public float openSpeed = 3f;

    private Quaternion closedRotation;
    private Quaternion openRotation;

    private bool isOpen = false;
    private bool isMoving = false;
    private bool isPlayerNearby = false;

    void Start()
    {
        closedRotation = transform.rotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
    }

    void Update()
    {
        if (isMoving)
        {
            Quaternion target = isOpen ? openRotation : closedRotation;

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                target,
                Time.deltaTime * openSpeed
            );

            if (Quaternion.Angle(transform.rotation, target) < 0.5f)
            {
                transform.rotation = target;
                isMoving = false;
            }
        }
    }

    public void SetPlayerNearby(bool nearby)
    {
        isPlayerNearby = nearby;
    }

    public void ToggleDoor()
    {
        // Pintu hanya bisa digunakan jika Hero berada di dekatnya
        if (!isPlayerNearby) return;

        // Jangan mengubah pintu ketika masih bergerak
        if (isMoving) return;

        isOpen = !isOpen;
        isMoving = true;
    }
}