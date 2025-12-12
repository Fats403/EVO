using UnityEngine;

/// <summary>
/// Simple global sound manager for background music.
/// - Plays a looping music track.
/// - Persists across scene loads.
/// - Exposes mute / unmute for UI.
/// </summary>
public class SoundManager : MonoBehaviour
{
    /// <summary>Global singleton instance.</summary>
    public static SoundManager Instance { get; private set; }

    [Header("Music Settings")]
    [Tooltip("Background music clip to loop.")]
    [SerializeField]
    private AudioClip musicClip;

    [Range(0f, 1f)]
    [Tooltip("Base music volume.")]
    [SerializeField]
    private float musicVolume = 0.6f;

    [Tooltip("Start playing music automatically on Awake.")]
    [SerializeField]
    private bool playOnStart = true;

    private AudioSource _musicSource;

    /// <summary>True if the music is currently muted.</summary>
    public bool IsMuted => _musicSource != null && _musicSource.mute;

    private void Awake()
    {
        // Enforce singleton pattern.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureMusicSource();

        if (playOnStart)
        {
            PlayMusic();
        }
    }

    private void EnsureMusicSource()
    {
        if (_musicSource != null)
            return;

        _musicSource = GetComponent<AudioSource>();
        if (_musicSource == null)
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
        }

        _musicSource.clip = musicClip;
        _musicSource.loop = true;
        _musicSource.playOnAwake = false;
        _musicSource.volume = musicVolume;
        _musicSource.mute = false;
    }

    /// <summary>Begin playing the background music (if we have a clip).</summary>
    public void PlayMusic()
    {
        EnsureMusicSource();

        if (_musicSource.clip == null)
        {
            Debug.LogWarning("SoundManager: No music clip assigned.");
            return;
        }

        if (!_musicSource.isPlaying)
        {
            _musicSource.Play();
        }
    }

    /// <summary>Stop playing the background music.</summary>
    public void StopMusic()
    {
        if (_musicSource == null)
            return;

        if (_musicSource.isPlaying)
        {
            _musicSource.Stop();
        }
    }

    /// <summary>Toggle mute state of the music.</summary>
    public void ToggleMute()
    {
        SetMuted(!IsMuted);
    }

    /// <summary>Set music muted / unmuted.</summary>
    public void SetMuted(bool muted)
    {
        EnsureMusicSource();
        _musicSource.mute = muted;
    }
}
