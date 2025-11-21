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
                break;
            case PowerUpType.ExtraLife:
                GameManager.Instance?.GrantExtraLife(1);
                PowerupUIManager.Instance?.OnExtraLifeGained();
                break;
        }

        // Destruye el pickup tras aplicarse
        Destroy(gameObject);
    }

    private void PlayPickupSfx()
    {
        if (commonPickupClip == null)
        {
            Debug.LogWarning($"[PowerUp] No hay commonPickupClip asignado en {name}. No se puede reproducir sonido.");
            return;
        }

        // 1) Usa SfxManager si existe
        if (SfxManager.Instance != null)
        {
            SfxManager.Instance.PlayOneShot(commonPickupClip, pickupVolume, pickupPitchJitter, pickupBasePitch);
            // Debug opcional:
            // Debug.Log("[PowerUp] SFX por SfxManager");
            return;
        }

        // 2) Fallback local: crea un AudioSource temporal en escena
        // (Esto asegura sonido aunque te hayas olvidado del manager)
        var go = new GameObject("OneShot_SFX_PowerUp");
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0f; // 2D
        src.volume = Mathf.Clamp01(pickupVolume);

        // Pitch aleatorio:
        float p = Mathf.Clamp(pickupBasePitch + Random.Range(-pickupPitchJitter, pickupPitchJitter), 0.1f, 3f);
        src.pitch = p;

        src.clip = commonPickupClip;
        src.Play();

        // Col�cala cerca de la c�mara por si tu listener est� all�
        var cam = Camera.main;
        go.transform.position = cam ? cam.transform.position : Vector3.zero;

        Object.Destroy(go, commonPickupClip.length / Mathf.Max(0.01f, p));
        // Debug opcional:
        // Debug.Log("[PowerUp] SFX por fallback local (sin SfxManager)");
    }
}
