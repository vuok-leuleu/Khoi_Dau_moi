using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * TroopTrainingManager.cs
 * Hệ thống Quản Lý Ô Huấn Luyện Lính (Troop Training System)
 * Mở khóa từ 0 -> 3 -> 5 -> 8 ô theo Cấp độ Trại Lính trong SettlementZone.
 */

[System.Serializable]
public class TroopTrainingSlotData
{
    public int slotIndex;
    public bool isUnlocked;
    public bool isTraining;
    public BuildingType troopType = BuildingType.BarracksMelee;
    public int remainingWaves = 1;
    public bool isCompleted;

    // Chỉ dùng khi hiển thị quân đồn trú ở các thành không có Trại Lính.
    // Dữ liệu này được tính lại từ UnitController, không lưu vào hàng đợi huấn luyện.
    public int stationedSoldierCount;
}

public class TroopTrainingManager : MonoBehaviour
{
    public static TroopTrainingManager Ins { get; private set; }

    public const int MAX_TRAINING_SLOTS = 8;
    private const int SOLDIERS_PER_TRAINING_UNIT = 3;

    // Bộ nhớ đệm lưu danh sách Ô huấn luyện cho từng Vùng đất (Key = settlementName)
    private Dictionary<string, TroopTrainingSlotData[]> zoneSlotsDict = new Dictionary<string, TroopTrainingSlotData[]>();
    private SettlementZone centralSettlement;

    public SettlementZone CentralSettlement => ResolveCentralSettlement();

    private void Awake()
    {
        if (Ins == null)
        {
            Ins = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        SubscribeDayNight();
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeDayNight();
    }

    private void Start()
    {
        SubscribeDayNight();
        SyncFoodToDataManager();
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        centralSettlement = null;
        SubscribeDayNight();
        SyncFoodToDataManager();
    }

    private void SubscribeDayNight()
    {
        if (DayNightManager.Ins != null)
        {
            DayNightManager.Ins.OnWaveStart -= OnWaveStartHandler;
            DayNightManager.Ins.OnWaveStart += OnWaveStartHandler;
        }
    }

    private void UnsubscribeDayNight()
    {
        if (DayNightManager.Ins != null)
        {
            DayNightManager.Ins.OnWaveStart -= OnWaveStartHandler;
        }
    }

    /// <summary>
    /// Kiểm tra các loại Trại Lính đặt sẵn ở thành khởi đầu. Các loại này không
    /// được phép xây thêm trong game.
    /// </summary>
    public static bool IsCentralBarracksType(BuildingType type)
    {
        return type == BuildingType.BarracksMelee ||
               type == BuildingType.BarracksArcher ||
               type == BuildingType.BarracksSpear ||
               type.ToString().StartsWith("Barracks");
    }

    /// <summary>
    /// Tìm thành có Trại Lính trung tâm. Scene cũ không tích cờ sẽ dùng vùng
    /// Tier 0 để vẫn tương thích với dữ liệu hiện có.
    /// </summary>
    private SettlementZone ResolveCentralSettlement()
    {
        if (centralSettlement != null) return centralSettlement;

        SettlementZone[] zones = Object.FindObjectsByType<SettlementZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (SettlementZone zone in zones)
        {
            if (zone != null && zone.isStartingSettlement)
            {
                centralSettlement = zone;
                return centralSettlement;
            }
        }

        if (SettlementManager.Ins != null)
        {
            centralSettlement = SettlementManager.Ins.GetZoneByTier(0);
            if (centralSettlement != null) return centralSettlement;
        }

        foreach (SettlementZone zone in zones)
        {
            if (zone != null && zone.zoneTier == 0)
            {
                centralSettlement = zone;
                return centralSettlement;
            }
        }

        return null;
    }

    public bool IsCentralTrainingSettlement(SettlementZone zone)
    {
        return zone != null && zone == ResolveCentralSettlement();
    }

    /// <summary>
    /// Xử lý đếm ngược 1 ngày/wave huấn luyện lính khi trôi qua ngày mới
    /// </summary>
    private void OnWaveStartHandler(int waveIndex)
    {
        SettlementZone centralZone = ResolveCentralSettlement();
        if (centralZone == null) return;

        Debug.Log($"[TroopTrainingManager] 🌅 Trôi qua Ngày mới (Wave {waveIndex}) -> Tiến hành đếm ngược Huấn Luyện Lính tại {centralZone.settlementName}...");

        TroopTrainingSlotData[] slots = GetSlotsForZone(centralZone);
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            TroopTrainingSlotData slot = slots[i];
            if (slot != null && slot.isTraining && !slot.isCompleted)
            {
                slot.remainingWaves--;
                if (slot.remainingWaves <= 0)
                {
                    slot.remainingWaves = 0;
                    slot.isTraining = false;
                    slot.isCompleted = true;

                    Debug.Log($"[TroopTrainingManager] 🎉 Ô {slot.slotIndex + 1} tại {centralZone.settlementName} đã hoàn tất huấn luyện {slot.troopType}!");
                    SpawnTrainedSoldierForZone(centralZone.settlementName, slot);
                }
            }
        }

        SaveZoneTrainingData(centralZone.settlementName);
        centralZone.UpdateZoneVisualText();

        // 🌾 Đồng bộ lại số lúa khả dụng lên HUD
        SyncFoodToDataManager();

        if (SettlementSidePanelUI.Ins != null)
        {
            SettlementSidePanelUI.Ins.RefreshPanel();
        }
    }

