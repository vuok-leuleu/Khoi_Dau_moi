using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * TroopTrainingManager.cs
 * Hệ thống Quản Lý Ô Huấn Luyện Lính (Troop Training System)
 * Mỗi vùng đã có Nhà Chính luôn có 3 ô huấn luyện cơ bản. Trại Lính chỉ mở
 * thêm các ô nâng cao (5 / 8), không còn là điều kiện để hiện ô cơ bản.
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
}

public class TroopTrainingManager : MonoBehaviour
{
    public static TroopTrainingManager Ins { get; private set; }

    public const int MAX_TRAINING_SLOTS = 8;
    private const int SOLDIERS_PER_TRAINING_UNIT = 3;

    // Bộ nhớ đệm lưu danh sách Ô huấn luyện cho từng Vùng đất (Key = settlementName)
    private Dictionary<string, TroopTrainingSlotData[]> zoneSlotsDict = new Dictionary<string, TroopTrainingSlotData[]>();

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
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        SubscribeDayNight();
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
    /// Xử lý đếm ngược 1 ngày/wave huấn luyện lính khi trôi qua ngày mới
    /// </summary>
    private void OnWaveStartHandler(int waveIndex)
    {
        Debug.Log($"[TroopTrainingManager] 🌅 Trôi qua Ngày mới (Wave {waveIndex}) -> Tiến hành đếm ngược Huấn Luyện Lính...");

        // Nạp tự động toàn bộ Vùng đất hiện có vào bộ nhớ trước khi đếm ngược
        if (SettlementManager.Ins != null && SettlementManager.Ins.AllSettlements != null)
        {
            foreach (var z in SettlementManager.Ins.AllSettlements)
            {
                if (z != null) GetSlotsForZone(z);
            }
        }
        else
        {
            SettlementZone[] sceneZones = Object.FindObjectsByType<SettlementZone>(FindObjectsSortMode.None);
            foreach (var z in sceneZones)
            {
                if (z != null) GetSlotsForZone(z);
            }
        }

        List<string> zoneKeys = new List<string>(zoneSlotsDict.Keys);
        foreach (string zoneName in zoneKeys)
        {
            TroopTrainingSlotData[] slots = zoneSlotsDict[zoneName];
            if (slots == null) continue;

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot != null && slot.isTraining && !slot.isCompleted)
                {
                    slot.remainingWaves--;
                    if (slot.remainingWaves <= 0)
                    {
                        slot.remainingWaves = 0;
                        slot.isTraining = false;
                        slot.isCompleted = true;

                        Debug.Log($"[TroopTrainingManager] 🎉 Ô {slot.slotIndex + 1} tại vùng {zoneName} đã hoàn tất huấn luyện {slot.troopType}!");

                        // Tự động thu hoạch & Sinh lính thật tại Doanh Trại của Vùng đất
                        SpawnTrainedSoldierForZone(zoneName, slot);
                        SettlementZone zoneObj = SettlementManager.Ins != null ? SettlementManager.Ins.GetZoneByName(zoneName) : null;
                        if (zoneObj != null) zoneObj.UpdateZoneVisualText();
                    }
                }
            }
            SaveZoneTrainingData(zoneName);
        }

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
        if (string.IsNullOrEmpty(zoneName)) return;

        TroopTrainingSlotData[] slots = LoadZoneTrainingData(zoneName);
        for (int i = 0; i < MAX_TRAINING_SLOTS; i++)
        {
            if (slots[i] != null)
            {
                slots[i].isCompleted = false;
                slots[i].isTraining = false;
                slots[i].remainingWaves = 1;
            }
        }
        zoneSlotsDict[zoneName] = slots;
        SaveZoneTrainingData(zoneName);
    }

    /// <summary>
    /// Tính số lượng ô huấn luyện mở khóa trong Vùng đất.
    /// - Vùng chưa có Nhà Chính: 0 ô mở.
    /// - Vùng đã có Nhà Chính: luôn có 3 ô cơ bản, giống vùng đất khởi đầu.
    /// - Trại Lính cấp 2 / 3 chỉ mở rộng tương ứng lên 5 / 8 ô.
    /// </summary>
    public int GetUnlockedSlotsCountForZone(SettlementZone zone)
    {
        if (zone == null || !zone.isTownHallEstablished) return 0;

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

        int unlockedSlotsFromBarracks;
        switch (highestBarracksLevel)
        {
            case 2:
                unlockedSlotsFromBarracks = 5;
                break;
            case 3:
                unlockedSlotsFromBarracks = 8;
                break;
            default:
                unlockedSlotsFromBarracks = highestBarracksLevel > 3 ? 8 : 3;
                break;
        }

        return Mathf.Clamp(Mathf.Max(3, unlockedSlotsFromBarracks), 0, MAX_TRAINING_SLOTS);
    }

    public bool IsBarracksBuilding(BuildingType type)
    {
        return type == BuildingType.BarracksMelee ||
               type == BuildingType.BarracksArcher ||
               type == BuildingType.BarracksSpear ||
               type.ToString().StartsWith("Barracks");
    }

    /// <summary>
    /// Lấy danh sách 8 Ô Huấn Luyện của Vùng đất (Tự động nạp từ PlayerPrefs nếu có)
    /// </summary>
    public TroopTrainingSlotData[] GetSlotsForZone(SettlementZone zone)
    {
        if (zone == null) return new TroopTrainingSlotData[MAX_TRAINING_SLOTS];

        string zoneName = zone.settlementName;

        if (!zoneSlotsDict.ContainsKey(zoneName))
        {
            TroopTrainingSlotData[] slots = LoadZoneTrainingData(zoneName);
            zoneSlotsDict[zoneName] = slots;
        }

        TroopTrainingSlotData[] currentSlots = zoneSlotsDict[zoneName];
        int unlockedCount = GetUnlockedSlotsCountForZone(zone);

        // Count the garrison of this settlement, not the historical list of a
        // local SpawnSoldier.  A squad can arrive here from a different region and
        // must immediately occupy this settlement's training/garrison slots.
        List<AttackMode> stationedSoldierModes = GetStationedSoldierModes(zone);
        int activeSoldierCount = stationedSoldierModes.Count;
        int requiredGarrisonSlots = Mathf.CeilToInt(activeSoldierCount / (float)SOLDIERS_PER_TRAINING_UNIT);
        int completedCount = 0;

        for (int i = 0; i < MAX_TRAINING_SLOTS; i++)
        {
            if (currentSlots[i] == null)
            {
                currentSlots[i] = new TroopTrainingSlotData { slotIndex = i };
            }
            currentSlots[i].isUnlocked = (i < unlockedCount);

            if (currentSlots[i].isUnlocked && currentSlots[i].isCompleted)
            {
                if (completedCount < requiredGarrisonSlots)
                {
                    completedCount++;
                }
                else
                {
                    // No soldier remains in this settlement for this occupied slot.
                    currentSlots[i].isCompleted = false;
                    currentSlots[i].isTraining = false;
                    currentSlots[i].remainingWaves = 1;
                }
            }
        }

        // New arrivals do not have an existing PlayerPrefs training slot in their
        // destination. Fill free unlocked slots so the settlement visibly accepts
        // the transferred garrison immediately.
        for (int i = 0; i < MAX_TRAINING_SLOTS && completedCount < requiredGarrisonSlots; i++)
        {
            TroopTrainingSlotData slot = currentSlots[i];
            if (!slot.isUnlocked || slot.isTraining || slot.isCompleted) continue;

            slot.isCompleted = true;
            slot.isTraining = false;
            slot.remainingWaves = 0;
            int representativeSoldierIndex = Mathf.Min(
                completedCount * SOLDIERS_PER_TRAINING_UNIT,
                stationedSoldierModes.Count - 1);
            slot.troopType = GetBuildingTypeForAttackMode(stationedSoldierModes[representativeSoldierIndex]);
            completedCount++;
        }

        return currentSlots;
    }

    private static List<AttackMode> GetStationedSoldierModes(SettlementZone zone)
    {
        List<AttackMode> modes = new List<AttackMode>();
        if (zone == null) return modes;

        foreach (UnitController unit in Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None))
        {
            if (unit != null && unit.gameObject.activeInHierarchy && !unit.isDead &&
                unit.IsStationedInZone(zone.settlementName))
            {
                modes.Add(unit.AttackMode);
            }
        }
        return modes;
    }

    private static BuildingType GetBuildingTypeForAttackMode(AttackMode attackMode)
    {
        switch (attackMode)
        {
            case AttackMode.Ranged: return BuildingType.BarracksArcher;
            case AttackMode.Tank: return BuildingType.BarracksSpear;
            default: return BuildingType.BarracksMelee;
        }
    }

    /// <summary>
    /// Bắt đầu huấn luyện lính tại ô slotIndex của Vùng đất
    /// </summary>
    public bool StartTraining(SettlementZone zone, int slotIndex, BuildingType troopType)
    {
        if (zone == null || slotIndex < 0 || slotIndex >= MAX_TRAINING_SLOTS) return false;

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

        slot.isTraining = true;
        slot.troopType = troopType;
        slot.remainingWaves = 1; // 1 Wave / Ngày
        slot.isCompleted = false;

        SaveZoneTrainingData(zone.settlementName);
        if (CampaignTutorialManager.Ins != null) CampaignTutorialManager.Ins.OnTroopTrainingStarted(troopType);
        zone.UpdateZoneVisualText();
        Debug.Log($"[TroopTrainingManager] ⚔️ Đã bắt đầu huấn luyện {troopType} tại Ô {slotIndex + 1} (Thời gian: 1 ngày)!");

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
