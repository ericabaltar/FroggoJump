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

    void Start()
    {
        // Posición inicial en la grid
        pos = new Vector2Int(0, 0);
        transform.position = new Vector3(0, 0.2f, 0);

        currentMoveDuration = baseMoveDuration;
        currentStamina = maxStamina;
    }

    void Update()
    {
        // Detect arrow key presses.
        
        Vector2Int inputDirection = Vector2Int.zero;
        // Single if/else don't want to move diagonally.
        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            inputDirection.y = 1;
        }
        else if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            inputDirection.y = -1;
        }
        else if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            inputDirection.x = -1;
        }
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            inputDirection.x = 1;
        }

        // If there was an input, save it
        if (inputDirection != Vector2Int.zero)
        {
            bufferedInput = inputDirection;
            bufferTimeLeft = inputBufferTime;
        }

        // Reduce buffer time left
        if (bufferTimeLeft > 0f)
        {
            bufferTimeLeft -= Time.deltaTime;

            // Remove the save input if enough time has passed
            if (bufferTimeLeft <= 0f)
                bufferedInput = Vector2Int.zero;
        }

        // If the user wants to move
        if (state == PlayerState.Ready && bufferedInput != Vector2Int.zero)
        {
            Vector2Int moveDirection = bufferedInput;

            TurnCharacter(moveDirection);

            Vector2Int destination = pos + moveDirection;
            if (GameManager.Instance.CheckIfAccessible(destination))
            {
                // Call coroutine to move the character object.
                StartCoroutine(MoveCharacter(destination));
            }
        }
    }

    private IEnumerator MoveCharacter(Vector2Int destination)
    {
        state = PlayerState.Moving;
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
            float percent = elapsedTime / moveDuration;

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

        // Actualizar coordenadas grid
        pos = destination;
        GameManager.Instance.UpdateFarthestDistance(destination.y);

        // --- LÓGICA DE MUERTE: solo Road sin base ---
        bool isRoadRow = GameManager.Instance.IsRoadRow(destination.y);

        // Si es carretera y no hay base en esa casilla → muerte
        if (isRoadRow && !GameManager.Instance.HasBaseAt(pos))
        {
            Die();
            yield break;
        }

        DecreaseStamina();

        if (state == PlayerState.Moving)
        {
            state = PlayerState.Ready;
        }
    }

    private void Die()
    {
        if (state == PlayerState.Dead)
            return;

        state = PlayerState.Dead;
        Debug.Log("Has pisado carretera sin base. GAME OVER.");

    }

    void TurnCharacter(Vector2Int moveDirection)
    {
        if (moveDirection.y == 1)
        {
            transform.localRotation = Quaternion.identity;
        }
        else if (moveDirection.y == -1)
        {
            transform.localRotation = Quaternion.Euler(0, 180, 0);
        }
        else if (moveDirection.x == 1)
        {
            transform.localRotation = Quaternion.Euler(0, 90, 0);
        }
        else if (moveDirection.x == -1)
        {
            transform.localRotation = Quaternion.Euler(0, -90, 0);
        }
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
                currentMoveDuration = baseMoveDuration * 5;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Fly"))
        {
            IncreaseStamina();
            Destroy(other.gameObject);
        }
    }
}


