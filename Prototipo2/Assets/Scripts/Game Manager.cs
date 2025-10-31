using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    [Header("Game objects")]
    [SerializeField] private Transform character;
    [SerializeField] private Transform characterModel;

    [Header("Game parameters")]
    [SerializeField] private float moveDuration = 0.2f;

    public int currentFarthestDistance = 0;

    enum GameState
    {
        Ready,
        Moving,
        Dead
    }
    private GameState gameState;
    private Vector2Int characterPos;

    void Awake()
    {
        // Initialise all the starting state.
        NewLevel();
    }

    private void NewLevel()
    {
        gameState = GameState.Ready;

        // Reset character position
        characterPos = new Vector2Int(0, -1);
        character.position = new Vector3(0, 0.2f, -1);
    }

    private bool InStartArea(Vector2Int location)
    {
        return true; // Movimiento libre para probar

        // Movement anywhere in the starting region is allowed.
        if ((location.y > -5) && (location.y < 0) && (location.x > -6) && (location.x < 6))
        {
            return true;
        }
        return false;
    }

    // Update is called once per frame
    void Update()
    {
        // Detect arrow key presses.
        if (gameState == GameState.Ready)
        {
            Vector2Int moveDirection = Vector2Int.zero;
            // Single if/else don't want to move diagonally.
            if (Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                character.localRotation = Quaternion.identity;
                moveDirection.y = 1;
            }
            else if (Keyboard.current.downArrowKey.wasPressedThisFrame)
            {
                character.localRotation = Quaternion.Euler(0, 180, 0);
                moveDirection.y = -1;
            }
            else if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
            {
                character.localRotation = Quaternion.Euler(0, -90, 0);
                moveDirection.x = -1;
            }
            else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
            {
                character.localRotation = Quaternion.Euler(0, 90, 0);
                moveDirection.x = 1;
            }

            // If the user wants to move
            if (moveDirection != Vector2Int.zero)
            {
                Vector2Int destination = characterPos + moveDirection;
                // In the start area there are no obstacles so you can move anywhere.
                if (InStartArea(destination))
                {
                    // Update our character grid coordinate.
                    characterPos = destination;
                    // Call coroutine to move the character object.
                    StartCoroutine(MoveCharacter());
                }
            }
        }
    }

    private IEnumerator MoveCharacter()
    {
        gameState = GameState.Moving;
        float elapsedTime = 0f;

        // The yHeight changes if we're on grass or road.
        float yHeight = 0.2f;

        Vector3 startPos = character.position;
        Vector3 endPos = new(characterPos.x, yHeight, characterPos.y);

        Quaternion startRotation = characterModel.localRotation;

        while (elapsedTime < moveDuration)
        {
            // How far through the animation are we.
            float percent = elapsedTime / moveDuration;

            // Update the character position
            Vector3 newPos = Vector3.Lerp(startPos, endPos, percent);
            // Make the character jump in an arc
            newPos.y = yHeight + (0.5f * Mathf.Sin(Mathf.PI * percent));
            character.position = newPos;

            // Update the model rotation
            Vector3 rotation = characterModel.localRotation.eulerAngles;
            characterModel.localRotation = Quaternion.Euler(-5f * Mathf.PI * Mathf.Cos(Mathf.PI * percent), rotation.y, rotation.z);

            // Update the elapsed time
            elapsedTime += Time.deltaTime;

            yield return null;
        }

        // Ensure we're at the end.
        character.position = endPos;
        characterModel.localRotation = startRotation;

        // Need to check we're still in moving at the end.
        // If we're dead we don't want to go back to ready.
        if (gameState == GameState.Moving)
        {
            gameState = GameState.Ready;
        }

        if (character.position.z > currentFarthestDistance)
            currentFarthestDistance = (int)character.position.z;
    }

    public int GetFarthestDistance()
    {
        return currentFarthestDistance;
    }
}
