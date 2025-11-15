using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float baseMoveDuration = 0.2f;
    [SerializeField] private int maxStamina = 15;
    [SerializeField] private float inputBufferTime = 0.15f;
    [SerializeField] private float maxTimeBetweenSameInputs = 0.01f;
    [SerializeField] private float holdTime = 0.5f;

    private enum InputType { Tap, Hold }

    private Vector2Int bufferedInputDirection;
    private InputType bufferedInputType;
    private float bufferTimeLeft = 0f;

    struct InputInfo { public float timeHeld; public Vector2Int direction; }
    private Dictionary<Key, InputInfo> inputHeldTimes = new Dictionary<Key, InputInfo>();

    private enum PlayerState { Ready, Moving, Dead }
    private PlayerState state = PlayerState.Ready;

    private Vector2Int pos;

    float currentMoveDuration;
    int currentStamina;

    // --- Bases móviles (nenúfares) ---
    private bool isOnMovingBase = false;
    private MovingBase currentMovingBase = null;

    private Transform defaultParent;

    // --------- POWERUPS ---------
    // Velocidad
    private Coroutine speedCoro;
    private float originalBaseMoveDuration;

    // Doble salto (TAP = 2 casillas mientras dure)
    private bool doubleJumpActive = false;
    private Coroutine doubleJumpCoro;

    void Start()
    {
        pos = new Vector2Int(0, 0);
        transform.position = new Vector3(0, 0.2f, 0);

        currentMoveDuration = baseMoveDuration;
        originalBaseMoveDuration = baseMoveDuration;

        currentStamina = maxStamina;

        defaultParent = transform.parent;

        HudManager.Instance?.SetStaminaBar((float)currentStamina / maxStamina);
    }

    void Update()
    {
        HandleInput();

        // Si no hay stamina, no aceptamos saltos
        if (currentStamina <= 0)
        {
            bufferedInputDirection = Vector2Int.zero;
        }

        // Intento de movimiento
        if (state == PlayerState.Ready && bufferedInputDirection != Vector2Int.zero)
        {
            if (currentStamina <= 0) return;

            Vector2Int moveDirection = bufferedInputDirection;
            TurnCharacter(moveDirection);

            // TAP = 1 casilla (o 2 si doubleJumpActive); HOLD = 2 casillas
            Vector2Int destination =
                (bufferedInputType == InputType.Tap)
                ? pos + (doubleJumpActive ? moveDirection * 2 : moveDirection)
                : pos + moveDirection * 2;

            if (GameManager.Instance.CheckIfAccessible(destination))
            {
                StartCoroutine(MoveCharacter(destination));
            }
        }

        if (isOnMovingBase)
        {
            // Seguir X de la plataforma para mantener coherencia con la grid
            pos.x = Mathf.RoundToInt(transform.position.x);
        }
    }

    private IEnumerator MoveCharacter(Vector2Int destination)
    {
        state = PlayerState.Moving;

        // Suelta plataforma antes de saltar
        if (currentMovingBase != null)
        {
            transform.SetParent(defaultParent, true);
        }

        float elapsedTime = 0f;

        float yHeight = 0.2f;
        if (destination.y >= 0)
            yHeight = GameManager.Instance.GetTerrainHeight(destination.y);

        Vector3 startPos = transform.position;
        Vector3 endPos = new Vector3(destination.x, yHeight, destination.y);

        float startYaw = transform.localEulerAngles.y;

        while (elapsedTime < currentMoveDuration)
        {
            float percent = elapsedTime / currentMoveDuration;

            Vector3 newPos = Vector3.Lerp(startPos, endPos, percent);
            newPos.y = yHeight + (0.5f * Mathf.Sin(Mathf.PI * percent));
            transform.position = newPos;

            // Mantén solo yaw (sin pitch/roll)
            transform.localRotation = Quaternion.Euler(0f, startYaw, 0f);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = endPos;
        transform.localRotation = Quaternion.Euler(0f, startYaw, 0f);

        // Consumir stamina por salto
        DecreaseStamina(1);

        // Actualiza grid
        pos = destination;
        GameManager.Instance.UpdateFarthestDistance(destination.y);

        // Muerte solo Road sin base estática ni móvil
        bool isRoadRow = GameManager.Instance.IsRoadRow(destination.y);
        if (isRoadRow && !GameManager.Instance.HasBaseAt(pos) && !isOnMovingBase)
        {
            Die();
            yield break;
        }

        if (state == PlayerState.Moving)
        {
            state = PlayerState.Ready;

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
                Vector2Int currentTotalDirection = Vector2Int.zero;
                foreach (InputInfo infoo in inputHeldTimes.Values)
                    currentTotalDirection += infoo.direction;

                TurnCharacter(currentTotalDirection);
            }
        }

        if (inputHeldTimes.TryGetValue(key.keyCode, out InputInfo inputInfo))
        {
            inputInfo.timeHeld += Time.deltaTime;
            inputHeldTimes[key.keyCode] = inputInfo;

            float duration = 0f;
            Vector2Int totalMovementDirection = Vector2Int.zero;
            foreach (InputInfo info in inputHeldTimes.Values)
            {
                if (info.timeHeld > duration)
                    duration = info.timeHeld;

                totalMovementDirection += info.direction;
            }

            if (key.wasReleasedThisFrame)
            {
                inputHeldTimes.Clear();

                if (duration < holdTime)
                    AddInputToBuffer(totalMovementDirection, InputType.Tap);
                else
                    AddInputToBuffer(totalMovementDirection, InputType.Hold);
            }
        }
    }

    private void Die()
    {
        if (state == PlayerState.Dead) return;

        state = PlayerState.Dead;
        transform.SetParent(defaultParent, true);
        Debug.Log("Has pisado carretera sin base. GAME OVER.");
    }

    void TurnCharacter(Vector2Int moveDirection)
    {
        float yaw = Mathf.Atan2(moveDirection.x, moveDirection.y) * Mathf.Rad2Deg;
        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // ------- STAMINA -------
    private void SetStamina(int value)
    {
        currentStamina = Mathf.Clamp(value, 0, maxStamina);
        HudManager.Instance?.SetStaminaBar((float)currentStamina / maxStamina);
    }

    private void DecreaseStamina(int amount)
    {
        SetStamina(currentStamina - Mathf.Abs(amount));
        if (currentStamina <= 0)
        {
            // feedback opcional al quedarte sin stamina
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

    // --- Triggers: Fly y MovingBase ---
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Fly"))
        {
            var fly = other.GetComponent<Fly>();
            int amount = (fly != null) ? fly.staminaAmount : 1;
            AddStamina(amount);
            Destroy(other.gameObject);
        }
        else if (!isOnMovingBase && other.CompareTag("MovingBase"))
        {
            isOnMovingBase = true;
            currentMovingBase = other.GetComponent<MovingBase>();

            if (state == PlayerState.Ready && currentMovingBase != null)
                GetOnMovingPlatform(currentMovingBase);
        }
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

    // ================= POWERUPS (llamados por PowerUp.cs) =================

    /// <summary>
    /// Aumenta la velocidad reduciendo temporalmente el moveDuration.
    /// speedMultiplier > 1 ⇒ más rápido.
    /// </summary>
    public void ApplySpeedPowerup(float duration, float speedMultiplier)
    {
        if (speedMultiplier <= 0f) speedMultiplier = 1f;

        // Reducimos la duración por salto (más velocidad)
        float boosted = Mathf.Max(0.01f, originalBaseMoveDuration / speedMultiplier);

        if (speedCoro != null) StopCoroutine(speedCoro);
        speedCoro = StartCoroutine(SpeedPowerRoutine(duration, boosted));
    }

    private IEnumerator SpeedPowerRoutine(float duration, float boostedMoveDuration)
    {
        baseMoveDuration = boostedMoveDuration;
        currentMoveDuration = baseMoveDuration;

        yield return new WaitForSeconds(duration);

        baseMoveDuration = originalBaseMoveDuration;
        currentMoveDuration = baseMoveDuration;
        speedCoro = null;
    }

    /// <summary>
    /// Mientras está activo, los TAP saltan 2 casillas.
    /// </summary>
    public void ApplyDoubleJumpPowerup(float duration)
    {
        if (doubleJumpCoro != null) StopCoroutine(doubleJumpCoro);
        doubleJumpCoro = StartCoroutine(DoubleJumpRoutine(duration));
    }

    private IEnumerator DoubleJumpRoutine(float duration)
    {
        doubleJumpActive = true;
        yield return new WaitForSeconds(duration);
        doubleJumpActive = false;
        doubleJumpCoro = null;
    }
}
