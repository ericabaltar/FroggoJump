using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    [Header("Game objects")]
    [SerializeField] private Transform character;
    [SerializeField] private Transform characterModel;
    [SerializeField] private Transform terrainHolder;

    [Header("Terrain objects")]
    [SerializeField] private Grass grassPrefab;
    [SerializeField] private Road roadPrefab;

    [Header("Game parameters")]
    [SerializeField] private float moveDuration = 0.2f;
    [SerializeField] private int spawnDistance = 20;

    enum GameState
    {
        Ready,
        Moving,
        Dead
    }
    private GameState gameState;
    private Vector2Int characterPos;
    private int spawnLocation;
    private List<(float terrainHeight, HashSet<int> locations)> obstacles = new();

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

        // Remove all terrain
        obstacles.Clear();
        foreach (Transform child in terrainHolder)
        {
            Destroy(child.gameObject);
        }

        // Reset level, and regenerate
        spawnLocation = 0;
        for (int i = 0; i < spawnDistance; i++)
        {
            SpawnObstacle();
        }
    }

    private void SpawnObstacle()
    {
        // Spawn more roads the further we get, at 250 have 90% chance of a road.
        float roadProbability = Mathf.Lerp(0.5f, 0.9f, spawnLocation / 250f);

        if (Random.value < roadProbability)
        {
            // Create road with terrain height of 0.1f.
            Road road = Instantiate(roadPrefab, terrainHolder);
            obstacles.Add((0.1f, road.Init(spawnLocation)));
        }
        else
        {
            // Create grass with terrain height of 0.2f.
            Grass grass = Instantiate(grassPrefab, terrainHolder);
            obstacles.Add((0.2f, grass.Init(spawnLocation)));
        }

        // Update to the next free location
        spawnLocation++;
    }

    private bool InStartArea(Vector2Int location)
    {
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
                if (InStartArea(destination) || ((destination.y >= 0) && !obstacles[destination.y].locations.Contains(destination.x)))
                {
                    // Update our character grid coordinate.
                    characterPos = destination;
                    // Call coroutine to move the character object.
                    StartCoroutine(MoveCharacter());
                }

                // Spawn new obstacles if necessary
                while (obstacles.Count < (characterPos.y + spawnDistance))
                {
                    SpawnObstacle();
                }
            }
        }

        // Camera follow at (+2, 4, -3)
        Vector3 cameraPosition = new(character.position.x + 2, 4, character.position.z - 3);

        // Limit camera movement in x direction.
        // Only follow the character as it moves to -3 and +3.
        // The camera offset is +2 so that's -1 to +5 in the camera x position.
        cameraPosition.x = Mathf.Clamp(cameraPosition.x, -1, 5);

        Camera.main.transform.position = cameraPosition;
    }

    private IEnumerator MoveCharacter()
    {
        gameState = GameState.Moving;
        float elapsedTime = 0f;

        // The yHeight changes if we're on grass or road.
        float yHeight = 0.2f;
        if (characterPos.y >= 0)
        {
            yHeight = obstacles[characterPos.y].terrainHeight;
        }

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
    }
}