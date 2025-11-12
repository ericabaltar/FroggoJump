using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HudManager : MonoBehaviour
{
    [SerializeField] private Image staminaBarFill;
    [SerializeField] private TextMeshProUGUI scoreText;
    public static HudManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SetStaminaBar(1);
    }

    public void SetStaminaBar(float staminaPercentage)
    {
        staminaBarFill.fillAmount = staminaPercentage;
    }

    public void SetScore(int maxDistance)
    {
        scoreText.text = maxDistance.ToString();
    }
}
