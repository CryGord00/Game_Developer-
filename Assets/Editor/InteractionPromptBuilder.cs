using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Taruh di folder Assets/Editor.
// Menu: Tools > Inventory > Build Interaction Prompt
public static class InteractionPromptBuilder
{
    [MenuItem("Tools/Inventory/Build Interaction Prompt")]
    public static void Build()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Interaction Prompt",
                "Canvas tidak ditemukan di scene.", "OK");
            return;
        }

        Transform old = canvas.transform.Find("InteractionPrompt");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // Kotak latar (bawah-tengah layar)
        GameObject root = new GameObject("InteractionPrompt", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas.transform, false);

        RectTransform rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 140f);
        rt.sizeDelta = new Vector2(380f, 56f);

        Image bg = root.GetComponent<Image>();
        bg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.05f, 0.05f, 0.08f, 0.75f);
        bg.raycastTarget = false;

        // Teks
        GameObject textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(root.transform, false);

        RectTransform trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(12f, 4f);
        trt.offsetMax = new Vector2(-12f, -4f);

        TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.text = "Tekan E";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 16;
        tmp.fontSizeMax = 26;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;

        // Script (CanvasGroup ikut ditambahkan otomatis)
        InteractionPrompt prompt = root.AddComponent<InteractionPrompt>();
        prompt.promptText = tmp;
        root.GetComponent<CanvasGroup>().alpha = 0f;   // disembunyikan di editor

        Undo.RegisterCreatedObjectUndo(root, "Build Interaction Prompt");
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = root;
        Debug.Log("Interaction Prompt dibuat di bawah Canvas.");
    }
}