using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MovingBase : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 2.5f;   // unidades/segundo
    public int minX = -5;
    public int maxX = 5;
    public bool startMovingRight = true;

    // Desplazamiento horizontal de este frame (lo leerá el Player para arrastre)
    public float DeltaX { get; private set; }

    private float dir;     // 1 o -1
    private float zRow;    // fila z fija
    private float yHeight; // altura fija de la fila
    private float lastX;

    public void Init(int z, float y, int rangeMinX, int rangeMaxX, bool moveRight, float moveSpeed, int startX)
    {
        zRow = z;
        yHeight = y;
        minX = rangeMinX;
        maxX = rangeMaxX;
        startMovingRight = moveRight;
        speed = moveSpeed;

        dir = startMovingRight ? 1f : -1f;

        Vector3 p = transform.position;
        p.x = Mathf.Clamp(startX, minX, maxX);
        p.y = yHeight;
        p.z = zRow;
        transform.position = p;

        lastX = p.x;
        DeltaX = 0f;

        // Asegura trigger
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        // Asegura tag (también configúralo en el prefab): "MovingBase"
        gameObject.tag = "MovingBase";
    }

    void Update()
    {
        // Mover en X
        Vector3 p = transform.position;
        p.x += dir * speed * Time.deltaTime;

        // Rebotar en límites
        if (p.x > maxX) { p.x = maxX; dir = -1f; }
        else if (p.x < minX) { p.x = minX; dir = 1f; }

        // Mantener Y/Z fijos
        p.y = yHeight;
        p.z = zRow;

        // Delta para arrastre del jugador
        DeltaX = p.x - lastX;

        transform.position = p;
        lastX = p.x;
    }
}



