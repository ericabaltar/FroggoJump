using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HudManager : MonoBehaviour
{
    public static HudManager Instance { get; private set; }

    [Header("Refs")]
    [SerializeField] private Image staminaBarFill;          // (igual que tu script)
    [SerializeField] private TextMeshProUGUI scoreText;     // (igual que tu script)

    [Header("Opcional: raíz para animar 'pop'")]
    [SerializeField] private RectTransform staminaBarRoot;  // arrastra el contenedor de la barra si quieres el efecto

    [Header("Comportamiento barra")]
    [Tooltip("Suavizado visual de la barra (segundos para alcanzar el valor objetivo). 0 = instantáneo.")]
    [SerializeField, Range(0f, 1f)] private float staminaSmoothTime = 0.12f;

    [Tooltip("Umbral de estamina baja (0..1). Por debajo de esto, parpadea/cambia color.")]
    [SerializeField, Range(0f, 1f)] private float lowStaminaThreshold = 0.15f;

    [Tooltip("Color normal del relleno.")]
    [SerializeField] private Color fillNormal = new Color(0.36f, 0.80f, 0.46f, 1f);

    [Tooltip("Color cuando la estamina es baja (parpadeo entre este y el normal).")]
    [SerializeField] private Color fillLow = new Color(0.95f, 0.30f, 0.25f, 1f);

    [Tooltip("Velocidad del parpadeo cuando hay poca estamina.")]
    [SerializeField] private float lowBlinkSpeed = 6f;

    [Header("Animación 'pop' al subir estamina (opcional)")]
    [SerializeField] private bool playPopOnIncrease = true;
    [SerializeField] private float popScale = 1.06f;
    [SerializeField] private float popTime = 0.08f;

    [Header("Score")]
    [Tooltip("Formatear con dígitos fijos (p.ej. 6 → 000123). 0 = sin formateo.")]
    [SerializeField] private int scoreFixedDigits = 0;

    // --- estado interno ---
    private float targetFill = 1f;
    private float currentFill = 1f;
    private float lastRequestedFill = 1f; // para detectar incrementos y disparar 'pop'
    private Coroutine popCoro;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Asegura configuración de imagen de relleno tipo "Filled Horizontal"
        if (staminaBarFill != null)
        {
            if (staminaBarFill.type != Image.Type.Filled)
            {
                staminaBarFill.type = Image.Type.Filled;
                staminaBarFill.fillMethod = Image.FillMethod.Horizontal;
                staminaBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
            staminaBarFill.fillAmount = 1f;
            staminaBarFill.color = fillNormal;
        }

        // Inicial
        targetFill = currentFill = 1f;
        lastRequestedFill = 1f;

        // Score inicial (opcional)
        if (scoreText != null && scoreFixedDigits > 0)
            scoreText.text = 0.ToString(new string('0', scoreFixedDigits));
        else if (scoreText != null)
            scoreText.text = "0";
    }

    private void Update()
    {
        if (staminaBarFill == null) return;

        // Suavizado hacia el objetivo (Time.unscaledDeltaTime para animar incluso si pausas)
        if (staminaSmoothTime <= 0f)
        {
            currentFill = targetFill;
        }
        else
        {
            float speed = 1f / staminaSmoothTime;
            currentFill = Mathf.MoveTowards(currentFill, targetFill, speed * Time.unscaledDeltaTime);
        }

        staminaBarFill.fillAmount = currentFill;

        // Estado de color/parpadeo cuando es baja
        bool isLow = currentFill <= lowStaminaThreshold;
        if (isLow)
        {
            float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * lowBlinkSpeed);
            staminaBarFill.color = Color.Lerp(fillLow, fillNormal, t);
        }
        else
        {
            staminaBarFill.color = fillNormal;
        }
    }

    // ===== API que ya usas desde PlayerController =====

    /// <summary>
    /// Actualiza la barra con un valor normalizado 0..1.
    /// Respeta suavizado y animación pop si sube.
    /// </summary>
    public void SetStaminaBar(float staminaPercentage)
    {
        staminaPercentage = Mathf.Clamp01(staminaPercentage);

        // Detecta incremento para efecto pop
        if (playPopOnIncrease && staminaPercentage > lastRequestedFill)
            TryPop();

        lastRequestedFill = staminaPercentage;
        targetFill = staminaPercentage;
    }

    /// <summary>
    /// Actualiza el texto del marcador.
    /// </summary>
    public void SetScore(int maxDistance)
    {
        if (scoreText == null) return;

        if (scoreFixedDigits > 0)
            scoreText.text = maxDistance.ToString(new string('0', scoreFixedDigits));
        else
            scoreText.text = maxDistance.ToString();
    }

    // ===== utilidades opcionales =====

    /// <summary>
    /// Fuerza la barra a un valor instantáneo (sin suavizado).
    /// </summary>
    public void SetStaminaInstant(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);
        targetFill = currentFill = normalized;
        if (staminaBarFill != null) staminaBarFill.fillAmount = currentFill;
        lastRequestedFill = normalized;
    }

    private void TryPop()
    {
        if (staminaBarRoot == null) return;
        if (popCoro != null) StopCoroutine(popCoro);
        popCoro = StartCoroutine(CoPop());
    }

    private System.Collections.IEnumerator CoPop()
    {
        Vector3 baseScale = staminaBarRoot.localScale;
        staminaBarRoot.localScale = baseScale * popScale;
        float t = 0f;
        while (t < popTime)
        {
            t += Time.unscaledDeltaTime;
            staminaBarRoot.localScale = Vector3.Lerp(baseScale * popScale, baseScale, t / popTime);
            yield return null;
        }
        staminaBarRoot.localScale = baseScale;
        popCoro = null;
    }
}
