using System.Collections.Generic;
using UnityEngine;

public class Grass : MonoBehaviour
{
    [SerializeField] private Transform treePrefab;

 
    public HashSet<int> Init(float z, int reservedPathX)
    {
        transform.position = new Vector3(0, 0, z);

        HashSet<int> locations = new() { -6, 6 };

 
        int numTrees = Random.Range(1, 5);

        int safeRadius = 1;

        for (int i = 0; i < numTrees; i++)
        {
            int xPos;
            int attempts = 0;

            do
            {
                attempts++;

                xPos = Random.Range(-5, 6); 

                if (attempts > 50)
                    break;

            }
            while (Mathf.Abs(xPos - reservedPathX) <= safeRadius);

            if (attempts > 50)
                break;

            // Instanciar árbol
            Transform tree = Instantiate(treePrefab, transform);
            tree.position = new Vector3(xPos, 0.2f, z);

            // Guardar posición como bloqueada
            locations.Add(xPos);
        }

        return locations;
    }
}




