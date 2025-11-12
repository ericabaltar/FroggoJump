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
                Debug.Log("hold detected"); // Feedback for the player to know if the movement will be tap or hold
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