    /// <summary>
    /// Xóa sạch dữ liệu ô huấn luyện của Vùng Đất khi bị phòng thủ thua hoặc thất bại trận đánh
    /// </summary>
    public void ClearZoneTrainingSlots(string zoneName)
    {
        SettlementZone centralZone = ResolveCentralSettlement();
        if (centralZone == null || string.IsNullOrEmpty(zoneName) || zoneName != centralZone.settlementName) return;

        TroopTrainingSlotData[] slots = GetSlotsForZone(centralZone);
        for (int i = 0; i < MAX_TRAINING_SLOTS; i++)
        {
            if (slots[i] != null)
            {
                slots[i].isCompleted = false;
                slots[i].isTraining = false;
                slots[i].remainingWaves = 1;
            }
        }
        SaveZoneTrainingData(centralZone.settlementName);

        // 🌾 Giải phóng slot lính -> hoàn trả lại lúa khả dụng
        SyncFoodToDataManager();
    }

    /// <summary>
    /// 🌾 TÍNH TỔNG LƯỢNG LÚA MÌ CUNG CẤP TỪ CÁC NHÀ LÚA ĐÃ XÂY XONG
    /// - Mặc định ban đầu luôn có 1 Lúa cơ bản (chưa có kho lúa nào = 1)
    /// - Xây / Nâng cấp Kho Lúa sẽ cộng thêm tương ứng vào Tổng Lúa
    /// </summary>
    public int GetTotalFoodCapacity()
    {
        int total = 1; // 🌾 Mặc định ban đầu luôn có 1 Lúa cơ bản

        UpgradeableBuilding[] allBuildings = Object.FindObjectsByType<UpgradeableBuilding>(FindObjectsSortMode.None);
        if (allBuildings == null || allBuildings.Length == 0) return total;

        foreach (var b in allBuildings)
        {
            if (b == null || !b.gameObject.activeInHierarchy || b.IsInitialBuildNeeded || b.IsRuined || b.IsUpgrading) continue;

            string nameLower = b.gameObject.name.ToLower();
            string bNameLower = b.buildingName != null ? b.buildingName.ToLower() : "";

            bool isFoodBuilding = b.buildingType == BuildingType.FoodStorage ||
                                  b.buildingType == BuildingType.Rice ||
                                  nameLower.Contains("food") || nameLower.Contains("lúa") || nameLower.Contains("lương") ||
                                  bNameLower.Contains("lúa") || bNameLower.Contains("lương");

            if (isFoodBuilding)
            {
                total += (b.CurrentLevel + 1); // Cấp 1 (Lv.1) +1, Cấp 2 (Lv.2) +2...
            }
        }
        return total;
    }

