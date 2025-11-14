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

    private enum PlayerState
    {
        Ready,
        Moving,
        Dead
    }

    private PlayerState state = PlayerState.Ready;
    private Vector2Int pos;

    float currentMoveDuration;
    int currentStamina;

    // --- Bases m�viles (nen�fares) ---
    private bool isOnMovingBase = false;
    private MovingBase currentMovingBase = null;

    // Guardamos el padre previo para restaurarlo si hace falta
    private Transform defaultParent;

    void Start()
    {
        // Posición inicial en la grid
        pos = new Vector2Int(0, 0);
        transform.position = new Vector3(0, 0.2f, 0);

        currentMoveDuration = baseMoveDuration;
        currentStamina = maxStamina;

        defaultParent = transform.parent;
    }

    void Update()
    {
        HandleInput();

        // If the user wants to move
        if (state == PlayerState.Ready && bufferedInputDirection != Vector2Int.zero)
        {
            Vector2Int moveDirection = bufferedInputDirection;

            TurnCharacter(moveDirection);

            // Move one or two tiles
            Vector2Int destination;
            if (bufferedInputType == InputType.Tap)
                destination = pos + moveDirection;
            else
                destination = pos + moveDirection * 2;

            if (GameManager.Instance.CheckIfAccessible(destination))
            {
                animator.SetTrigger("JumpTrigger");
                StartCoroutine(MoveCharacter(destination));
            }
        }

        if (isOnMovingBase)
        {
            // Update grid positon
            pos.x = Mathf.RoundToInt(transform.position.x);
        }
    }

    private IEnumerator MoveCharacter(Vector2Int destination)
    {
        state = PlayerState.Moving;

        // Si estamos subidos a una base m�vil, soltamos antes de saltar
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

        Quaternion startRotation = transform.localRotation;

        while (elapsedTime < currentMoveDuration)
        {
            float percent = elapsedTime / baseMoveDuration;

            // Interpolación + arco de salto
            Vector3 newPos = Vector3.Lerp(startPos, endPos, percent);
            newPos.y = yHeight + (0.5f * Mathf.Sin(Mathf.PI * percent));
            transform.position = newPos;

            // Pequeño bamboleo en X (como ya tenías)
            Vector3 rotation = transform.localRotation.eulerAngles;
            transform.localRotation = Quaternion.Euler(
                -5f * Mathf.PI * Mathf.Cos(Mathf.PI * percent),
                rotation.y,
                rotation.z
            );

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Aseguramos posición final
        transform.position = endPos;
        transform.localRotation = startRotation;

        pos = destination;
        GameManager.Instance.UpdateFarthestDistance(destination.y);

        // --- L�GICA DE MUERTE: solo Road sin base est�tica y sin base m�vil ---
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

            // Si hemos aterrizado encima de una base m�vil (trigger activo),
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

            // Remove the save input if enough time has passed
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
        if (key.wasPressedThisFrame)
        {
            InputInfo info = new InputInfo
            {
                timeHeld = 0f,
                direction = direction
            };

            // Add key to dictionary when pressed
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
                // Get duration of the current pressed key that was held longer
                if (info.timeHeld > duration)
                    duration = info.timeHeld;

                // Get total movement direction
                totalMovementDirection += info.direction;
            }

            if (duration > holdTime)
            {
                // Feedback for the player to know if the movement will be tap or hold
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
        if (state == PlayerState.Dead)
            return;

        state = PlayerState.Dead;
        transform.SetParent(defaultParent, true); // por si est�bamos parentados
        Debug.Log("Has pisado carretera sin base. GAME OVER.");
    }

    void TurnCharacter(Vector2Int moveDirection)
    {
        // Rotaci�n hacia la direcci�n real (incluye diagonales)
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

    void GetOnMovingPlatform(MovingBase currentMovingBase)
    {
        isOnMovingBase = true;
        transform.SetParent(currentMovingBase.transform, true);
        transform.position = new Vector3(currentMovingBase.transform.position.x, currentMovingBase.transform.position.y, currentMovingBase.transform.position.z);
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
            // Si sales de la base m�vil, deja de ser su hijo
            if (other.GetComponent<MovingBase>() == currentMovingBase)
            {
                isOnMovingBase = false;
                currentMovingBase = null;
                transform.SetParent(defaultParent, true);
            }
        }
    }
}
