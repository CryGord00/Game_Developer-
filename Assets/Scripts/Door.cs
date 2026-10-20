using UnityEngine;

public class Door : MonoBehaviour
{
    [Header("Settings")]
    public float openAngle = 90f;     // pakai -90 kalau arah bukaannya terbalik
    public float openSpeed = 120f;    // derajat per detik

    private Quaternion closedRot;
    private Quaternion openRot;
    private bool isOpen;

    void Awake()
    {
        // Nilai di Inspector bisa 0 walau default di kode bukan 0
        if (Mathf.Approximately(openAngle, 0f)) openAngle = 90f;
        if (openSpeed <= 0f) openSpeed = 120f;

        closedRot = transform.localRotation;
        openRot = closedRot * Quaternion.Euler(0f, openAngle, 0f);

        // Animator akan menimpa rotasi dari script
        Animator anim = GetComponentInParent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            Debug.LogWarning("[Door] Ada Animator di '" + anim.name + "', dinonaktifkan supaya tidak menimpa rotasi.", anim);
            anim.enabled = false;
        }
    }

    void Update()
    {
        Quaternion target = isOpen ? openRot : closedRot;

        // unscaledDeltaTime: tetap jalan walau Time.timeScale = 0
        transform.localRotation = Quaternion.RotateTowards(
            transform.localRotation,
            target,
            openSpeed * Time.unscaledDeltaTime
        );
    }

    public void ToggleDoor()
    {
        isOpen = !isOpen;
        Debug.Log("[Door] '" + name + "' sekarang " + (isOpen ? "TERBUKA" : "TERTUTUP"), this);
    }
}