    /// <summary>
    /// 🌾 TÍNH TỔNG SỐ LÚA ĐANG BỊ CHIẾM DỤNG BỞI CÁC Ô LÍNH (Đang huấn luyện hoặc đã có lính)
    /// </summary>
    public int GetTotalUsedFoodCount()
    {
        int usedCount = 0;
        SettlementZone centralZone = ResolveCentralSettlement();
        if (centralZone == null) return usedCount;

        TroopTrainingSlotData[] slots = GetSlotsForZone(centralZone);
        if (slots == null) return usedCount;

        for (int i = 0; i < MAX_TRAINING_SLOTS; i++)
        {
            if (slots[i] != null && (slots[i].isTraining || slots[i].isCompleted))
            {
                usedCount++;
            }
        }

        return usedCount;
    }

    /// <summary>
    /// 🌾 SỐ LƯỢNG LÚA KHẢ DỤNG HIỆN TẠI ĐỂ HUẤN LUYỆN LÍNH MỚI
    /// </summary>
    public int GetAvailableFoodCount()
    {
        int capacity = GetTotalFoodCapacity();
        int used = GetTotalUsedFoodCount();
        return Mathf.Max(0, capacity - used);
    }

    /// <summary>
    /// 🌾 ĐỒNG BỘ CHỈ SỐ LÚA KHẢ DỤNG SANG JsonDataManager VÀ HUD (Định dạng {Used}/{Max})
    /// </summary>
    public void SyncFoodToDataManager()
    {
        int available = GetAvailableFoodCount();
        if (JsonDataManager.Ins != null)
        {
            JsonDataManager.Ins.SetFood(available);
        }

        if (HUDController.Instance != null)
        {
            HUDController.Instance.RefreshFoodDisplay();
        }
    }

    /// <summary>
    /// Tính số lượng ô huấn luyện mở khóa dựa theo Cấp độ Trại Lính cao nhất trong Vùng đất
    /// - Chưa có trại (Lv 0): 0 ô mở (tất cả 8 ô bị khóa)
    /// - Trại Cấp 1: 3 ô mở
    /// - Trại Cấp 2: 5 ô mở
    /// - Trại Cấp 3: 8 ô mở
    /// </summary>
    public int GetUnlockedSlotsCountForZone(SettlementZone zone)
    {
        if (zone == null || !zone.isTownHallEstablished || !IsCentralTrainingSettlement(zone)) return 0;

        int highestBarracksLevel = 0;

        // 1. Quét danh sách các công trình đã được đăng ký chuẩn của Vùng đất này
        if (zone.builtStructures != null)
        {
            foreach (var ub in zone.builtStructures)
            {
                if (ub != null && ub.gameObject.activeInHierarchy && IsBarracksBuilding(ub.buildingType))
                {
                    // Chỉ mở khóa ô nếu Trại Lính ĐÃ HOÀN THÀNH XÂY DỰNG (không phải chưa xây / đang bị tàn phá)
                    if (!ub.IsInitialBuildNeeded && !ub.IsRuined)
                    {
                        int level = ub.CurrentLevel + 1;
                        if (level > highestBarracksLevel)
                        {
                            highestBarracksLevel = level;
                        }
                    }
                }
            }
        }

        // 2. Fallback: Nếu danh sách builtStructures chưa kịp nạp, quét các công trình con thuộc zone.transform
        if (highestBarracksLevel == 0)
        {
            UpgradeableBuilding[] ubs = zone.GetComponentsInChildren<UpgradeableBuilding>(true);
            foreach (var ub in ubs)
            {
                if (ub != null && ub.gameObject.activeInHierarchy && IsBarracksBuilding(ub.buildingType))
                {
                    if (!ub.IsInitialBuildNeeded && !ub.IsRuined)
                    {
                        int level = ub.CurrentLevel + 1;
                        if (level > highestBarracksLevel)
                        {
                            highestBarracksLevel = level;
                        }
                    }
                }
            }
        }

        switch (highestBarracksLevel)
        {
            case 1: return 3;
            case 2: return 5;
            case 3: return 8;
            default:
                return highestBarracksLevel > 3 ? 8 : 0;
        }
    }

    public bool IsBarracksBuilding(BuildingType type)
    {
        return IsCentralBarracksType(type);
    }

