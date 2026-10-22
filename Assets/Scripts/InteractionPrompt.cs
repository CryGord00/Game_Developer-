using UnityEngine;
using UnityEngine.UI;

// Teks "Tekan E" yang dipakai bersama oleh laci, pintu, dan benda interaktif lain.
// Pasang di object UI yang SELALU AKTIF (dibuat otomatis lewat Tools > Inventory > Build Interaction Prompt).
// Pemakaian dari script lain:
//     InteractionPrompt.Show(this, "Tekan E - Buka pintu");
//     InteractionPrompt.Hide(this);
// "owner" (biasanya "this") dipakai supaya satu script tidak menyembunyikan teks milik script lain.
[RequireComponent(typeof(CanvasGroup))]
public class InteractionPrompt : MonoBehaviour
{
    public static InteractionPrompt Instance { get; private set; }

    public Text promptText;

    private CanvasGroup group;
    private object currentOwner;

    void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Show(object owner, string message)
    {
        if (Instance == null) return;

        Instance.currentOwner = owner;
        if (Instance.promptText != null)
            Instance.promptText.text = message;
        Instance.group.alpha = 1f;
    }

    public static void Hide(object owner)
    {
        if (Instance == null || Instance.currentOwner != owner) return;

        Instance.currentOwner = null;
        Instance.group.alpha = 0f;
    }
}