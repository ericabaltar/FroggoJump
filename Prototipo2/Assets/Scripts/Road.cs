using System.Collections.Generic;
using UnityEngine;

public class Road : MonoBehaviour
{
    public HashSet<int> Init(float z)
    {
        transform.position = new Vector3(0, 0, z);

        return new() { -6, 6 };
    }
}

