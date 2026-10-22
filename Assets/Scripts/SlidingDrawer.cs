using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// Pasang di setiap laci (Drawer B 1, Drawer B 2, ...).
// Hero berdiri di depan lemari dan tekan E: laci yang paling dekat bergeser keluar.
// Teks "Tekan E" muncul otomatis kalau ada InteractionPrompt di scene.
public class SlidingDrawer : MonoBehaviour
{
    [Header("Gerakan laci")]
    public Vector3 openOffset = new Vector3(0f, 0f, 0.4f);   // arah & jarak tarik (koordinat lokal parent)
    public float openSpeed = 1.5f;                           // meter per detik
    public bool canClose = true;                             // tekan E lagi untuk menutup
    [SerializeField] private Transform movingPart;            // Bagian laci/pintu yang bergerak, bukan badan lemari

    [Header("Interaksi")]
    public string heroTag = "hero";
    public float interactDistance = 1.2f;                    // jarak maksimal dari titik di depan hero ke laci
    public float probeHeight = 1.0f;                         // tinggi titik "tangan" hero
    public bool showPrompt = true;                           // tampilkan teks "Tekan E"
    public bool debugLog = false;                            // log diagnosis di Console saat E ditekan

    [Header("Kunci (opsional)")]
    public ItemData requiredKey;                             // kosongkan = laci tidak terkunci
    public bool consumeKey = false;

    [Header("Event")]
    public UnityEvent onOpened;
    public UnityEvent onLocked;

    private static readonly List<SlidingDrawer> allDrawers = new List<SlidingDrawer>();
    private static readonly object PromptOwner = new object();
    private static SlidingDrawer currentBest;
    private static int bestFrame = -1;

    private Vector3 closedPos;
    private Vector3 openPos;
    private bool isOpen;
    private bool movementTargetValid;
    private Transform hero;
    private Inventory inventory;
    private Renderer rend;

    void OnEnable() { allDrawers.Add(this); }

    void OnDisable()
    {
        allDrawers.Remove(this);
        if (allDrawers.Count == 0)
            InteractionPrompt.Hide(PromptOwner);
    }

    void Start()
    {
        ResolveMovingPart();
        if (movingPart == null)
        {
            Debug.LogWarning("[Drawer] " + name + " tidak memiliki bagian laci yang dapat digerakkan. Isi Moving Part agar badan lemari tidak bergeser.", this);
            enabled = false;
            return;
        }

        movementTargetValid = true;
        closedPos = movingPart.localPosition;
        openPos = closedPos + openOffset;
        rend = movingPart.GetComponent<Renderer>() ?? movingPart.GetComponentInChildren<Renderer>();
    }

    void Update()
    {
        // Gerakkan laci pelan-pelan
        if (movementTargetValid && movingPart != null)
        {
            Vector3 target = isOpen ? openPos : closedPos;
            movingPart.localPosition = Vector3.MoveTowards(
                movingPart.localPosition, target, openSpeed * Time.deltaTime);
        }

        if (!FindHero()) return;

        bool inventoryOpen = InventoryUI.IsOpen;
        SlidingDrawer best = inventoryOpen ? null : FindBestDrawer();

        // Satu laci (yang pertama di daftar) mengurus teks dan log, supaya tidak dobel
        bool reporter = allDrawers.Count > 0 && allDrawers[0] == this;
        if (reporter && showPrompt)
            UpdatePrompt(best);

        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;

        if (inventoryOpen)
        {
            if (reporter && debugLog) Debug.Log("[Drawer] E ditekan, tapi inventory sedang terbuka.");
            return;
        }

        if (reporter && debugLog)
            LogDiagnosis(best);

        if (best == this)
            Interact();
    }

    bool FindHero()
    {
        if (hero == null)
        {
            GameObject go = GameObject.FindWithTag(heroTag);
            if (go == null) return false;

            hero = go.transform;
            inventory = go.GetComponentInChildren<Inventory>();
        }
        return true;
    }

