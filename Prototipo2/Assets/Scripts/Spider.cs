using UnityEngine;

public class Spider : MonoBehaviour
{
    [Header("Refs")]
    public Transform spider;
    public Transform character;   // opcional, si lo usas en otra l�gica
    public Transform webSpawn;
    public Transform target;
    public GameObject webBall;
    public Animator animSpider;

    [Header("Movimiento")]
    public float speed = 0.005f;

    [Header("Disparo")]
    public float shootForce = 5.0f;
    public float fireRate = 50.0f;     
    public float nextFireTime = 10.0f;

    [Header("Detecci�n de jugador")]
    [Tooltip("Tag del jugador")]
    public string playerTag = "Player";

    private void Update()
    {
        SpiderMovement();

        if (target != null && Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + 10.0f / fireRate;
        }
    }

    public void SpiderMovement()
    {
        if (spider != null)
        {
            spider.transform.position = new Vector3(
                spider.position.x,
                spider.position.y,
                spider.position.z + speed
            );
        }
        else
        {
            transform.position += Vector3.forward * speed;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryKillPlayer(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryKillPlayer(collision.collider.gameObject);
    }

    private void TryKillPlayer(GameObject hit)
    {
        if (!string.IsNullOrEmpty(playerTag) && hit.CompareTag(playerTag))
        {
            hit.SendMessage("Die", SendMessageOptions.DontRequireReceiver);
            return;
        }

        var pc = hit.GetComponent<PlayerController>() ?? hit.GetComponentInParent<PlayerController>();
        if (pc != null)
        {
            pc.gameObject.SendMessage("Die", SendMessageOptions.DontRequireReceiver);
        }
    }

    public void Shoot()
    {
        if (animSpider != null) animSpider.SetBool("isShooting", true);

        if (webBall == null || webSpawn == null || target == null)
        {
            if (animSpider != null) animSpider.SetBool("isShooting", false);
            return;
        }

        GameObject web = Instantiate(webBall, webSpawn.position, Quaternion.identity);

        if (web.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = CalculateLaunchVelocity(webSpawn.position, target.position, 1.0f);
        }

        if (animSpider != null) animSpider.SetBool("isShooting", false);
        Destroy(web, 5.0f);
    }

    private Vector3 CalculateLaunchVelocity(Vector3 origin, Vector3 targetPos, float time)
    {
        Vector3 distance = targetPos - origin;
        Vector3 distanceXZ = new Vector3(distance.x, 0f, distance.z);

        float Sy = distance.y;
        float Sxz = distanceXZ.magnitude;

        float Vxz = Sxz / time;
        float Vy = Sy / time + 0.5f * Mathf.Abs(Physics.gravity.y) * time;

        Vector3 result = distanceXZ.normalized * Vxz;
        result.y = Vy;

        return result;
    }
}
