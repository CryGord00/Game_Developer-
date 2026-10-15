using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class SlotUI : MonoBehaviour,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Image iconImage;
    public TMP_Text amountText;

    private Inventory inventory;
    private int index;
    private Transform panelRoot;
    private GameObject ghost;
    private bool hovered;

    public void Setup(Inventory inv, int slotIndex, Transform panel)
    {
        inventory = inv;
        index = slotIndex;
        panelRoot = panel;
    }

    public void Refresh(InventorySlot slot)
    {
        if (slot.IsEmpty)
        {
            iconImage.enabled = false;
            amountText.text = "";
        }
        else
        {
            iconImage.enabled = true;
            iconImage.sprite = slot.item.icon;
            amountText.text = slot.amount > 1 ? slot.amount.ToString() : "";
        }

        // Update tooltip kalau kursor sedang di atas slot ini
        if (hovered)
        {
            if (slot.IsEmpty) TooltipUI.Hide();
            else TooltipUI.Show(slot.item);
        }
    }

    // Klik kiri = pakai item, klik kanan = hapus 1 item
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            inventory.UseItem(index);
        else if (eventData.button == PointerEventData.InputButton.Right)
            inventory.RemoveItem(index, 1);
    }

    // ---------- Tooltip ----------
    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        var slot = inventory.slots[index];
        if (!slot.IsEmpty) TooltipUI.Show(slot.item);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        TooltipUI.Hide();
    }

    // ---------- Drag and drop ----------
    public void OnBeginDrag(PointerEventData eventData)
    {
        var slot = inventory.slots[index];
        if (slot.IsEmpty) return;

        TooltipUI.Hide();

        Canvas canvas = GetComponentInParent<Canvas>().rootCanvas;
        ghost = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
        ghost.transform.SetParent(canvas.transform, false);
        ghost.transform.SetAsLastSibling();

        var img = ghost.GetComponent<Image>();
        img.sprite = slot.item.icon;
        img.preserveAspect = true;
        img.raycastTarget = false;   // supaya tidak menghalangi deteksi drop

        ghost.GetComponent<RectTransform>().sizeDelta = iconImage.rectTransform.rect.size;
        ghost.transform.position = eventData.position;

        SetIconAlpha(0.4f);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ghost != null)
            ghost.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (ghost == null) return;

        Destroy(ghost);
        ghost = null;
        SetIconAlpha(1f);

        // Dilepas di luar panel inventory = hapus seluruh isi slot
        bool overPanel = eventData.pointerEnter != null &&
                         eventData.pointerEnter.transform.IsChildOf(panelRoot);
        if (!overPanel)
            inventory.DeleteSlot(index);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        SlotUI source = eventData.pointerDrag.GetComponent<SlotUI>();
        if (source != null && source != this)
            inventory.SwapSlots(source.index, index);
    }

    private void SetIconAlpha(float a)
    {
        Color c = iconImage.color;
        c.a = a;
        iconImage.color = c;
    }

    private void OnDisable()
    {
        hovered = false;
        if (ghost != null) { Destroy(ghost); ghost = null; }
        SetIconAlpha(1f);
    }
}