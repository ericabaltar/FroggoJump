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
    [Tooltip("Multiplicador del gasto de estamina (<1 gasta menos). Ej: 0.5 = gasta la mitad")]
    public float staminaDrainMultiplier = 0.5f;

    [Header("Vidas")]
    public int extraLives = 1;

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

                break;
            case PowerUpType.FastSpeed:
                player.ApplySpeedMultiplier(duration, Mathf.Max(0.05f, speedMultiplier));
                break;

            case PowerUpType.StaminaRegen:
                player.ApplyStaminaDrainModifier(duration, Mathf.Clamp(staminaDrainMultiplier, 0.05f, 1f));
                break;

            case PowerUpType.ExtraLife:
                GameManager.Instance?.GrantExtraLife(extraLives);
                break;
        }

        Destroy(gameObject);
    }
}
