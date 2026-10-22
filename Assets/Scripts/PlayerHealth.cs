using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.InputSystem;    

// Pasang di hero (object yang sama dengan komponen Inventory).
public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    public bool startAtMax = false;     // false = mulai dari startHealth (biar efek potion terlihat)
    public int startHealth = 50;

    [Header("UI (opsional)")]
    public Slider healthBar;            // UI > Slider
    public Text healthText;         // contoh: "50 / 100"

    [Header("Event")]
    public UnityEvent onDeath;          // hubungkan ke game over, animasi mati, dll

    [Header("Testing")]
    public bool enableTestKeys = true;  // H = heal 10, K = damage 10

    public int CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    private Inventory inventory;
    private Animator playerAnimator;

    void Awake()
    {
        CurrentHealth = startAtMax ? maxHealth : Mathf.Clamp(startHealth, 0, maxHealth);
        playerAnimator = GetComponentInChildren<Animator>(true);
        UpdateUI();
    }

    void OnEnable()
    {
        inventory = GetComponent<Inventory>();
        if (inventory != null)
            inventory.OnItemUsed += HandleItemUsed;
    }

    void OnDisable()
    {
        if (inventory != null)
            inventory.OnItemUsed -= HandleItemUsed;
    }

    void Update()
    {
        if (!enableTestKeys || Keyboard.current == null) return;

        if (Keyboard.current.hKey.wasPressedThisFrame) Heal(10);
        if (Keyboard.current.kKey.wasPressedThisFrame) TakeDamage(10);
    }

    // Dipanggil saat item dipakai dari inventory
    void HandleItemUsed(ItemData item)
    {
        if (item.itemType == ItemType.Consumable)
            Heal(item.effectValue);
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        UpdateUI();
        Debug.Log("HP: " + CurrentHealth + "/" + maxHealth);
    }

    // Panggil dari script musuh/jebakan: playerHealth.TakeDamage(15);
    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        UpdateUI();
        Debug.Log("HP: " + CurrentHealth + "/" + maxHealth);

        if (CurrentHealth == 0)
            Die();
    }

    void Die()
    {
        IsDead = true;
        if (playerAnimator != null)
        {
            playerAnimator.applyRootMotion = false;
            playerAnimator.CrossFade("m_death_A", 0.08f, 0);
        }

        HeroMovement movement = GetComponent<HeroMovement>();
        if (movement != null)
        {
            movement.enabled = false;
        }

        CharacterController characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        Debug.Log("Player mati!");
        onDeath?.Invoke();

        StartCoroutine(ShowGameOverAfterDeathAnimation());
    }

    private System.Collections.IEnumerator ShowGameOverAfterDeathAnimation()
    {
        // Biarkan animasi mati terlihat sebelum layar Game Over membekukan permainan.
        yield return new WaitForSecondsRealtime(1.2f);

        // Hubungkan Game Over secara otomatis agar scene tetap bekerja meski
        // UnityEvent onDeath belum diisi di Inspector.
        GameFlow gameFlow = GameFlow.Instance;
        if (gameFlow == null)
        {
            gameFlow = FindObjectOfType<GameFlow>();
        }

        if (gameFlow != null)
        {
            gameFlow.Lose();
        }
    }

    void UpdateUI()
    {
        if (healthBar != null)
        {
            healthBar.minValue = 0;
            healthBar.maxValue = maxHealth;
            healthBar.value = CurrentHealth;
        }

        if (healthText != null)
            healthText.text = CurrentHealth + " / " + maxHealth;
    }
}
