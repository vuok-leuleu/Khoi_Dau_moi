using UnityEngine;

/*
 * WaveResourceManager.cs
 * Folder: Scripts/Managers/
 * Dự án: KHẨN HOANG (PENTA DEV)
 * Tác dụng: Tự động cộng tài nguyên từ các công trình thu thập (Xưởng Gỗ, Mỏ Đá, Nhà Bếp, Nhà Chính...) theo từng Wave/Ngày!
 */

public class WaveResourceManager : MonoBehaviour
{
    private static WaveResourceManager instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    private void OnEnable()
    {
        RegisterEvents();
    }

    private void OnDisable()
    {
        UnregisterEvents();
    }

    private void Start()
    {
        RegisterEvents();
    }

    private void RegisterEvents()
    {
        if (DayNightManager.Ins != null)
        {
            DayNightManager.Ins.OnWaveStart -= HandleWaveStart;
            DayNightManager.Ins.OnWaveStart += HandleWaveStart;
        }
    }

    private void UnregisterEvents()
    {
        if (DayNightManager.Ins != null)
        {
            DayNightManager.Ins.OnWaveStart -= HandleWaveStart;
        }
    }

    private void HandleWaveStart(int waveIndex)
    {
        // ⚠️ FIX RACE CONDITION:
        // DayNightManager.OnWaveStart được invoke NGAY TRONG cùng frame với currentWave++
        // Coroutine xây dựng trong UpgradeableBuilding chưa kịp chạy yield return null
        // → IsUpgrading vẫn còn = true tại thời điểm này → bị bỏ qua sai!
        // Giải pháp: Đợi 1 frame để tất cả coroutine xây dựng kịp cập nhật xong rồi mới thu tài nguyên.
        StartCoroutine(CollectResourcesAfterBuildingsUpdate(waveIndex));
    }

    private System.Collections.IEnumerator CollectResourcesAfterBuildingsUpdate(int waveIndex)
    {
        // Đợi 1 frame: cho tất cả coroutine UpgradeableBuilding kịp chạy và cập nhật IsUpgrading
        yield return null;

        CollectBuildingResourcesForWave(waveIndex);
    }

    /// <summary>
    /// <summary>
    /// Thu thập tài nguyên từ tất cả các công trình sản xuất trên các Vùng Đất khi bắt đầu Wave/Ngày mới
    /// </summary>
    public static void CollectBuildingResourcesForWave(int waveIndex)
    {
        if (JsonDataManager.Ins == null) return;

        UpgradeableBuilding[] allBuildings = Object.FindObjectsByType<UpgradeableBuilding>(FindObjectsSortMode.None);
        if (allBuildings == null || allBuildings.Length == 0) return;

        int totalWoodGained = 0;
        int totalStoneGained = 0;
        int totalFoodGained = 0;
        int totalGoldGained = 0;

        foreach (var b in allBuildings)
        {
            if (b == null || !b.gameObject.activeInHierarchy) continue;
            if (b.IsInitialBuildNeeded || b.IsRuined || b.IsUpgrading) continue; // Công trình chưa xây xong hoặc hỏng -> Chưa sinh tài nguyên

            int lvl = b.CurrentLevel; // 0-indexed (0 là Lv1, 1 là Lv2...)
            string nameLower = b.gameObject.name.ToLower();
            string bNameLower = b.buildingName != null ? b.buildingName.ToLower() : "";

            bool isWood = b.buildingType == BuildingType.WoodCutter || b.buildingType == BuildingType.Wood || nameLower.Contains("wood") || nameLower.Contains("gỗ") || bNameLower.Contains("gỗ") || bNameLower.Contains("mộc");
            bool isStone = b.buildingType == BuildingType.StoneMine || b.buildingType == BuildingType.StoneStorage || b.buildingType == BuildingType.Stone || nameLower.Contains("stone") || nameLower.Contains("đá") || bNameLower.Contains("đá");
            bool isFood = b.buildingType == BuildingType.Kitchen || b.buildingType == BuildingType.FoodStorage || b.buildingType == BuildingType.Rice || nameLower.Contains("food") || nameLower.Contains("lương") || nameLower.Contains("lúa") || nameLower.Contains("bếp") || bNameLower.Contains("lương") || bNameLower.Contains("lúa");
            bool isHouse = b.buildingType == BuildingType.House || nameLower.Contains("house") || nameLower.Contains("chính");

            if (isWood)
            {
                int woodAmount = (lvl + 1) * 30;
                totalWoodGained += woodAmount;
            }
            else if (isStone)
            {
                int stoneAmount = (lvl + 1) * 30;
                totalStoneGained += stoneAmount;
            }
            else if (isFood)
            {
                int foodAmount = (lvl + 1) * 30;
                totalFoodGained += foodAmount;
            }
            else if (isHouse)
            {
                int goldAmount = (lvl + 1) * 25;
                totalGoldGained += goldAmount;
            }
        }

        // Thưởng thêm Vàng từ Thủ Đô / Nhà Chính mỗi khi qua ngày mới
        totalGoldGained += 20;

        if (totalWoodGained > 0) JsonDataManager.Ins.AddWood(totalWoodGained);
        if (totalStoneGained > 0) JsonDataManager.Ins.AddStone(totalStoneGained);
        if (totalFoodGained > 0) JsonDataManager.Ins.AddFood(totalFoodGained);
        if (totalGoldGained > 0) JsonDataManager.Ins.AddGold(totalGoldGained);

        // KHÔNG gọi BroadcastAllResources() ở đây vì AddXxx đã invoke OnXxxChanged event rồi.
        // Gọi thêm BroadcastAllResources sẽ dư thừa (HUDController nhận delta=0, bỏ qua).

        Debug.Log($"[WaveResourceManager] 🌾 NGÀY/WAVE {waveIndex}: Thu hoạch tài nguyên thành công! +{totalWoodGained} Gỗ, +{totalStoneGained} Đá, +{totalFoodGained} Lương, +{totalGoldGained} Vàng.");

        if (UIManager.Ins != null && (totalWoodGained > 0 || totalStoneGained > 0 || totalFoodGained > 0 || totalGoldGained > 0))
        {
            UIManager.Ins.ShowWarning($"🌾 Ngày {waveIndex}: Thu hoạch +{totalWoodGained} Gỗ, +{totalStoneGained} Đá, +{totalFoodGained} Lương, +{totalGoldGained} Vàng!");
        }
    }
}
