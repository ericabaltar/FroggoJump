using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveDuration = 0.2f;

    enum PlayerState
    {
        Ready,
        Moving,
        Dead
    }

    private PlayerState state;
    private Vector2Int pos;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Reset character position
        pos = new Vector2Int(0, 0);
        transform.position = new Vector3(0, 0.2f, 0);
    }

    // Update is called once per frame
    void Update()
    {
        // Detect arrow key presses.
        if (state == PlayerState.Ready)
        {
            Vector2Int moveDirection = Vector2Int.zero;
            // Single if/else don't want to move diagonally.
            if (Keyboard.current.upArrowKey.wasPressedThisFrame)
            {
                transform.localRotation = Quaternion.identity;
                moveDirection.y = 1;
            }
            else if (Keyboard.current.downArrowKey.wasPressedThisFrame)
            {
                transform.localRotation = Quaternion.Euler(0, 180, 0);
                moveDirection.y = -1;
            }
            else if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
            {
                transform.localRotation = Quaternion.Euler(0, -90, 0);
                moveDirection.x = -1;
            }
            else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
            {
                transform.localRotation = Quaternion.Euler(0, 90, 0);
                moveDirection.x = 1;
            }

            // If the user wants to move
            if (moveDirection != Vector2Int.zero)
            {
                Vector2Int destination = pos + moveDirection;
                // In the start area there are no obstacles so you can move anywhere.
                if (GameManager.Instance.CheckIfAccessible(destination))
                {
                    // Call coroutine to move the character object.
                    StartCoroutine(MoveCharacter(destination));
                }
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

        while (elapsedTime < moveDuration)
        {
            // How far through the animation are we.
            float percent = elapsedTime / moveDuration;

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

        // Need to check we're still in moving at the end.
        // If we're dead we don't want to go back to ready.
        if (state == PlayerState.Moving)
        {
            state = PlayerState.Ready;
        }
    }
}
