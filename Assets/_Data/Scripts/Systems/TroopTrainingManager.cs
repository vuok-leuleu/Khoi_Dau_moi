using UnityEngine;

/*
 * TroopTrainingManager.cs
 * Hệ thống Quản Lý Ô Huấn Luyện Lính (Troop Training System)
 * Mỗi vùng đã có Nhà Chính luôn có 3 ô huấn luyện cơ bản. Trại Lính chỉ mở
 * thêm các ô nâng cao (5 / 8), không còn là điều kiện để hiện ô cơ bản.
 * Hệ thống huấn luyện quân của Trại Lính trung tâm.
 * Chỉ thành khởi đầu có Trại Lính và có thể bắt đầu huấn luyện.
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
    private const string CentralSaveKeyPrefix = "Training_Central";

    private TroopTrainingSlotData[] centralSlots;
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
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        centralSettlement = null;
        SubscribeDayNight();
    }

    private void SubscribeDayNight()
    {
        if (DayNightManager.Ins == null) return;

        DayNightManager.Ins.OnWaveStart -= OnWaveStartHandler;
        DayNightManager.Ins.OnWaveStart += OnWaveStartHandler;
    }

    private void UnsubscribeDayNight()
    {
        if (DayNightManager.Ins != null)
        {
            DayNightManager.Ins.OnWaveStart -= OnWaveStartHandler;
        }
    }

    /// <summary>
    /// Kiểm tra ba loại Trại Lính. Các loại này không được phép xây thêm trong game.
    /// </summary>
    public static bool IsCentralBarracksType(BuildingType type)
    {
        return type == BuildingType.BarracksMelee ||
               type == BuildingType.BarracksArcher ||
               type == BuildingType.BarracksSpear ||
               type.ToString().StartsWith("Barracks");
    }

    public bool IsBarracksBuilding(BuildingType type)
    {
        return IsCentralBarracksType(type);
    }

    /// <summary>
    /// Thành trung tâm được chỉ định bằng cờ Is Starting Settlement.
    /// Nếu Scene cũ chưa tích cờ, tự dùng vùng có Zone Tier = 0 để không làm hỏng save cũ.
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
    /// Sang ngày mới, chỉ tiến hành một hàng đợi huấn luyện tại Trại Lính trung tâm.
    /// </summary>
    private void OnWaveStartHandler(int waveIndex)
    {
        SettlementZone centralZone = ResolveCentralSettlement();
        if (centralZone == null) return;

        TroopTrainingSlotData[] slots = EnsureCentralSlots();
        if (slots == null) return;

        SyncSlotsWithCentralBarracks(slots);

        Debug.Log($"[TroopTrainingManager] 🌅 Ngày mới (Wave {waveIndex}) -> cập nhật huấn luyện tại {centralZone.settlementName}.");

        for (int i = 0; i < slots.Length; i++)
        {
            TroopTrainingSlotData slot = slots[i];
            if (slot == null || !slot.isTraining || slot.isCompleted) continue;

            slot.remainingWaves--;
            if (slot.remainingWaves > 0) continue;

            slot.remainingWaves = 0;
            slot.isTraining = false;
            slot.isCompleted = true;

            Debug.Log($"[TroopTrainingManager] ⚔️ Ô {slot.slotIndex + 1} tại {centralZone.settlementName} hoàn tất huấn luyện {slot.troopType}.");
            SpawnTrainedSoldierAtCentralSettlement(slot);
        }

        SaveCentralTrainingData();
        centralZone.UpdateZoneVisualText();
        SettlementSidePanelUI.Ins?.RefreshPanel();
    }

    /// <summary>
    /// Chỉ xóa hàng đợi lính khi chính thành trung tâm bị phòng thủ thua.
    /// Thành khác thua không được xóa dữ liệu Trại Lính trung tâm.
    /// </summary>
    public void ClearZoneTrainingSlots(string zoneName)
    {
        SettlementZone centralZone = ResolveCentralSettlement();
        if (centralZone == null || string.IsNullOrEmpty(zoneName) || zoneName != centralZone.settlementName) return;

        TroopTrainingSlotData[] slots = EnsureCentralSlots();
        for (int i = 0; i < MAX_TRAINING_SLOTS; i++)
        {
            if (slots[i] == null) continue;

            slots[i].isCompleted = false;
            slots[i].isTraining = false;
            slots[i].remainingWaves = 1;
        }

        SaveCentralTrainingData();
    }

    /// <summary>
    /// Tính số lượng ô huấn luyện mở khóa trong Vùng đất.
    /// - Vùng chưa có Nhà Chính: 0 ô mở.
    /// - Vùng đã có Nhà Chính: luôn có 3 ô cơ bản, giống vùng đất khởi đầu.
    /// - Trại Lính cấp 2 / 3 chỉ mở rộng tương ứng lên 5 / 8 ô.
    /// Cấp Trại Lính trung tâm quyết định số ô mở: Lv.1 = 3, Lv.2 = 5, Lv.3 = 8.
    /// </summary>
    public int GetUnlockedSlotsCountForZone(SettlementZone zone)
    {
        if (zone == null || !zone.isTownHallEstablished) return 0;
        if (!IsCentralTrainingSettlement(zone)) return 0;
        return GetUnlockedSlotsCountForCentralSettlement();
    }

    private int GetUnlockedSlotsCountForCentralSettlement()
    {
        SettlementZone centralZone = ResolveCentralSettlement();
        if (centralZone == null) return 0;

        int highestBarracksLevel = 0;

        if (centralZone.builtStructures != null)
        {
            foreach (UpgradeableBuilding building in centralZone.builtStructures)
            {
                UpdateHighestBarracksLevel(building, ref highestBarracksLevel);
            }
        }

        if (highestBarracksLevel == 0)
        {
            UpgradeableBuilding[] buildings = centralZone.GetComponentsInChildren<UpgradeableBuilding>(true);
            foreach (UpgradeableBuilding building in buildings)
            {
                UpdateHighestBarracksLevel(building, ref highestBarracksLevel);
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
            case 1: return 3;
            case 2: return 5;
            default: return highestBarracksLevel >= 3 ? 8 : 0;
        }

        return Mathf.Clamp(Mathf.Max(3, unlockedSlotsFromBarracks), 0, MAX_TRAINING_SLOTS);
    }

    private void UpdateHighestBarracksLevel(UpgradeableBuilding building, ref int highestBarracksLevel)
    {
        if (building == null || !building.gameObject.activeInHierarchy || !IsCentralBarracksType(building.buildingType)) return;
        if (building.IsInitialBuildNeeded || building.IsRuined) return;

        highestBarracksLevel = Mathf.Max(highestBarracksLevel, building.CurrentLevel + 1);
    }

    /// <summary>
    /// Chỉ thành trung tâm có dữ liệu huấn luyện thật. Các thành khác dùng cùng
    /// khung UI để hiển thị quân đang đồn trú theo từng loại.
    /// </summary>
    public TroopTrainingSlotData[] GetSlotsForZone(SettlementZone zone)
    {
        if (zone == null) return CreateLockedSlots();

        if (!IsCentralTrainingSettlement(zone))
        {
            return CreateGarrisonDisplaySlots(zone);
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
        TroopTrainingSlotData[] slots = EnsureCentralSlots();
        SyncSlotsWithCentralBarracks(slots);
        return slots;
    }

    public TroopTrainingSlotData[] GetCentralTrainingSlots()
    {
        return GetSlotsForZone(ResolveCentralSettlement());
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
    /// Bắt đầu huấn luyện. Các lệnh từ thành khác bị chặn ở đây, kể cả khi UI gọi trực tiếp.
    /// </summary>
    public bool StartTraining(SettlementZone zone, int slotIndex, BuildingType troopType)
    {
        SettlementZone centralZone = ResolveCentralSettlement();
        if (centralZone == null || zone == null || !IsCentralTrainingSettlement(zone))
        {
            const string warning = "Chỉ có thể huấn luyện lính tại thành đầu tiên có Trại Lính!";
            UIManager.Ins?.ShowWarning(warning);
            Debug.LogWarning($"[TroopTrainingManager] {warning}");
            return false;
        }

        if (slotIndex < 0 || slotIndex >= MAX_TRAINING_SLOTS) return false;

        TroopTrainingSlotData[] slots = GetSlotsForZone(centralZone);
        TroopTrainingSlotData slot = slots[slotIndex];

        if (!slot.isUnlocked)
        {
            int reqLevel = GetRequiredBarracksLevelForSlot(slotIndex);
            string warning = $"⚠️ Ô {slotIndex + 1} đang bị khóa. Yêu cầu Trại Lính Lv.{reqLevel}!";
            UIManager.Ins?.ShowWarning(warning);
            Debug.LogWarning($"[TroopTrainingManager] {warning}");
            return false;
        }

        if (slot.isTraining || slot.isCompleted)
        {
            string warning = $"⚠️ Ô {slotIndex + 1} đang bận huấn luyện hoặc đã chứa lính!";
            UIManager.Ins?.ShowWarning(warning);
            Debug.LogWarning($"[TroopTrainingManager] {warning}");
            return false;
        }

        slot.isTraining = true;
        slot.troopType = troopType;
        slot.remainingWaves = 1;
        slot.isCompleted = false;

        SaveCentralTrainingData();
        CampaignTutorialManager.Ins?.OnTroopTrainingStarted(troopType);
        centralZone.UpdateZoneVisualText();
        Debug.Log($"[TroopTrainingManager] ⚔️ Bắt đầu huấn luyện {troopType} tại {centralZone.settlementName}, ô {slotIndex + 1} (1 ngày).");
        SettlementSidePanelUI.Ins?.RefreshPanel();
        return true;
    }

    public int GetRequiredBarracksLevelForSlot(int slotIndex)
    {
        if (slotIndex < 3) return 1;
        if (slotIndex < 5) return 2;
        return 3;
    }

    private void SpawnTrainedSoldierAtCentralSettlement(TroopTrainingSlotData slot)
    {
        SettlementZone centralZone = ResolveCentralSettlement();
        if (centralZone == null) return;

        SpawnSoldier targetSpawner = null;
        SpawnSoldier[] spawners = centralZone.GetComponentsInChildren<SpawnSoldier>(true);

        foreach (SpawnSoldier spawner in spawners)
        {
            if (spawner == null || !spawner.gameObject.activeInHierarchy) continue;

            UpgradeableBuilding building = spawner.GetComponent<UpgradeableBuilding>();
            if (building == null) building = spawner.GetComponentInParent<UpgradeableBuilding>();

            if (building != null && IsCentralBarracksType(building.buildingType) && spawner.CanSpawnTrainedSoldier(slot.troopType))
            {
                targetSpawner = spawner;
                break;
            }
        }

        if (targetSpawner == null)
        {
            Debug.LogWarning($"[TroopTrainingManager] Không tìm thấy SpawnSoldier hợp lệ tại Trại Lính trung tâm {centralZone.settlementName} cho {slot.troopType}.");
            return;
        }

        int spawnedCount = targetSpawner.SpawnTrainedSoldiers(slot.troopType, SOLDIERS_PER_TRAINING_UNIT);
        if (spawnedCount != SOLDIERS_PER_TRAINING_UNIT)
        {
            Debug.LogWarning($"[TroopTrainingManager] {slot.troopType} chỉ spawn được {spawnedCount}/{SOLDIERS_PER_TRAINING_UNIT} lính tại {centralZone.settlementName}.");
        }
    }

    private TroopTrainingSlotData[] EnsureCentralSlots()
    {
        if (centralSlots != null) return centralSlots;

        SettlementZone centralZone = ResolveCentralSettlement();
        if (HasSavedData(CentralSaveKeyPrefix))
        {
            centralSlots = LoadSlots(CentralSaveKeyPrefix);
        }
        else if (centralZone != null && HasSavedData($"Training_{centralZone.settlementName}"))
        {
            // Chuyển một lần dữ liệu huấn luyện cũ của ZEFFIRA sang Trại Lính trung tâm.
            centralSlots = LoadSlots($"Training_{centralZone.settlementName}");
            SaveCentralTrainingData();
        }
        else
        {
            centralSlots = CreateLockedSlots();
        }

        return centralSlots;
    }

    private void SyncSlotsWithCentralBarracks(TroopTrainingSlotData[] slots)
    {
        if (slots == null) return;

        int unlockedCount = GetUnlockedSlotsCountForCentralSettlement();
        int activeSoldierCount = GetCentralActiveSoldierCount();
        int completedCount = 0;

        for (int i = 0; i < MAX_TRAINING_SLOTS; i++)
        {
            if (slots[i] == null) slots[i] = new TroopTrainingSlotData { slotIndex = i };

            slots[i].isUnlocked = i < unlockedCount;
            if (!slots[i].isUnlocked)
            {
                slots[i].isTraining = false;
                slots[i].isCompleted = false;
                slots[i].remainingWaves = 1;
                continue;
            }

            if (slots[i].isCompleted)
            {
                if (completedCount < activeSoldierCount)
                {
                    completedCount++;
                }
                else
                {
                    slots[i].isCompleted = false;
                    slots[i].isTraining = false;
                    slots[i].remainingWaves = 1;
                }
            }
        }
    }

    private int GetCentralActiveSoldierCount()
    {
        SettlementZone centralZone = ResolveCentralSettlement();
        if (centralZone == null) return 0;

        return GetStationedSoldierCount(centralZone, BuildingType.None);
    }

    private static TroopTrainingSlotData[] CreateGarrisonDisplaySlots(SettlementZone zone)
    {
        if (zone == null || !zone.isTownHallEstablished)
        {
            return CreateLockedSlots();
        }

        // Ba ô đầu dành cố định cho ba loại quân; các ô sau tiếp tục bị khóa như
        // giao diện Trại Lính. Nhờ đó một thành có 9 lính cùng loại vẫn hiện "x9"
        // thay vì bị giới hạn bởi số ô UI.
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
            if (soldier == null || !soldier.gameObject.activeInHierarchy ||
                soldier.isDead || soldier.isExpeditionMarching)
            {
                continue;
            }

            bool isStationedHere;
            if (!string.IsNullOrEmpty(soldier.stationedSettlementZoneName))
            {
                isStationedHere = soldier.stationedSettlementZoneName == zone.settlementName;
            }
            else
            {
                isStationedHere = soldier.GetComponentInParent<SettlementZone>() == zone;
            }

            if (!isStationedHere) continue;

            if (troopType == BuildingType.None || ToBuildingType(soldier.AttackMode) == troopType)
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

    private void SaveCentralTrainingData()
    {
        if (centralSlots == null) return;

        for (int i = 0; i < MAX_TRAINING_SLOTS; i++)
        {
            TroopTrainingSlotData slot = centralSlots[i];
            if (slot == null) continue;

            PlayerPrefs.SetInt($"{CentralSaveKeyPrefix}_Slot_{i}_IsTraining", slot.isTraining ? 1 : 0);
            PlayerPrefs.SetInt($"{CentralSaveKeyPrefix}_Slot_{i}_TroopType", (int)slot.troopType);
            PlayerPrefs.SetInt($"{CentralSaveKeyPrefix}_Slot_{i}_Remaining", slot.remainingWaves);
            PlayerPrefs.SetInt($"{CentralSaveKeyPrefix}_Slot_{i}_Completed", slot.isCompleted ? 1 : 0);
        }

        PlayerPrefs.Save();
    }

    private static bool HasSavedData(string keyPrefix)
    {
        return PlayerPrefs.HasKey($"{keyPrefix}_Slot_0_IsTraining");
    }

    private static TroopTrainingSlotData[] LoadSlots(string keyPrefix)
    {
        TroopTrainingSlotData[] slots = CreateLockedSlots();
        for (int i = 0; i < MAX_TRAINING_SLOTS; i++)
        {
            slots[i].isTraining = PlayerPrefs.GetInt($"{keyPrefix}_Slot_{i}_IsTraining", 0) == 1;
            slots[i].troopType = (BuildingType)PlayerPrefs.GetInt($"{keyPrefix}_Slot_{i}_TroopType", (int)BuildingType.BarracksMelee);
            slots[i].remainingWaves = PlayerPrefs.GetInt($"{keyPrefix}_Slot_{i}_Remaining", 1);
            slots[i].isCompleted = PlayerPrefs.GetInt($"{keyPrefix}_Slot_{i}_Completed", 0) == 1;
        }

        return slots;
    }

    private static TroopTrainingSlotData[] CreateLockedSlots()
    {
        return CreateDisplaySlots(0);
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
}
