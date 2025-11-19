using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DeathSpotlightController : MonoBehaviour
{
    public static DeathSpotlightController Instance { get; private set; }

    [Header("Setup")]
    [SerializeField] private Camera cam;                // tu cámara principal
    [SerializeField] private Image overlayImage;        // UI Image pantalla completa
    [SerializeField] private Material spotlightMaterial;// material (SpotlightCutout o SpotlightCutoutBG)

    [Header("Animación")]
    [SerializeField] private float inDuration = 1.2f;   // <-- más lento que antes
    [SerializeField] private float startRadius = 1.6f;
    [SerializeField] private float endRadius = 0.25f;
    [SerializeField] private float feather = 0.18f;
    [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 1f);

    [Header("Pausa de juego durante la animación")]
    [SerializeField] private bool pauseGameDuringTransition = true;
    [SerializeField] private bool resumeTimeAfterTransition = false; // para “reverse”/respawn
    private float _prevTimeScale = 1f;

    // (Opcionales si usas el shader con fondo)
    [Header("Fondo opcional")]
    [SerializeField] private Texture2D transitionBackground;
    [SerializeField, Range(0f, 1f)] private float bgMix = 1f;
    [SerializeField] private Color bgTint = Color.white;

    Material _runtimeMat;
    Transform _target;         // jugador a seguir
    Coroutine _coro;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _runtimeMat = new Material(spotlightMaterial);
        _runtimeMat.SetFloat("_Feather", feather);
        _runtimeMat.SetColor("_Color", overlayColor);

        // Soporte para el shader con imagen de fondo (si lo usas)
        if (transitionBackground != null)
            _runtimeMat.SetTexture("_BgTex", transitionBackground);
        _runtimeMat.SetColor("_BgTint", bgTint);
        _runtimeMat.SetFloat("_BgMix", bgMix);

        overlayImage.material = _runtimeMat;
        overlayImage.raycastTarget = false;
        overlayImage.enabled = false;
    }

    /// <summary>
    /// Lanza la transición (cierre de foco). Pausa el juego si está activado.
    /// </summary>
    public void Play(Transform target, System.Action onComplete = null)
    {
        if (_coro != null) StopCoroutine(_coro);
        _coro = StartCoroutine(CoPlay(target, onComplete));
    }

    IEnumerator CoPlay(Transform target, System.Action onComplete)
    {
        _target = target;
        overlayImage.enabled = true;

        // Pausar juego si procede
        if (pauseGameDuringTransition)
        {
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        // Estado inicial
        _runtimeMat.SetFloat("_Radius", startRadius);
        _runtimeMat.SetFloat("_Feather", feather);
        _runtimeMat.SetColor("_Color", overlayColor);

        float t = 0f;
        while (t < inDuration)
        {
            t += Time.unscaledDeltaTime;         // animación independiente del timeScale
            float k = Mathf.Clamp01(t / inDuration);

            // Interpolar radio
            float r = Mathf.Lerp(startRadius, endRadius, k);
            _runtimeMat.SetFloat("_Radius", r);

            // Centro al jugador cada frame
            if (cam != null && _target != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(_target.position);
                vp.x = Mathf.Clamp01(vp.x);
                vp.y = Mathf.Clamp01(vp.y);
                _runtimeMat.SetVector("_Center", new Vector4(vp.x, vp.y, 0f, 0f));
            }

            yield return null;
        }

        // Valor final por seguridad
        _runtimeMat.SetFloat("_Radius", endRadius);

        // Reanudar timeScale si quieres (para casos de reverse/respawn en la misma escena)
        if (pauseGameDuringTransition && resumeTimeAfterTransition)
        {
            Time.timeScale = _prevTimeScale;
        }

        onComplete?.Invoke();
    }

    /// <summary>
    /// Transición inversa (abrir el foco). Útil para respawn.
    /// </summary>
    public void PlayReverse(Transform target, System.Action onComplete = null)
    {
        if (_coro != null) StopCoroutine(_coro);
        _coro = StartCoroutine(CoPlayReverse(target, onComplete));
    }

    IEnumerator CoPlayReverse(Transform target, System.Action onComplete)
    {
        _target = target;
        overlayImage.enabled = true;

        if (pauseGameDuringTransition)
        {
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        float t = 0f;
        while (t < inDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / inDuration);

            float r = Mathf.Lerp(endRadius, startRadius, k);
            _runtimeMat.SetFloat("_Radius", r);

            if (cam != null && _target != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(_target.position);
                vp.x = Mathf.Clamp01(vp.x);
                vp.y = Mathf.Clamp01(vp.y);
                _runtimeMat.SetVector("_Center", new Vector4(vp.x, vp.y, 0f, 0f));
            }

            yield return null;
        }

        overlayImage.enabled = false;

        // Al terminar el reverse, normalmente quieres reanudar el juego
        if (pauseGameDuringTransition)
        {
            Time.timeScale = _prevTimeScale;
        }

        onComplete?.Invoke();
    }
}
