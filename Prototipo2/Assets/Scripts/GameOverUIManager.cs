using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameOverUIManager : MonoBehaviour
{
    public static GameOverUIManager Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private Button retryButton;

    [Header("Escena del nivel")]
    [SerializeField] private string levelSceneName = "Level1";

    [Header("Fade")]
    [SerializeField] private float showFadeTime = 0.15f;

    [Header("Hard Reset")]
    [SerializeField] private bool hardResetManagers = true;
    [SerializeField] private bool alsoResetHudManager = false;
    [SerializeField] private bool alsoResetSfxManager = false;
    [SerializeField] private bool alsoResetPowerupUIManager = false;
    [SerializeField] private bool alsoResetDeathSpotlight = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (retryButton != null)
            retryButton.onClick.AddListener(OnRetry);

        HideInstant();
    }

    public void Show(int finalScore)
    {
        if (scoreText != null)
            scoreText.text = $"Score: {finalScore}";

        gameObject.SetActive(true);

        Time.timeScale = 0f;

        StartCoroutine(FadeCanvas(0f, 1f, showFadeTime));
    }

    public void HideInstant()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }

    private System.Collections.IEnumerator FadeCanvas(float from, float to, float duration)
    {
        if (canvasGroup == null || duration <= 0f)
        {
            if (canvasGroup != null) canvasGroup.alpha = to;
            if (canvasGroup != null)
            {
                canvasGroup.interactable = to > 0.99f;
                canvasGroup.blocksRaycasts = to > 0.99f;
            }
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime; 
            float a = Mathf.Lerp(from, to, t / duration);
            canvasGroup.alpha = a;
            yield return null;
        }
        canvasGroup.alpha = to;
        canvasGroup.interactable = to > 0.99f;
        canvasGroup.blocksRaycasts = to > 0.99f;
    }

    private void OnRetry()
    {
       
        Time.timeScale = 1f;

        HideInstant();

        if (hardResetManagers)
        {
            
            if (GameManager.Instance != null) Destroy(GameManager.Instance.gameObject);
            if (alsoResetHudManager && HudManager.Instance != null) Destroy(HudManager.Instance.gameObject);
            if (alsoResetSfxManager && SfxManager.Instance != null) Destroy(SfxManager.Instance.gameObject);
            if (alsoResetPowerupUIManager && PowerupUIManager.Instance != null) Destroy(PowerupUIManager.Instance.gameObject);
            if (alsoResetDeathSpotlight && DeathSpotlightController.Instance != null) Destroy(DeathSpotlightController.Instance.gameObject);
        }

        
        if (!string.IsNullOrEmpty(levelSceneName))
            SceneManager.LoadScene(levelSceneName, LoadSceneMode.Single);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);
    }
}
