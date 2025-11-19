using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Fly : MonoBehaviour
{
    [SerializeField] private SphereCollider sphereCollider;
    [Header("Stamina")]
    [Tooltip("Cuánta stamina otorga esta mosca al comerse.")]
    public int staminaAmount = 3;

    [Header("Detección")]
    [Tooltip("Radio mínimo para poder 'comer' la mosca. Usa un SphereCollider como trigger con este radio.")]
    public float regularPickupRadius = 0.2f;
    public float upgradedPickupRadius = 1f;

    [Header("Animación")]
    public bool bob = true;
    public float bobAmplitude = 0.1f;
    public float bobSpeed = 2f;

    private Vector3 basePos;

    private void Awake()
    {
        basePos = transform.position;
    }

    private void Update()
    {
        if (bob)
        {
            float y = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            Vector3 p = basePos;
            p.y += y;
            
            transform.position = p;
        }
    }

    public void SetBasePos(Vector3 newBasePos)
    {
        basePos = newBasePos;
    }

    public void SetPickupRange()
    {
        if (GameManager.Instance.GetUpgradedRange())
            sphereCollider.radius = upgradedPickupRadius;
        else
            sphereCollider.radius = regularPickupRadius;
    }
}
