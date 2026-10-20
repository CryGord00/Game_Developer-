using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryUI : MonoBehaviour
{
    // Script lain (kamera, gerakan) bisa cek ini: if (InventoryUI.IsOpen) return;
    public static bool IsOpen { get; private set; }

    public Inventory inventory;
    public GameObject panel;        // InventoryPanel
    public Transform slotParent;    // SlotContainer
    public SlotUI slotPrefab;
    public float animDuration = 0.2f;

    private List<SlotUI> slotUIs = new List<SlotUI>();
    private CanvasGroup canvasGroup;
    private Coroutine anim;
    private bool isOpen;

    private CursorLockMode prevLockState;
    private bool prevCursorVisible;

    void Start()
    {
        canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = panel.AddComponent<CanvasGroup>();

        for (int i = 0; i < inventory.slots.Count; i++)
        {
            SlotUI s = Instantiate(slotPrefab, slotParent);
            s.Setup(inventory, i, panel.transform);
            slotUIs.Add(s);
        }

        inventory.OnInventoryChanged += Refresh;
        Refresh();

        canvasGroup.alpha = 0f;
        panel.transform.localScale = Vector3.one * 0.9f;
        panel.SetActive(false);
    }

    void OnDestroy()
    {
        IsOpen = false;
        if (inventory != null)
            inventory.OnInventoryChanged -= Refresh;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
            SetOpen(!isOpen);
    }

    // LateUpdate berjalan setelah script kamera/player, jadi kursor
    // tetap tampil walau script lain mencoba mengunci kursor.
    void LateUpdate()
    {
        if (isOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void SetOpen(bool open)
    {
        isOpen = open;
        IsOpen = open;

        if (open)
        {
            prevLockState = Cursor.lockState;
            prevCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = prevLockState;
            Cursor.visible = prevCursorVisible;
            TooltipUI.Hide();
        }

        if (anim != null) StopCoroutine(anim);
        if (open) panel.SetActive(true);

        canvasGroup.blocksRaycasts = open;
        anim = StartCoroutine(Animate(open));
    }

    IEnumerator Animate(bool open)
    {
        float startAlpha = canvasGroup.alpha;
        float endAlpha = open ? 1f : 0f;
        Vector3 startScale = panel.transform.localScale;
        Vector3 endScale = open ? Vector3.one : Vector3.one * 0.9f;

        float t = 0f;
        while (t < animDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / animDuration));
            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, k);
            panel.transform.localScale = Vector3.Lerp(startScale, endScale, k);
            yield return null;
        }

        canvasGroup.alpha = endAlpha;
        panel.transform.localScale = endScale;

        if (!open)
            panel.SetActive(false);
    }

    void Refresh()
    {
        for (int i = 0; i < slotUIs.Count; i++)
            slotUIs[i].Refresh(inventory.slots[i]);
    }
}