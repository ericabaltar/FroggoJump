using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PowerUp : MonoBehaviour
{
    public enum PowerUpType
    {
        Speed,          
        DoubleJump,     
        DoubleScore     
    }

    [Header("Config")]
    public PowerUpType type = PowerUpType.Speed;
    [Tooltip("Duración del efecto en segundos.")]
    public float duration = 6f;

    [Header("Velocidad")]
    [Tooltip("Multiplica la velocidad. Ej: 1.5 => 50% más rápido. Internamente reduce moveDuration.")]
    public float speedMultiplier = 1.5f;

    [Header("Visual/FX")]
    public bool rotateIdle = true;
    public float rotateSpeed = 90f; 

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        gameObject.tag = "PowerUp";
    }

    private void Update()
    {
        if (rotateIdle)
            transform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        switch (type)
        {
            case PowerUpType.Speed:
                player.ApplySpeedPowerup(duration, speedMultiplier);
                break;
            case PowerUpType.DoubleJump:
                player.ApplyDoubleJumpPowerup(duration);
                break;
            case PowerUpType.DoubleScore:
                GameManager.Instance?.ActivateScoreMultiplier(2f, duration);
                break;
        }

        Destroy(gameObject);
    }
}

