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
    [SerializeField] private int spawnDistance = 20;

    private int spawnLocation;
    private List<(float terrainHeight, HashSet<int> locations)> obstacles = new();
    private int currentFarthestDistance = 0;
    public static GameManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Initialise all the starting state.
        NewLevel();
    }
    private void NewLevel()
    {
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

    public void UpdateFarthestDistance(int distance)
    {
        if (distance > currentFarthestDistance)
        {
            currentFarthestDistance = distance;

            // Spawn new obstacles if necessary
            while (obstacles.Count < (distance + spawnDistance))
            {
                SpawnObstacle();
            }
        }
    }

    public int GetFarthestDistance()
    {
        return currentFarthestDistance;
    }

    public bool CheckIfAccessible(Vector2Int pos)
    {
        return pos.y >= 0 && !obstacles[pos.y].locations.Contains(pos.x);
    }
}