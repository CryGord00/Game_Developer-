using UnityEngine;
using UnityEngine.SceneManagement;

public class GameFlow : MonoBehaviour
{
    public static GameFlow Instance;

    [Header("Panel UI")]
    public GameObject winPanel;
    public GameObject losePanel;

    [Header("Nama Scene")]
    [Tooltip("Harus persis sama dengan nama scene Main Menu di Build Settings")]
    public string mainMenuScene = "MainMenu";

    private bool gameEnded = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Pastikan game berjalan normal setiap scene dimulai (penting setelah Restart)
        Time.timeScale = 1f;
        gameEnded = false;

        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
    }

    // Panggil dari script lain saat pemain menang
    public void Win()
    {
        EndGame(winPanel);
    }

    // Panggil dari script lain saat pemain kalah
    public void Lose()
    {
        EndGame(losePanel);
    }

    void EndGame(GameObject panel)
    {
        if (gameEnded)
            return;

        gameEnded = true;

        if (panel != null)
            panel.SetActive(true);

        // Bekukan game dan tampilkan kursor supaya tombol bisa diklik
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Hubungkan ke tombol Restart
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Hubungkan ke tombol Main Menu
    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(mainMenuScene);
    }
}