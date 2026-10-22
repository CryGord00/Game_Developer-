using UnityEngine;
using UnityEngine.InputSystem;

public class CollectibleItem : MonoBehaviour
{
    [SerializeField] private ItemData item;
    [SerializeField, Min(1)] private int amount = 1;
    [SerializeField, Min(0.1f)] private float interactDistance = 2f;

    private Inventory inventory;
    private Transform hero;
    private bool confirmationOpen;
    private string confirmationMessage;
    private bool declinedUntilOutOfRange;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisibility;
    private float previousTimeScale;

    private void Awake()
    {
        if (item != null && item.itemType == ItemType.Key)
        {
            CreateKeyVisual();
        }
    }

    private void Update()
    {
        if (item == null)
        {
            return;
        }

        if (confirmationOpen)
        {
            HandleConfirmationKeys();
            return;
        }

        ResolveHeroAndInventory();
        if (hero == null || InventoryUI.IsOpen)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, hero.position);
        if (distance > interactDistance)
        {
            declinedUntilOutOfRange = false;
            return;
        }

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            declinedUntilOutOfRange = false;
            OpenConfirmation();
            return;
        }

        if (!declinedUntilOutOfRange)
            OpenConfirmation();
    }

    private void HandleConfirmationKeys()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.yKey.wasPressedThisFrame ||
            Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        {
            ConfirmPickup();
        }
        else if (Keyboard.current.nKey.wasPressedThisFrame ||
                 Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            DeclinePickup();
        }
    }

    private void OpenConfirmation()
    {
        previousCursorLock = Cursor.lockState;
        previousCursorVisibility = Cursor.visible;
        previousTimeScale = Time.timeScale;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
        confirmationOpen = true;
        confirmationMessage = item.itemType == ItemType.Key
            ? "Ambil Kunci?"
            : "Ambil " + item.itemName + "?";
    }

    public void ConfigurePickup(ItemData pickupItem)
    {
        if (pickupItem == null)
        {
            Debug.LogError("CollectibleItem membutuhkan ItemData agar bisa dimasukkan ke inventory.", this);
            return;
        }

        item = pickupItem;
        if (item.itemType == ItemType.Key)
        {
            CreateKeyVisual();
        }
    }

    public void ShowPickupConfirmation()
    {
        if (item == null || confirmationOpen)
        {
            return;
        }

        ResolveHeroAndInventory();
        OpenConfirmation();
    }

    private void ConfirmPickup()
    {
        if (inventory == null)
        {
            confirmationMessage = "Inventory Hero tidak ditemukan. Kunci belum diambil.";
            return;
        }

        if (!inventory.AddItem(item, amount))
        {
            confirmationMessage = "Inventory penuh. Kosongkan slot dulu.";
            return;
        }

        Debug.Log("Item diambil: " + item.itemName, this);
        CloseConfirmation();
        Destroy(gameObject);
    }

    private void DeclinePickup()
    {
        declinedUntilOutOfRange = true;
        CloseConfirmation();
    }

    private void CloseConfirmation()
    {
        confirmationOpen = false;
        Time.timeScale = previousTimeScale;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisibility;
    }

    private void OnGUI()
    {
        if (!confirmationOpen)
        {
            return;
        }

        const float panelWidth = 420f;
        const float panelHeight = 190f;
        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);

        Color oldBackground = GUI.backgroundColor;
        Color oldColor = GUI.color;
        GUI.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 0.98f);
        GUI.Box(panel, GUIContent.none);
        GUI.backgroundColor = oldBackground;
        GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 18f, panel.width - 40f, panel.height - 36f));
        GUIStyle messageStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        messageStyle.normal.textColor = Color.white;
        GUILayout.Label(confirmationMessage, messageStyle, GUILayout.Height(65f));
        GUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.12f, 0.55f, 0.25f, 1f);
        if (GUILayout.Button("YA", GUILayout.Height(48f)))
        {
            ConfirmPickup();
        }

        GUI.backgroundColor = new Color(0.58f, 0.16f, 0.16f, 1f);
        if (GUILayout.Button("TIDAK", GUILayout.Height(48f)))
        {
            DeclinePickup();
        }
        GUI.backgroundColor = oldBackground;
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
        GUI.color = oldColor;
    }

    private void OnDestroy()
    {
        if (confirmationOpen)
        {
            CloseConfirmation();
        }

    }

    private void ResolveHeroAndInventory()
    {
        if (hero == null)
        {
            GameObject heroObject = GameObject.FindGameObjectWithTag("hero");
            if (heroObject == null)
            {
                return;
            }

            hero = heroObject.transform;
        }

        if (inventory == null)
        {
            inventory = hero.GetComponentInParent<Inventory>() ??
                        hero.GetComponentInChildren<Inventory>(true);
        }
    }

    private void CreateKeyVisual()
    {
        EnsureKeyIcon();

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
        {
            return;
        }

        MeshRenderer placeholder = GetComponent<MeshRenderer>();
        if (placeholder != null)
        {
            placeholder.enabled = false;
        }

        Material gold = new Material(shader)
        {
            color = new Color(0.95f, 0.62f, 0.12f)
        };

        CreateKeyPart("Key Shaft", new Vector3(0.1f, 0f, 0f), new Vector3(0.58f, 0.08f, 0.08f), gold);
        CreateKeyPart("Key Head Top", new Vector3(-0.34f, 0.13f, 0f), new Vector3(0.22f, 0.07f, 0.08f), gold);
        CreateKeyPart("Key Head Bottom", new Vector3(-0.34f, -0.13f, 0f), new Vector3(0.22f, 0.07f, 0.08f), gold);
        CreateKeyPart("Key Head End", new Vector3(-0.46f, 0f, 0f), new Vector3(0.07f, 0.24f, 0.08f), gold);
        CreateKeyPart("Key Tooth", new Vector3(0.36f, -0.08f, 0f), new Vector3(0.1f, 0.12f, 0.08f), gold);
    }

    private void EnsureKeyIcon()
    {
        EnsureItemIcon(item);
    }

    // Drawer pickups also use ItemData directly, so prepare the icon even when
    // the world object's CollectibleItem behaviour is disabled by the drawer.
    public static void EnsureItemIcon(ItemData itemData)
    {
        Inventory.EnsureItemIcon(itemData);
    }

    private void CreateKeyPart(string partName, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        part.transform.SetParent(transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = localScale;

        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null)
        {
            Destroy(partCollider);
        }

        MeshRenderer partRenderer = part.GetComponent<MeshRenderer>();
        if (partRenderer != null)
        {
            partRenderer.sharedMaterial = material;
        }
    }
}