    /// <summary>
    /// Lấy danh sách 8 Ô Huấn Luyện của Vùng đất (Tự động nạp từ PlayerPrefs nếu có)
    /// </summary>
    public TroopTrainingSlotData[] GetSlotsForZone(SettlementZone zone)
    {
        if (zone == null) return CreateDisplaySlots(0);

        // Chỉ thành khởi đầu có dữ liệu huấn luyện thật. Các thành khác dùng
        // cùng khung UI để hiển thị quân đang đồn trú theo từng loại.
        if (!IsCentralTrainingSettlement(zone))
        {
            return CreateGarrisonDisplaySlots(zone);
        }

        string zoneName = zone.settlementName;

        if (!zoneSlotsDict.ContainsKey(zoneName))
        {
            TroopTrainingSlotData[] slots = LoadZoneTrainingData(zoneName);
            zoneSlotsDict[zoneName] = slots;
        }

        TroopTrainingSlotData[] currentSlots = zoneSlotsDict[zoneName];
        int unlockedCount = GetUnlockedSlotsCountForZone(zone);

        int activeSoldierCount = GetStationedSoldierCount(zone, BuildingType.None);
        int requiredGarrisonSlots = Mathf.CeilToInt(activeSoldierCount / (float)SOLDIERS_PER_TRAINING_UNIT);
        int completedCount = 0;

        for (int i = 0; i < MAX_TRAINING_SLOTS; i++)
        {
            if (currentSlots[i] == null)
            {
                currentSlots[i] = new TroopTrainingSlotData { slotIndex = i };
            }
            currentSlots[i].isUnlocked = (i < unlockedCount);

            // Đồng bộ ô hoàn thành chứa lính với số lính thực tế đang có trên bản đồ
            if (currentSlots[i].isUnlocked && currentSlots[i].isCompleted)
            {
                if (completedCount < requiredGarrisonSlots)
                {
                    completedCount++;
                }
                else
                {
                    // Nếu số lính thực tế ít hơn (VD: 0 lính), reset ô về Ô Trống
                    currentSlots[i].isCompleted = false;
                    currentSlots[i].isTraining = false;
                    currentSlots[i].remainingWaves = 1;
                }
            }
        }

        return currentSlots;
    }

    private static TroopTrainingSlotData[] CreateGarrisonDisplaySlots(SettlementZone zone)
    {
        if (zone == null || !zone.isTownHallEstablished)
        {
            return CreateDisplaySlots(0);
        }

        TroopTrainingSlotData[] slots = CreateDisplaySlots(3);
        BuildingType[] troopTypes =
        {
            BuildingType.BarracksMelee,
            BuildingType.BarracksArcher,
            BuildingType.BarracksSpear
        };

        for (int i = 0; i < troopTypes.Length; i++)
        {
            int stationedCount = GetStationedSoldierCount(zone, troopTypes[i]);
            slots[i].troopType = troopTypes[i];
            slots[i].stationedSoldierCount = stationedCount;
            slots[i].isCompleted = stationedCount > 0;
        }

        return slots;
    }

    private static int GetStationedSoldierCount(SettlementZone zone, BuildingType troopType)
    {
        if (zone == null) return 0;

        int total = 0;
        UnitController[] allUnits = Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        foreach (UnitController soldier in allUnits)
        {
            if (soldier == null || !soldier.gameObject.activeInHierarchy || soldier.isDead || soldier.isExpeditionMarching)
            {
                continue;
            }

            bool isStationedHere = !string.IsNullOrEmpty(soldier.stationedSettlementZoneName)
                ? soldier.stationedSettlementZoneName == zone.settlementName
                : soldier.GetComponentInParent<SettlementZone>() == zone;

            if (isStationedHere && (troopType == BuildingType.None || ToBuildingType(soldier.AttackMode) == troopType))
            {
                total++;
            }
        }

        return total;
    }

    private static BuildingType ToBuildingType(AttackMode attackMode)
    {
        switch (attackMode)
        {
            case AttackMode.Ranged: return BuildingType.BarracksArcher;
            case AttackMode.Tank: return BuildingType.BarracksSpear;
            default: return BuildingType.BarracksMelee;
        }
    }

