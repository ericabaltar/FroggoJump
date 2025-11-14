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

    // ---------- POWERUPS (prefab por tipo) ----------
    [Header("PowerUps")]
    [Tooltip("Prefab del powerup de velocidad (debe tener PowerUp con Type=Speed)")]
    [SerializeField] private PowerUp speedPowerupPrefab;
    [Range(0f, 1f)][SerializeField] private float speedDropChance = 0.20f;

    [Tooltip("Prefab del powerup de doble salto (debe tener PowerUp con Type=DoubleJump)")]
    [SerializeField] private PowerUp doubleJumpPowerupPrefab;
    [Range(0f, 1f)][SerializeField] private float doubleJumpDropChance = 0.12f;

    [Tooltip("Prefab del powerup de doble puntuación (debe tener PowerUp con Type=DoubleScore)")]
    [SerializeField] private PowerUp doubleScorePowerupPrefab;
    [Range(0f, 1f)][SerializeField] private float doubleScoreDropChance = 0.08f;

    [Tooltip("Offset local para colocar el powerup sobre la base")]
    [SerializeField] private Vector3 powerupLocalOffset = new Vector3(0f, 0.35f, 0f);

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

    // -------- SCORE MULTIPLIER --------
    private float scoreMultiplier = 1f;
    private Coroutine scoreMultiplierCoro;

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
        // Siguiente x del camino (serpenteo)
        int desiredPathX = currentPathX + Random.Range(-1, 2); // -1,0,1
        desiredPathX = Mathf.Clamp(desiredPathX, minX, maxX);

        float roadProbability = Mathf.Lerp(0.5f, 0.9f, spawnLocation / 250f);

        float terrainHeight;
        HashSet<int> obstaclePositions;

        if (Random.value < roadProbability)
        {
            // ROAD
            var road = Instantiate(roadPrefab, terrainHolder);
            obstaclePositions = road.Init(spawnLocation);
            terrainHeight = 0.2f;

            // Asegurar columna del camino
            currentPathX = FindNearestFreeX(desiredPathX, obstaclePositions);

            obstacles.Add((true, terrainHeight, obstaclePositions));

            // Una fila ROAD es SOLO de un tipo: móviles o estáticas
            bool rowIsMoving = Random.value < movingRowChance;

            if (rowIsMoving)
            {
                SpawnMovingRow(spawnLocation, terrainHeight, obstaclePositions, currentPathX);
                baseLocations.Add(new HashSet<int>()); // no hay estáticas en esta fila
            }
            else
            {
                var staticXs = SpawnStaticRow(spawnLocation, terrainHeight, obstaclePositions, currentPathX);
                baseLocations.Add(staticXs);
            }
        }
        else
        {
            // GRASS
            var grass = Instantiate(grassPrefab, terrainHolder);
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

    /// Fila ROAD SOLO estáticas
    private HashSet<int> SpawnStaticRow(int z, float yHeight, HashSet<int> blockedPositions, int forcedPathX)
    {
        HashSet<int> staticXs = new();

        if (staticBasePrefab == null)
        {
            Debug.LogWarning("staticBasePrefab sin asignar.");
            return staticXs;
        }

        // 1) Camino garantizado
        int fx = blockedPositions.Contains(forcedPathX) ? FindNearestFreeX(forcedPathX, blockedPositions) : forcedPathX;
        Transform b = Instantiate(staticBasePrefab, terrainHolder);
        b.position = new Vector3(fx, yHeight, z);
        staticXs.Add(fx);

        // Powerup sobre esta base (si sale)
        TrySpawnPowerup(b);

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

            // Powerup sobre esta base (si sale)
            TrySpawnPowerup(sb);
        }

        return staticXs;
    }

    /// Fila ROAD SOLO móviles (exactamente 1)
    private void SpawnMovingRow(int z, float yHeight, HashSet<int> blockedPositions, int forcedPathX)
    {
        if (movingBasePrefab == null)
        {
            Debug.LogWarning("movingBasePrefab sin asignar.");
            return;
        }

        int startX = blockedPositions.Contains(forcedPathX)
            ? FindNearestFreeX(forcedPathX, blockedPositions)
            : forcedPathX;

        var mb = Instantiate(movingBasePrefab, terrainHolder);
        float spd = Random.Range(movingBaseSpeedMin, movingBaseSpeedMax);
        bool toRight = Random.value < 0.5f;
        mb.Init(z, yHeight, minX, maxX, toRight, spd, startX);

        // Powerup como HIJO de la base móvil (se mueve con ella)
        TrySpawnPowerup(mb.transform);
    }

    // ---------- POWERUP SPAWN (prefab por tipo, 0 o 1 por base) ----------
    private void TrySpawnPowerup(Transform baseTransform)
    {
        if (baseTransform == null) return;

        // Construimos una lista de candidatos activos (prefab + chance)
        // Para evitar sesgo por orden, aleatorizamos el orden cada vez.
        var candidates = new List<(PowerUp prefab, float chance)>(3);
        if (speedPowerupPrefab != null && speedDropChance > 0f) candidates.Add((speedPowerupPrefab, speedDropChance));
        if (doubleJumpPowerupPrefab != null && doubleJumpDropChance > 0f) candidates.Add((doubleJumpPowerupPrefab, doubleJumpDropChance));
        if (doubleScorePowerupPrefab != null && doubleScoreDropChance > 0f) candidates.Add((doubleScorePowerupPrefab, doubleScoreDropChance));

        if (candidates.Count == 0) return;

        // Shuffle
        for (int i = 0; i < candidates.Count; i++)
        {
            int j = Random.Range(i, candidates.Count);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        // Probamos en orden aleatorio y spawneamos el primero que “gane” su tirada
        foreach (var c in candidates)
        {
            if (Random.value <= c.chance)
            {
                var pu = Instantiate(c.prefab, baseTransform);
                pu.transform.localPosition = powerupLocalOffset; // encima del nenúfar
                // Seguridad: es trigger
                if (pu.TryGetComponent<Collider>(out var col) && !col.isTrigger) col.isTrigger = true;
                break; // 1 por base
            }
        }
    }

    public void UpdateFarthestDistance(int distance)
    {
        if (distance > currentFarthestDistance)
        {
            currentFarthestDistance = distance;
            while (obstacles.Count < (distance + spawnDistance))
                SpawnObstacle();
        }

        int shownScore = Mathf.RoundToInt(currentFarthestDistance * scoreMultiplier);
        HudManager.Instance?.SetScore(shownScore);
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

    public int GetFarthestDistance() => currentFarthestDistance;

    // ---------- SCORE MULTIPLIER CONTROL ----------
    public void ActivateScoreMultiplier(float multiplier, float duration)
    {
        if (scoreMultiplierCoro != null) StopCoroutine(scoreMultiplierCoro);
        scoreMultiplierCoro = StartCoroutine(ScoreMultiplierRoutine(multiplier, duration));
    }

    private IEnumerator ScoreMultiplierRoutine(float mult, float duration)
    {
        scoreMultiplier = Mathf.Max(1f, mult);
        int shownScore = Mathf.RoundToInt(currentFarthestDistance * scoreMultiplier);
        HudManager.Instance?.SetScore(shownScore);

        yield return new WaitForSeconds(duration);

        scoreMultiplier = 1f;
        shownScore = Mathf.RoundToInt(currentFarthestDistance * scoreMultiplier);
        HudManager.Instance?.SetScore(shownScore);
        scoreMultiplierCoro = null;
    }
}
