using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float baseMoveDuration = 0.2f;
    [SerializeField] private int maxStamina = 15;
    [SerializeField] private float inputBufferTime = 0.01f;

    private Vector2Int bufferedInput;
    private float bufferTimeLeft = 0f;

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

    // --- Bases móviles (nenúfares) ---
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
        // --- INPUT CON DIAGONALES + BUFFER ---
        bool anyDirPressedThisFrame =
            Keyboard.current.upArrowKey.wasPressedThisFrame ||
            Keyboard.current.downArrowKey.wasPressedThisFrame ||
            Keyboard.current.leftArrowKey.wasPressedThisFrame ||
            Keyboard.current.rightArrowKey.wasPressedThisFrame;

        if (anyDirPressedThisFrame)
        {
            Vector2Int inputDirection = Vector2Int.zero;

            if (Keyboard.current.upArrowKey.isPressed) inputDirection.y += 1;
            if (Keyboard.current.downArrowKey.isPressed) inputDirection.y -= 1;
            if (Keyboard.current.rightArrowKey.isPressed) inputDirection.x += 1;
            if (Keyboard.current.leftArrowKey.isPressed) inputDirection.x -= 1;

            inputDirection.x = Mathf.Clamp(inputDirection.x, -1, 1);
            inputDirection.y = Mathf.Clamp(inputDirection.y, -1, 1);

            if (inputDirection != Vector2Int.zero)
            {
                bufferedInput = inputDirection;
                bufferTimeLeft = inputBufferTime;
            }
        }

        if (bufferTimeLeft > 0f)
        {
            bufferTimeLeft -= Time.deltaTime;
            if (bufferTimeLeft <= 0f)
                bufferedInput = Vector2Int.zero;
        }

        if (state == PlayerState.Ready && bufferedInput != Vector2Int.zero)
        {
            Vector2Int moveDirection = bufferedInput;

            TurnCharacter(moveDirection);

            Vector2Int destination = pos + moveDirection;
            if (GameManager.Instance.CheckIfAccessible(destination))
            {
                StartCoroutine(MoveCharacter(destination));
            }
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

        Quaternion startRotation = transform.localRotation;

        while (elapsedTime < currentMoveDuration)
        {
            float percent = elapsedTime / currentMoveDuration;

            // Interpolación + arco de salto
            Vector3 newPos = Vector3.Lerp(startPos, endPos, percent);
            newPos.y = yHeight + (0.5f * Mathf.Sin(Mathf.PI * percent));
            transform.position = newPos;

            // Pequeño bamboleo en X
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

        // Actualizar coordenadas grid
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
                transform.SetParent(currentMovingBase.transform, true); // conservar world pos
            }
            else
            {
                transform.SetParent(defaultParent, true);
            }
        }
    }

    private void Die()
    {
        if (state == PlayerState.Dead)
            return;

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
    }

    // --- Triggers: Fly y MovingBase ---
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Fly"))
        {
            IncreaseStamina();
            Destroy(other.gameObject);
        }
        else if (other.CompareTag("MovingBase"))
        {
            isOnMovingBase = true;
            currentMovingBase = other.GetComponent<MovingBase>();

            // Solo nos subimos si no estamos en mitad de un salto
            if (state == PlayerState.Ready && currentMovingBase != null)
            {
                transform.SetParent(currentMovingBase.transform, true);
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("MovingBase"))
        {
            isOnMovingBase = true;

            if (currentMovingBase == null)
                currentMovingBase = other.GetComponent<MovingBase>();

            if (state == PlayerState.Ready && currentMovingBase != null && transform.parent != currentMovingBase.transform)
            {
                transform.SetParent(currentMovingBase.transform, true);
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
}
