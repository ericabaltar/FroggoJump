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

    [Header("Bases ESTÁTICAS por fila ROAD")]
    [SerializeField] private int minBasesPerRow = 2;
    [SerializeField] private int maxBasesPerRow = 4;

    [Header("¿Qué % de filas ROAD serán MÓVILES? (resto: ESTÁTICAS)")]
    [Range(0f, 1f)]
    [SerializeField] private float movingRowChance = 0.45f;

    [Header("Parámetros bases móviles")]
    [SerializeField] private float movingBaseSpeedMin = 2f;
    [SerializeField] private float movingBaseSpeedMax = 3.5f;

    // ---------- POWERUPS ----------
    [Header("PowerUps")]
    [SerializeField] private PowerUp moreRangePrefab;        // Reducir velocidad
    [Range(0f, 1f)][SerializeField] private float slowSpeedDropChance = 0.10f;

    [SerializeField] private PowerUp fastSpeedPrefab;        // Aumentar velocidad
    [Range(0f, 1f)][SerializeField] private float fastSpeedDropChance = 0.16f;

    [SerializeField] private PowerUp staminaRegenPrefab;     // Estamina se regenera automáticamente
    [Range(0f, 1f)][SerializeField] private float slowStaminaDropChance = 0.12f;

    [SerializeField] private PowerUp extraLifePrefab;        // Vida extra
    [Range(0f, 1f)][SerializeField] private float extraLifeDropChance = 0.06f;

    [Header("Placement de PowerUps (Sockets)")]
    [Tooltip("Nombre del hijo vacío en la base donde se ancla el powerup.")]
    [SerializeField] private string powerupSocketName = "PowerupSocket";
    [Tooltip("Ajuste fino vertical extra sobre el socket.")]
    [SerializeField] private float powerupExtraOffsetY = 0.0f;
    [Tooltip("Si no hay socket, usamos este offset local respecto a la base.")]
    [SerializeField] private Vector3 powerupLocalOffset = new Vector3(0f, 0.75f, 0f);

    // ---------- FLIES (stamina pickups) ----------
    [Header("Flies (stamina pickups) - suelo")]
    [SerializeField] private GameObject flyPrefab;     // Prefab de mosca (GameObject)
    [SerializeField] private int fliesPerRowMin = 0;
    [SerializeField] private int fliesPerRowMax = 2;
    [Range(0f, 1f)][SerializeField] private float flySpawnChance = 0.6f;
    [SerializeField] private float flyYOffset = 0.45f;

    [Header("Flies sobre bases")]
    [Range(0f, 1f)][SerializeField] private float flyOnBaseChance = 0.35f;
    [SerializeField] private Vector3 flyOnBaseLocalOffset = new Vector3(0f, 0.45f, 0f);

    // ---------- VIDAS ----------
    [Header("Vidas")]
    [SerializeField] private int startingExtraLives = 0;
    private int extraLives = 0;

    // obstacles: (isRoad, yHeight, blockedX)
    private readonly List<(bool isRoad, float terrainHeight, HashSet<int> locations)> obstacles = new();
    // posiciones X con BASE ESTÁTICA (las móviles NO se guardan aquí)
    private readonly List<HashSet<int>> baseLocations = new();

    private int spawnLocation;
    private int currentFarthestDistance = 0;

    public static GameManager Instance { get; private set; }

    // Camino serpenteante garantizado
    private int currentPathX;

    bool upgradedRange = false;

    // (Opcional) multiplicador de score si lo usas
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

        extraLives = startingExtraLives;

        for (int i = 0; i < spawnDistance; i++) SpawnObstacle();
    }

    private void SpawnObstacle()
    {
        int desiredPathX = Mathf.Clamp(currentPathX + Random.Range(-1, 2), minX, maxX);
        float roadProbability = Mathf.Lerp(0.5f, 0.9f, spawnLocation / 250f);

        float terrainHeight;
        HashSet<int> obstaclePositions;

        if (Random.value < roadProbability)
        {
            // ROAD
            var road = Instantiate(roadPrefab, terrainHolder);
            obstaclePositions = road.Init(spawnLocation);
            terrainHeight = 0.2f;

            currentPathX = FindNearestFreeX(desiredPathX, obstaclePositions);

            obstacles.Add((true, terrainHeight, obstaclePositions));

            // solo estáticas O solo 1 móvil
            bool rowIsMoving = Random.value < movingRowChance;

            if (rowIsMoving)
            {
                SpawnMovingRow(spawnLocation, terrainHeight, obstaclePositions, currentPathX);
                baseLocations.Add(new HashSet<int>());
            }
            else
            {
                var staticXs = SpawnStaticRow(spawnLocation, terrainHeight, obstaclePositions, currentPathX);
                baseLocations.Add(staticXs);
            }

            // moscas en suelo de ROAD
            SpawnFliesInRow(spawnLocation, terrainHeight, obstaclePositions);
        }
        else
        {
            // GRASS
            var grass = Instantiate(grassPrefab, terrainHolder);
            obstaclePositions = grass.Init(spawnLocation, desiredPathX); // evita obst en el camino
            terrainHeight = 0.2f;

            currentPathX = desiredPathX;

            obstacles.Add((false, terrainHeight, obstaclePositions));
            baseLocations.Add(new HashSet<int>());

            // moscas en suelo de GRASS
            SpawnFliesInRow(spawnLocation, terrainHeight, obstaclePositions);
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
        return Mathf.Clamp(preferredX, minX, maxX);
    }

    // ---------- Filas estáticas ----------
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

        TrySpawnPowerup(b);
        TrySpawnFlyOnBase(b);

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

            TrySpawnPowerup(sb);
            TrySpawnFlyOnBase(sb);
        }

        return staticXs;
    }

    // ---------- Fila móvil (exactamente 1 base móvil) ----------
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

        TrySpawnPowerup(mb.transform);
        TrySpawnFlyOnBase(mb.transform);
    }

    // ---------- Spawn de PowerUps (0 o 1 por base) con SOCKET ----------
    private void TrySpawnPowerup(Transform baseTransform)
    {
        if (baseTransform == null) return;

        var candidates = new List<(PowerUp prefab, float chance)>(4);
        if (moreRangePrefab != null && slowSpeedDropChance > 0f) candidates.Add((moreRangePrefab, slowSpeedDropChance));
        if (fastSpeedPrefab != null && fastSpeedDropChance > 0f) candidates.Add((fastSpeedPrefab, fastSpeedDropChance));
        if (staminaRegenPrefab != null && slowStaminaDropChance > 0f) candidates.Add((staminaRegenPrefab, slowStaminaDropChance));
        if (extraLifePrefab != null && extraLifeDropChance > 0f) candidates.Add((extraLifePrefab, extraLifeDropChance));
        if (candidates.Count == 0) return;

        // Shuffle simple
        for (int i = 0; i < candidates.Count; i++)
        {
            int j = Random.Range(i, candidates.Count);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        foreach (var c in candidates)
        {
            if (Random.value > c.chance) continue;

            // 1) Si existe socket, lo usamos
            Transform socket = FindChildRecursive(baseTransform, powerupSocketName);
            if (socket != null)
            {
                var pu = Instantiate(c.prefab, socket);
                pu.gameObject.SetActive(true);
                pu.transform.localPosition = Vector3.up * powerupExtraOffsetY;
                if (pu.TryGetComponent<Collider>(out var col)) col.isTrigger = true;
                break;
            }

            // 2) Fallback: offset local respecto a la base
            var fallback = Instantiate(c.prefab, baseTransform);
            fallback.gameObject.SetActive(true);
            fallback.transform.localPosition = powerupLocalOffset;
            if (fallback.TryGetComponent<Collider>(out var col2)) col2.isTrigger = true;
            break;
        }
    }

    private Transform FindChildRecursive(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var t = FindChildRecursive(root.GetChild(i), name);
            if (t != null) return t;
        }
        return null;
    }

    // ---------- Flies SOBRE bases ----------
    private void TrySpawnFlyOnBase(Transform baseTransform)
    {
        if (flyPrefab == null || baseTransform == null) return;
        if (Random.value > flyOnBaseChance) return;

        GameObject go = Instantiate(flyPrefab, baseTransform);
        go.SetActive(true);

        Fly fly = go.GetComponent<Fly>();
        if (fly == null) fly = go.AddComponent<Fly>();
        fly.SetBasePos(flyOnBaseLocalOffset);

        Collider col = go.GetComponent<Collider>();
        if (col == null) col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;

        if (col is SphereCollider sc)
        {
            if (sc.radius < 0.05f) sc.radius = Mathf.Max(0.3f, fly.regularPickupRadius);
            sc.center = Vector3.zero;
        }

        if (go.tag == "Untagged") go.tag = "Fly";
    }

    // ---------- Flies en SUELO ----------
    private void SpawnFliesInRow(int z, float yHeight, HashSet<int> blockedPositions)
    {
        if (flyPrefab == null) return;

        int tries = Random.Range(fliesPerRowMin, fliesPerRowMax + 1);
        for (int i = 0; i < tries; i++)
        {
            if (Random.value > flySpawnChance) continue;

            int x = Random.Range(minX, maxX + 1);

            GameObject go = Instantiate(flyPrefab, terrainHolder);
            go.SetActive(true);

            Fly fly = go.GetComponent<Fly>();
            if (fly == null) fly = go.AddComponent<Fly>();
            fly.SetBasePos(new Vector3(x, yHeight + flyYOffset, z));

            Collider col = go.GetComponent<Collider>();
            if (col == null) col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;

            if (col is SphereCollider sc)
            {
                if (sc.radius < 0.05f) sc.radius = Mathf.Max(0.3f, fly.regularPickupRadius);
                sc.center = Vector3.zero;
            }

            if (go.tag == "Untagged") go.tag = "Fly";
        }
    }

    // ---------- Score / HUD ----------
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

    // ---------- Queries ----------
    public bool CheckIfAccessible(Vector2Int pos)
    {
        if (pos.y < 0 || pos.y >= obstacles.Count) return false;
        return !obstacles[pos.y].locations.Contains(pos.x);
    }

    // Solo bases ESTÁTICAS
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

    // ---------- Vidas ----------
    public void GrantExtraLife(int count = 1)
    {
        extraLives += Mathf.Max(1, count);
        // TODO: actualizar HUD de vidas si procede
    }

    public bool TryConsumeExtraLife()
    {
        if (extraLives <= 0) return false;
        extraLives--;
        PowerupUIManager.Instance?.OnExtraLifeConsumed();
        return true;
    }

    public void ActivateUpgradedRange(float duration)
    {
        SetUpgradedRange(true);

        GameObject[] flies = GameObject.FindGameObjectsWithTag("Fly");

        foreach (GameObject fly in flies)
        {
            fly.GetComponent<Fly>()?.SetPickupRange();
        }
    }

    public bool GetUpgradedRange()
    {
        return upgradedRange;
    }

    void SetUpgradedRange(bool active)
    {
        upgradedRange = active;
    }

    // (Opcional) multiplicador de score si lo usas
    public void ActivateScoreMultiplier(float multiplier, float duration)
    {
        if (scoreMultiplierCoro != null) StopCoroutine(scoreMultiplierCoro);
        scoreMultiplierCoro = StartCoroutine(ScoreMultiplierRoutine(multiplier, duration));
    }

    private IEnumerator ScoreMultiplierRoutine(float mult, float duration)
    {
        scoreMultiplier = Mathf.Max(1f, mult);
        HudManager.Instance?.SetScore(Mathf.RoundToInt(currentFarthestDistance * scoreMultiplier));
        yield return new WaitForSeconds(duration);
        scoreMultiplier = 1f;
        HudManager.Instance?.SetScore(Mathf.RoundToInt(currentFarthestDistance * scoreMultiplier));
        scoreMultiplierCoro = null;
    }
}