    private static TroopTrainingSlotData[] CreateDisplaySlots(int unlockedCount)
    {
        TroopTrainingSlotData[] slots = new TroopTrainingSlotData[MAX_TRAINING_SLOTS];
        for (int i = 0; i < MAX_TRAINING_SLOTS; i++)
        {
            slots[i] = new TroopTrainingSlotData
            {
                slotIndex = i,
                isUnlocked = i < unlockedCount,
                isTraining = false,
                isCompleted = false,
                remainingWaves = 1
            };
        }

        return slots;
    }

    /// <summary>
    /// Bắt đầu huấn luyện lính tại ô slotIndex của Vùng đất
    /// </summary>
    public bool StartTraining(SettlementZone zone, int slotIndex, BuildingType troopType)
    {
        if (zone == null || slotIndex < 0 || slotIndex >= MAX_TRAINING_SLOTS) return false;

        if (!IsCentralTrainingSettlement(zone))
        {
            const string warning = "Chỉ có thể huấn luyện lính tại thành đầu tiên có Trại Lính!";
            UIManager.Ins?.ShowWarning(warning);
            Debug.LogWarning($"[TroopTrainingManager] {warning}");
            return false;
        }

        TroopTrainingSlotData[] slots = GetSlotsForZone(zone);
        TroopTrainingSlotData slot = slots[slotIndex];

        if (!slot.isUnlocked)
        {
            int reqLevel = GetRequiredBarracksLevelForSlot(slotIndex);
            string warnMsg = $"⚠️ Ô {slotIndex + 1} đang bị khóa. Yêu cầu Trại Lính Lv.{reqLevel}!";
            if (UIManager.Ins != null) UIManager.Ins.ShowWarning(warnMsg);
            Debug.LogWarning($"[TroopTrainingManager] {warnMsg}");
            return false;
        }

        if (slot.isTraining || slot.isCompleted)
        {
            string warnMsg = $"⚠️ Ô {slotIndex + 1} đang bận huấn luyện hoặc đã chứa lính!";
            if (UIManager.Ins != null) UIManager.Ins.ShowWarning(warnMsg);
            Debug.LogWarning($"[TroopTrainingManager] {warnMsg}");
            return false;
        }

        // 🌾 KIỂM TRA ĐỦ LÚA MÌ: Mỗi ô lính cần 1 đơn vị Lúa mì khả dụng từ Nhà Lúa
        if (GetAvailableFoodCount() < 1)
        {
            string warnMsg = "🌾 Không đủ Lúa mì! Cần 1 Lúa cho mỗi ô lính. Hãy xây thêm hoặc nâng cấp Nhà Lúa!";
            if (UIManager.Ins != null) UIManager.Ins.ShowWarning(warnMsg);
            Debug.LogWarning($"[TroopTrainingManager] {warnMsg}");
            return false;
        }

        slot.isTraining = true;
        slot.troopType = troopType;
        slot.remainingWaves = 1; // 1 Wave / Ngày
        slot.isCompleted = false;

        SaveZoneTrainingData(zone.settlementName);
        if (CampaignTutorialManager.Ins != null) CampaignTutorialManager.Ins.OnTroopTrainingStarted(troopType);
        zone.UpdateZoneVisualText();
        
        // 🌾 Đồng bộ số lúa khả dụng (giảm 1 lúa đã dùng) lên JsonDataManager và HUD
        SyncFoodToDataManager();

        Debug.Log($"[TroopTrainingManager] ⚔️ Đã bắt đầu huấn luyện {troopType} tại Ô {slotIndex + 1} (Thời gian: 1 ngày, Tiêu hao: 1 Lúa)!");

        if (SettlementSidePanelUI.Ins != null)
        {
            SettlementSidePanelUI.Ins.RefreshPanel();
        }

        return true;
    }

    public int GetRequiredBarracksLevelForSlot(int slotIndex)
    {
        if (slotIndex < 3) return 1;
        if (slotIndex < 5) return 2;
        return 3;
    }

