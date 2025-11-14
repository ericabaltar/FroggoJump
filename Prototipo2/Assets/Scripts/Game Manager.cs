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

    [Header("Base prefabs")]
    [SerializeField] private Transform staticBasePrefab;   // BASE ESTÁTICA
    [SerializeField] private MovingBase movingBasePrefab;  // BASE MÓVIL

    [Header("Spawn params")]
    [SerializeField] private int spawnDistance = 20;

    [Header("Grid X range")]
    [SerializeField] private int minX = -5;
    [SerializeField] private int maxX = 5;

    [Header("Cantidad de bases ESTÁTICAS por fila ROAD")]
    [SerializeField] private int minBasesPerRow = 2;
    [SerializeField] private int maxBasesPerRow = 4;

    [Header("¿Qué porcentaje de filas ROAD serán MÓVILES? (el resto, ESTÁTICAS)")]
    [Range(0f, 1f)]
    [SerializeField] private float movingRowChance = 0.45f;

    [Header("Parámetros bases móviles")]
    [SerializeField] private float movingBaseSpeedMin = 2f;
    [SerializeField] private float movingBaseSpeedMax = 3.5f;

    // obstacles: (isRoad, yHeight, blockedX)
    private List<(bool isRoad, float terrainHeight, HashSet<int> locations)> obstacles = new();
    // posiciones X con BASE ESTÁTICA (las móviles NO se guardan aquí)
    private List<HashSet<int>> baseLocations = new();

    private int spawnLocation;
    private int currentFarthestDistance = 0;
    public static GameManager Instance { get; private set; }

    // Camino serpenteante garantizado
    private int currentPathX;
    private bool pathInitialized = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        NewLevel();
    }

    private void NewLevel()
    {
        obstacles.Clear();
        baseLocations.Clear();

        foreach (Transform child in terrainHolder) Destroy(child.gameObject);

        spawnLocation = 0;
        currentFarthestDistance = 0;

        currentPathX = Mathf.Clamp(0, minX, maxX);
        pathInitialized = true;

        for (int i = 0; i < spawnDistance; i++) SpawnObstacle();
    }

    private void SpawnObstacle()
    {
        // Siguiente x del camino (serpenteo) -1,0,1
        int desiredPathX = currentPathX + Random.Range(-1, 2);
        desiredPathX = Mathf.Clamp(desiredPathX, minX, maxX);

        float roadProbability = Mathf.Lerp(0.5f, 0.9f, spawnLocation / 250f);

        float terrainHeight;
        HashSet<int> obstaclePositions;

        if (Random.value < roadProbability)
        {
            // ROAD
            var road = Instantiate(roadPrefab, terrainHolder);
            obstaclePositions = road.Init(spawnLocation);     // típicamente {-6, 6}
            terrainHeight = 0.2f;

            // Asegurar que la columna del camino está libre
            currentPathX = FindNearestFreeX(desiredPathX, obstaclePositions);

            obstacles.Add((true, terrainHeight, obstaclePositions));

            // ---- REGLA: una fila ROAD es SOLO de un tipo ----
            bool rowIsMoving = Random.value < movingRowChance;

            if (rowIsMoving)
            {
                // SOLO MÓVILES (exactamente UNA, para evitar solapes/choques)
                SpawnMovingRow(spawnLocation, terrainHeight, obstaclePositions, currentPathX);
                baseLocations.Add(new HashSet<int>()); // no hay estáticas en esta fila
            }
            else
            {
                // SOLO ESTÁTICAS (incluye la forzada del camino)
                var staticXs = SpawnStaticRow(spawnLocation, terrainHeight, obstaclePositions, currentPathX);
                baseLocations.Add(staticXs);
            }
        }
        else
        {
            // GRASS
            var grass = Instantiate(grassPrefab, terrainHolder);
            // Si tu Grass.Init original no recibe desiredPathX, usa la otra firma: grass.Init(spawnLocation);
            obstaclePositions = grass.Init(spawnLocation, desiredPathX); // evita árboles en el camino
            terrainHeight = 0.2f;

            currentPathX = desiredPathX;

            obstacles.Add((false, terrainHeight, obstaclePositions));
            baseLocations.Add(new HashSet<int>()); // sin bases en grass
        }

        spawnLocation++;
    }

    private int FindNearestFreeX(int preferredX, HashSet<int> blocked)
    {
        if (!blocked.Contains(preferredX)) return preferredX;

        for (int offset = 1; offset <= maxX - minX; offset++)
        {
            int left = preferredX - offset;
            int right = preferredX + offset;

            if (left >= minX && !blocked.Contains(left)) return left;
            if (right <= maxX && !blocked.Contains(right)) return right;
        }

        Debug.LogWarning("No X libre para camino en fila " + spawnLocation);
        return Mathf.Clamp(preferredX, minX, maxX);
    }

    /// <summary>
    /// Fila ROAD SOLO de bases ESTÁTICAS.
    /// - Garantiza una base estática en forcedPathX (camino).
    /// - Añade estáticas extra aleatorias (sin pisar obstáculos ni duplicar X).
    /// </summary>
    private HashSet<int> SpawnStaticRow(int z, float yHeight, HashSet<int> blockedPositions, int forcedPathX)
    {
        HashSet<int> staticXs = new();

        if (staticBasePrefab == null)
        {
            Debug.LogWarning("staticBasePrefab sin asignar: no se garantiza camino seguro.");
            return staticXs;
        }

        // 1) Base estática garantizada (camino)
        int fx = blockedPositions.Contains(forcedPathX) ? FindNearestFreeX(forcedPathX, blockedPositions) : forcedPathX;
        Transform b = Instantiate(staticBasePrefab, terrainHolder);
        b.position = new Vector3(fx, yHeight, z);
        staticXs.Add(fx);

        // 2) Estáticas extra
        int targetTotal = Random.Range(minBasesPerRow, maxBasesPerRow + 1);
        int attempts = 0;

        while (staticXs.Count < targetTotal && attempts < 80)
        {
            attempts++;
            int x = Random.Range(minX, maxX + 1);
            if (blockedPositions.Contains(x)) continue;
            if (staticXs.Contains(x)) continue;

            Transform sb = Instantiate(staticBasePrefab, terrainHolder);
            sb.position = new Vector3(x, yHeight, z);
            staticXs.Add(x);
        }

        return staticXs;
    }

    /// <summary>
    /// Fila ROAD SOLO de bases MÓVILES.
    /// - Garantiza exactamente UNA base móvil en forcedPathX (o la libre más cercana).
    /// - No spawnea estáticas en esta fila.
    /// </summary>
    private void SpawnMovingRow(int z, float yHeight, HashSet<int> blockedPositions, int forcedPathX)
    {
        if (movingBasePrefab == null)
        {
            Debug.LogWarning("movingBasePrefab sin asignar: no se podrán spawnear móviles.");
            return;
        }

        int startX = blockedPositions.Contains(forcedPathX)
            ? FindNearestFreeX(forcedPathX, blockedPositions)
            : forcedPathX;

        var mb = Instantiate(movingBasePrefab, terrainHolder);
        float spd = Random.Range(movingBaseSpeedMin, movingBaseSpeedMax);
        bool toRight = Random.value < 0.5f;
        mb.Init(z, yHeight, minX, maxX, toRight, spd, startX);

        // Importante: NO añadimos nada a baseLocations (solo guarda estáticas).
    }

    public void UpdateFarthestDistance(int distance)
    {
        if (distance > currentFarthestDistance)
        {
            currentFarthestDistance = distance;
            while (obstacles.Count < (distance + spawnDistance))
                SpawnObstacle();
        }

        HudManager.Instance?.SetScore(currentFarthestDistance);
    }

    public bool CheckIfAccessible(Vector2Int pos)
    {
        if (pos.y < 0 || pos.y >= obstacles.Count) return false;
        return !obstacles[pos.y].locations.Contains(pos.x);
    }

    // Solo devuelve true para bases ESTÁTICAS
    public bool HasBaseAt(Vector2Int pos)
    {
        if (pos.y < 0 || pos.y >= baseLocations.Count) return false;
        return baseLocations[pos.y].Contains(pos.x);
    }

    public bool IsRoadRow(int z)
    {
        if (z < 0 || z >= obstacles.Count) return false;
        return obstacles[z].isRoad;
    }

    public float GetTerrainHeight(int z)
    {
        if (z < 0 || z >= obstacles.Count) return 0.2f;
        return obstacles[z].terrainHeight;
    }

    public int GetFarthestDistance()
    {
        return currentFarthestDistance;
    }
}
