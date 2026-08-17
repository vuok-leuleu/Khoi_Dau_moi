using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using System.Collections.Generic;

/*
 * AudioManager.cs
 * Thư mục: Assets/ThanhVien/NhatTien/Script/Sound/
 * Tác giả: Nhật Tiến
 * 
 * CHỨC NĂNG:
 * Quản lý toàn bộ hệ thống âm thanh trong game (Settings UI, SFX, BGM, UI Sound).
 * - Phân chia 3 kênh chính:
 *   1. BGM (Nhạc nền) - Phát lặp lại, có thể chuyển bài mượt mà.
 *   2. SFX (Hiệu ứng âm thanh) - Tiếng đánh, tiếng bắn, tiếng bước chân, v.v.
 *   3. UI Sound (Âm thanh giao diện) - Tiếng click button, mở panel, thông báo.
 * - Tự động lưu/tải cấu hình (Volume, Mute) qua PlayerPrefs.
 * - Hỗ trợ gán trực tiếp Slider UI hoặc Toggle từ Menu Settings.
 * - Tự động tạo AudioSource nếu chưa được gán thủ công trong Inspector.
 */

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Mixer (Tùy chọn - nếu dùng Mixer)")]
    public AudioMixer mainAudioMixer;
    public string masterVolumeParam = "MasterVolume";
    public string bgmVolumeParam    = "BGMVolume";
    public string sfxVolumeParam    = "SFXVolume";
    public string uiVolumeParam     = "UIVolume";

    [Header("Audio Sources (Tự tạo nếu rỗng)")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;
    public AudioSource uiSource;

    [Header("Âm thanh UI mặc định (Tùy chọn gán sẵn)")]
    public AudioClip defaultButtonClickClip;
    public AudioClip defaultPanelOpenClip;
    public AudioClip defaultPanelCloseClip;
    public AudioClip defaultPurchaseSuccessClip;
    public AudioClip defaultErrorClip;

    [Header("Âm thanh Nhạc nền mặc định (Tùy chọn)")]
    public AudioClip defaultMainBGM;

    // ── Cài đặt Âm lượng (0.0 đến 1.0) ──
    [Header("Chỉ số Âm lượng Hiện tại (Read-Only)")]
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float bgmVolume    = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume    = 1f;
    [Range(0f, 1f)] [SerializeField] private float uiVolume     = 1f;

    // ── Trạng thái Mute ──
    [SerializeField] private bool isMasterMuted = false;
    [SerializeField] private bool isBGMMuted    = false;
    [SerializeField] private bool isSFXMuted    = false;
    [SerializeField] private bool isUIMuted     = false;

    // ── Key PlayerPrefs ──
    private const string KEY_MASTER_VOL   = "Audio_MasterVolume";
    private const string KEY_BGM_VOL      = "Audio_BGMVolume";
    private const string KEY_SFX_VOL      = "Audio_SFXVolume";
    private const string KEY_UI_VOL       = "Audio_UIVolume";

    private const string KEY_MASTER_MUTE  = "Audio_MasterMute";
    private const string KEY_BGM_MUTE     = "Audio_BGMMute";
    private const string KEY_SFX_MUTE     = "Audio_SFXMute";
    private const string KEY_UI_MUTE      = "Audio_UIMute";

    // Properties cho script bên ngoài đọc
    public float MasterVolume => masterVolume;
    public float BGMVolume    => bgmVolume;
    public float SFXVolume    => sfxVolume;
    public float UIVolume     => uiVolume;

    public bool IsMasterMuted => isMasterMuted;
    public bool IsBGMMuted    => isBGMMuted;
    public bool IsSFXMuted    => isSFXMuted;
    public bool IsUIMuted     => isUIMuted;

    // Pool nhỏ cho SFX 3D/nhiều âm thanh trùng nhau
    private List<AudioSource> sfxPool = new List<AudioSource>();

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        EnsureAudioSources();
        LoadSettings();
    }

    void Start()
    {
        ApplyAllVolumes();

        // Tự động phát nhạc nền mặc định nếu có
        if (defaultMainBGM != null && (bgmSource != null && !bgmSource.isPlaying))
        {
            PlayBGM(defaultMainBGM);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // TỰ ĐỘNG KHỞI TẠO AUDIO SOURCES
    // ════════════════════════════════════════════════════════════════

    private void EnsureAudioSources()
    {
        if (bgmSource == null)
        {
            GameObject bgmObj = new GameObject("BGM_Source");
            bgmObj.transform.SetParent(transform);
            bgmSource = bgmObj.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            GameObject sfxObj = new GameObject("SFX_Source");
            sfxObj.transform.SetParent(transform);
            sfxSource = sfxObj.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }

        if (uiSource == null)
        {
            GameObject uiObj = new GameObject("UI_Source");
            uiObj.transform.SetParent(transform);
            uiSource = uiObj.AddComponent<AudioSource>();
            uiSource.loop = false;
            uiSource.playOnAwake = false;
        }
    }

    // ════════════════════════════════════════════════════════════════
    // 1. NHẠC NỀN (BGM)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Phát Nhạc Nền (BGM). Nếu đang phát cùng 1 clip thì không phát lại.
    /// </summary>
    public void PlayBGM(AudioClip clip, bool loop = true)
    {
        if (clip == null || bgmSource == null) return;

        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.Play();
        UpdateBGMVolumeInternal();
    }

    /// <summary>
    /// Dừng phát nhạc nền.
    /// </summary>
    public void StopBGM()
    {
        if (bgmSource != null) bgmSource.Stop();
    }

    /// <summary>
    /// Tạm dừng nhạc nền.
    /// </summary>
    public void PauseBGM()
    {
        if (bgmSource != null) bgmSource.Pause();
    }

    /// <summary>
    /// Tiếu tục phát nhạc nền.
    /// </summary>
    public void ResumeBGM()
    {
        if (bgmSource != null && !bgmSource.isPlaying) bgmSource.UnPause();
    }

    // ════════════════════════════════════════════════════════════════
    // 2. HIỆU ỨNG ÂM THANH (SFX)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Phát 1 hiệu ứng âm thanh 2D (SFX).
    /// </summary>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || isSFXMuted || isMasterMuted) return;

        float finalVol = sfxVolume * masterVolume * volumeScale;
        if (sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, finalVol);
        }
    }

    /// <summary>
    /// Phát SFX tại một vị trí 3D trong không gian game.
    /// </summary>
    public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volumeScale = 1f)
    {
        if (clip == null || isSFXMuted || isMasterMuted) return;

        float finalVol = sfxVolume * masterVolume * volumeScale;
        AudioSource.PlayClipAtPoint(clip, position, finalVol);
    }

    // ════════════════════════════════════════════════════════════════
    // 3. ÂM THANH UI (UI SOUND)
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Phát âm thanh UI giao diện.
    /// </summary>
    public void PlayUISound(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || isUIMuted || isMasterMuted) return;

        float finalVol = uiVolume * masterVolume * volumeScale;
        if (uiSource != null)
        {
            uiSource.PlayOneShot(clip, finalVol);
        }
    }

    // ── Các hàm tiện ích phát âm thanh UI mặc định ──
    public void PlayButtonClick()        => PlayUISound(defaultButtonClickClip);
    public void PlayPanelOpen()          => PlayUISound(defaultPanelOpenClip);
    public void PlayPanelClose()         => PlayUISound(defaultPanelCloseClip);
    public void PlayPurchaseSuccess()    => PlayUISound(defaultPurchaseSuccessClip);
    public void PlayErrorSound()         => PlayUISound(defaultErrorClip);

    // ════════════════════════════════════════════════════════════════
    // QUẢN LÝ ÂM LƯỢNG & ĐIỀU CHỈNH SETTINGS
    // ════════════════════════════════════════════════════════════════

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        ApplyAllVolumes();
        SaveSettings();
    }

    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        UpdateBGMVolumeInternal();
        SaveSettings();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        UpdateSFXVolumeInternal();
        SaveSettings();
    }

    public void SetUIVolume(float volume)
    {
        uiVolume = Mathf.Clamp01(volume);
        UpdateUIVolumeInternal();
        SaveSettings();
    }

    // ── Mute Toggles ──

    public void SetMasterMute(bool isMuted)
    {
        isMasterMuted = isMuted;
        ApplyAllVolumes();
        SaveSettings();
    }

    public void SetBGMMute(bool isMuted)
    {
        isBGMMuted = isMuted;
        UpdateBGMVolumeInternal();
        SaveSettings();
    }

    public void SetSFXMute(bool isMuted)
    {
        isSFXMuted = isMuted;
        UpdateSFXVolumeInternal();
        SaveSettings();
    }

    public void SetUIMute(bool isMuted)
    {
        isUIMuted = isMuted;
        UpdateUIVolumeInternal();
        SaveSettings();
    }

    public void ToggleMasterMute() => SetMasterMute(!isMasterMuted);
    public void ToggleBGMMute()    => SetBGMMute(!isBGMMuted);
    public void ToggleSFXMute()    => SetSFXMute(!isSFXMuted);
    public void ToggleUIMute()     => SetUIMute(!isUIMuted);

    // ── Cập nhật volume thực tế lên AudioSources / AudioMixer ──

    private void ApplyAllVolumes()
    {
        // Cập nhật âm lượng tổng toàn game của Unity (ảnh hưởng đến toàn bộ AudioSource trong game)
        AudioListener.volume = isMasterMuted ? 0f : masterVolume;

        UpdateBGMVolumeInternal();
        UpdateSFXVolumeInternal();
        UpdateUIVolumeInternal();

        if (mainAudioMixer != null)
        {
            SetMixerVolume(masterVolumeParam, isMasterMuted ? 0f : masterVolume);
        }
    }

    private void UpdateBGMVolumeInternal()
    {
        bool muted = isMasterMuted || isBGMMuted;
        float effectiveVolume = muted ? 0f : (bgmVolume * masterVolume);

        if (bgmSource != null)
        {
            bgmSource.volume = effectiveVolume;
        }

        if (mainAudioMixer != null)
        {
            SetMixerVolume(bgmVolumeParam, muted ? 0f : bgmVolume);
        }

        // Đồng bộ sang SoundMgr nếu có
        if (SoundMgr.HasInstance && SoundMgr.Ins != null)
        {
            SoundMgr.Ins.SetBGMVolume(muted ? 0f : bgmVolume);
        }

        // Đồng bộ sang SoundManager của thành viên khác nếu có
        SoundManager[] dungManagers = Object.FindObjectsByType<SoundManager>(FindObjectsSortMode.None);
        foreach (var sm in dungManagers)
        {
            if (sm != null)
            {
                sm.bgmVolume = effectiveVolume;
            }
        }
    }

    private void UpdateSFXVolumeInternal()
    {
        if (sfxSource != null)
        {
            bool muted = isMasterMuted || isSFXMuted;
            sfxSource.volume = muted ? 0f : (sfxVolume * masterVolume);
        }

        if (mainAudioMixer != null)
        {
            SetMixerVolume(sfxVolumeParam, (isMasterMuted || isSFXMuted) ? 0f : sfxVolume);
        }
    }

    private void UpdateUIVolumeInternal()
    {
        if (uiSource != null)
        {
            bool muted = isMasterMuted || isUIMuted;
            uiSource.volume = muted ? 0f : (uiVolume * masterVolume);
        }

        if (mainAudioMixer != null)
        {
            SetMixerVolume(uiVolumeParam, (isMasterMuted || isUIMuted) ? 0f : uiVolume);
        }
    }

    private void SetMixerVolume(string paramName, float normalizedVol)
    {
        if (string.IsNullOrEmpty(paramName)) return;

        // Chuyển tuyến tính 0..1 sang dB (-80dB đến 0dB)
        float dB = (normalizedVol <= 0.0001f) ? -80f : 20f * Mathf.Log10(normalizedVol);
        mainAudioMixer.SetFloat(paramName, dB);
    }

    // ════════════════════════════════════════════════════════════════
    // BÌNH THƯỜNG HÓA GẮN KẾT VỚI SLIDERS VÀ TOGGLES TRONG SETTINGS UI
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gán liên kết nhanh các Slider UI trong Menu Settings.
    /// </summary>
    public void BindSettingControls(Slider masterSld, Slider bgmSld, Slider sfxSld, Slider uiSld)
    {
        if (masterSld != null)
        {
            masterSld.value = masterVolume;
            masterSld.onValueChanged.RemoveAllListeners();
            masterSld.onValueChanged.AddListener(SetMasterVolume);
        }

        if (bgmSld != null)
        {
            bgmSld.value = bgmVolume;
            bgmSld.onValueChanged.RemoveAllListeners();
            bgmSld.onValueChanged.AddListener(SetBGMVolume);
        }

        if (sfxSld != null)
        {
            sfxSld.value = sfxVolume;
            sfxSld.onValueChanged.RemoveAllListeners();
            sfxSld.onValueChanged.AddListener(SetSFXVolume);
        }

        if (uiSld != null)
        {
            uiSld.value = uiVolume;
            uiSld.onValueChanged.RemoveAllListeners();
            uiSld.onValueChanged.AddListener(SetUIVolume);
        }
    }

    /// <summary>
    /// Gán liên kết nhanh các Toggle Mute UI trong Menu Settings.
    /// </summary>
    public void BindSettingToggles(Toggle masterTgl, Toggle bgmTgl, Toggle sfxTgl, Toggle uiTgl)
    {
        if (masterTgl != null)
        {
            masterTgl.isOn = isMasterMuted;
            masterTgl.onValueChanged.RemoveAllListeners();
            masterTgl.onValueChanged.AddListener(SetMasterMute);
        }

        if (bgmTgl != null)
        {
            bgmTgl.isOn = isBGMMuted;
            bgmTgl.onValueChanged.RemoveAllListeners();
            bgmTgl.onValueChanged.AddListener(SetBGMMute);
        }

        if (sfxTgl != null)
        {
            sfxTgl.isOn = isSFXMuted;
            sfxTgl.onValueChanged.RemoveAllListeners();
            sfxTgl.onValueChanged.AddListener(SetSFXMute);
        }

        if (uiTgl != null)
        {
            uiTgl.isOn = isUIMuted;
            uiTgl.onValueChanged.RemoveAllListeners();
            uiTgl.onValueChanged.AddListener(SetUIMute);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // SAVE & LOAD VIA PLAYERPREFS
    // ════════════════════════════════════════════════════════════════

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(KEY_MASTER_VOL, masterVolume);
        PlayerPrefs.SetFloat(KEY_BGM_VOL, bgmVolume);
        PlayerPrefs.SetFloat(KEY_SFX_VOL, sfxVolume);
        PlayerPrefs.SetFloat(KEY_UI_VOL, uiVolume);

        PlayerPrefs.SetInt(KEY_MASTER_MUTE, isMasterMuted ? 1 : 0);
        PlayerPrefs.SetInt(KEY_BGM_MUTE, isBGMMuted ? 1 : 0);
        PlayerPrefs.SetInt(KEY_SFX_MUTE, isSFXMuted ? 1 : 0);
        PlayerPrefs.SetInt(KEY_UI_MUTE, isUIMuted ? 1 : 0);

        PlayerPrefs.Save();
    }

    public void LoadSettings()
    {
        masterVolume  = PlayerPrefs.GetFloat(KEY_MASTER_VOL, 1f);
        bgmVolume     = PlayerPrefs.GetFloat(KEY_BGM_VOL, 0.8f);
        sfxVolume     = PlayerPrefs.GetFloat(KEY_SFX_VOL, 1f);
        uiVolume      = PlayerPrefs.GetFloat(KEY_UI_VOL, 1f);

        isMasterMuted = PlayerPrefs.GetInt(KEY_MASTER_MUTE, 0) == 1;
        isBGMMuted    = PlayerPrefs.GetInt(KEY_BGM_MUTE, 0) == 1;
        isSFXMuted    = PlayerPrefs.GetInt(KEY_SFX_MUTE, 0) == 1;
        isUIMuted     = PlayerPrefs.GetInt(KEY_UI_MUTE, 0) == 1;
    }
}
