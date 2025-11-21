using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PowerupIconUI : MonoBehaviour
{
    [Header("Refs (en este GO o hijos)")]
    [SerializeField] private Image iconImage;     // sprite del powerup
    [SerializeField] private Image darkMaskImage; // MISMO sprite, encima del icono

    [Header("Ajustes visuales")]
    [SerializeField, Range(0f, 1f)] private float darkAlpha = 0.9f;                 // opacidad de la máscara
    [SerializeField] private Color inactiveIconTint = new(0.18f, 0.18f, 0.18f, 1f);   // icono muy oscuro
    [SerializeField] private Color activeIconTint = Color.white;                    // icono normal
    [SerializeField] private bool clockwise = true;
    [SerializeField] private bool startHidden = true;                               // << oculto al inicio

    [Header("Tiempo")]
    [SerializeField] private bool unscaledTime = true;

    private Coroutine timerCoro;

    void Reset()
    {
        var images = GetComponentsInChildren<Image>(true);
        if (images.Length > 0) iconImage = images[0];
        if (images.Length > 1) darkMaskImage = images[1];
    }

    void Awake()
    {
        EnsureSetup();
        if (startHidden) gameObject.SetActive(false);
        else ForceInactiveNow();
        iconImage.enabled = false;
        darkMaskImage.enabled = false;
    }

    private void EnsureSetup()
    {
        if (darkMaskImage == null || iconImage == null) return;

        // La máscara debe estar encima
        if (darkMaskImage.transform.GetSiblingIndex() < iconImage.transform.GetSiblingIndex())
            darkMaskImage.transform.SetAsLastSibling();

        darkMaskImage.type = Image.Type.Filled;
        darkMaskImage.fillMethod = Image.FillMethod.Radial360;
        darkMaskImage.fillOrigin = 2; // Top
        darkMaskImage.fillClockwise = clockwise;
        darkMaskImage.raycastTarget = false;

        var c = Color.black; c.a = Mathf.Clamp01(darkAlpha);
        darkMaskImage.color = c;

        iconImage.raycastTarget = false;
    }

    /// Oculta completamente el icono (GameObject.SetActive(false))
    public void HideCompletely()
    {
        if (timerCoro != null) { StopCoroutine(timerCoro); timerCoro = null; }
        iconImage.enabled = false;
    }

    /// Muestra el icono, en estado APAGADO (oscuro total + tinte gris)
    public void ShowOff()
    {
        iconImage.enabled = true;
        ForceInactiveNow();
    }

    /// Enciende al 100% (icono visible sin máscara)
    public void ShowFullyLit()
    {
        if (timerCoro != null) { StopCoroutine(timerCoro); timerCoro = null; }
        EnsureSetup();
        iconImage.enabled = true;
        if (darkMaskImage != null) { darkMaskImage.enabled = true; darkMaskImage.fillAmount = 0f; }
        if (iconImage != null) iconImage.color = activeIconTint;
    }

    /// Apagado fuerte (oscuro + icono tintado)
    public void ForceInactiveNow()
    {
        if (timerCoro != null) { StopCoroutine(timerCoro); timerCoro = null; }
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (darkMaskImage == null || iconImage == null) return;

        EnsureSetup();
        darkMaskImage.enabled = true;
        darkMaskImage.fillAmount = 1f;
        iconImage.color = inactiveIconTint;
    }

    /// Reloj que descubre el icono y lo aclara. Si autoHide = true, se oculta al terminar.
    public void PlayTimer(float duration, bool autoHide = true)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (timerCoro != null) StopCoroutine(timerCoro);
        timerCoro = StartCoroutine(CoTimer(duration, autoHide));
    }

    private IEnumerator CoTimer(float duration, bool autoHide)
    {
        if (darkMaskImage == null || iconImage == null) yield break;

        EnsureSetup();
        darkMaskImage.enabled = true;
        darkMaskImage.fillAmount = 1f;
        iconImage.color = inactiveIconTint;

        float t = duration;
        while (t > 0)
        {
            t -= unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);

            darkMaskImage.fillAmount = 1f - k;
            iconImage.color = Color.Lerp(inactiveIconTint, activeIconTint, k);
            yield return null;
        }

        darkMaskImage.fillAmount = 0f;
        iconImage.color = activeIconTint;
        timerCoro = null;

        if (autoHide) HideCompletely();
    }

    public void SetIconSprite(Sprite s)
    {
        if (iconImage != null) iconImage.sprite = s;
        if (darkMaskImage != null) darkMaskImage.sprite = s;
        EnsureSetup();
    }

    public void SetClockwise(bool value)
    {
        clockwise = value;
        if (darkMaskImage != null) darkMaskImage.fillClockwise = clockwise;
    }
}
