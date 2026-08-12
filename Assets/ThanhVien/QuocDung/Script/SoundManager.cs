using UnityEngine;

/// <summary>
/// Quản lý nhạc nền (Background Music - BGM) đơn giản và độc lập.
/// Gán script này vào 1 GameObject trong Scene và gán file nhạc vào ô BGM Clip.
/// </summary>
public class SoundManager : MonoBehaviour
{
    [Header("--- Background Music Settings ---")]
    [Tooltip("File nhạc nền (AudioClip) cần phát")]
    public AudioClip bgmClip;

    [Range(0f, 1f)]
    [Tooltip("Âm lượng nhạc nền (0.0 đến 1.0)")]
    public float bgmVolume = 0.5f;

    [Tooltip("Tự động phát nhạc ngay khi load Scene")]
    public bool playOnAwake = true;

    [Tooltip("Lặp lại nhạc nền khi hết bài")]
    public bool loop = true;

    [Tooltip("Giữ nhạc nền tiếp tục phát khi chuyển qua Scene khác")]
    public bool dontDestroyOnLoad = false;

    private AudioSource audioSource;

    private void Awake()
    {
        if (dontDestroyOnLoad)
        {
            SoundManager[] existingManagers = FindObjectsByType<SoundManager>(FindObjectsSortMode.None);
            if (existingManagers.Length > 1)
            {
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        if (playOnAwake && bgmClip != null)
        {
            PlayBGM(bgmClip);
        }
    }

    private void Update()
    {
        if (audioSource != null && audioSource.volume != bgmVolume)
        {
            audioSource.volume = bgmVolume;
        }
    }

    #region Public API

    /// <summary>
    /// Phát nhạc nền
    /// </summary>
    public void PlayBGM(AudioClip clip = null)
    {
        if (audioSource == null) return;

        AudioClip clipToPlay = clip != null ? clip : bgmClip;
        if (clipToPlay == null)
        {
            Debug.LogWarning("[SoundManager] Chưa gán BGM Clip trong Inspector!");
            return;
        }

        audioSource.clip = clipToPlay;
        audioSource.volume = bgmVolume;
        audioSource.loop = loop;
        audioSource.Play();
    }

    /// <summary>
    /// Dừng nhạc nền
    /// </summary>
    public void StopBGM()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    /// <summary>
    /// Tạm dừng nhạc nền
    /// </summary>
    public void PauseBGM()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Pause();
        }
    }

    /// <summary>
    /// Tiếp tục phát nhạc nền
    /// </summary>
    public void ResumeBGM()
    {
        if (audioSource != null && !audioSource.isPlaying)
        {
            audioSource.UnPause();
        }
    }

    /// <summary>
    /// Thay đổi âm lượng nhạc nền (0.0 đến 1.0)
    /// </summary>
    public void SetVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        if (audioSource != null)
        {
            audioSource.volume = bgmVolume;
        }
    }

    #endregion
}

