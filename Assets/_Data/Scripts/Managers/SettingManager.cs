using UnityEngine;

/*
 * SettingManager.cs
 * Folder: Scripts/System/
 * Dự án: KHẨN HOANG (PENTA DEV)
 */

public class SettingManager : Singleton<SettingManager>
{
    public float masterVolume { get; private set; } = 1f;
    public float musicVolume { get; private set; } = 1f;
    public int screenModeIndex { get; private set; } = 1; // 0: Window, 1: Fullscreen, 2: 1920x1080
    public float mouseSpeed { get; private set; } = 5f;

    private void Awake()
    {
        base.Awake();
        LoadSettings();
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMasterVolume(masterVolume);
        }

        if (SoundMgr.HasInstance && SoundMgr.Ins != null)
        {
            SoundMgr.Ins.SetMasterVolume(masterVolume);
        }
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetBGMVolume(musicVolume);
        }

        if (SoundMgr.HasInstance && SoundMgr.Ins != null)
        {
            SoundMgr.Ins.SetBGMVolume(musicVolume);
        }

        // Đồng bộ các SoundManager khác của dự án nếu có
        SoundManager[] managers = Object.FindObjectsByType<SoundManager>(FindObjectsSortMode.None);
        foreach (var sm in managers)
        {
            if (sm != null)
            {
                sm.bgmVolume = musicVolume * masterVolume;
            }
        }
    }

    // HÀM MỚI: Xử lý Dropdown Màn hình & Độ phân giải
    public void SetScreenMode(int index)
    {
        screenModeIndex = index;
        PlayerPrefs.SetInt("ScreenModeIndex", screenModeIndex);

        switch (screenModeIndex)
        {
            case 0: // Windowed (Cửa sổ - lấy độ phân giải hiện tại của màn nhưng không tràn viền)
                Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.Windowed);
                break;
            case 1: // Fullscreen toàn màn hình theo màn gốc của người chơi
                Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
                break;
            case 2: // Ép độ phân giải 1920 x 1080 Fullscreen
                Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow);
                break;
        }
    }

    public void SetMouseSpeed(float speed)
    {
        mouseSpeed = Mathf.Max(0.1f, speed);
        PlayerPrefs.SetFloat("MouseSpeed", mouseSpeed);
    }

    private void LoadSettings()
    {
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 1f);
        screenModeIndex = PlayerPrefs.GetInt("ScreenModeIndex", 1); // Mặc định là Fullscreen (1)
        mouseSpeed = PlayerPrefs.GetFloat("MouseSpeed", 5f);

        // Đội đồng bộ volume ngay khi vừa load settings
        SetMasterVolume(masterVolume);
        SetMusicVolume(musicVolume);

        // Áp dụng cấu hình màn hình ngay khi mở game
        SetScreenMode(screenModeIndex);
    }
}