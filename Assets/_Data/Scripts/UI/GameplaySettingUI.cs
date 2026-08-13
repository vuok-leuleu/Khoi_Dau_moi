using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameplaySettingUI : MonoBehaviour
{
    [Header("UI Cấu Hình")]
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public TMP_Dropdown screenDropdown; 
    public Slider mouseSpeedSlider;

    [Header("Các Nút Chức Năng Gameplay")]
    public Button resumeButton;
    public Button mainMenuButton;
    public Button quitButton;
    public Button saveButton;
    public Button loadButton;

    private void Awake()
    {
        // Đăng ký sự kiện Click cho các nút 1 lần duy nhất ở Awake
        if (resumeButton) resumeButton.onClick.AddListener(ResumeGame);
        if (mainMenuButton) mainMenuButton.onClick.AddListener(BackToMainMenu);
        if (quitButton) quitButton.onClick.AddListener(QuitGame);
        if (saveButton) saveButton.onClick.AddListener(SaveGameData);
        if (loadButton) loadButton.onClick.AddListener(LoadGameData);
    }

    private void OnEnable()
    {
        // Chỉ cập nhật thông số Slider/Dropdown khi bảng được bật
        if (SettingManager.HasInstance && SettingManager.Ins != null)
        {
            masterVolumeSlider.value = SettingManager.Ins.masterVolume;
            musicVolumeSlider.value = SettingManager.Ins.musicVolume;
            screenDropdown.value = SettingManager.Ins.screenModeIndex;
            mouseSpeedSlider.value = SettingManager.Ins.mouseSpeed;

            masterVolumeSlider.onValueChanged.AddListener(SettingManager.Ins.SetMasterVolume);
            musicVolumeSlider.onValueChanged.AddListener(SettingManager.Ins.SetMusicVolume);
            screenDropdown.onValueChanged.AddListener(SettingManager.Ins.SetScreenMode);
            mouseSpeedSlider.onValueChanged.AddListener(SettingManager.Ins.SetMouseSpeed);
        }
    }

    private void OnDisable()
    {
        if (SettingManager.HasInstance && SettingManager.Ins != null)
        {
            masterVolumeSlider.onValueChanged.RemoveAllListeners();
            musicVolumeSlider.onValueChanged.RemoveAllListeners();
            screenDropdown.onValueChanged.RemoveAllListeners();
            mouseSpeedSlider.onValueChanged.RemoveAllListeners();
        }

        PlayerPrefs.Save();
    }

    // ====================================================================
    // CÁC HÀM ĐIỀU KHIỂN MỞ / ĐÓNG BẢNG SETTING CHỦ ĐỘNG
    // ====================================================================

    /// <summary>
    /// Gọi hàm này khi người chơi bấm nút "Bánh Răng / Menu Setting"
    /// </summary>
    public void OpenPanel()
    {
        gameObject.SetActive(true);
        Time.timeScale = 0f; // Chỉ đóng băng game khi người chơi THỰC SỰ bấm mở bảng
    }

    /// <summary>
    /// Gọi hàm này khi đóng bảng Setting
    /// </summary>
    public void ClosePanel()
    {
        Time.timeScale = 1f; // Khôi phục thời gian chạy bình thường
        gameObject.SetActive(false);
    }

    public void ResumeGame()
    {
        ClosePanel();
    }

    public void BackToMainMenu()
    {
        Time.timeScale = 1f; // Đảm bảo trả timeScale về 1 trước khi chuyển Scene
        SceneManager.LoadScene("Menu");
    }

    public void QuitGame()
    {
        Debug.Log("Thoát game!");
        Application.Quit();
    }

    public void SaveGameData()
    {
        if (BuildingSystem.Ins != null)
        {
            BuildingSystem.Ins.SaveBuildingsToSlot(1);
            Debug.Log("[GameplaySettingUI] ✅ Đã lưu dữ liệu màn chơi JSON vào Slot 1 thành công!");
        }
    }

    public void LoadGameData()
    {
        if (ConstructionManager.Ins != null)
        {
            ConstructionManager.Ins.ResetBuildingCounts();
        }

        if (BuildingSystem.Ins != null)
        {
            BuildingSystem.Ins.LoadBuildingsFromSlot(1);

            if (ConstructionManager.Ins != null)
            {
                ConstructionManager.Ins.UpdateAllCostUI();
            }
            
            Debug.Log("[GameplaySettingUI] ✅ Đã tải lại tiến trình chơi JSON từ Slot 1 thành công!");
            ClosePanel();
        }
    }
}