using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

/*
 * JsonDataManager.cs
 * Folder: Scripts/Managers/
 * Người làm: DŨNG / ĐĂNG
 *
 * Quản lý tài nguyên runtime và Save/Load JSON. (ĐÃ BỎ MAX CAPACITY)
 *
 * EVENT SIGNATURES:
 * OnGoldChanged  → Action<int>
 * OnWoodChanged  → Action<int>
 * OnStoneChanged → Action<int>
 * OnFoodChanged  → Action<int>
 */

public class JsonDataManager : Singleton<JsonDataManager>
{
    [Header("File Settings")]
    public string saveFileName = "builder.json";
    public string configFileName = "building_config.json";

    // ──────────────────────────────────────────────
    // EVENTS  (UIResourceObserver lắng nghe)
    // ──────────────────────────────────────────────

    public event Action<int> OnGoldChanged;
    public event Action<int> OnWoodChanged;
    public event Action<int> OnStoneChanged;
    public event Action<int> OnFoodChanged;

    // ──────────────────────────────────────────────
    // TÀI NGUYÊN RUNTIME
    // ──────────────────────────────────────────────

    public int gold { get; private set; } = 200;
    public int wood { get; private set; } = 500;
    public int stone { get; private set; } = 500;
    public int food { get; private set; } = 0;

    // ──────────────────────────────────────────────
    // BỔ SUNG: TÀI NGUYÊN TÍCH LŨY SUỐT TRẬN ĐẤU (Phục vụ EndGameUI)
    // ──────────────────────────────────────────────
    public int TotalWoodCollected { get; private set; }
    public int TotalStoneCollected { get; private set; }
    public int TotalFoodCollected { get; private set; }
    public int TotalGoldCollected { get; private set; }

    private BuildingConfigRoot _loadedConfig;

    protected override void Awake()
    {
        base.Awake();
        LoadBuildingConfigs();
    }

    // ──────────────────────────────────────────────
    // THÊM TÀI NGUYÊN  (Cộng dồn vô hạn)
    // ──────────────────────────────────────────────

    // ── THÊM HÀM KIỂM TRA ĐỦ TÀI NGUYÊN (Thêm vào JsonDataManager.cs) ──
    public bool HasEnoughResources(int reqWood, int reqStone, int reqFood, int reqGold = 0)
    {
        return wood >= reqWood && stone >= reqStone && food >= reqFood && gold >= reqGold;
    }

    // ── SỬA LẠI CÁC HÀM CỘNG TRỪ TÀI NGUYÊN ĐỂ CHỐNG ÂM ──
    public void AddWood(int amount)
    {
        wood = Mathf.Max(0, wood + amount); // Giữ tài nguyên luôn >= 0
        if (amount > 0) TotalWoodCollected += amount;
        OnWoodChanged?.Invoke(wood);
    }

    public void AddStone(int amount)
    {
        stone = Mathf.Max(0, stone + amount); // Giữ tài nguyên luôn >= 0
        if (amount > 0) TotalStoneCollected += amount;
        OnStoneChanged?.Invoke(stone);
    }

    public void AddFood(int amount)
    {
        food = Mathf.Max(0, food + amount); // Giữ tài nguyên luôn >= 0
        if (amount > 0) TotalFoodCollected += amount;
        OnFoodChanged?.Invoke(food);
    }

    public void SetFood(int amount)
    {
        food = Mathf.Max(0, amount);
        OnFoodChanged?.Invoke(food);
    }

    public void AddGold(int amount)
    {
        gold = Mathf.Max(0, gold + amount); // Giữ tài nguyên luôn >= 0
        if (amount > 0) TotalGoldCollected += amount;
        OnGoldChanged?.Invoke(gold);
    }

    /// <summary>
    /// Giảm 50% toàn bộ tài nguyên (dùng khi Phòng Thủ căn cứ bị thua)
    /// </summary>
    public void HalveAllResources()
    {
        int halfWood = Mathf.FloorToInt(wood * 0.5f);
        int halfStone = Mathf.FloorToInt(stone * 0.5f);
        int halfFood = Mathf.FloorToInt(food * 0.5f);
        int halfGold = Mathf.FloorToInt(gold * 0.5f);

        AddWood(-halfWood);
        AddStone(-halfStone);
        AddFood(-halfFood);
        AddGold(-halfGold);

        BuildingSystem buildingSys = BuildingSystem.Ins != null ? BuildingSystem.Ins : UnityEngine.Object.FindFirstObjectByType<BuildingSystem>();
        if (buildingSys != null)
        {
            buildingSys.SaveBuildingsToSlot(1);
        }
        Debug.Log($"[JsonDataManager] 💀 PHÒNG THỦ THUA -> Bị cướp 50% tài nguyên! (-{halfWood} Gỗ, -{halfStone} Đá, -{halfFood} Lương, -{halfGold} Vàng)");
    }
    // ──────────────────────────────────────────────
    // CHI TIÊU TÀI NGUYÊN AN TOÀN (không cho phép âm)
    // ──────────────────────────────────────────────

