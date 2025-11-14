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

    struct InputInfo
    {
        public float timeHeld;
        public Vector2Int direction;
    }
    private Dictionary<Key, InputInfo> inputHeldTimes = new Dictionary<Key, InputInfo>();

    private enum PlayerState { Ready, Moving, Dead }
    private PlayerState state = PlayerState.Ready;

    private Vector2Int pos;

    float currentMoveDuration;
    int currentStamina;

    // --- Bases móviles (nenúfares) ---
    private bool isOnMovingBase = false;
    private MovingBase currentMovingBase = null;

    // Guardamos el padre previo para restaurarlo si hace falta
    private Transform defaultParent;

    // --- POWERUPS ---
    private bool doubleJumpActive = false;
    private Coroutine speedCoro;
    private float speedOriginalMoveDuration;

    void Start()
    {
        // Posición inicial en la grid
        pos = new Vector2Int(0, 0);
        transform.position = new Vector3(0, 0.2f, 0);

        currentMoveDuration = baseMoveDuration;
        currentStamina = maxStamina;

        speedOriginalMoveDuration = baseMoveDuration;

        defaultParent = transform.parent;
    }

    void Update()
    {
        HandleInput();

        // Si el jugador quiere moverse
        if (state == PlayerState.Ready && bufferedInputDirection != Vector2Int.zero)
        {
            Vector2Int moveDirection = bufferedInputDirection;
            TurnCharacter(moveDirection);

            // TAP = 1 casilla (o 2 si doubleJump activo); HOLD = 2 casillas
            Vector2Int destination;
            if (bufferedInputType == InputType.Tap)
                destination = pos + (doubleJumpActive ? moveDirection * 2 : moveDirection);
            else
                destination = pos + moveDirection * 2;

            if (GameManager.Instance.CheckIfAccessible(destination))
            {
                StartCoroutine(MoveCharacter(destination));
            }
        }

        if (isOnMovingBase)
        {
            // Actualiza pos.x con la plataforma (por si se redondea entre frames)
            pos.x = Mathf.RoundToInt(transform.position.x);
        }
    }

    private IEnumerator MoveCharacter(Vector2Int destination)
    {
        state = PlayerState.Moving;

        // Si estamos subidos a una base móvil, soltamos antes de saltar
        if (currentMovingBase != null)
        {
            transform.SetParent(defaultParent, true); // mantener world pos
        }

        float elapsedTime = 0f;

        // Altura según el tipo de terreno de la fila destino
        float yHeight = 0.2f;
        if (destination.y >= 0)
        {
            yHeight = GameManager.Instance.GetTerrainHeight(destination.y);
        }

        Vector3 startPos = transform.position;
        Vector3 endPos = new Vector3(destination.x, yHeight, destination.y);

        // Conserva yaw, sin inclinaciones raras
        float startYaw = transform.localEulerAngles.y;

        while (elapsedTime < currentMoveDuration)
        {
            float percent = elapsedTime / currentMoveDuration;

            // Interpolación + arco de salto
            Vector3 newPos = Vector3.Lerp(startPos, endPos, percent);
            newPos.y = yHeight + (0.5f * Mathf.Sin(Mathf.PI * percent));
            transform.position = newPos;

            // Mantener solo yaw (sin pitch/roll)
            transform.localRotation = Quaternion.Euler(0f, startYaw, 0f);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Aseguramos posición final
        transform.position = endPos;
        transform.localRotation = Quaternion.Euler(0f, startYaw, 0f);

        // Actualizamos coordenadas grid
        pos = destination;
        GameManager.Instance.UpdateFarthestDistance(destination.y);

        // --- LÓGICA DE MUERTE: solo Road sin base estática y sin base móvil ---
        bool isRoadRow = GameManager.Instance.IsRoadRow(destination.y);

        if (isRoadRow && !GameManager.Instance.HasBaseAt(pos) && !isOnMovingBase)
        {
            Die();
            yield break;
        }

        DecreaseStamina();

        if (state == PlayerState.Moving)
        {
            state = PlayerState.Ready;

            // Si hemos aterrizado encima de una base móvil (trigger activo),
            // nos volvemos a subir (parentar) para movernos juntos.
            if (isOnMovingBase && currentMovingBase != null)
            {
                GetOnMovingPlatform(currentMovingBase);
            }
            else
            {
                transform.SetParent(defaultParent, true);
            }
        }
    }

    private void HandleInput()
    {
        // Detect arrow key presses.
        DetectDirectionalInput(Keyboard.current.upArrowKey, Vector2Int.up);
        DetectDirectionalInput(Keyboard.current.downArrowKey, Vector2Int.down);
        DetectDirectionalInput(Keyboard.current.leftArrowKey, Vector2Int.left);
        DetectDirectionalInput(Keyboard.current.rightArrowKey, Vector2Int.right);

        // Reduce buffer time left
        if (bufferTimeLeft > 0f)
        {
            bufferTimeLeft -= Time.deltaTime;

            // Remove the saved input if enough time has passed
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
            InputInfo info = new InputInfo { timeHeld = 0f, direction = direction };
            inputHeldTimes[key.keyCode] = info;

            if (state == PlayerState.Ready)
            {
                Vector2Int currentTotalDirection = Vector2Int.zero;
                foreach (InputInfo infoo in inputHeldTimes.Values)
                    currentTotalDirection += infoo.direction;

                TurnCharacter(currentTotalDirection);
            }
        }

        // If the key is being held (exists in the dictionary)
        if (inputHeldTimes.TryGetValue(key.keyCode, out InputInfo inputInfo))
        {
            // Update time held
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

            // When the key is released, remove it from the dictionary and add the input to the buffer
            if (key.wasReleasedThisFrame)
            {
                inputHeldTimes.Clear();

                if (duration < holdTime)
                {
                    AddInputToBuffer(totalMovementDirection, InputType.Tap);
                }
                else
                {
                    AddInputToBuffer(totalMovementDirection, InputType.Hold);
                }
            }
        }
    }

    private void Die()
    {
        if (state == PlayerState.Dead) return;

        state = PlayerState.Dead;
        transform.SetParent(defaultParent, true); // por si estábamos parentados
        Debug.Log("Has pisado carretera sin base. GAME OVER.");
    }

    void TurnCharacter(Vector2Int moveDirection)
    {
        // Rotación hacia la dirección real (incluye diagonales)
        float yaw = Mathf.Atan2(moveDirection.x, moveDirection.y) * Mathf.Rad2Deg;
        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    void IncreaseStamina()
    {
        currentStamina = maxStamina;
        currentMoveDuration = baseMoveDuration;
        HudManager.Instance.SetStaminaBar((float)currentStamina / maxStamina);
    }

    void DecreaseStamina()
    {
        if (currentStamina > 0)
        {
            currentStamina -= 1;

            if (currentStamina == 0)
            {
                currentMoveDuration = baseMoveDuration * 5f;
            }
        }

        HudManager.Instance.SetStaminaBar((float)currentStamina / maxStamina);
    }

    void GetOnMovingPlatform(MovingBase movingBase)
    {
        isOnMovingBase = true;
        currentMovingBase = movingBase;
        transform.SetParent(currentMovingBase.transform, true);
        transform.position = new Vector3(
            currentMovingBase.transform.position.x,
            currentMovingBase.transform.position.y,
            currentMovingBase.transform.position.z
        );
    }

    // --- Triggers: Fly y MovingBase ---
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Fly"))
        {
            IncreaseStamina();
            Destroy(other.gameObject);
        }
        else if (!isOnMovingBase && other.CompareTag("MovingBase"))
        {
            isOnMovingBase = true;
            currentMovingBase = other.GetComponent<MovingBase>();

            // Solo nos subimos si no estamos en mitad de un salto
            if (state == PlayerState.Ready && currentMovingBase != null)
            {
                GetOnMovingPlatform(currentMovingBase);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("MovingBase"))
        {
            // Si sales de la base móvil, deja de ser su hijo
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
    /// </summary>
    /// <param name="duration">Duración del efecto (segundos)</param>
    /// <param name="speedMultiplier">>1 para ir más rápido (p.ej. 1.5)</param>
    public void ApplySpeedPowerup(float duration, float speedMultiplier)
    {
        // velocidad ↑ ⇒ moveDuration ↓
        float newDuration = Mathf.Max(0.01f, baseMoveDuration / Mathf.Max(0.01f, speedMultiplier));

        if (speedCoro != null) StopCoroutine(speedCoro);
        speedCoro = StartCoroutine(SpeedPowerRoutine(duration, newDuration));
    }

    private IEnumerator SpeedPowerRoutine(float duration, float boostedMoveDuration)
    {
        float prevBase = baseMoveDuration;

        baseMoveDuration = boostedMoveDuration;
        currentMoveDuration = baseMoveDuration; // aplica ya si estamos en Ready

        yield return new WaitForSeconds(duration);

        baseMoveDuration = speedOriginalMoveDuration;
        currentMoveDuration = baseMoveDuration;
        speedCoro = null;
    }

    /// <summary>
    /// Mientras está activo, los TAP saltan 2 casillas.
    /// </summary>
    public void ApplyDoubleJumpPowerup(float duration)
    {
        StopCoroutine(nameof(DoubleJumpRoutine));
        StartCoroutine(DoubleJumpRoutine(duration));
    }

    private IEnumerator DoubleJumpRoutine(float duration)
    {
        doubleJumpActive = true;
        yield return new WaitForSeconds(duration);
        doubleJumpActive = false;
    }
}
