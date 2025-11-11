using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Game objects")]
    [SerializeField] private Transform character;
    [SerializeField] private Transform characterModel;
    [SerializeField] private Transform terrainHolder;

    [Header("Terrain objects")]
    [SerializeField] private Grass grassPrefab;
    [SerializeField] private Road roadPrefab;

    [Header("Base objects")]
    [SerializeField] private Transform basePrefab;

    [Header("Game parameters")]
    [SerializeField] private int spawnDistance = 20;

    [Header("Base / camino seguro")]
    [SerializeField] private int minX = -5;
    [SerializeField] private int maxX = 5;
    [SerializeField] private int minBasesPerRow = 1;
    [SerializeField] private int maxBasesPerRow = 3;

    private int spawnLocation;

    private List<(bool isRoad, float terrainHeight, HashSet<int> locations)> obstacles = new();

    // Por cada fila (z), qué x tienen base (en Grass estará vacío)
    private List<HashSet<int>> baseLocations = new();

    private int currentFarthestDistance = 0;
    public static GameManager Instance { get; private set; }

    // X del camino seguro que serpentea
    private int currentPathX;
    private bool pathInitialized = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        NewLevel();
    }

    private void NewLevel()
    {
        obstacles.Clear();
        baseLocations.Clear();

        foreach (Transform child in terrainHolder)
        {
            Destroy(child.gameObject);
        }

        spawnLocation = 0;
        currentFarthestDistance = 0;

        // Camino empieza en 0 (clamp por si minX/maxX cambian)
        currentPathX = Mathf.Clamp(0, minX, maxX);
        pathInitialized = true;

        for (int i = 0; i < spawnDistance; i++)
        {
            SpawnObstacle();
        }
    }

    private void SpawnObstacle()
    {
        // Camino seguro: nueva X objetivo (serpentea -1, 0, +1)
        int desiredPathX = currentPathX;
        if (pathInitialized)
        {
            int delta = Random.Range(-1, 2); // -1, 0, 1
            desiredPathX = Mathf.Clamp(currentPathX + delta, minX, maxX);
        }
        else
        {
            desiredPathX = Mathf.Clamp(0, minX, maxX);
            pathInitialized = true;
        }

        // Probabilidad de Road
        float roadProbability = Mathf.Lerp(0.5f, 0.9f, spawnLocation / 250f);

        float terrainHeight;
        HashSet<int> obstaclePositions;

        if (Random.value < roadProbability)
        {
            // -------- FILA ROAD --------
            Road road = Instantiate(roadPrefab, terrainHolder);
            obstaclePositions = road.Init(spawnLocation);   // normalmente solo -6 y 6
            terrainHeight = 0.1f;

            // Aseguramos que la X de camino no esté bloqueada
            int safePathX = FindNearestFreeX(desiredPathX, obstaclePositions);
            currentPathX = safePathX;

            obstacles.Add((true, terrainHeight, obstaclePositions));

            // Generar bases (nenúfares) siguiendo el camino seguro
            HashSet<int> basesThisRow = SpawnBasesForRow(spawnLocation, terrainHeight, obstaclePositions, currentPathX);
            baseLocations.Add(basesThisRow);
        }
        else
        {
            // -------- FILA GRASS --------
            Grass grass = Instantiate(grassPrefab, terrainHolder);
            // Pasamos la X del camino para NO poner árboles ahí
            obstaclePositions = grass.Init(spawnLocation, desiredPathX);
            terrainHeight = 0.2f;

            currentPathX = desiredPathX;

            obstacles.Add((false, terrainHeight, obstaclePositions));

            // En Grass NO hay bases
            baseLocations.Add(new HashSet<int>());
        }

        spawnLocation++;
    }

    // Busca la X libre más cercana a preferredX dentro de [minX, maxX]
    private int FindNearestFreeX(int preferredX, HashSet<int> blocked)
    {
        if (!blocked.Contains(preferredX))
            return preferredX;

        for (int offset = 1; offset <= maxX - minX; offset++)
        {
            int left = preferredX - offset;
            int right = preferredX + offset;

            bool leftOk = left >= minX && !blocked.Contains(left);
            bool rightOk = right <= maxX && !blocked.Contains(right);

            if (leftOk && rightOk)
            {
                // Si ambos sirven, elegimos uno al azar
                return Random.value < 0.5f ? left : right;
            }

            if (leftOk) return left;
            if (rightOk) return right;
        }

        Debug.LogWarning("No se encontró posición libre para el camino en fila " + spawnLocation);
        return Mathf.Clamp(preferredX, minX, maxX);
    }

    // Genera bases en una fila de Road, forzando una en forcedPathX (camino seguro)
    private HashSet<int> SpawnBasesForRow(int z, float yHeight, HashSet<int> blockedPositions, int forcedPathX)
    {
        HashSet<int> bases = new();

        if (basePrefab == null)
        {
            Debug.LogWarning("BasePrefab no asignado en GameManager, no se generarán bases.");
            return bases;
        }

        // 1) Nenúfar garantizado en la X del camino seguro
        int forcedX = forcedPathX;

        // Por si acaso esa X está bloqueada (no debería en Road, pero seguridad extra)
        if (blockedPositions.Contains(forcedX))
        {
            forcedX = FindNearestFreeX(forcedX, blockedPositions);
        }

        Transform forcedBase = Instantiate(basePrefab, terrainHolder);
        forcedBase.position = new Vector3(forcedX, yHeight, z);
        bases.Add(forcedX);

        // 2) Nenúfares extra aleatorios
        int numBases = Random.Range(minBasesPerRow, maxBasesPerRow + 1);
        int attempts = 0;

        while (bases.Count < numBases && attempts < 50)
        {
            attempts++;

            int x = Random.Range(minX, maxX + 1);

            if (blockedPositions.Contains(x))
                continue;

            if (bases.Contains(x))
                continue;

            Transform b = Instantiate(basePrefab, terrainHolder);
            b.position = new Vector3(x, yHeight, z);

            bases.Add(x);
        }

        return bases;
    }

    public void UpdateFarthestDistance(int distance)
    {
        if (distance > currentFarthestDistance)
        {
            currentFarthestDistance = distance;

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
        if (pos.y < 0 || pos.y >= obstacles.Count)
            return false;

        return !obstacles[pos.y].locations.Contains(pos.x);
    }

    public bool HasBaseAt(Vector2Int pos)
    {
        if (pos.y < 0 || pos.y >= baseLocations.Count)
            return false;

        return baseLocations[pos.y].Contains(pos.x);
    }

    public bool IsRoadRow(int z)
    {
        if (z < 0 || z >= obstacles.Count)
            return false;

        return obstacles[z].isRoad;
    }

    public float GetTerrainHeight(int z)
    {
        if (z < 0 || z >= obstacles.Count)
            return 0.2f;

        return obstacles[z].terrainHeight;
    }
}