    /// <summary>
    /// Sinh lính thật tại Doanh Trại của Vùng đất khi hoàn thành huấn luyện
    /// </summary>
    private void SpawnTrainedSoldierForZone(string zoneName, TroopTrainingSlotData slot)
    {
        SettlementZone zone = SettlementManager.Ins != null ? SettlementManager.Ins.GetZoneByName(zoneName) : null;
        if (zone == null) zone = Object.FindFirstObjectByType<SettlementZone>();
        if (zone == null) return;

        // Quét tìm Spawner thuộc Doanh trại duy nhất (hoặc bất kỳ Spawner nào) trong Vùng đất
        SpawnSoldier targetSpawner = null;
        SpawnSoldier[] spawners = zone.GetComponentsInChildren<SpawnSoldier>(true);

        foreach (SpawnSoldier spawner in spawners)
        {
            if (spawner == null || !spawner.gameObject.activeInHierarchy) continue;

            UpgradeableBuilding building = spawner.GetComponent<UpgradeableBuilding>();
            if (building == null) building = spawner.GetComponentInParent<UpgradeableBuilding>();

            if (building != null && building.buildingType == slot.troopType && spawner.CanSpawnTrainedSoldier(slot.troopType))
            {
                targetSpawner = spawner;
                break;
            }
        }

        if (targetSpawner == null)
        {
            foreach (SpawnSoldier spawner in spawners)
            {
                if (spawner != null && spawner.gameObject.activeInHierarchy && spawner.CanSpawnTrainedSoldier(slot.troopType))
                {
                    targetSpawner = spawner;
                    break;
                }
            }
        }

        if (targetSpawner == null)
        {
            Debug.LogWarning($"[TroopTrainingManager] Không tìm thấy spawner có prefab phù hợp cho {slot.troopType} tại vùng {zoneName}.");
        }
        else
        {
            int spawnedCount = targetSpawner.SpawnTrainedSoldiers(slot.troopType, SOLDIERS_PER_TRAINING_UNIT);
            if (spawnedCount != SOLDIERS_PER_TRAINING_UNIT)
            {
                Debug.LogWarning($"[TroopTrainingManager] {slot.troopType} chỉ spawn được {spawnedCount}/{SOLDIERS_PER_TRAINING_UNIT} lính tại vùng {zoneName}.");
            }
        }

        // Ô huấn luyện giữ nguyên trạng thái ĐÃ HOÀN THÀNH (chứa lính)
        slot.isTraining = false;
        slot.isCompleted = true;
        slot.remainingWaves = 0;
        if (zone != null) zone.UpdateZoneVisualText();
    }

    private void SaveZoneTrainingData(string zoneName)
    {
        if (!zoneSlotsDict.ContainsKey(zoneName)) return;

        TroopTrainingSlotData[] slots = zoneSlotsDict[zoneName];
        for (int i = 0; i < MAX_TRAINING_SLOTS; i++)
        {
            if (slots[i] == null) continue;
            PlayerPrefs.SetInt($"Training_{zoneName}_Slot_{i}_IsTraining", slots[i].isTraining ? 1 : 0);
            PlayerPrefs.SetInt($"Training_{zoneName}_Slot_{i}_TroopType", (int)slots[i].troopType);
            PlayerPrefs.SetInt($"Training_{zoneName}_Slot_{i}_Remaining", slots[i].remainingWaves);
            PlayerPrefs.SetInt($"Training_{zoneName}_Slot_{i}_Completed", slots[i].isCompleted ? 1 : 0);
        }
        PlayerPrefs.Save();
    }

    private TroopTrainingSlotData[] LoadZoneTrainingData(string zoneName)
    {
        TroopTrainingSlotData[] slots = new TroopTrainingSlotData[MAX_TRAINING_SLOTS];
        for (int i = 0; i < MAX_TRAINING_SLOTS; i++)
        {
            slots[i] = new TroopTrainingSlotData { slotIndex = i };
            if (PlayerPrefs.HasKey($"Training_{zoneName}_Slot_{i}_IsTraining"))
            {
                slots[i].isTraining = PlayerPrefs.GetInt($"Training_{zoneName}_Slot_{i}_IsTraining", 0) == 1;
                slots[i].troopType = (BuildingType)PlayerPrefs.GetInt($"Training_{zoneName}_Slot_{i}_TroopType", (int)BuildingType.BarracksMelee);
                slots[i].remainingWaves = PlayerPrefs.GetInt($"Training_{zoneName}_Slot_{i}_Remaining", 1);
                slots[i].isCompleted = PlayerPrefs.GetInt($"Training_{zoneName}_Slot_{i}_Completed", 0) == 1;
            }
        }
        return slots;
    }
}
