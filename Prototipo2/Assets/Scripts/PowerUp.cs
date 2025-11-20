using UnityEngine;

public enum PowerUpKind
{
    FastSpeed,     // Aumenta velocidad (duración)
    SlowSpeed,     // Reduce velocidad (duración)
    StaminaSlow,   // Estamina se gasta más lento (duración)
    ExtraLife      // Vida extra (enciende icono hasta consumir)
}

[RequireComponent(typeof(Collider))]
public class PowerUp : MonoBehaviour
{
    [Header("Tipo")]
    public PowerUpKind kind = PowerUpKind.ExtraLife;

    [Header("Parámetros comunes")]
    [SerializeField] private float durationSeconds = 6f;   // para temporales
    [SerializeField] private float pickupRadius = 0.6f;    // si no hay collider, se crea Sphere

    [Header("Velocidad")]
    [SerializeField] private float speedMultiplier = 1.5f; // >1 acelera, <1 frena

    [Header("Estamina")]
    [SerializeField, Range(0.05f, 1f)] private float staminaDrainFactor = 0.5f; // 0.5 = gasta la mitad

    private void Awake()
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

        // Tag opcional (no imprescindible)
        if (CompareTag("Untagged")) gameObject.tag = "PowerUp";
    }

    private void OnTriggerEnter(Collider other)
    {
        // Intenta encontrar PlayerController en el propio objeto o en sus padres
        var player = other.GetComponent<PlayerController>();
        if (player == null)
            player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        switch (kind)
        {
            case PowerUpKind.FastSpeed:
                // El PlayerController ya enciende el icono al aplicar velocidad
                player.ApplySpeedMultiplier(durationSeconds, speedMultiplier);
                break;

            case PowerUpKind.SlowSpeed:
                player.ApplySpeedMultiplier(durationSeconds, Mathf.Max(0.05f, speedMultiplier < 1f ? speedMultiplier : 0.5f));
                break;

            case PowerUpKind.StaminaSlow:
                // El PlayerController ya enciende el icono al aplicar stamina lenta
                player.ApplyStaminaDrainModifier(durationSeconds, staminaDrainFactor);
                break;

            case PowerUpKind.ExtraLife:
                // Aquí sí encendemos el icono desde el Manager
                if (GameManager.Instance != null)
                    GameManager.Instance.GrantExtraLife(1);
                PowerupUIManager.Instance?.OnExtraLifeGained();
                break;
        }

        // Destruye el pickup tras aplicarse
        Destroy(gameObject);
    }
}
