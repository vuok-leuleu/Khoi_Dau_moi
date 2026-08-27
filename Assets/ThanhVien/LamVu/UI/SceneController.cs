using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    [Header("Settings Configuration")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Game Launch")]
    [Tooltip("Scene bắt đầu khi người chơi chọn Chơi mới.")]
    [SerializeField] private string newGameSceneName = "BuildMapTest";
    [Tooltip("Scene dự phòng khi bản lưu cũ không có tên scene hợp lệ.")]
    [SerializeField] private string fallbackGameplaySceneName = "BuildMapTest";
    [SerializeField, Min(1)] private int saveSlot = 1;
    [Tooltip("Nút Tiếp tục trên Menu. Nút này chỉ hiện khi có bản lưu hợp lệ.")]
    [SerializeField] private GameObject continueButton;

    private const string SoldierSaveFileName = "game_save_data.json";
    private const string LegacySaveFileName = "builder.json";
    private const string EndGameStatsFileName = "endgame_stats.json";

    private void Awake()
    {
        RefreshContinueButton();
    }

    /// <summary>
    /// Bắt đầu một ván hoàn toàn mới. Dữ liệu tiến trình của slot hiện tại,
    /// công trình/lính và PlayerPrefs của màn chơi sẽ được xóa trước khi vào game.
    /// </summary>
    public void StartNewGame()
    {
        Time.timeScale = 1f;
        ClearCurrentGameProgress();

        BattleData.ResetData();
        // BattleData có cơ chế tự nạp save khi đổi scene. Ván mới phải bỏ qua đúng một lần.
        BattleData.SkipAutoLoadOnNextSceneLoad = true;

        LoadGameplayScene(newGameSceneName, fallbackGameplaySceneName);
    }

    /// <summary>
    /// Mở lại đúng scene của bản lưu ở slot hiện tại. Gắn hàm này cho nút "Tiếp tục".
    /// </summary>
    public void ContinueGame()
    {
        Time.timeScale = 1f;

        if (!TryReadSaveData(out JsonDataManager.GameSaveData saveData))
        {
            Debug.LogWarning("[SceneController] Chưa có bản lưu để tiếp tục.");
            return;
        }

        // Không mang theo kết quả/trạng thái của một trận Battle cũ vào phiên Continue mới.
        BattleData.ResetData();
        BattleData.SkipAutoLoadOnNextSceneLoad = false;

        string sceneToLoad = string.IsNullOrWhiteSpace(saveData.sceneName)
            ? fallbackGameplaySceneName
            : saveData.sceneName;

        LoadGameplayScene(sceneToLoad, fallbackGameplaySceneName);
    }

    /// <summary>
    /// Dùng cho UI để bật/tắt nút "Tiếp tục" khi chưa từng có bản lưu.
    /// </summary>
    public bool HasSavedGame()
    {
        return TryReadSaveData(out _);
    }

    /// <summary>
    /// Đồng bộ trạng thái hiển thị của nút Tiếp tục với bản lưu hiện có.
    /// Có thể gọi lại hàm này nếu Menu được mở mà không load lại scene.
    /// </summary>
    public void RefreshContinueButton()
    {
        if (continueButton != null)
        {
            continueButton.SetActive(HasSavedGame());
        }
    }

    /// <summary>
    /// Hàm chuyển cảnh nhận tên Scene trực tiếp từ sự kiện OnClick UI
    /// </summary>
    /// <param name="sceneName">Tên chính xác của Scene cần chuyển đến</param>
    public void LoadSceneByName(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError("Tên Scene truyền vào bị trống! Vui lòng kiểm tra lại trong OnClick.");
        }
    }

    private void LoadGameplayScene(string requestedSceneName, string fallbackSceneName)
    {
        string sceneName = requestedSceneName;

        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"[SceneController] Không thể tải scene '{requestedSceneName}'. Dùng scene dự phòng '{fallbackSceneName}'.");
            sceneName = fallbackSceneName;
        }

        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("[SceneController] Chưa cấu hình scene gameplay hợp lệ trong Build Settings.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private bool TryReadSaveData(out JsonDataManager.GameSaveData saveData)
    {
        saveData = null;
        string path = Path.Combine(Application.persistentDataPath, $"save_slot_{saveSlot}.json");

        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            saveData = JsonUtility.FromJson<JsonDataManager.GameSaveData>(json);
            return saveData != null;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SceneController] Không thể đọc bản lưu: {exception.Message}");
            return false;
        }
    }

    private void ClearCurrentGameProgress()
    {
        string[] filesToDelete =
        {
            $"save_slot_{saveSlot}.json",
            SoldierSaveFileName,
            LegacySaveFileName,
            EndGameStatsFileName
        };

        foreach (string fileName in filesToDelete)
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            if (!File.Exists(path)) continue;

            try
            {
                File.Delete(path);
                Debug.Log($"[SceneController] Đã xóa dữ liệu ván cũ: {fileName}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SceneController] Không thể xóa '{fileName}': {exception.Message}");
            }
        }

        // Settlement, tutorial và các trạng thái tiến trình khác đang được lưu bằng PlayerPrefs.
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Hàm CHUYÊN DÙNG ĐỂ MỞ Setting Panel (Gán cho nút Setting ở Menu chính)
    /// </summary>
    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("Chưa gán Object Settings vào SceneController trong Inspector!");
        }
    }

    /// <summary>
    /// Hàm CHUYÊN DÙNG ĐỂ ĐÓNG Setting Panel (Gán cho nút X hoặc nút Back bên trong Panel)
    /// </summary>
    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
        else
        {
            Debug.LogError("Chưa gán Object Settings vào SceneController trong Inspector!");
        }
    }

    /// <summary>
    /// Hàm thoát game hoàn toàn
    /// </summary>
    public void ExitGame()
    {
        Debug.Log("Đã bấm nút Thoát Game!");
        
        // Thoát ứng dụng khi đã build thành phẩm (PC/Mobile)
        Application.Quit();

        #if UNITY_EDITOR
        // Dừng chế độ Playmode nếu đang test trong Unity Editor
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}
