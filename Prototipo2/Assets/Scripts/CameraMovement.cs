using System.Collections;
using UnityEngine;
using UnityEngine.TextCore.Text;
using static UnityEngine.GraphicsBuffer;

public class CameraMovement : MonoBehaviour
{
    //[SerializeField] float distanceThreshold = 0.01f;

    Vector3 velocity = Vector3.zero;
    float smoothTime = 0.3f;

    void Update()
    {
        if (GameManager.Instance.GetFarthestDistance() > transform.position.z)
        {
            Vector3 targetPosition = new Vector3(0f, 0f, GameManager.Instance.GetFarthestDistance());

            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
        }
    }
}
