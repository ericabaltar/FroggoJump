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
        pos = new Vector2Int(0, 0);
        transform.position = new Vector3(0, 0.2f, 0);

        currentMoveDuration = baseMoveDuration;
        currentStamina = maxStamina;
    }

    void Update()
    {
  
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
                // Guardamos en buffer
                bufferedInput = inputDirection;
                bufferTimeLeft = inputBufferTime;
            }
        }

        // Reducir tiempo de buffer
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
        float elapsedTime = 0f;

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

            Vector3 newPos = Vector3.Lerp(startPos, endPos, percent);
            newPos.y = yHeight + (0.5f * Mathf.Sin(Mathf.PI * percent));
            transform.position = newPos;

            Vector3 rotation = transform.localRotation.eulerAngles;
            transform.localRotation = Quaternion.Euler(
                -5f * Mathf.PI * Mathf.Cos(Mathf.PI * percent),
                rotation.y,
                rotation.z
            );

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = endPos;
        transform.localRotation = startRotation;

        pos = destination;
        GameManager.Instance.UpdateFarthestDistance(destination.y);

        bool isRoadRow = GameManager.Instance.IsRoadRow(destination.y);

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

