using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

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
            EditorUtility.DisplayDialog(
                "Interaction Prompt",
                "Canvas tidak ditemukan di scene.",
                "OK");

            return;
        }

        Transform oldPrompt = canvas.transform.Find("InteractionPrompt");

        if (oldPrompt != null)
        {
            Undo.DestroyObjectImmediate(oldPrompt.gameObject);
        }

        GameObject root = new GameObject(
            "InteractionPrompt",
            typeof(RectTransform),
            typeof(Image),
            typeof(CanvasGroup));

        Undo.RegisterCreatedObjectUndo(root, "Build Interaction Prompt");

        root.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0f);
        rootRect.anchorMax = new Vector2(0.5f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = new Vector2(0f, 140f);
        rootRect.sizeDelta = new Vector2(380f, 56f);

        Image background = root.GetComponent<Image>();
        background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(
            "UI/Skin/UISprite.psd");
        background.type = Image.Type.Sliced;
        background.color = new Color(0.05f, 0.05f, 0.08f, 0.75f);
        background.raycastTarget = false;

        GameObject textObject = new GameObject(
            "Text",
            typeof(RectTransform),
            typeof(Text));

        Undo.RegisterCreatedObjectUndo(textObject, "Build Interaction Prompt Text");

        textObject.transform.SetParent(root.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 4f);
        textRect.offsetMax = new Vector2(-12f, -4f);

        Text promptText = textObject.GetComponent<Text>();
        promptText.text = "Tekan E";
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.fontStyle = FontStyle.Bold;
        promptText.color = Color.white;
        promptText.fontSize = 22;
        promptText.resizeTextForBestFit = true;
        promptText.resizeTextMinSize = 16;
        promptText.resizeTextMaxSize = 26;
        promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
        promptText.verticalOverflow = VerticalWrapMode.Truncate;
        promptText.raycastTarget = false;

        InteractionPrompt prompt = root.AddComponent<InteractionPrompt>();
        prompt.promptText = promptText;

        CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = root;

        Debug.Log("Interaction Prompt dibuat di bawah Canvas.");
    }
}