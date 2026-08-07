using UnityEngine;
using TMPro;
using System.IO;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement; // 🔥 THÊM: Để reload lại đúng Scene ban đầu

public class UILinh : MonoBehaviour
{
    [Header("UI Reference")]
    public TMP_Text textCount;

    [Header("Settings")]
    public string soldierTag = "Soldier";

    [Header("Save Settings")]
    public string saveFileName = "game_save_data.json";

    private string savePath;
    public int soldierCount;
    private int lastCount = -1;

    [System.Serializable]
    public class BuildingSaveEntry
    {
        public string buildingName;
        public int level;
        public int soldierCount;
        public bool isRuined;
        public bool isInitialBuildNeeded;
    }

    [System.Serializable]
    public class GameSaveData
    {
        public List<BuildingSaveEntry> buildings = new List<BuildingSaveEntry>();
        public int totalSoldierCount;
        public string lastSavedTime;
    }

    void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, saveFileName);
    }

    void Start()
    {
        LoadGame();
    }

    void Update()
    {
        int count = CountSoldiers();

        if (textCount != null)
        {
            textCount.text = "" + count;
        }

        if (count != lastCount)
        {
            lastCount = count;
            SaveGame();
        }

        // ==========================================
        // 🔥 ĐIỀU KHIỂN BẰNG PHÍM TẮT
        // ==========================================
        
        // Phím V: Lưu Game
        if (Input.GetKeyDown(KeyCode.V))
        {
            Debug.Log("[UILinh] [Phím V] Đang lưu game...");
            SaveGame();
        }

        // Phím B: Reset / Hoàn nguyên Scene về trạng thái ban đầu
        if (Input.GetKeyDown(KeyCode.B))
        {
            Debug.Log("[UILinh] [Phím B] Đang tiến hành Hoàn nguyên Scene...");
            ResetGame();
        }

        // Phím N: Xóa file Save JSON
        if (Input.GetKeyDown(KeyCode.N))
        {
            Debug.Log("[UILinh] [Phím N] Đang tiến hành xóa file Save JSON...");
            DeleteSave();
        }

        // Phím M: Load lại bản Save
        if (Input.GetKeyDown(KeyCode.M))
        {
            Debug.Log("[UILinh] [Phím M] Đang tải bản Save...");
            LoadGame();
        }
    }

    public int CountSoldiers()
    {
        GameObject[] soldiers = GameObject.FindGameObjectsWithTag(soldierTag);
        return soldiers.Length;
    }

    public int GetSoldierCount()
    {
        return soldierCount;
    }

    public void SaveGame()
    {
        try
        {
            GameSaveData saveData = new GameSaveData();
            saveData.lastSavedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            UpgradeableBuilding[] buildings = FindObjectsOfType<UpgradeableBuilding>();
            foreach (UpgradeableBuilding building in buildings)
            {
                BuildingSaveEntry entry = new BuildingSaveEntry();
                entry.buildingName = building.gameObject.name;
                entry.level = building.CurrentLevel;
                entry.isRuined = building.IsRuined;
                entry.isInitialBuildNeeded = building.IsInitialBuildNeeded;

                SpawnSoldier spawner = building.GetComponentInChildren<SpawnSoldier>();
                if (spawner != null)
                {
                    entry.soldierCount = spawner.GetActiveSoldiersCount();
                }
                else
                {
                    entry.soldierCount = 0;
                }

                saveData.buildings.Add(entry);
            }

            saveData.totalSoldierCount = CountSoldiers();

            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(savePath, json);
            Debug.Log($"[UILinh] Đã lưu dữ liệu trò chơi thành công vào JSON: {savePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[UILinh] Lỗi khi lưu dữ liệu game: {e.Message}");
        }
    }

    public void LoadGame()
    {
        try
        {
            if (File.Exists(savePath))
            {
                string json = File.ReadAllText(savePath);
                GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);

                UpgradeableBuilding[] sceneBuildings = FindObjectsOfType<UpgradeableBuilding>();

                foreach (BuildingSaveEntry entry in saveData.buildings)
                {
                    UpgradeableBuilding building = Array.Find(sceneBuildings, b => b.gameObject.name == entry.buildingName);
                    if (building != null)
                    {
                        building.LoadBuildingData(entry.level, entry.isRuined, entry.isInitialBuildNeeded);

                        SpawnSoldier spawner = building.GetComponentInChildren<SpawnSoldier>();
                        if (spawner != null)
                        {
                            spawner.LoadAndSpawnSoldiers(entry.soldierCount, entry.level);
                        }
                    }
                }

                lastCount = saveData.totalSoldierCount;
                if (textCount != null)
                {
                    textCount.text = "" + lastCount;
                }

                Debug.Log($"[UILinh] Đã tải dữ liệu trò chơi thành công từ JSON: {savePath}");
            }
            else
            {
                Debug.LogWarning($"[UILinh] Không tìm thấy dữ liệu đã lưu tại: {savePath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[UILinh] Lỗi khi tải file JSON: {e.Message}");
        }
    }

    /// <summary>
    /// 🔥 Phím B: Xóa file Save và Reload lại Scene để khôi phục 100% trạng thái ban đầu của Scene
    /// </summary>
    public void ResetGame()
    {
        try
        {
            // 1. Xóa file save cũ (của UILinh và của JsonDataManager slot 1)
            DeleteSave();

            // Xóa thêm file save_slot_1.json của hệ thống Building
            string slot1Path = System.IO.Path.Combine(Application.persistentDataPath, "save_slot_1.json");
            if (System.IO.File.Exists(slot1Path))
            {
                System.IO.File.Delete(slot1Path);
                Debug.Log("[UILinh] Đã xóa file save_slot_1.json của hệ thống Building.");
            }

            // 2. Bật cờ để BattleData KHÔNG tự động load lại file Save khi scene mới
            BattleData.SkipAutoLoadOnNextSceneLoad = true;
            BattleData.LastBattleWasVictory = false;

            // 3. Load lại đúng Scene hiện tại → Scene sẽ chạy từ dữ liệu gốc 100%
            Scene currentScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(currentScene.buildIndex);

            Debug.Log("[UILinh] Đã hoàn nguyên toàn bộ Scene về trạng thái khởi tạo ban đầu thành công!");
        }
        catch (Exception e)
        {
            Debug.LogError($"[UILinh] Lỗi khi hoàn nguyên Scene: {e.Message}");
        }
    }

    /// <summary>
    /// 🔥 Phím N: Xóa hẳn file Save JSON
    /// </summary>
    public void DeleteSave()
    {
        try
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
                Debug.Log($"[UILinh] Đã xóa thành công file Save JSON tại: {savePath}");
            }
            else
            {
                Debug.LogWarning($"[UILinh] File Save không tồn tại để xóa: {savePath}");
            }

            // 🔥 Xóa cờ trạng thái Tutorial & Ngày đã lưu để có thể test lại từ đầu
            PlayerPrefs.DeleteKey("TutorialCompleted");
            PlayerPrefs.DeleteKey("SavedCurrentWave");
            PlayerPrefs.Save();

            if (DayNightManager.Ins != null)
            {
                DayNightManager.Ins.ResetWaveState();
            }

            Debug.Log("[UILinh] Đã reset trạng thái hoàn thành Tutorial và Day/Wave trong PlayerPrefs.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[UILinh] Lỗi khi xóa file Save: {e.Message}");
        }
    }
}