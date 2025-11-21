using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float baseMoveDuration = 0.2f;
    [SerializeField] private float slowMoveDuration = 1f;
    [SerializeField] private int maxStamina = 15;
    [SerializeField] private float inputBufferTime = 0.15f;
    [SerializeField] private float holdTime = 0.5f;

    // -------- SFX PowerUp (común) --------
    [Header("SFX PowerUp (común)")]
    [SerializeField] private AudioClip powerupPickupClip;
    [SerializeField, Range(0f, 1f)] private float powerupPickupVolume = 0.9f;
    [SerializeField, Range(0f, 0.3f)] private float powerupPickupPitchJitter = 0.06f;
    [SerializeField] private float powerupPickupBasePitch = 1f;

    // -------- SFX Fly (moscas) --------
    [Header("SFX Fly (al comer mosca)")]
    [SerializeField] private AudioClip flyPickupClip;
    [SerializeField, Range(0f, 1f)] private float flyPickupVolume = 0.9f;
    [SerializeField, Range(0f, 0.3f)] private float flyPickupPitchJitter = 0.03f;
    [SerializeField] private float flyPickupBasePitch = 1f;

    // -------- Señales de movimiento para MovementSFX --------
    public event System.Action OnJumpStart;
    public event System.Action OnLanded;

    [Header("(Opcional) Referencia directa al MovementSFX (no se usa para llamadas directas)")]
    [SerializeField] private MovementSFX movementSFX;

    private enum InputType { Tap, Hold }
    private Vector2Int bufferedInputDirection;
    private InputType bufferedInputType;
    private float bufferTimeLeft = 0f;

    struct InputInfo { public float timeHeld; public Vector2Int direction; }
    private readonly Dictionary<Key, InputInfo> inputHeldTimes = new();

    private enum PlayerState { Ready, Moving, Dead }
    private PlayerState state = PlayerState.Ready;

    private Vector2Int pos;

    private enum MovementSpeed { Slow, Regular }
    MovementSpeed currentMovementSpeed = MovementSpeed.Regular;
    float currentMoveDuration;
    int currentStamina;

    // Plataformas móviles
    private bool isOnMovingBase = false;
    private MovingBase currentMovingBase = null;
    private Transform defaultParent;

    // --------- POWERUPS / EFECTOS ---------
    private float speedMultiplier = 1f;
    private float originalBaseMoveDuration;
    private Coroutine speedCoro;

    private float staminaDrainMultiplier = 1f;
    private float staminaResidue = 0f;
    private Coroutine staminaCoro;

    void Start()
    {
        pos = new Vector2Int(0, 0);
        transform.position = new Vector3(0, 0.2f, 0);

        originalBaseMoveDuration = baseMoveDuration;
        currentMoveDuration = baseMoveDuration;
        currentStamina = maxStamina;

        defaultParent = transform.parent;

        if (movementSFX == null) movementSFX = GetComponent<MovementSFX>();

        HudManager.Instance?.SetStaminaBar((float)currentStamina / maxStamina);
    }

    void Update()
    {
        HandleInput();

        if (state == PlayerState.Ready && bufferedInputDirection != Vector2Int.zero)
        {
            Vector2Int moveDirection = bufferedInputDirection;
            TurnCharacter(moveDirection);

            int tiles = (bufferedInputType == InputType.Tap) ? 1 : 2;
            Vector2Int destination = pos + moveDirection * tiles;

            if (GameManager.Instance.CheckIfAccessible(destination))
            {
                OnJumpStart?.Invoke();

                if (animator) animator.SetTrigger("JumpTrigger");
                StartCoroutine(MoveCharacter(destination));
            }
        }

        if (isOnMovingBase)
            pos.x = Mathf.RoundToInt(transform.position.x);
    }

    private IEnumerator MoveCharacter(Vector2Int destination)
    {
        transform.localScale = Vector3.one;

        DecreaseStamina(1);
        state = PlayerState.Moving;

        if (currentMovingBase != null)
            transform.SetParent(defaultParent, true);

        float elapsedTime = 0f;

        float yHeight = 0.2f;
        if (destination.y >= 0)
            yHeight = GameManager.Instance.GetTerrainHeight(destination.y);

        Vector3 startPos = transform.position;
        Vector3 endPos = new Vector3(destination.x, yHeight, destination.y);

        float startYaw = transform.localEulerAngles.y;

        float moveDuration = currentMoveDuration;
        while (elapsedTime < moveDuration)
        {
            float percent = elapsedTime / moveDuration;

            Vector3 newPos = Vector3.Lerp(startPos, endPos, percent);
            float arc = 0.5f;
            newPos.y = yHeight + (arc * Mathf.Sin(Mathf.PI * percent));
            transform.position = newPos;

            transform.localRotation = Quaternion.Euler(0f, startYaw, 0f);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = endPos;
        transform.localRotation = Quaternion.Euler(0f, startYaw, 0f);

        pos = destination;
        GameManager.Instance.UpdateFarthestDistance(destination.y);

        bool isRoadRow = GameManager.Instance.IsRoadRow(destination.y);
        if (isRoadRow && !GameManager.Instance.HasBaseAt(pos) && !isOnMovingBase)
        {
            Die();
            yield break;
        }

        if (state == PlayerState.Moving)
        {
            state = PlayerState.Ready;

            OnLanded?.Invoke();

            if (isOnMovingBase && currentMovingBase != null)
                GetOnMovingPlatform(currentMovingBase);
            else
                transform.SetParent(defaultParent, true);
        }
    }

    private void HandleInput()
    {
        DetectDirectionalInput(Keyboard.current.upArrowKey, Vector2Int.up);
        DetectDirectionalInput(Keyboard.current.downArrowKey, Vector2Int.down);
        DetectDirectionalInput(Keyboard.current.leftArrowKey, Vector2Int.left);
        DetectDirectionalInput(Keyboard.current.rightArrowKey, Vector2Int.right);

        if (bufferTimeLeft > 0f)
        {
            bufferTimeLeft -= Time.deltaTime;
            if (bufferTimeLeft <= 0f)
                bufferedInputDirection = Vector2Int.zero;
        }
    }

    void AddInputToBuffer(Vector2Int direction, InputType inputType)
    {
        bufferedInputDirection = direction;
        bufferedInputType = inputType;
        bufferTimeLeft = inputBufferTime;
    }

    void DetectDirectionalInput(KeyControl key, Vector2Int direction)
    {
        if (key == null) return;

        if (key.wasPressedThisFrame)
        {
            inputHeldTimes[key.keyCode] = new InputInfo { timeHeld = 0f, direction = direction };

            if (state == PlayerState.Ready)
            {
                Vector2Int total = Vector2Int.zero;
                foreach (var kv in inputHeldTimes) total += kv.Value.direction;
                TurnCharacter(total);
            }
        }

        if (inputHeldTimes.TryGetValue(key.keyCode, out InputInfo info))
        {
            info.timeHeld += Time.deltaTime;
            inputHeldTimes[key.keyCode] = info;

            float duration = 0f;
            Vector2Int totalDir = Vector2Int.zero;
            foreach (var kv in inputHeldTimes)
            {
                if (kv.Value.timeHeld > duration) duration = kv.Value.timeHeld;
                totalDir += kv.Value.direction;
            }

            if (duration < holdTime)
            {
                float t = duration / holdTime * 2f;
                float yScale = Mathf.Lerp(1f, 0.8f, t);
                transform.localScale = new Vector3(1f, yScale, 1f);
            }
            else
            {
                float vibrate = 0.825f + 0.025f * Mathf.Sin(Time.time * 20f);
                transform.localScale = new Vector3(1f, vibrate, 1f);
            }

            if (key.wasReleasedThisFrame)
            {
                inputHeldTimes.Clear();

                transform.localScale = Vector3.one;

                if (duration < holdTime)
                    AddInputToBuffer(totalDir, InputType.Tap);
                else
                    AddInputToBuffer(totalDir, InputType.Hold);
            }
        }
    }

    private void Die()
    {
        if (GameManager.Instance != null && GameManager.Instance.TryConsumeExtraLife())
        {
            state = PlayerState.Ready;
            transform.SetParent(defaultParent, true);
            return;
        }

        if (state == PlayerState.Dead) return;
        state = PlayerState.Dead;

        isOnMovingBase = false;
        currentMovingBase = null;
        transform.SetParent(defaultParent, true);

        bufferedInputDirection = Vector2Int.zero;
        StopAllCoroutines();

        var col = GetComponent<Collider>();
        if (col) col.enabled = false;

        var spotlight = DeathSpotlightController.Instance;
        if (spotlight != null)
        {
            spotlight.Play(this.transform, OnDeathFocusComplete);
        }
        else
        {
            Debug.Log("Has muerto. (DeathSpotlightController no encontrado)");
            OnDeathFocusComplete();
        }
    }

    private void OnDeathFocusComplete()
    {
        // Score final (usa el que tengas disponible)
        int finalScore = 0;
        if (GameManager.Instance != null)
        {
            // Si tienes un método de score mostrado, úsalo; si no, la distancia:
            finalScore = GameManager.Instance.GetFarthestDistance();
        }

        // Muestra la UI de Game Over (pausa se hace dentro del manager)
        if (GameOverUIManager.Instance != null)
        {
            GameOverUIManager.Instance.Show(finalScore);
        }
        else
        {
            Debug.LogWarning("GameOverUIManager no encontrado en la escena.");
        }
    }

    void TurnCharacter(Vector2Int moveDirection)
    {
        if (moveDirection == Vector2Int.zero) return;
        float yaw = Mathf.Atan2(moveDirection.x, moveDirection.y) * Mathf.Rad2Deg;
        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // ------- STAMINA -------
    private void SetStamina(int value)
    {
        currentStamina = Mathf.Clamp(value, 0, maxStamina);
        HudManager.Instance?.SetStaminaBar((float)currentStamina / maxStamina);

        if (currentStamina == 0)
            ChangeMovementSpeed(MovementSpeed.Slow);
        else
            ChangeMovementSpeed(MovementSpeed.Regular);
    }

    private void ChangeMovementSpeed(MovementSpeed newMovementSpeed)
    {
        if (currentMovementSpeed == newMovementSpeed) return;

        if (newMovementSpeed == MovementSpeed.Slow)
        {
            currentMoveDuration = slowMoveDuration;
            if (animator) animator.SetFloat("JumpSpeedMult", 0.2f);
        }
        else
        {
            currentMoveDuration = baseMoveDuration;
            if (animator) animator.SetFloat("JumpSpeedMult", 1f);
        }

        currentMovementSpeed = newMovementSpeed;
    }

    private void DecreaseStamina(int baseCost)
    {
        float effective = baseCost * Mathf.Max(0.05f, staminaDrainMultiplier);
        staminaResidue += effective;

        int spend = Mathf.FloorToInt(staminaResidue);
        if (spend > 0)
        {
            staminaResidue -= spend;
            SetStamina(currentStamina - spend);
        }
    }

    private void AddStamina(int amount)
    {
        if (amount <= 0) return;
        SetStamina(currentStamina + amount);
    }

    void GetOnMovingPlatform(MovingBase movingBase)
    {
        isOnMovingBase = true;
        currentMovingBase = movingBase;
        transform.SetParent(currentMovingBase.transform, true);
        transform.position = currentMovingBase.transform.position;
    }

    // --- Triggers: Fly / MovingBase / PowerUp ---
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Fly"))
        {
            var fly = other.GetComponent<Fly>();
            int amount = (fly != null) ? fly.staminaAmount : 1;
            AddStamina(amount);

            PlayFlyPickupSfx();

            Destroy(other.gameObject);
            return;
        }

        if (other.CompareTag("PowerUp") || other.GetComponent<PowerUp>() != null)
        {
            var pu = other.GetComponent<PowerUp>() ?? other.GetComponentInParent<PowerUp>();
            if (pu != null)
            {
                ApplyPowerUp(pu);
                PlayPowerupPickupSfx();
                Destroy(pu.gameObject);
            }
            return;
        }

        if (!isOnMovingBase && other.CompareTag("MovingBase"))
        {
            isOnMovingBase = true;
            currentMovingBase = other.GetComponent<MovingBase>();

            if (state == PlayerState.Ready && currentMovingBase != null)
                GetOnMovingPlatform(currentMovingBase);
        }
    }

    // ================== APLICACIÓN POWERUPS DESDE PLAYER ==================
    private void ApplyPowerUp(PowerUp pu)
    {
        switch (pu.kind)
        {
            case PowerUpKind.FastSpeed:
                ApplySpeedMultiplier(GetPuDuration(pu), Mathf.Max(1.01f, GetPuSpeedMult(pu)));
                PowerupUIManager.Instance?.ActivateTimed(PowerupType.FastSpeed, GetPuDuration(pu));
                break;

            case PowerUpKind.SlowSpeed:
                {
                    float mult = GetPuSpeedMult(pu) < 1f ? Mathf.Clamp(GetPuSpeedMult(pu), 0.05f, 0.99f) : 0.5f;
                    ApplySpeedMultiplier(GetPuDuration(pu), mult);
                    PowerupUIManager.Instance?.ActivateTimed(PowerupType.SlowSpeed, GetPuDuration(pu));
                    break;
                }

            case PowerUpKind.StaminaSlow:
                ApplyStaminaDrainModifier(GetPuDuration(pu), GetPuStaminaFactor(pu));
                PowerupUIManager.Instance?.ActivateTimed(PowerupType.StaminaSlow, GetPuDuration(pu));
                break;

            case PowerUpKind.ExtraLife:
                GameManager.Instance?.GrantExtraLife(1);
                PowerupUIManager.Instance?.OnExtraLifeGained();
                break;
        }
    }

    private float GetPuDuration(PowerUp pu) =>
        (float)pu.GetType().GetField("durationSeconds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(pu);

    private float GetPuSpeedMult(PowerUp pu) =>
        (float)pu.GetType().GetField("speedMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(pu);

    private float GetPuStaminaFactor(PowerUp pu) =>
        (float)pu.GetType().GetField("staminaDrainFactor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(pu);

    // ================== SFX PICKUPS ==================
    private void PlayPowerupPickupSfx()
    {
        if (powerupPickupClip == null) return;

        float pitch = Mathf.Clamp(powerupPickupBasePitch + Random.Range(-powerupPickupPitchJitter, powerupPickupPitchJitter), 0.1f, 3f);

        if (SfxManager.Instance != null)
        {
            SfxManager.Instance.PlayOneShot(powerupPickupClip, powerupPickupVolume, powerupPickupPitchJitter, powerupPickupBasePitch);
            return;
        }

        var pos = Camera.main ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(powerupPickupClip, pos, powerupPickupVolume);
    }

    private void PlayFlyPickupSfx()
    {
        if (flyPickupClip == null) return;

        float pitch = Mathf.Clamp(flyPickupBasePitch + Random.Range(-flyPickupPitchJitter, flyPickupPitchJitter), 0.1f, 3f);

        if (SfxManager.Instance != null)
        {
            SfxManager.Instance.PlayOneShot(flyPickupClip, flyPickupVolume, flyPickupPitchJitter, flyPickupBasePitch);
            return;
        }

        var pos = Camera.main ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(flyPickupClip, pos, flyPickupVolume);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("MovingBase"))
        {
            if (other.GetComponent<MovingBase>() == currentMovingBase)
            {
                isOnMovingBase = false;
                currentMovingBase = null;
                transform.SetParent(defaultParent, true);
            }
        }
    }

    // ====== MÉTODOS POWERUPS ======
    public void ApplySpeedMultiplier(float duration, float multiplier)
    {
        if (speedCoro != null) StopCoroutine(speedCoro);
        speedCoro = StartCoroutine(SpeedRoutine(duration, Mathf.Max(0.05f, multiplier)));
    }

    private IEnumerator SpeedRoutine(float duration, float multiplier)
    {
        speedMultiplier = multiplier;
        baseMoveDuration = originalBaseMoveDuration / speedMultiplier;
        currentMoveDuration = baseMoveDuration;

        yield return new WaitForSeconds(duration);

        speedMultiplier = 1f;
        baseMoveDuration = originalBaseMoveDuration;
        currentMoveDuration = baseMoveDuration;
        speedCoro = null;
    }

    public void ApplyStaminaDrainModifier(float duration, float factor)
    {
        factor = Mathf.Clamp(factor, 0.05f, 1f);
        if (staminaCoro != null) StopCoroutine(staminaCoro);
        staminaCoro = StartCoroutine(StaminaDrainRoutine(duration, factor));
    }

    private IEnumerator StaminaDrainRoutine(float duration, float factor)
    {
        staminaDrainMultiplier = factor;
        yield return new WaitForSeconds(duration);
        staminaDrainMultiplier = 1f;
        staminaCoro = null;
    }
}
