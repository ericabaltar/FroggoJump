using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveDuration = 0.2f;

    private enum PlayerState
    {
        Ready,
        Moving,
        Dead
    }

    private PlayerState state = PlayerState.Ready;
    private Vector2Int pos;

    void Start()
    {
        // Posición inicial en la grid
        pos = new Vector2Int(0, 0);
        transform.position = new Vector3(0, 0.2f, 0);
        state = PlayerState.Ready;
    }

    void Update()
    {
        if (state != PlayerState.Ready)
            return;

        // --- INPUT CON DIAGONALES ---
        Vector2Int moveDirection = Vector2Int.zero;

        // Ahora NO usamos if/else if, sino if independientes
        if (Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            moveDirection.y += 1;
        }
        if (Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            moveDirection.y -= 1;
        }
        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            moveDirection.x -= 1;
        }
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            moveDirection.x += 1;
        }

        // Por si acaso, clamp a [-1, 1]
        moveDirection.x = Mathf.Clamp(moveDirection.x, -1, 1);
        moveDirection.y = Mathf.Clamp(moveDirection.y, -1, 1);

        // Si no se ha pulsado nada este frame, no nos movemos
        if (moveDirection == Vector2Int.zero)
            return;

        // Rotar al jugador hacia la dirección del movimiento (incluye diagonales)
        // Eje z = adelante (usamos y del grid), eje x = lateral
        float yaw = Mathf.Atan2(moveDirection.x, moveDirection.y) * Mathf.Rad2Deg;
        transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        Vector2Int destination = pos + moveDirection;

        // Comprobar que la casilla destino no está bloqueada
        if (GameManager.Instance.CheckIfAccessible(destination))
        {
            StartCoroutine(MoveCharacter(destination));
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

        while (elapsedTime < moveDuration)
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
}


