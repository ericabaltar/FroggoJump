using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private string levelSceneName = "Level1";
    [SerializeField] private Button startButton; 
    private void Awake()
    {

        Time.timeScale = 1f;
    }

    private void OnEnable()
    {
        
        if (startButton != null)
            EventSystem.current?.SetSelectedGameObject(startButton.gameObject);
    }

    public void OnClickStart()
    {
    
        SceneManager.LoadScene(levelSceneName);
    }

    public void OnClickQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
