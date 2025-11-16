using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Fly : MonoBehaviour
{
    [Header("Stamina")]
    [Tooltip("Cuánta stamina otorga esta mosca al comerse.")]
    public int staminaAmount = 3;

    [Header("Detección")]
    [Tooltip("Radio mínimo para poder 'comer' la mosca. Usa un SphereCollider como trigger con este radio.")]
    public float pickupRadius = 0.8f;

    [Header("Animación")]
    public bool bob = true;
    public float bobAmplitude = 0.1f;
    public float bobSpeed = 2f;

    private Vector3 basePos;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        if (col is SphereCollider sc) sc.radius = pickupRadius;
        gameObject.tag = "Fly";
    }

    private void Awake()
    {
        basePos = transform.position;
        var col = GetComponent<Collider>();
        if (col is SphereCollider sc) sc.isTrigger = true;
    }

    private void Update()
    {
        if (bob)
        {
            float y = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            var p = basePos; p.y += y;
            transform.position = p;
        }
    }
}
