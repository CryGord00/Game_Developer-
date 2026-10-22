using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameFlow : MonoBehaviour
{
    public static GameFlow Instance;

    private static bool hasRetryCheckpoint;
    private static Vector3 retryCheckpointPosition;

    [Header("Panel UI")]
    public GameObject winPanel;
    public GameObject losePanel;

    [Header("Nama Scene")]
    [Tooltip("Harus persis sama dengan nama scene Main Menu di Build Settings")]
    public string mainMenuScene = "MainMenu";

    [Header("Timer Permainan")]
    [SerializeField] private bool showGameTimer = true;

    private bool gameEnded = false;
    private bool gameLost;
    private bool showingFallbackLoseScreen;
    private float elapsedGameTime;
    private Text gameTimerText;
    private Text gameTimerCaption;

    public static void SetRetryCheckpoint(Vector3 position)
    {
        retryCheckpointPosition = position;
        hasRetryCheckpoint = true;
    }

    public static bool TryConsumeRetryCheckpoint(out Vector3 position)
    {
        position = retryCheckpointPosition;
        if (!hasRetryCheckpoint)
        {
            return false;
        }

        hasRetryCheckpoint = false;
        return true;
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Pastikan game berjalan normal setiap scene dimulai (penting setelah Restart)
        Time.timeScale = 1f;
        gameEnded = false;
        gameLost = false;
        showingFallbackLoseScreen = false;
        elapsedGameTime = 0f;

        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        CreateGameTimerUI();
        UpdateGameTimerUI();
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
        gameLost = isLose;

        if (panel != null)
            panel.SetActive(true);
        else if (isLose)
            showingFallbackLoseScreen = true;

        // Bekukan game dan tampilkan kursor supaya tombol bisa diklik
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (!gameEnded)
        {
            elapsedGameTime += Time.deltaTime;
            UpdateGameTimerUI();
        }

        if (!gameEnded || !gameLost || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            Restart();
        }
    }

    private void CreateGameTimerUI()
    {
        if (!showGameTimer)
        {
            return;
        }

        GameObject canvasObject = new GameObject(
            "GameplayTimerCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject(
            "GameplayTimerPanel",
            typeof(RectTransform),
            typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(38f, -20f);
        panelRect.sizeDelta = new Vector2(270f, 88f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.035f, 0.055f, 0.075f, 0.94f);
        panelImage.raycastTarget = false;

        GameObject accentObject = new GameObject(
            "GameplayTimerAccent",
            typeof(RectTransform),
            typeof(Image));
        accentObject.transform.SetParent(panelObject.transform, false);
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(5f, 0f);
        accentObject.GetComponent<Image>().color = new Color(0.18f, 0.82f, 0.78f, 1f);

        gameTimerCaption = CreateTimerText("GameplayTimerCaption", panelObject.transform);
        RectTransform captionRect = gameTimerCaption.rectTransform;
        captionRect.anchorMin = new Vector2(0f, 1f);
        captionRect.anchorMax = new Vector2(1f, 1f);
        captionRect.pivot = new Vector2(0f, 1f);
        captionRect.anchoredPosition = new Vector2(20f, -12f);
        captionRect.sizeDelta = new Vector2(-32f, 22f);
        gameTimerCaption.text = "WAKTU BERMAIN";
        gameTimerCaption.fontSize = 13;
        gameTimerCaption.color = new Color(0.64f, 0.76f, 0.8f, 1f);

        gameTimerText = CreateTimerText("GameplayTimerText", panelObject.transform);
        RectTransform timerRect = gameTimerText.rectTransform;
        timerRect.anchorMin = new Vector2(0f, 0f);
        timerRect.anchorMax = new Vector2(1f, 1f);
        timerRect.pivot = new Vector2(0f, 0f);
        timerRect.offsetMin = new Vector2(20f, 9f);
        timerRect.offsetMax = new Vector2(-12f, -32f);
        gameTimerText.fontSize = 33;
        gameTimerText.color = Color.white;
    }

    private static Text CreateTimerText(string objectName, Transform parent)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private void UpdateGameTimerUI()
    {
        if (gameTimerText == null)
        {
            return;
        }

        int totalSeconds = Mathf.FloorToInt(elapsedGameTime);
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;
        gameTimerText.text = hours > 0
            ? $"{hours:00}:{minutes:00}:{seconds:00}"
            : $"{minutes:00}:{seconds:00}";
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
        hasRetryCheckpoint = false;
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
        if (GUI.Button(new Rect(buttonX, panel.y + 108f, buttonWidth, 48f), "Retry (R)"))
        {
            Restart();
        }

        if (GUI.Button(new Rect(buttonX, panel.y + 170f, buttonWidth, 48f), "Kembali ke Main Menu"))
        {
            GoToMainMenu();
        }
    }
}
