using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class TooltipUI : MonoBehaviour
{
    public static TooltipUI Instance { get; private set; }

    public Text nameText;
    public Text descriptionText;
    public Vector2 offset = new Vector2(16f, -16f);

    private CanvasGroup group;
    private RectTransform rect;

    void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();
        rect = transform as RectTransform;
        if (rect != null)
            rect.pivot = new Vector2(0f, 1f);   // pojok kiri atas mengikuti kursor

        if (group != null)
        {
            group.blocksRaycasts = false;       // tooltip tidak boleh menghalangi mouse
            group.interactable = false;
            group.alpha = 0f;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Show(ItemData item)
    {
        if (Instance == null || item == null) return;

        if (Instance.nameText != null)
            Instance.nameText.text = item.itemName;

        if (Instance.descriptionText != null)
            Instance.descriptionText.text = item.description;

        if (Instance.group != null)
            Instance.group.alpha = 1f;

        Instance.transform.SetAsLastSibling();
        Instance.UpdatePosition();
    }

    public static void Hide()
    {
        if (Instance != null && Instance.group != null)
            Instance.group.alpha = 0f;
    }

    void LateUpdate()
    {
        if (group != null && group.alpha > 0f)
            UpdatePosition();
    }

    void UpdatePosition()
    {
        if (Mouse.current == null || rect == null) return;

        Vector2 mouse = Mouse.current.position.ReadValue();
        Vector2 size = new Vector2(rect.rect.width * rect.lossyScale.x,
                                   rect.rect.height * rect.lossyScale.y);

        Vector2 pos = mouse + offset;
        pos.x = Mathf.Min(pos.x, Screen.width - size.x);   // jangan keluar kanan
        pos.y = Mathf.Max(pos.y, size.y);                  // jangan keluar bawah
        rect.position = pos;
    }
}
