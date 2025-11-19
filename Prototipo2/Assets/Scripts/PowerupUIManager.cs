using UnityEngine;

public enum PowerupType { SlowSpeed, FastSpeed, StaminaSlow, ExtraLife }

public class PowerupUIManager : MonoBehaviour
{
    public static PowerupUIManager Instance { get; private set; }

    [Header("Iconos en HUD (arrástralos)")]
    [SerializeField] private PowerupIconUI slowSpeedIcon;   // Rango.png
    [SerializeField] private PowerupIconUI fastSpeedIcon;   // Velocidad.png
    [SerializeField] private PowerupIconUI staminaIcon;     // Energia.png
    [SerializeField] private PowerupIconUI extraLifeIcon;   // Corazon.png

    [Header("Sprites (forzados en Awake)")]
    [SerializeField] private Sprite slowSpeedSprite;   // Rango.png
    [SerializeField] private Sprite fastSpeedSprite;   // Velocidad.png
    [SerializeField] private Sprite staminaSprite;     // Energia.png
    [SerializeField] private Sprite extraLifeSprite;   // Corazon.png

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Fuerza sprites correctos (evita “arrastrar” el sprite equivocado desde prefabs)
        slowSpeedIcon?.SetIconSprite(slowSpeedSprite);
        fastSpeedIcon?.SetIconSprite(fastSpeedSprite);
        staminaIcon?.SetIconSprite(staminaSprite);
        extraLifeIcon?.SetIconSprite(extraLifeSprite);

        // Arrancan ocultos
        slowSpeedIcon?.HideCompletely();
        fastSpeedIcon?.HideCompletely();
        staminaIcon?.HideCompletely();
        extraLifeIcon?.HideCompletely();
    }

    public void ActivateTimed(PowerupType type, float durationSeconds)
    {
        switch (type)
        {
            case PowerupType.SlowSpeed:
                slowSpeedIcon?.ShowOff();
                slowSpeedIcon?.PlayTimer(durationSeconds);
                break;

            case PowerupType.FastSpeed:
                fastSpeedIcon?.ShowOff();
                fastSpeedIcon?.PlayTimer(durationSeconds);
                break;

            case PowerupType.StaminaSlow:
                staminaIcon?.ShowOff();
                staminaIcon?.PlayTimer(durationSeconds);
                break;

            case PowerupType.ExtraLife:
                extraLifeIcon?.ShowFullyLit(); 
                break;
        }
    }

    public void Deactivate(PowerupType type)
    {
        switch (type)
        {
            case PowerupType.SlowSpeed: slowSpeedIcon?.HideCompletely(); break;
            case PowerupType.FastSpeed: fastSpeedIcon?.HideCompletely(); break;
            case PowerupType.StaminaSlow: staminaIcon?.HideCompletely(); break;
            case PowerupType.ExtraLife: extraLifeIcon?.HideCompletely(); break;
        }
    }

    public void OnExtraLifeGained() => extraLifeIcon?.ShowFullyLit();
    public void OnExtraLifeConsumed() => extraLifeIcon?.HideCompletely();
}
