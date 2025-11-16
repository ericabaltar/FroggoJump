using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DeathSpotlightController : MonoBehaviour
{
    public static DeathSpotlightController Instance { get; private set; }

    [Header("Setup")]
    [SerializeField] private Camera cam;                // tu cámara principal
    [SerializeField] private Image overlayImage;        // UI Image pantalla completa
    [SerializeField] private Material spotlightMaterial;// material con el shader SpotlightCutout

    [Header("Animación")]
    [SerializeField] private float inDuration = 0.6f;   // tiempo de cerrar el foco
    [SerializeField] private float startRadius = 1.2f;  // empieza sin oscurecer (agujero grandote)
    [SerializeField] private float endRadius = 0.12f;   // final: foco pequeño sobre el player
    [SerializeField] private float feather = 0.18f;     // borde suave
    [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 1f); // negro

    Material _runtimeMat;
    Transform _target;         // jugador a seguir
    Coroutine _coro;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Material en runtime (no pisamos el asset)
        _runtimeMat = new Material(spotlightMaterial);
        _runtimeMat.SetFloat("_Feather", feather);
        _runtimeMat.SetColor("_Color", overlayColor);

        overlayImage.material = _runtimeMat;
        overlayImage.raycastTarget = false; // para no bloquear UI si no quieres
        overlayImage.enabled = false;       // apagado por defecto
    }

    /// <summary>
    /// Lanza la transición: foco cerrándose alrededor de target.
    /// onComplete se llama al terminar.
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

        // Estado inicial
        _runtimeMat.SetFloat("_Radius", startRadius);
        _runtimeMat.SetFloat("_Feather", feather);
        _runtimeMat.SetColor("_Color", overlayColor);

        float t = 0f;
        while (t < inDuration)
        {
            t += Time.unscaledDeltaTime; // que no dependa de Time.timeScale si pausarás
            float k = Mathf.Clamp01(t / inDuration);

            // Interpolar radio
            float r = Mathf.Lerp(startRadius, endRadius, k);
            _runtimeMat.SetFloat("_Radius", r);

            // Actualizar centro a la posición del jugador cada frame
            if (cam != null && _target != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(_target.position);
                // Clamp por si sale de pantalla
                vp.x = Mathf.Clamp01(vp.x);
                vp.y = Mathf.Clamp01(vp.y);
                _runtimeMat.SetVector("_Center", new Vector4(vp.x, vp.y, 0f, 0f));
            }

            yield return null;
        }

        // Radio final y centro definitivo
        if (_target != null && cam != null)
        {
            Vector3 vp = cam.WorldToViewportPoint(_target.position);
            _runtimeMat.SetVector("_Center", new Vector4(Mathf.Clamp01(vp.x), Mathf.Clamp01(vp.y), 0f, 0f));
        }
        _runtimeMat.SetFloat("_Radius", endRadius);

        onComplete?.Invoke();
    }

    /// <summary>
    /// Si quieres revertir (abrir el círculo) para volver al juego o al fade-in de respawn.
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
        onComplete?.Invoke();
    }
}
