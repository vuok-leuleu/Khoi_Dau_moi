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
}

public class TroopTrainingManager : MonoBehaviour
{
    public static TroopTrainingManager Ins { get; private set; }

    public const int MAX_TRAINING_SLOTS = 8;

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
        if (DayNightManager.Ins != null)
        {
            DayNightManager.Ins.OnWaveStart += OnWaveStartHandler;
        }
    }

    private void OnDisable()
    {
        if (DayNightManager.Ins != null)
        {
            DayNightManager.Ins.OnWaveStart -= OnWaveStartHandler;
        }
    }

    private void Start()
    {
        if (DayNightManager.Ins != null)
        {
            DayNightManager.Ins.OnWaveStart -= OnWaveStartHandler;
            DayNightManager.Ins.OnWaveStart += OnWaveStartHandler;
        }
    }

    /// <summary>
    /// Xử lý đếm ngược 1 ngày/wave huấn luyện lính khi trôi qua ngày mới
    /// </summary>
    private void OnWaveStartHandler(int waveIndex)
    {
        Debug.Log($"[TroopTrainingManager] 🌅 Trôi qua Ngày mới (Wave {waveIndex}) -> Tiến hành đếm ngược Huấn Luyện Lính...");

        foreach (var kvp in zoneSlotsDict)
        {
            string zoneName = kvp.Key;
            TroopTrainingSlotData[] slots = kvp.Value;
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
    /// Tính số lượng ô huấn luyện mở khóa dựa theo Cấp độ Trại Lính cao nhất trong Vùng đất
    /// - Chưa có trại (Lv 0): 0 ô mở (tất cả 8 ô bị khóa)
    /// - Trại Cấp 1: 3 ô mở
    /// - Trại Cấp 2: 5 ô mở
    /// - Trại Cấp 3: 8 ô mở
    /// </summary>
    public int GetUnlockedSlotsCountForZone(SettlementZone zone)
    {
        if (zone == null) return 0;

        int highestBarracksLevel = 0;
        UpgradeableBuilding[] ubs = zone.GetComponentsInChildren<UpgradeableBuilding>(true);

        foreach (var ub in ubs)
        {
            if (ub != null && ub.gameObject.activeInHierarchy && IsBarracksBuilding(ub.buildingType))
            {
                int level = ub.CurrentLevel + 1;
                if (level > highestBarracksLevel)
                {
                    highestBarracksLevel = level;
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
        return type == BuildingType.BarracksMelee ||
               type == BuildingType.BarracksArcher ||
               type == BuildingType.BarracksSpear;
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

        for (int i = 0; i < MAX_TRAINING_SLOTS; i++)
        {
            if (currentSlots[i] == null)
            {
                currentSlots[i] = new TroopTrainingSlotData { slotIndex = i };
            }
            currentSlots[i].isUnlocked = (i < unlockedCount);
        }

        return currentSlots;
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
            Debug.LogWarning($"[TroopTrainingManager] ⚠️ Ô {slotIndex + 1} đang bị khóa!");
            return false;
        }

        if (slot.isTraining || slot.isCompleted)
        {
            Debug.LogWarning($"[TroopTrainingManager] ⚠️ Ô {slotIndex + 1} đang bận huấn luyện!");
            return false;
        }

        slot.isTraining = true;
        slot.troopType = troopType;
        slot.remainingWaves = 1; // 1 Wave / Ngày
        slot.isCompleted = false;

        SaveZoneTrainingData(zone.settlementName);
        Debug.Log($"[TroopTrainingManager] ⚔️ Đã bắt đầu huấn luyện {troopType} tại Ô {slotIndex + 1} (Thời gian: 1 ngày)!");

        if (SettlementSidePanelUI.Ins != null)
        {
            SettlementSidePanelUI.Ins.RefreshPanel();
        }

        return true;
    }

    /// <summary>
    /// Sinh lính thật tại Doanh Trại của Vùng đất khi hoàn thành huấn luyện
    /// </summary>
    private void SpawnTrainedSoldierForZone(string zoneName, TroopTrainingSlotData slot)
    {
        SettlementZone zone = SettlementManager.Ins != null ? SettlementManager.Ins.GetZoneByName(zoneName) : null;
        if (zone == null) zone = Object.FindFirstObjectByType<SettlementZone>();
        if (zone == null) return;

        // Quét tìm Spawner tương ứng với loại lính
        SpawnSoldier targetSpawner = null;
        SpawnSoldier[] spawners = zone.GetComponentsInChildren<SpawnSoldier>(true);

        foreach (var s in spawners)
        {
            if (s != null && s.gameObject.activeInHierarchy)
            {
                UpgradeableBuilding ub = s.GetComponent<UpgradeableBuilding>();
                if (ub == null) ub = s.GetComponentInParent<UpgradeableBuilding>();
                if (ub != null && ub.buildingType == slot.troopType)
                {
                    targetSpawner = s;
                    break;
                }
            }
        }

        if (targetSpawner == null && spawners.Length > 0)
        {
            targetSpawner = spawners[0];
        }

        if (targetSpawner != null)
        {
            targetSpawner.SpawnOneTrainedSoldier();
        }

        // Reset ô huấn luyện về trạng thái trống
        slot.isTraining = false;
        slot.isCompleted = false;
        slot.remainingWaves = 1;
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