    public bool TrySpendWood(int amount)
    {
        if (amount < 0 || wood < amount) return false;
        AddWood(-amount);
        return true;
    }

    public bool TrySpendStone(int amount)
    {
        if (amount < 0 || stone < amount) return false;
        AddStone(-amount);
        return true;
    }

    public bool TrySpendFood(int amount)
    {
        if (amount < 0 || food < amount) return false;
        AddFood(-amount);
        return true;
    }

    public bool TrySpendGold(int amount)
    {
        if (amount < 0 || gold < amount) return false;
        AddGold(-amount);
        return true;
    }

    /// <summary>
    /// Kiểm tra đủ tài nguyên cho một chi phí tổng hợp (ví dụ giá xây/nâng cấp một
    /// tòa nhà cần cả gỗ + đá + lúa + vàng) TRƯỚC khi trừ bất cứ thứ gì.
    /// </summary>
    // public bool HasEnoughResources(int woodCost = 0, int stoneCost = 0, int foodCost = 0, int goldCost = 0)
    // {
    //     return wood >= woodCost && stone >= stoneCost && food >= foodCost && gold >= goldCost;
    // }

    /// <summary>
    /// Trừ đồng thời nhiều loại tài nguyên theo kiểu tất-cả-hoặc-không-gì-cả.
    /// Kiểm tra đủ toàn bộ trước, chỉ trừ khi chắc chắn đủ hết — tránh trừ được
    /// gỗ nhưng thiếu đá khiến trạng thái nửa vời hoặc bị âm.
    /// </summary>
    public bool TrySpendCombined(int woodCost = 0, int stoneCost = 0, int foodCost = 0, int goldCost = 0)
    {
        if (!HasEnoughResources(woodCost, stoneCost, foodCost, goldCost)) return false;

        if (woodCost > 0) AddWood(-woodCost);
        if (stoneCost > 0) AddStone(-stoneCost);
        if (foodCost > 0) AddFood(-foodCost);
        if (goldCost > 0) AddGold(-goldCost);

        return true;
    }

    // ──────────────────────────────────────────────
    // NÂNG CẤP SỨC CHỨA KHO (Đã bỏ logic Max, giữ hàm để không lỗi hệ thống khác)
    // ──────────────────────────────────────────────
    public void UpdateCapacities(int warehouseLevel)
    {
        // Hệ thống không còn dùng Max Capacity, hàm này giữ lại để 
        // các script gọi nâng cấp kho không bị báo lỗi reference.
        Debug.Log($"[JsonDataManager] Kho Lvl {warehouseLevel} upgraded (Max limits removed).");
    }

    // ──────────────────────────────────────────────
    // SAVE / LOAD
    // ──────────────────────────────────────────────

    // 1. Hàm tạo tên file theo Slot (Ví dụ: save_slot_1.json, save_slot_2.json)
    public string GetSlotFileName(int slotIndex)
    {
        return $"save_slot_{slotIndex}.json";
    }

    // 2. Nâng cấp SaveGame cho phép chọn Slot
    public bool SaveGame(int slotIndex, GameSaveData save)
    {
        try
        {
            save.resources = new List<ResourceData>
            {
                new ResourceData { resourceType = "Gold",  amount = gold  },
                new ResourceData { resourceType = "Wood",  amount = wood  },
                new ResourceData { resourceType = "Stone", amount = stone },
                new ResourceData { resourceType = "Food",  amount = food  },
            };

            string targetFile = GetSlotFileName(slotIndex);
            string json = JsonUtility.ToJson(save, true);
            FileIO.SaveToFile(json, targetFile);
            Debug.Log($"[JsonDataManager] ✅ Saved Slot {slotIndex} → {targetFile}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[JsonDataManager] ❌ Save Slot {slotIndex} thất bại: " + ex.Message);
            return false;
        }
    }

