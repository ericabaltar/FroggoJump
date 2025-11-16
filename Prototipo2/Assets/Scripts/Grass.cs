using System.Collections.Generic;
using UnityEngine;

public class Grass : MonoBehaviour
{
    [Header("Prefabs de obstáculos")]
    [Tooltip("Arrastra aquí los prefabs (GameObject) que quieras spawnear en el césped (árboles, rocas, setos, etc.).")]
    [SerializeField] private GameObject[] obstaclePrefabs;

    [Header("Cantidad por fila")]
    [SerializeField] private int minObstaclesPerRow = 1;
    [SerializeField] private int maxObstaclesPerRow = 4;

    [Header("Grid y altura")]
    [SerializeField] private int minX = -5;
    [SerializeField] private int maxX = 5;
    [Tooltip("Altura local del obstáculo respecto al tramo Grass (usa 0.2 si tus pivotes están a ras).")]
    [SerializeField] private float localY = 0.2f;

    [Header("Camino reservado")]
    [Tooltip("Radio de seguridad alrededor de reservedPathX donde no se spawnea.")]
    [SerializeField] private int safeRadius = 1;

    // ========= Aleatoriedad visual =========
    [Header("Aleatoriedad visual")]
    [Tooltip("Si está activo, se aplica a TODOS los obstáculos. Si no, solo a árboles (por tag o nombre en este objeto o sus hijos).")]
    [SerializeField] private bool applyRandomToAllObstacles = true;

    [Tooltip("Tag que identifica árboles en tus prefabs (asigna este tag a los prefabs de árbol).")]
    [SerializeField] private string treeTag = "Tree";

    [Header("Escala aleatoria")]
    [Tooltip("Usar escala uniforme (mismo factor en X/Y/Z). Si está desactivado, usa rango por-eje.")]
    [SerializeField] private bool randomUniformScale = true;

    [Tooltip("Rango de escala uniforme (multiplica la escala original). Usa 0.5–1.5 para notar el cambio.")]
    [SerializeField] private Vector2 uniformScaleRange = new Vector2(0.9f, 1.2f);

    [Tooltip("Escala por eje (multiplica la escala original en cada eje).")]
    [SerializeField] private Vector2 scaleXRange = new Vector2(0.9f, 1.2f);
    [SerializeField] private Vector2 scaleYRange = new Vector2(0.9f, 1.2f);
    [SerializeField] private Vector2 scaleZRange = new Vector2(0.9f, 1.2f);

    [Header("Rotación aleatoria")]
    [Tooltip("Giro aleatorio en Y (0..360 para giro completo).")]
    [SerializeField] private bool randomYRotation = true;
    [SerializeField] private Vector2 rotationYRange = new Vector2(0f, 360f);

    [Tooltip("Inclinación leve en X/Z (para árboles con ligera caída).")]
    [SerializeField] private bool randomTiltXZ = false;
    [SerializeField] private Vector2 tiltXRange = new Vector2(-5f, 5f);
    [SerializeField] private Vector2 tiltZRange = new Vector2(-5f, 5f);

    [Header("Escalado del nodo visual")]
    [Tooltip("Si el objeto raíz NO tiene Renderer, escalar el primer Renderer encontrado en hijos.")]
    [SerializeField] private bool scaleChildRendererIfRootHasNoRenderer = true;

    [Header("Depuración")]
    [Tooltip("Registra logs detallados (usa Warning/Error para que aparezcan con filtros).")]
    [SerializeField] private bool debugLogs = true;

    /// <summary>
    /// Genera obstáculos en la fila Z indicada evitando el camino reservado.
    /// Devuelve el conjunto de columnas X que quedan bloqueadas (incluye guardarraíles).
    /// </summary>
    public HashSet<int> Init(float z, int reservedPathX)
    {
        if (debugLogs) Debug.LogWarning($"[Grass.Init] z={z} reservedPathX={reservedPathX}", this);

        // Posicionar el tramo en su fila Z (mundo)
        Vector3 worldPos = transform.position;
        worldPos.x = 0f;
        worldPos.y = 0f;
        worldPos.z = z;
        transform.position = worldPos;

        HashSet<int> blockedCols = new() { minX - 1, maxX + 1 };

        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0)
        {
            Debug.LogError("[Grass] obstaclePrefabs vacío. No se spawnea nada.", this);
            return blockedCols;
        }

        int targetCount = Mathf.Clamp(Random.Range(minObstaclesPerRow, maxObstaclesPerRow + 1), 0, (maxX - minX + 1));

        List<int> available = BuildAvailableColumns(reservedPathX, safeRadius);
        if (available.Count == 0) available = BuildAvailableColumns(reservedPathX, 0);

        if (available.Count == 0 || targetCount == 0)
        {
            Debug.LogWarning($"[Grass] Sin columnas disponibles o targetCount=0 en z={z}.", this);
            return blockedCols;
        }

        Shuffle(available);

        int placed = 0;
        int idx = 0;

        while (placed < targetCount && idx < available.Count)
        {
            int x = available[idx++];
            if (blockedCols.Contains(x)) continue;

            GameObject prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
            if (prefab == null)
            {
                Debug.LogError("[Grass] Prefab nulo en obstaclePrefabs.", this);
                continue;
            }

            GameObject go = Instantiate(prefab, transform);
            go.transform.localPosition = new Vector3(x, localY, 0f);

            Transform targetForVisualScale = ResolveVisualTransform(go.transform);

            // Aleatoriedad (árboles o todos, según config)
            if (ShouldRandomize(targetForVisualScale.gameObject))
            {
                ApplyRandomScaleAndRotation(targetForVisualScale);
                if (debugLogs)
                {
                    Debug.LogWarning(
                        $"[Grass] Spawn '{go.name}' at x={x} | scaledNode='{targetForVisualScale.name}' | finalLocalScale={targetForVisualScale.localScale}",
                        go
                    );
                }
            }
            else if (debugLogs)
            {
                Debug.LogWarning($"[Grass] Spawn '{go.name}' at x={x} | sin aleatoriedad por filtro.", go);
            }

            blockedCols.Add(x);
            placed++;
        }

