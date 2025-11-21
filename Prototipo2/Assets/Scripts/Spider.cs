using Mono.Cecil;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Spider : MonoBehaviour
{
    public Transform spider;
    public Transform character;
    public Transform webSpawn;
    public Transform target;
    public GameObject webBall;
    public float speed = 0.005f;
    public GameObject frog;
    public Animator animSpider;

    public float shootForce = 5.0f;
    public float fireRate = 50.0f;
    public float nextFireTime = 10.0f;

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
        spider.transform.position = new Vector3(spider.position.x, spider.position.y, spider.position.z + speed);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject == frog)
        {
            Destroy(frog);
            EditorApplication.ExitPlaymode();
            //Application.Quit();
        }
    }

    public void Shoot()
    {
        animSpider.SetBool("isShooting", true);
        GameObject web = Instantiate(webBall, webSpawn.position, Quaternion.identity);

        Rigidbody rb = web.GetComponent<Rigidbody>();

        rb.linearVelocity = CalculateLaunchVelocity(webSpawn.position, target.position, 1.0f);

        animSpider.SetBool("isShooting", false);
        Destroy(web, 5.0f);
    }

    private Vector3 CalculateLaunchVelocity(Vector3 origin, Vector3 target, float time)
    {
        Vector3 distance = target - origin;
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