    // 3. Nâng cấp LoadGame cho phép chọn Slot
    public GameSaveData LoadGame(int slotIndex)
    {
        string targetFile = GetSlotFileName(slotIndex);
        string json = FileIO.LoadFromFile(targetFile);

        if (string.IsNullOrEmpty(json))
        {
            Debug.Log($"[JsonDataManager] Slot {slotIndex} chưa có dữ liệu save.");
            return null;
        }

        try
        {
            GameSaveData save = JsonUtility.FromJson<GameSaveData>(json);

            if (save.resources != null)
            {
                foreach (var res in save.resources)
                {
                    switch (res.resourceType)
                    {
                        case "Gold": gold = res.amount; break;
                        case "Wood": wood = res.amount; break;
                        case "Stone": stone = res.amount; break;
                        case "Food": food = res.amount; break;
                    }
                }
            }

            BroadcastAllResources();
            Debug.Log($"[JsonDataManager] ✅ Loaded Slot {slotIndex} ← {targetFile}");
            return save;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[JsonDataManager] ❌ Load Slot {slotIndex} thất bại: " + ex.Message);
            return null;
        }
    }

    // 4. Kiểm tra Slot có dữ liệu hay chưa (Dành cho UI hiển thị danh sách Slot)
    public bool HasSaveData(int slotIndex)
    {
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, GetSlotFileName(slotIndex));
        return System.IO.File.Exists(filePath);
    }

    public bool DeleteSave() => FileIO.Delete(saveFileName);

    public void BroadcastAllResources()
    {
        // 1. Phát Event cho các hệ thống khác (nếu có)
        OnGoldChanged?.Invoke(gold);
        OnWoodChanged?.Invoke(wood);
        OnStoneChanged?.Invoke(stone);
        OnFoodChanged?.Invoke(food);

        // 2. ÉP TRỰC TIẾP HUD PHẢI CẬP NHẬT (SỬA LỖI ĐỨNG HÌNH UI)
        if (HUDController.Instance != null)
        {
            HUDController.Instance.UpdateGold(gold);
            HUDController.Instance.UpdateWood(wood);
            HUDController.Instance.UpdateStone(stone);
            HUDController.Instance.UpdateFood(food);
            Debug.Log("[JsonDataManager] ⏩ Đã ép HUD cập nhật trực tiếp thành công!");
        }
        else
        {
            Debug.LogWarning("[JsonDataManager] ❌ Không tìm thấy HUDController.Instance trong Scene!");
        }
    }

    public IEnumerator LoadData(Action<float> onProgress)
    {
        float progress = 0f;
        while (progress < 1f)
        {
            progress += Time.deltaTime * 0.2f;
            onProgress?.Invoke(Mathf.Min(progress, 1f));
            yield return null;
        }
    }

    // ──────────────────────────────────────────────
    // BUILDING CONFIG (Giữ nguyên class data để không lỗi JSON cũ)
    // ──────────────────────────────────────────────

    private void LoadBuildingConfigs()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, configFileName);

        if (!File.Exists(filePath))
            GenerateDefaultConfigFile(filePath);

        try
        {
            string json = File.ReadAllText(filePath);
            _loadedConfig = JsonUtility.FromJson<BuildingConfigRoot>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError("[JsonDataManager] ❌ Lỗi đọc config: " + ex.Message);
        }
    }

    private void GenerateDefaultConfigFile(string path)
    {
        var root = new BuildingConfigRoot
        {
            buildingConfigs = new List<BuildingConfig>
            {
                new BuildingConfig
                {
                    buildingType = "Warehouse",
                    levelConfigs = new List<WarehouseLevelData>
                    {
                        new WarehouseLevelData { level = 1, woodCapacity = 500,  stoneCapacity = 500,  foodCapacity = 500  },
                        new WarehouseLevelData { level = 2, woodCapacity = 1200, stoneCapacity = 1200, foodCapacity = 1200 },
                        new WarehouseLevelData { level = 3, woodCapacity = 3000, stoneCapacity = 3000, foodCapacity = 3000 },
                    }
                }
            }
        };

        string json = JsonUtility.ToJson(root, true);
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, json);
    }
    // Thêm vào cuối class JsonDataManager.cs để các script khác dễ dàng ghi nhận thành tích
    // ──────────────────────────────────────────────
    // CẤU TRÚC LƯU TRỮ CHỈ SỐ END GAME BẰNG JSON
    // ──────────────────────────────────────────────
    [Serializable]
    public class EndGameStats
    {
        public int totalWood = 0;
        public int totalStone = 0;
        public int totalFood = 0;
        public int totalGold = 0;
        public int totalBuildings = 0;
        public int survivalDays = 0;
    }

    private static string EndGameStatsPath => Path.Combine(Application.persistentDataPath, "endgame_stats.json");

    /// <summary>
    /// Đọc dữ liệu End Game từ file JSON
    /// </summary>
    public static EndGameStats LoadEndGameStats()
    {
        try
        {
            if (File.Exists(EndGameStatsPath))
            {
                string json = File.ReadAllText(EndGameStatsPath);
                return JsonUtility.FromJson<EndGameStats>(json);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[JsonDataManager] Lỗi Load EndGameStats: " + ex.Message);
        }
        return new EndGameStats(); // Trả về data trống nếu chưa có file
    }

    /// <summary>
    /// Ghi dữ liệu End Game vào file JSON
    /// </summary>
    public static void SaveEndGameStats(EndGameStats data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(EndGameStatsPath, json);
        }
        catch (Exception ex)
        {
            Debug.LogError("[JsonDataManager] Lỗi Save EndGameStats: " + ex.Message);
        }
    }

    // ──────────────────────────────────────────────
    // CÁC HÀM ĐĂNG KÝ THÀNH TÍCH (ĐÃ CHUYỂN SANG JSON)
    // ──────────────────────────────────────────────

    public static void RegisterStat_ResourceCollected(string resourceType, int amount)
    {
        if (amount <= 0) return;

        EndGameStats stats = LoadEndGameStats();

        switch (resourceType.ToLower())
        {
            case "wood": stats.totalWood += amount; break;
            case "stone": stats.totalStone += amount; break;
            case "food": stats.totalFood += amount; break;
            case "gold": stats.totalGold += amount; break;
        }

        SaveEndGameStats(stats);
    }

    public static void RegisterStat_BuildingConstructed()
    {
        EndGameStats stats = LoadEndGameStats();
        stats.totalBuildings += 1;
        SaveEndGameStats(stats);
    }

    public static void RegisterStat_DaysSurvived(int days)
    {
        EndGameStats stats = LoadEndGameStats();
        stats.survivalDays = days;
        SaveEndGameStats(stats);
    }

    /// <summary>
    /// Reset sạch sẽ file JSON khi bấm Restart hoặc Về Menu
    /// </summary>
    public static void ResetEndGameStats()
    {
        try
        {
            if (File.Exists(EndGameStatsPath))
            {
                File.Delete(EndGameStatsPath);
                Debug.Log("[JsonDataManager] 🧹 Đã dọn dẹp sạch sẽ file JSON EndGame!");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[JsonDataManager] Không thể xóa file JSON EndGame: " + ex.Message);
        }
    }

    /// <summary>
    /// Đồng bộ toàn bộ tài nguyên hiện tại từ HUD/Runtime vào file JSON Endgame
    /// </summary>
    public static void SaveFinalSessionStats()
    {
        if (Ins == null) return;

        EndGameStats stats = LoadEndGameStats();

        // LỰA CHỌN 1: Hiện đúng số tài nguyên đang có trên HUD lúc bấm F kết thúc game (Nên dùng)
        stats.totalWood = Ins.wood;
        stats.totalStone = Ins.stone;
        stats.totalFood = Ins.food;
        stats.totalGold = Ins.gold;

        /* 
        // LỰA CHỌN 2: Nếu Vũ muốn chỉ hiện lượng tài nguyên thực tế ĐÃ KHAI THÁC được trong trận 
        // (Nếu chọn cách này, tài nguyên mặc định lúc vào game sẽ không được tính)
        stats.totalWood = Instance.TotalWoodCollected;
        stats.totalStone = Instance.TotalStoneCollected;
        stats.totalFood = Instance.TotalFoodCollected;
        stats.totalGold = Instance.TotalGoldCollected;
        */

        SaveEndGameStats(stats);
    }

    [Serializable] public class GameSaveData { public string sceneName; public long savedAtUnix; public List<BuildingState> buildings; public List<ResourceData> resources; public List<WorkerState> workers; public List<ResourceEntityState> resourceEntities; }
    [Serializable] public class ResourceData { public string resourceType; public int amount; }
    [Serializable] public class BuildingConfigRoot { public List<BuildingConfig> buildingConfigs; }
    [Serializable] public class BuildingConfig { public string buildingType; public List<WarehouseLevelData> levelConfigs; }
    [Serializable] public class WarehouseLevelData { public int level; public int woodCapacity; public int stoneCapacity; public int foodCapacity; }
}