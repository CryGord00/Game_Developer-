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

    [Header("Item di dalam laci")]
    [SerializeField] private GameObject itemInsideDrawer;
    [SerializeField] private ItemData itemToCollect;
    [SerializeField] private Vector3 itemLocalPosition = new Vector3(0f, 0.08f, 0f);

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
    private bool pickupConfirmationOpen;
    private string pickupMessage;
    private float previousTimeScale;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisibility;

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

        if (itemInsideDrawer != null)
        {
            itemInsideDrawer.transform.SetParent(movingPart, false);
            itemInsideDrawer.transform.localPosition = itemLocalPosition;
            itemInsideDrawer.transform.localRotation = Quaternion.identity;
            itemInsideDrawer.transform.localScale = Vector3.one * 0.15f;
            itemInsideDrawer.SetActive(false);
        }
    }

    void Update()
    {
        // Gerakkan laci pelan-pelan
        if (movementTargetValid && movingPart != null)
        {
            Vector3 target = isOpen ? openPos : closedPos;
            movingPart.localPosition = Vector3.MoveTowards(
                movingPart.localPosition, target, openSpeed * Time.unscaledDeltaTime);
        }

        if (pickupConfirmationOpen)
        {
            HandlePickupConfirmationInput();
            return;
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
            inventory = go.GetComponentInParent<Inventory>() ??
                        go.GetComponentInChildren<Inventory>(true);
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
        {
            if (itemInsideDrawer != null && itemInsideDrawer.activeSelf)
                return "Tekan E - Ambil kunci dahulu";

            return canClose ? "Tekan E - Tutup laci" : null;
        }

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
            if (itemInsideDrawer != null && itemInsideDrawer.activeSelf && itemToCollect != null)
            {
                OpenPickupConfirmation();
                return;
            }

            // Biarkan laci terbuka sampai item di dalamnya sudah diambil.
            if (canClose && (itemInsideDrawer == null || !itemInsideDrawer.activeSelf))
                isOpen = false;
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

        KeepHeroClearOfDrawer();
        isOpen = true;
        if (itemInsideDrawer != null)
        {
            // OldKey has no serialized icon. Generate one before the inventory
            // refreshes so the new slot is visible immediately.
            Inventory.EnsureItemIcon(itemToCollect);

            itemInsideDrawer.SetActive(true);
            foreach (Behaviour behaviour in itemInsideDrawer.GetComponents<Behaviour>())
            {
                if (behaviour != null && behaviour.GetType().Name == "CollectibleItem")
                {
                    behaviour.enabled = false;
                }
            }

            OpenPickupConfirmation();
        }
        onOpened?.Invoke();
    }

    private void KeepHeroClearOfDrawer()
    {
        if (hero == null || movingPart == null || rend == null)
        {
            return;
        }

        CharacterController heroController = hero.GetComponent<CharacterController>();
        if (heroController == null || !rend.bounds.Intersects(heroController.bounds))
        {
            return;
        }

        // The moving mesh collider can push a CharacterController as it opens.
        // Move the Hero out of the drawer's current volume before starting motion.
        Vector3 away = hero.position - rend.bounds.center;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f)
        {
            away = hero.forward;
            away.y = 0f;
        }

        if (away.sqrMagnitude < 0.0001f)
        {
            away = -movingPart.forward;
            away.y = 0f;
        }

        away.Normalize();
        float clearance = heroController.radius + openOffset.magnitude + 0.15f;
        heroController.Move(away * clearance);
    }

    private void OpenPickupConfirmation()
    {
        if (itemToCollect == null || pickupConfirmationOpen)
        {
            return;
        }

        previousCursorLock = Cursor.lockState;
        previousCursorVisibility = Cursor.visible;
        previousTimeScale = Time.timeScale;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
        pickupConfirmationOpen = true;
        pickupMessage = "Ambil Kunci?";
    }

    private void HandlePickupConfirmationInput()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.yKey.wasPressedThisFrame ||
            Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        {
            ConfirmDrawerPickup();
        }
        else if (Keyboard.current.nKey.wasPressedThisFrame ||
                 Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            DeclineDrawerPickup();
        }
    }

    private void ConfirmDrawerPickup()
    {
        if (itemToCollect == null)
        {
            pickupMessage = "Data kunci belum terhubung.";
            return;
        }

        if (inventory == null && hero != null)
        {
            inventory = hero.GetComponentInParent<Inventory>() ??
                        hero.GetComponentInChildren<Inventory>(true);
        }

        if (inventory == null)
        {
            pickupMessage = "Inventory Hero tidak ditemukan.";
            return;
        }

        // Recover cleanly if an earlier UI callback added the key before a UI
        // error interrupted the rest of the pickup flow.
        if (inventory.HasItem(itemToCollect))
        {
            FinishDrawerPickup();
            return;
        }

        Inventory.EnsureItemIcon(itemToCollect);
        if (!inventory.AddItem(itemToCollect, 1))
        {
            pickupMessage = "Inventory penuh. Kosongkan slot dulu.";
            return;
        }

        if (!inventory.HasItem(itemToCollect))
        {
            pickupMessage = "Kunci belum masuk ke inventory. Coba lagi.";
            Debug.LogError("[Drawer] AddItem selesai tetapi kunci tidak ditemukan di inventory.", this);
            return;
        }

        Debug.Log("Item diambil: " + itemToCollect.itemName, this);
        FinishDrawerPickup();
    }

    private void FinishDrawerPickup()
    {
        if (itemInsideDrawer != null)
        {
            Destroy(itemInsideDrawer);
            itemInsideDrawer = null;
        }

        ClosePickupConfirmation();
    }

    private void ClosePickupConfirmation()
    {
        pickupConfirmationOpen = false;
        Time.timeScale = previousTimeScale;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisibility;
    }

    private void DeclineDrawerPickup()
    {
        // Menolak hanya menutup dialog. Item tetap di laci dan tidak masuk inventory.
        ClosePickupConfirmation();
    }

    private void OnGUI()
    {
        if (!pickupConfirmationOpen)
        {
            return;
        }

        const float panelWidth = 460f;
        const float panelHeight = 210f;
        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);

        Color oldBackground = GUI.backgroundColor;
        Color oldColor = GUI.color;
        GUI.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 0.98f);
        GUI.Box(panel, GUIContent.none);
        GUILayout.BeginArea(new Rect(panel.x + 24f, panel.y + 20f, panel.width - 48f, panel.height - 40f));

        GUIStyle messageStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        messageStyle.normal.textColor = Color.white;
        GUILayout.Label(pickupMessage, messageStyle, GUILayout.Height(80f));

        GUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.12f, 0.55f, 0.25f, 1f);
        if (GUILayout.Button("YA", GUILayout.Height(54f)))
        {
            ConfirmDrawerPickup();
        }

        GUI.backgroundColor = new Color(0.58f, 0.16f, 0.16f, 1f);
        if (GUILayout.Button("TIDAK", GUILayout.Height(54f)))
        {
            DeclineDrawerPickup();
        }
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
        GUI.backgroundColor = oldBackground;
        GUI.color = oldColor;
    }

    private void OnDestroy()
    {
        if (pickupConfirmationOpen)
        {
            ClosePickupConfirmation();
        }
    }
}