        if (debugLogs) Debug.LogWarning($"[Grass] z={z} → colocados {placed}/{targetCount}.", this);
        return blockedCols;
    }

    private Transform ResolveVisualTransform(Transform root)
    {
        // Si el objeto raíz ya tiene renderer, escalar ese (más intuitivo)
        var rootRenderer = root.GetComponent<Renderer>();
        if (rootRenderer != null) return root;

        if (!scaleChildRendererIfRootHasNoRenderer) return root;

        // Busca el primer renderer en hijos
        var anyRenderer = root.GetComponentInChildren<Renderer>(true);
        if (anyRenderer != null) return anyRenderer.transform;

        // Sin renderer: escala el root (mejor que nada)
        return root;
    }

    private bool ShouldRandomize(GameObject go)
    {
        if (applyRandomToAllObstacles) return true;

        // Por tag en el objeto o en algún hijo
        if (!string.IsNullOrEmpty(treeTag))
        {
            if (go.CompareTag(treeTag)) return true;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.CompareTag(treeTag)) return true;
        }

        // Por nombre (en este objeto o en hijos con renderer)
        if (NameLooksLikeTree(go.name)) return true;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            if (NameLooksLikeTree(r.gameObject.name)) return true;

        return false;
    }

    private bool NameLooksLikeTree(string n)
    {
        n = n.ToLower();
        return n.Contains("tree") || n.Contains("arbol") || n.Contains("árbol");
    }

    private void ApplyRandomScaleAndRotation(Transform t)
    {
        // ---- Escala ----
        Vector3 original = t.localScale;
        Vector3 scaled = original;

        if (randomUniformScale)
        {
            float s = Random.Range(uniformScaleRange.x, uniformScaleRange.y);
            s = Mathf.Max(0.001f, s);
            scaled = original * s;
        }
        else
        {
            float sx = Random.Range(scaleXRange.x, scaleXRange.y);
            float sy = Random.Range(scaleYRange.x, scaleYRange.y);
            float sz = Random.Range(scaleZRange.x, scaleZRange.y);
            scaled = new Vector3(
                original.x * Mathf.Max(0.001f, sx),
                original.y * Mathf.Max(0.001f, sy),
                original.z * Mathf.Max(0.001f, sz)
            );
        }

        t.localScale = scaled;

        // ---- Rotación ----
        Vector3 euler = t.localEulerAngles;

        if (randomYRotation)
        {
            euler.y = Random.Range(rotationYRange.x, rotationYRange.y);
        }

        if (randomTiltXZ)
        {
            euler.x += Random.Range(tiltXRange.x, tiltXRange.y);
            euler.z += Random.Range(tiltZRange.x, tiltZRange.y);
        }

        t.localRotation = Quaternion.Euler(euler);
    }

    private List<int> BuildAvailableColumns(int reservedPathX, int radius)
    {
        var list = new List<int>();
        for (int x = minX; x <= maxX; x++)
        {
            if (Mathf.Abs(x - reservedPathX) <= radius) continue;
            list.Add(x);
        }
        return list;
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void OnValidate()
    {
        if (minX > maxX) { int t = minX; minX = maxX; maxX = t; }
        if (minObstaclesPerRow < 0) minObstaclesPerRow = 0;
        if (maxObstaclesPerRow < minObstaclesPerRow) maxObstaclesPerRow = minObstaclesPerRow;

        // Clamp de rangos de escala
        if (uniformScaleRange.x < 0.01f) uniformScaleRange.x = 0.01f;
        if (uniformScaleRange.y < uniformScaleRange.x) uniformScaleRange.y = uniformScaleRange.x;

        if (scaleXRange.x < 0.01f) scaleXRange.x = 0.01f;
        if (scaleYRange.x < 0.01f) scaleYRange.x = 0.01f;
        if (scaleZRange.x < 0.01f) scaleZRange.x = 0.01f;

        if (scaleXRange.y < scaleXRange.x) scaleXRange.y = scaleXRange.x;
        if (scaleYRange.y < scaleYRange.x) scaleYRange.y = scaleYRange.x;
        if (scaleZRange.y < scaleZRange.x) scaleZRange.y = scaleZRange.x;

        // Clamp de rotaciones razonables
        rotationYRange.x = Mathf.Clamp(rotationYRange.x, -360f, 360f);
        rotationYRange.y = Mathf.Clamp(rotationYRange.y, -360f, 360f);
        if (rotationYRange.y < rotationYRange.x) rotationYRange.y = rotationYRange.x;

        tiltXRange.x = Mathf.Clamp(tiltXRange.x, -45f, 45f);
        tiltXRange.y = Mathf.Clamp(tiltXRange.y, -45f, 45f);
        if (tiltXRange.y < tiltXRange.x) tiltXRange.y = tiltXRange.x;

        tiltZRange.x = Mathf.Clamp(tiltZRange.x, -45f, 45f);
        tiltZRange.y = Mathf.Clamp(tiltZRange.y, -45f, 45f);
        if (tiltZRange.y < tiltZRange.x) tiltZRange.y = tiltZRange.x;
    }
}
