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

    [Header("Aleatoriedad visual (opcional)")]
    [SerializeField] private bool randomYRotation = true;
    [SerializeField] private bool randomUniformScale = false;
    [SerializeField] private Vector2 scaleRange = new Vector2(0.9f, 1.1f);

    [Header("Depuración")]
    [SerializeField] private bool debugLog = false;

    /// <summary>
    /// Genera obstáculos en la fila Z indicada evitando el camino reservado.
    /// Devuelve el conjunto de columnas X que quedan bloqueadas (incluye guardarraíles).
    /// </summary>
    /// <param name="z">Fila Z (mundo) donde spawnear este tramo</param>
    /// <param name="reservedPathX">Columna X (grid) del camino a proteger</param>
    public HashSet<int> Init(float z, int reservedPathX)
    {
        // Posicionar el tramo en su fila Z (mundo)
        Vector3 worldPos = transform.position;
        worldPos.x = 0f;
        worldPos.y = 0f;
        worldPos.z = z;
        transform.position = worldPos;

        HashSet<int> blockedCols = new() { minX - 1, maxX + 1 };


        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0)
        {
            if (debugLog) Debug.LogWarning("[Grass] obstaclePrefabs vacío. No se spawnea nada.", this);
            return blockedCols;
        }


        int targetCount = Mathf.Clamp(Random.Range(minObstaclesPerRow, maxObstaclesPerRow + 1), 0, (maxX - minX + 1));


        List<int> available = BuildAvailableColumns(reservedPathX, safeRadius);

        if (available.Count == 0)
            available = BuildAvailableColumns(reservedPathX, 0);

        if (available.Count == 0 || targetCount == 0)
        {
            if (debugLog) Debug.Log($"[Grass] Sin columnas disponibles o targetCount=0 en z={z}.", this);
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
            if (prefab == null) continue;


            GameObject go = Instantiate(prefab, transform);


            go.transform.localPosition = new Vector3(x, localY, 0f);

            if (randomYRotation)
            {
                Vector3 e = go.transform.localEulerAngles;
                e.y = Random.Range(0f, 360f);
                go.transform.localEulerAngles = e;
            }
            if (randomUniformScale)
            {
                float s = Random.Range(scaleRange.x, scaleRange.y);
                go.transform.localScale = Vector3.one * s;
            }

            blockedCols.Add(x);
            placed++;
        }

        if (debugLog) Debug.Log($"[Grass] z={z} → colocados {placed}/{targetCount}.", this);
        return blockedCols;
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
        if (scaleRange.x < 0.01f) scaleRange.x = 0.01f;
        if (scaleRange.y < scaleRange.x) scaleRange.y = scaleRange.x;
        if (safeRadius < 0) safeRadius = 0;
    }
}





