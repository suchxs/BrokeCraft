using UnityEngine;

/// <summary>
/// Simple global music loop that persists across scenes.
/// Avoids duplicate playback when moving from main menu to game.
/// </summary>
public class PersistentMusicPlayer : MonoBehaviour
{
    public static PersistentMusicPlayer Instance { get; private set; }

    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.priority = 0;
    }

    public static PersistentMusicPlayer EnsureExists(AudioClip clip, float volume)
    {
        if (Instance == null)
        {
            var go = new GameObject("PersistentMusicPlayer");
            go.AddComponent<PersistentMusicPlayer>();
        }

        if (clip != null && Instance.audioSource.clip != clip)
        {
            Instance.audioSource.clip = clip;
        }

        Instance.SetVolume(volume);
        Instance.Play();
        return Instance;
    }

    public void SetVolume(float volume)
    {
        audioSource.volume = Mathf.Clamp01(volume);
    }

    public void Play()
    {
        if (audioSource.clip == null)
        {
            return;
        }

        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }
}
