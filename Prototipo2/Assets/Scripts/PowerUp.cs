using UnityEngine;

public enum PowerUpType
{
    MoreRange,         // Reducir velocidad del jugador
    FastSpeed,         // Aumentar velocidad del jugador
    StaminaRegen,      // La estamina se gasta más lento
    ExtraLife          // Vida extra (uso único)
}

[RequireComponent(typeof(Collider))]
public class PowerUp : MonoBehaviour
{
    [Header("Tipo")]
    public PowerUpType type = PowerUpType.FastSpeed;

    [Header("Duración (si aplica)")]
    public float duration = 6f;

    [Header("Parámetros de velocidad")]
    [Tooltip("Multiplicador de velocidad (>1 más rápido, <1 más lento). Se usa en Fast/Slow Speed")]
    public float speedMultiplier = 1.5f;   // FastSpeed: 1.5, SlowSpeed: 0.6 por ejemplo

    [Header("Parámetros de estamina")]
    public int staminaRegenPerInterval = 1;
    public float regenInterval = 0.5f;

    [Header("Vidas")]
    public int extraLives = 1;

    [Header("Rango")]
    public float upgradedRadius = 1f;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        if (gameObject.tag == "Untagged") gameObject.tag = "PowerUp";
    }

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        switch (type)
        {
            case PowerUpType.MoreRange:
                GameManager.Instance?.ActivateUpgradedRange(duration);
                break;
            case PowerUpType.FastSpeed:
                player.ApplySpeedMultiplier(duration, Mathf.Max(0.05f, speedMultiplier));
                break;

            case PowerUpType.StaminaRegen:
                player.ApplyStaminaRegeneration(1, 0.5f, duration);
                break;

            case PowerUpType.ExtraLife:
                GameManager.Instance?.GrantExtraLife(extraLives);
                break;
        }

        Destroy(gameObject);
    }
}
