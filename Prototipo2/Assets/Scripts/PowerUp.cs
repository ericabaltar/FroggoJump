using UnityEngine;

public enum PowerUpType
{
    FastSpeed,     // Aumenta velocidad (duraci�n)
    MoreRange,     // Reduce velocidad (duraci�n)
    StaminaRegen,   // Estamina se gasta m�s lento (duraci�n)
    ExtraLife      // Vida extra (enciende icono hasta consumir)
}

[RequireComponent(typeof(Collider))]
public class PowerUp : MonoBehaviour
{
    [Header("Tipo")]
    public PowerUpType type = PowerUpType.ExtraLife;

    [Header("Par�metros comunes")]
    [SerializeField] private float durationSeconds = 6f;   // para temporales
    [SerializeField] private float pickupRadius = 0.6f;    // si no hay collider, se crea Sphere

    [Header("Velocidad")]
    [SerializeField] private float speedMultiplier = 1.5f; // >1 acelera, <1 frena

    [Header("Par�metros de estamina")]
    public int staminaRegenPerInterval = 1;
    public float regenInterval = 0.5f;

    // -------- SFX PICKUP (COM�N) --------
    [Header("SFX �nico para TODOS los powerups")]
    [Tooltip("Mismo clip para todos los powerups. Arr�stralo en cada prefab o config�ralo por script.")]
    [SerializeField] private AudioClip commonPickupClip;
    [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.9f;
    [SerializeField, Range(0f, 0.3f)] private float pickupPitchJitter = 0.06f;
    [SerializeField] private float pickupBasePitch = 1f;

    [Header("Rango")]
    public float upgradedRadius = 1f;

    private void Reset()
    {
        // Asegura collider + trigger
        var col = GetComponent<Collider>();
        if (col == null) col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;

        if (col is SphereCollider sc)
        {
            if (sc.radius < 0.05f) sc.radius = pickupRadius;
            sc.center = Vector3.zero;
        }

        if (CompareTag("Untagged")) gameObject.tag = "PowerUp";
    }

    private void OnTriggerEnter(Collider other)
    {
        // Intenta encontrar PlayerController en el propio objeto o en sus padres
        var player = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        player.PlayPowerupPickupSfx();

        // Aplica efecto + UI
        switch (type)
        {
            case PowerUpType.MoreRange:
                GameManager.Instance?.ActivateUpgradedRange(durationSeconds);
                PowerupUIManager.Instance?.ActivateTimed(PowerUpType.MoreRange, durationSeconds);
                break;
            case PowerUpType.FastSpeed:
                player.ApplySpeedMultiplier(durationSeconds, Mathf.Max(1.01f, speedMultiplier));
                PowerupUIManager.Instance?.ActivateTimed(PowerUpType.FastSpeed, durationSeconds);
                break;
            case PowerUpType.StaminaRegen:
                player.ApplyStaminaRegeneration(staminaRegenPerInterval, regenInterval, durationSeconds);
                PowerupUIManager.Instance?.ActivateTimed(PowerUpType.StaminaRegen, durationSeconds);
                break;
            case PowerUpType.ExtraLife:
                GameManager.Instance?.GrantExtraLife(1);
                PowerupUIManager.Instance?.OnExtraLifeGained();
                break;
        }

        // Destruye el pickup tras aplicarse
        Destroy(gameObject);
    }
}
