using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Taruh file ini di folder Assets/Editor (nama folder harus persis "Editor").
// Menu: Tools > Inventory > Build Inventory UI
public static class InventoryUIBuilder
{
    // ===== Warna tema: ubah di sini kalau mau ganti gaya =====
    static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.12f, 0.95f);
    static readonly Color Gold = new Color(0.96f, 0.77f, 0.26f, 1f);
    static readonly Color SlotNormal = new Color(0.15f, 0.15f, 0.22f, 1f);
    static readonly Color SlotHover = new Color(0.26f, 0.26f, 0.38f, 1f);
    static readonly Color SlotPressed = new Color(0.10f, 0.10f, 0.16f, 1f);
    static readonly Color SlotBorder = new Color(0.35f, 0.35f, 0.50f, 1f);
    static readonly Color TooltipColor = new Color(0.06f, 0.06f, 0.10f, 0.96f);

    const string PrefabFolder = "Assets/Prefabs";
    const string SlotPrefabPath = "Assets/Prefabs/Slot.prefab";

    static Sprite roundSprite;

    [MenuItem("Tools/Inventory/Build Inventory UI")]
    public static void Build()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Inventory UI", "Canvas tidak ditemukan di scene. Buat dulu lewat GameObject > UI > Canvas.", "OK");
            return;
        }

        roundSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // Skala UI mengikuti ukuran layar
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Hapus InventoryPanel dan Tooltip lama (kalau ada) supaya tidak dobel
        RemoveChild(canvas.transform, "InventoryPanel");
        RemoveChild(canvas.transform, "Tooltip");

        // ---------- Panel ----------
        RectTransform panel = NewUI("InventoryPanel", canvas.transform, typeof(Image), typeof(Outline));
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(640, 560);
        panel.anchoredPosition = Vector2.zero;
        StyleImage(panel.GetComponent<Image>(), PanelColor);
        Outline panelOutline = panel.GetComponent<Outline>();
        panelOutline.effectColor = Gold;
        panelOutline.effectDistance = new Vector2(2, -2);
        Undo.RegisterCreatedObjectUndo(panel.gameObject, "Build Inventory UI");

        // Judul
        RectTransform title = NewUI("Title", panel, typeof(TextMeshProUGUI));
        TopStretch(title, -16, 50, 20);
        TextMeshProUGUI titleText = title.GetComponent<TextMeshProUGUI>();
        titleText.text = "INVENTORY";
        titleText.fontSize = 34;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Gold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.enableWordWrapping = false;
        titleText.raycastTarget = false;

        // Garis pemisah
        RectTransform divider = NewUI("Divider", panel, typeof(Image));
        TopStretch(divider, -72, 2, 30);
        Image dividerImg = divider.GetComponent<Image>();
        dividerImg.color = new Color(Gold.r, Gold.g, Gold.b, 0.5f);
        dividerImg.raycastTarget = false;

        // Wadah slot (grid 5 kolom)
        RectTransform container = NewUI("SlotContainer", panel, typeof(GridLayoutGroup));
        Stretch(container, new Vector2(30, 30), new Vector2(-30, -90));
        GridLayoutGroup grid = container.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(100, 100);
        grid.spacing = new Vector2(10, 10);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;

        // ---------- Prefab Slot ----------
        SlotUI slotPrefab = BuildSlotPrefab();

        // ---------- Tooltip ----------
        TooltipUI tooltip = BuildTooltip(canvas.transform);

        // ---------- Pasang & isi InventoryUI ----------
        InventoryUI ui = canvas.GetComponent<InventoryUI>();
        if (ui == null) ui = canvas.gameObject.AddComponent<InventoryUI>();

        Inventory inv = null;
        GameObject hero = null;
        try { hero = GameObject.FindWithTag("hero"); } catch { }
        if (hero != null)
        {
            inv = hero.GetComponentInChildren<Inventory>();
            if (inv == null)
            {
                inv = Undo.AddComponent<Inventory>(hero);
                Debug.Log("Komponen Inventory ditambahkan ke " + hero.name);
            }
        }
        if (inv == null) inv = Object.FindObjectOfType<Inventory>();

        ui.inventory = inv;
        ui.panel = panel.gameObject;
        ui.slotParent = container;
        ui.slotPrefab = slotPrefab;

        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = panel.gameObject;

        if (inv == null)
            Debug.LogWarning("Inventory tidak ditemukan. Pasang komponen Inventory di hero lalu seret hero ke field Inventory di InventoryUI (Canvas).");
        else
            Debug.Log("Inventory UI selesai dibuat. Inventory dipakai dari: " + inv.gameObject.name);
    }

    // ================= Slot =================
    static SlotUI BuildSlotPrefab()
    {
        RectTransform slot = NewUI("Slot", null, typeof(Image), typeof(Outline), typeof(Button));
        slot.sizeDelta = new Vector2(100, 100);

        Image bg = slot.GetComponent<Image>();
        StyleImage(bg, Color.white);   // warna diatur oleh Button (tint) di bawah

        Outline outline = slot.GetComponent<Outline>();
        outline.effectColor = SlotBorder;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = slot.GetComponent<Button>();
        btn.targetGraphic = bg;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        cb.normalColor = SlotNormal;
        cb.highlightedColor = SlotHover;
        cb.pressedColor = SlotPressed;
        cb.selectedColor = SlotNormal;
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;

        // Icon
        RectTransform icon = NewUI("Icon", slot, typeof(Image));
        Stretch(icon, new Vector2(12, 12), new Vector2(-12, -12));
        Image iconImg = icon.GetComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        iconImg.enabled = false;   // dinyalakan oleh SlotUI saat ada item

        // Jumlah
        RectTransform amount = NewUI("Amount", slot, typeof(TextMeshProUGUI));
        amount.anchorMin = amount.anchorMax = amount.pivot = new Vector2(1, 0);
        amount.sizeDelta = new Vector2(56, 28);
        amount.anchoredPosition = new Vector2(-8, 6);
        TextMeshProUGUI amountText = amount.GetComponent<TextMeshProUGUI>();
        amountText.text = "";
        amountText.fontSize = 22;
        amountText.fontStyle = FontStyles.Bold;
        amountText.color = Color.white;
        amountText.alignment = TextAlignmentOptions.BottomRight;
        amountText.enableWordWrapping = false;
        amountText.raycastTarget = false;

        SlotUI slotUI = slot.gameObject.AddComponent<SlotUI>();
        slotUI.iconImage = iconImg;
        slotUI.amountText = amountText;

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(slot.gameObject, SlotPrefabPath);
        Object.DestroyImmediate(slot.gameObject);
        return prefab.GetComponent<SlotUI>();
    }

    // ================= Tooltip =================
    static TooltipUI BuildTooltip(Transform canvas)
    {
        RectTransform tip = NewUI("Tooltip", canvas, typeof(Image), typeof(Outline),
                                  typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        tip.anchorMin = tip.anchorMax = Vector2.zero;
        tip.pivot = new Vector2(0, 1);
        tip.sizeDelta = new Vector2(260, 100);

        Image bg = tip.GetComponent<Image>();
        StyleImage(bg, TooltipColor);
        bg.raycastTarget = false;

        Outline outline = tip.GetComponent<Outline>();
        outline.effectColor = Gold;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        VerticalLayoutGroup v = tip.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(14, 14, 10, 10);
        v.spacing = 4;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        ContentSizeFitter fitter = tip.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform nameRt = NewUI("NameText", tip, typeof(TextMeshProUGUI));
        TextMeshProUGUI nameText = nameRt.GetComponent<TextMeshProUGUI>();
        nameText.text = "Item";
        nameText.fontSize = 24;
        nameText.fontStyle = FontStyles.Bold;
        nameText.color = Gold;
        nameText.enableWordWrapping = true;
        nameText.raycastTarget = false;

        RectTransform descRt = NewUI("DescText", tip, typeof(TextMeshProUGUI));
        TextMeshProUGUI descText = descRt.GetComponent<TextMeshProUGUI>();
        descText.text = "Deskripsi";
        descText.fontSize = 18;
        descText.color = new Color(0.82f, 0.82f, 0.90f, 1f);
        descText.enableWordWrapping = true;
        descText.raycastTarget = false;

        TooltipUI tooltip = tip.gameObject.AddComponent<TooltipUI>();   // CanvasGroup ikut ditambahkan otomatis
        tooltip.nameText = nameText;
        tooltip.descriptionText = descText;

        CanvasGroup group = tip.GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 0f;   // disembunyikan di editor

        Undo.RegisterCreatedObjectUndo(tip.gameObject, "Build Inventory UI");
        return tooltip;
    }

    // ================= Helper =================
    static RectTransform NewUI(string name, Transform parent, params System.Type[] components)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        foreach (System.Type t in components)
            go.AddComponent(t);
        return go.GetComponent<RectTransform>();
    }

    static void StyleImage(Image img, Color color)
    {
        img.sprite = roundSprite;
        img.type = Image.Type.Sliced;
        img.color = color;
    }

    static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    static void TopStretch(RectTransform rt, float y, float height, float sideMargin)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(-2f * sideMargin, height);
        rt.anchoredPosition = new Vector2(0, y);
    }

    static void RemoveChild(Transform parent, string childName)
    {
        Transform t = parent.Find(childName);
        if (t != null)
            Undo.DestroyObjectImmediate(t.gameObject);
    }
}