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
    private bool showingFallbackLoseScreen;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Pastikan game berjalan normal setiap scene dimulai (penting setelah Restart)
        Time.timeScale = 1f;
        gameEnded = false;
        showingFallbackLoseScreen = false;

        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
    }

    // Panggil dari script lain saat pemain menang
    public void Win()
    {
        EndGame(winPanel, false);
    }

    // Panggil dari script lain saat pemain kalah
    public void Lose()
    {
        EndGame(losePanel, true);
    }

    void EndGame(GameObject panel, bool isLose)
    {
        if (gameEnded)
            return;

        gameEnded = true;

        if (panel != null)
            panel.SetActive(true);
        else if (isLose)
            showingFallbackLoseScreen = true;

        // Bekukan game dan tampilkan kursor supaya tombol bisa diklik
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Hubungkan ke tombol Restart
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Hubungkan ke tombol Main Menu
    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(mainMenuScene);
    }

    void OnGUI()
    {
        if (!showingFallbackLoseScreen)
            return;

        float panelWidth = Mathf.Min(520f, Screen.width - 40f);
        float panelHeight = 250f;
        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);

        GUI.color = new Color(0f, 0f, 0f, 0.78f);
        GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none);
        GUI.color = Color.white;
        GUI.Box(panel, GUIContent.none);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 38,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(new Rect(panel.x, panel.y + 24f, panel.width, 56f), "GAME OVER", titleStyle);

        float buttonWidth = Mathf.Min(360f, panel.width - 40f);
        float buttonX = panel.x + (panel.width - buttonWidth) * 0.5f;
        if (GUI.Button(new Rect(buttonX, panel.y + 108f, buttonWidth, 48f), "Retry"))
        {
            Restart();
        }

        if (GUI.Button(new Rect(buttonX, panel.y + 170f, buttonWidth, 48f), "Kembali ke Main Menu"))
        {
            GoToMainMenu();
        }
    }
}
