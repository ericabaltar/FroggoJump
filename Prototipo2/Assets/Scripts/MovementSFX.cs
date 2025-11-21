using UnityEngine;

[DisallowMultipleComponent]
public class MovementSFX : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private AudioClip[] moveClips;

    [Header("Volumen / Pitch")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
    [SerializeField] private float pitch = 1.0f;
    [SerializeField] private float pitchJitter = 0.06f; 

    [Header("Opciones")]
    [SerializeField] private bool noImmediateRepeat = true; 
    [SerializeField] private bool use2D = true;             

    private AudioSource source;
    private int lastIndex = -1;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = use2D ? 0f : 1f;
    }

    public void PlayMove()
    {
        if (moveClips == null || moveClips.Length == 0) return;

        int idx = Random.Range(0, moveClips.Length);
        if (noImmediateRepeat && moveClips.Length > 1)
        {
            
            if (idx == lastIndex) idx = (idx + 1 + Random.Range(0, moveClips.Length - 1)) % moveClips.Length;
        }
        lastIndex = idx;

        float p = pitch + Random.Range(-pitchJitter, pitchJitter);
        source.pitch = Mathf.Max(0.1f, p);
        source.PlayOneShot(moveClips[idx], volume);
    }
}
