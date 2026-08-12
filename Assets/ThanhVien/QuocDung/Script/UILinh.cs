using UnityEngine;
using TMPro;
using System.IO;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement; // 🔥 THÊM: Để reload lại đúng Scene ban đầu

public class UILinh : MonoBehaviour
{
    public static UILinh Ins { get; private set; }

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
        if (Ins == null) Ins = this;
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
                entry.isInitialBuildNeeded = (building.CurrentLevel > 0) ? false : building.IsInitialBuildNeeded;

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

                bool isReturningFromBattle = BattleData.HasData || BattleData.HasResult || BattleData.LastBattleWasVictory;

                foreach (BuildingSaveEntry entry in saveData.buildings)
                {
                    UpgradeableBuilding building = Array.Find(sceneBuildings, b => b.gameObject.name == entry.buildingName);
                    if (building != null)
                    {
                        bool initBuildNeeded = isReturningFromBattle ? false : entry.isInitialBuildNeeded;
                        building.LoadBuildingData(entry.level, entry.isRuined, initBuildNeeded);

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
    /// 🔥 Phím B: Xóa sạch 100% PlayerPrefs + File Save JSON và Reload lại Scene về trạng thái ban đầu của Scene
    /// </summary>
    public void ResetGame()
    {
        try
        {
            // 1. Xóa toàn bộ file save JSON trong persistentDataPath
            if (Directory.Exists(Application.persistentDataPath))
            {
                string[] saveFiles = Directory.GetFiles(Application.persistentDataPath, "*.json");
                foreach (var file in saveFiles)
                {
                    try
                    {
                        File.Delete(file);
                        Debug.Log($"[UILinh] Đã xóa file save: {file}");
                    }
                    catch { }
                }
            }

            // 2. Xóa sạch 100% PlayerPrefs (Bao gồm dữ liệu Vùng đất SettlementZone, Tutorial, Level...)
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[UILinh] Đã xóa sạch 100% PlayerPrefs.");

            // 3. Reset toàn bộ RAM & BattleData
            BattleData.ResetData();
            BattleData.SkipAutoLoadOnNextSceneLoad = true;
            BattleData.LastBattleWasVictory = false;

            JsonDataManager.ResetEndGameStats();

            // 4. Load lại đúng Scene hiện tại → Scene sẽ khởi tạo 100% mới tinh
            Scene currentScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(currentScene.buildIndex);

            Debug.Log("[UILinh] 🎉 Đã hoàn nguyên 100% game về trạng thái khởi tạo ban đầu thành công!");
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

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[UILinh] Đã xóa sạch PlayerPrefs.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[UILinh] Lỗi khi xóa file Save: {e.Message}");
        }
    }
}