    private void ResolveMovingPart()
    {
        if (movingPart != null)
        {
            return;
        }

        // Existing scene components live on Drawer B 1/2/3. One legacy
        // component lives on the cabinet root, so resolve a drawer leaf instead
        // of ever translating the whole cabinet.
        if (transform.name.IndexOf("Drawer", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            movingPart = transform;
            return;
        }

        Vector3 probe = hero != null ? ProbePoint() : transform.position;
        Transform closestDrawer = null;
        float closestDistance = float.MaxValue;
        Transform[] parts = GetComponentsInChildren<Transform>(true);
        foreach (Transform part in parts)
        {
            if (part == transform || part.name.IndexOf("Drawer", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            Renderer partRenderer = part.GetComponent<Renderer>() ?? part.GetComponentInChildren<Renderer>();
            if (partRenderer == null)
            {
                continue;
            }

            float distance = Vector3.Distance(partRenderer.bounds.ClosestPoint(probe), probe);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestDrawer = part;
            }
        }

        movingPart = closestDrawer;
    }

    // Titik sedikit di depan hero, setinggi tangan
    Vector3 ProbePoint()
    {
        return hero.position + hero.forward * 0.5f + Vector3.up * probeHeight;
    }

    float DistanceTo(Vector3 point)
    {
        if (movingPart == null)
            return float.MaxValue;

        if (rend == null) return Vector3.Distance(movingPart.position, point);
        return Vector3.Distance(rend.bounds.ClosestPoint(point), point);
    }

    // Dihitung sekali per frame untuk semua laci
    SlidingDrawer FindBestDrawer()
    {
        if (bestFrame == Time.frameCount) return currentBest;

        bestFrame = Time.frameCount;
        currentBest = null;

        Vector3 probe = ProbePoint();
        float bestDist = float.MaxValue;
        foreach (SlidingDrawer d in allDrawers)
        {
            float dist = d.DistanceTo(probe);
            if (dist <= d.interactDistance && dist < bestDist)
            {
                currentBest = d;
                bestDist = dist;
            }
        }
        return currentBest;
    }

    string GetPromptMessage()
    {
        if (isOpen)
            return canClose ? "Tekan E - Tutup laci" : null;

        if (requiredKey != null && (inventory == null || !inventory.HasItem(requiredKey)))
            return "Terkunci - butuh " + requiredKey.itemName;

        return "Tekan E - Buka laci";
    }

    static void UpdatePrompt(SlidingDrawer best)
    {
        string message = best != null ? best.GetPromptMessage() : null;

        if (message != null)
            InteractionPrompt.Show(PromptOwner, message);
        else
            InteractionPrompt.Hide(PromptOwner);
    }

    void LogDiagnosis(SlidingDrawer best)
    {
        if (best != null)
        {
            Debug.Log("[Drawer] Laci terpilih: " + best.name);
            return;
        }

        Vector3 probe = ProbePoint();
        SlidingDrawer nearest = null;
        float nearestDist = float.MaxValue;
        foreach (SlidingDrawer d in allDrawers)
        {
            float dist = d.DistanceTo(probe);
            if (dist < nearestDist) { nearest = d; nearestDist = dist; }
        }

        if (nearest == null)
        {
            Debug.Log("[Drawer] Tidak ada bagian laci yang siap diinteraksikan.");
            return;
        }

        Debug.Log("[Drawer] Tidak ada laci dalam jangkauan. Terdekat: " + nearest.name +
                  ", jarak " + nearestDist.ToString("F2") + " (batas " + nearest.interactDistance + ").");
    }

    // Garis kuning di Scene view = arah & jarak laci akan bergeser (muncul saat laci dipilih)
    void OnDrawGizmosSelected()
    {
        Transform target = movingPart != null ? movingPart : transform;
        Transform p = target.parent;
        Vector3 worldOffset = p != null ? p.TransformVector(openOffset) : openOffset;
        Vector3 start = target.position;
        Vector3 end = start + worldOffset;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(end, 0.05f);
    }

    void Interact()
    {
        if (isOpen)
        {
            if (canClose) isOpen = false;
            return;
        }

        if (requiredKey != null)
        {
            if (inventory == null || !inventory.HasItem(requiredKey))
            {
                Debug.Log("Laci terkunci. Butuh: " + requiredKey.itemName);
                onLocked?.Invoke();
                return;
            }

            if (consumeKey)
                inventory.RemoveItem(requiredKey);
        }

        isOpen = true;
        onOpened?.Invoke();
    }
}
