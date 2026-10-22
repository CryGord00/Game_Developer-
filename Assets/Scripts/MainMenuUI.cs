using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private string gameScene = "SampleScene";

    private void OnGUI()
    {
        GUI.color = new Color(0.025f, 0.035f, 0.06f, 1f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float width = Mathf.Min(520f, Screen.width - 40f);
        float x = (Screen.width - width) * 0.5f;
        float centerY = Screen.height * 0.5f;

        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 42,
            fontStyle = FontStyle.Bold
        };
        GUI.Label(new Rect(x, centerY - 135f, width, 70f), "GAME BRANCE ESCAPE", title);

        float buttonWidth = Mathf.Min(360f, width - 40f);
        float buttonX = (Screen.width - buttonWidth) * 0.5f;
        if (GUI.Button(new Rect(buttonX, centerY - 25f, buttonWidth, 54f), "Mulai Bermain"))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameScene);
        }

        if (GUI.Button(new Rect(buttonX, centerY + 45f, buttonWidth, 54f), "Keluar"))
        {
            Application.Quit();
        }
    }
}
