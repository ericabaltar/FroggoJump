using UnityEngine;

public class PowerupUIManager : MonoBehaviour
{
    public static PowerupUIManager Instance { get; private set; }

    [Header("Iconos en HUD (arrástralos)")]
    [SerializeField] private PowerupIconUI moreRangeIcon;   // Rango.png
    [SerializeField] private PowerupIconUI fastSpeedIcon;   // Velocidad.png
    [SerializeField] private PowerupIconUI staminaRegenIcon;     // Energia.png
    [SerializeField] private PowerupIconUI extraLifeIcon;   // Corazon.png

    [Header("Sprites (forzados en Awake)")]
    [SerializeField] private Sprite moreRangeSprite;   // Rango.png
    [SerializeField] private Sprite fastSpeedSprite;   // Velocidad.png
    [SerializeField] private Sprite staminaRegenSprite;     // Energia.png
    [SerializeField] private Sprite extraLifeSprite;   // Corazon.png

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Fuerza sprites correctos (evita “arrastrar” el sprite equivocado desde prefabs)
        moreRangeIcon?.SetIconSprite(moreRangeSprite);
        fastSpeedIcon?.SetIconSprite(fastSpeedSprite);
        staminaRegenIcon?.SetIconSprite(staminaRegenSprite);
        extraLifeIcon?.SetIconSprite(extraLifeSprite);

        // Arrancan ocultos
        moreRangeIcon?.HideCompletely();
        fastSpeedIcon?.HideCompletely();
        staminaRegenIcon?.HideCompletely();
        extraLifeIcon?.HideCompletely();
    }

    public void ActivateTimed(PowerUpType type, float durationSeconds)
    {
        Debug.Log("Activandose");
        switch (type)
        {
            case PowerUpType.MoreRange:
                moreRangeIcon?.ShowOff();
                moreRangeIcon?.PlayTimer(durationSeconds);
                break;

            case PowerUpType.FastSpeed:
                fastSpeedIcon?.ShowOff();
                fastSpeedIcon?.PlayTimer(durationSeconds);
                break;

            case PowerUpType.StaminaRegen:
                staminaRegenIcon?.ShowOff();
                staminaRegenIcon?.PlayTimer(durationSeconds);
                break;
        }
    }

    public void Deactivate(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.MoreRange: moreRangeIcon?.HideCompletely(); break;
            case PowerUpType.FastSpeed: fastSpeedIcon?.HideCompletely(); break;
            case PowerUpType.StaminaRegen: staminaRegenIcon?.HideCompletely(); break;
            case PowerUpType.ExtraLife: extraLifeIcon?.HideCompletely(); break;
        }
    }

    public void OnExtraLifeGained()
    {
        extraLifeIcon?.ShowFullyLit();

    }

    public void OnExtraLifeConsumed() => extraLifeIcon?.HideCompletely();
}
