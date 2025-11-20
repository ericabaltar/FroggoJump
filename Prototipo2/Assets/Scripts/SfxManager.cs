using UnityEngine;

[DisallowMultipleComponent]
public class SfxManager : MonoBehaviour
{
    public static SfxManager Instance { get; private set; }

    [Header("AudioSource 2D para SFX")]
    [SerializeField] private AudioSource sfxSource;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f; 
            sfxSource.volume = 0.8f;
        }
    }

    public void PlayOneShot(AudioClip clip, float volume = 1f, float pitchJitter = 0.06f, float basePitch = 1f)
    {
        if (clip == null || sfxSource == null) return;

        float p = basePitch + Random.Range(-pitchJitter, pitchJitter);
        sfxSource.pitch = Mathf.Max(0.1f, p);
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }


    public void PlayOneShotRandom(AudioClip[] clips, float volume = 1f, float pitchJitter = 0.06f, float basePitch = 1f)
    {
        if (clips == null || clips.Length == 0) return;
        var clip = clips[Random.Range(0, clips.Length)];
        PlayOneShot(clip, volume, pitchJitter, basePitch);
    }
}
