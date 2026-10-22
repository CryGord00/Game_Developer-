using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.InputSystem;    
using UnityEngine.SceneManagement;

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
    private BossPatrolWaypoint[] retryCheckpoints = System.Array.Empty<BossPatrolWaypoint>();
    private BossPatrolWaypoint lastReachedCheckpoint;
    private const float CheckpointReachDistance = 2.5f;

    void Awake()
    {
        CurrentHealth = startAtMax ? maxHealth : Mathf.Clamp(startHealth, 0, maxHealth);
        playerAnimator = GetComponentInChildren<Animator>(true);
        UpdateUI();
    }

    void Start()
    {
        retryCheckpoints = FindObjectsOfType<BossPatrolWaypoint>();

        string retryKey = GetRetryCheckpointKey();
        if (PlayerPrefs.HasKey(retryKey))
        {
            // PlayerPrefs menyimpan nama marker; kosongkan juga salinan runtime
            // agar tidak tertinggal untuk pemuatan scene berikutnya.
            GameFlow.TryConsumeRetryCheckpoint(out Vector3 ignoredCheckpointPosition);
            string checkpointName = PlayerPrefs.GetString(retryKey);
            foreach (BossPatrolWaypoint checkpoint in retryCheckpoints)
            {
                if (checkpoint != null && checkpoint.name == checkpointName && !IsBossRoomCheckpoint(checkpoint))
                {
                    lastReachedCheckpoint = checkpoint;
                    MoveToRetryCheckpoint(checkpoint.transform.position);
                    PlayerPrefs.DeleteKey(retryKey);
                    PlayerPrefs.Save();
                    return;
                }
            }

            // Marker mungkin sudah dihapus/berganti nama; jangan menyimpan
            // checkpoint yang tidak dapat ditemukan untuk Retry berikutnya.
            PlayerPrefs.DeleteKey(retryKey);
            PlayerPrefs.Save();
        }

        if (GameFlow.TryConsumeRetryCheckpoint(out Vector3 checkpointPosition))
        {
            MoveToRetryCheckpoint(checkpointPosition);
        }
    }

    private void UpdateCheckpoint()
    {
        if (IsDead || retryCheckpoints.Length == 0)
        {
            return;
        }

        // Simpan titik terakhir yang benar-benar dikunjungi Hero. Jangan
        // memilih titik terdekat ketika mati, karena itu bisa menjadi titik
        // berikutnya atau titik pertama jika Hero berada di antara ruangan.
        BossPatrolWaypoint nearest = null;
        float nearestDistance = CheckpointReachDistance;
        foreach (BossPatrolWaypoint checkpoint in retryCheckpoints)
        {
            if (checkpoint == null || IsBossRoomCheckpoint(checkpoint))
            {
                continue;
            }

            float distance = HorizontalDistance(transform.position, checkpoint.transform.position);
            if (distance < nearestDistance)
            {
                nearest = checkpoint;
                nearestDistance = distance;
            }
        }

        if (nearest != null)
        {
            lastReachedCheckpoint = nearest;
        }
    }

    private void MoveToRetryCheckpoint(Vector3 checkpointPosition)
    {
        CharacterController characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        transform.position = checkpointPosition;

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        Debug.Log("Retry: Hero kembali ke checkpoint " + checkpointPosition, this);
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
        UpdateCheckpoint();

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

        // Gunakan checkpoint terakhir yang benar-benar disinggahi, bukan
        // marker terdekat dari posisi kematian.
        if (lastReachedCheckpoint == null && retryCheckpoints.Length > 0)
        {
            float nearestDistance = float.MaxValue;
            foreach (BossPatrolWaypoint checkpoint in retryCheckpoints)
            {
                if (checkpoint == null || IsBossRoomCheckpoint(checkpoint))
                {
                    continue;
                }

                float distance = HorizontalDistance(transform.position, checkpoint.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    lastReachedCheckpoint = checkpoint;
                }
            }
        }

        if (lastReachedCheckpoint != null)
        {
            GameFlow.SetRetryCheckpoint(lastReachedCheckpoint.transform.position);
            PlayerPrefs.SetString(GetRetryCheckpointKey(), lastReachedCheckpoint.name);
            PlayerPrefs.Save();
            Debug.Log("Checkpoint Retry disimpan: " + lastReachedCheckpoint.name, this);
        }
        else
        {
            // Jika scene tidak memiliki marker patrol, Retry tetap kembali ke
            // lokasi Hero saat mati.
            GameFlow.SetRetryCheckpoint(transform.position);
        }

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

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        first.y = 0f;
        second.y = 0f;
        return Vector3.Distance(first, second);
    }

    private static bool IsBossRoomCheckpoint(BossPatrolWaypoint checkpoint)
    {
        return checkpoint != null &&
               checkpoint.name.IndexOf("BossRoom", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetRetryCheckpointKey()
    {
        return "PendingRetryCheckpoint_" + SceneManager.GetActiveScene().name;
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
