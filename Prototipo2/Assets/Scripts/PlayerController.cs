using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float baseMoveDuration = 0.2f;
    [SerializeField] private int maxStamina = 15;
    [SerializeField] private float inputBufferTime = 0.01f;

    private Vector2Int bufferedInput;
    private float bufferTimeLeft = 0f;

    enum PlayerState
    {
        Ready,
        Moving,
        Dead
    }

    private PlayerState state;
    private Vector2Int pos;

    float currentMoveDuration;
    int currentStamina;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Reset character position
        pos = new Vector2Int(0, 0);
        transform.position = new Vector3(0, 0.2f, 0);

        currentMoveDuration = baseMoveDuration;
        currentStamina = maxStamina;
    }

    // Update is called once per frame
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

        // The yHeight changes if we're on grass or road.
        float yHeight = 0.2f;

        Vector3 startPos = transform.position;
        Vector3 endPos = new(destination.x, yHeight, destination.y);

        Quaternion startRotation = transform.localRotation;

        while (elapsedTime < currentMoveDuration)
        {
            // How far through the animation are we.
            float percent = elapsedTime / currentMoveDuration;

            // Update the character position
            Vector3 newPos = Vector3.Lerp(startPos, endPos, percent);
            // Make the character jump in an arc
            newPos.y = yHeight + (0.5f * Mathf.Sin(Mathf.PI * percent));
            transform.position = newPos;

            // Update the model rotation
            Vector3 rotation = transform.localRotation.eulerAngles;
            transform.localRotation = Quaternion.Euler(-5f * Mathf.PI * Mathf.Cos(Mathf.PI * percent), rotation.y, rotation.z);

            // Update the elapsed time
            elapsedTime += Time.deltaTime;

            yield return null;
        }

        // Ensure we're at the end.
        transform.position = endPos;
        transform.localRotation = startRotation;

        // Update our character grid coordinate.
        pos = destination;
        GameManager.Instance.UpdateFarthestDistance(destination.y);

        DecreaseStamina();

        // Need to check we're still in moving at the end.
        // If we're dead we don't want to go back to ready.
        if (state == PlayerState.Moving)
        {
            state = PlayerState.Ready;
        }
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
