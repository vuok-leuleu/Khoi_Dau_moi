using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public static class BattleData
{
    [System.Serializable]
    public class BuildingInfo
    {
        public BuildingType buildingType;
        public int level = 1;
        public int soldierCount = 0;
        public Vector3 originalPosition;
    }

    [System.Serializable]
    public class EnemyMarchInfo
    {
        public Vector3 position;
        public Vector3 startSpawnPosition;
        public int spawnWave;
        public int targetWave;
        public int wavesToReachTarget;
    }

    [System.Serializable]
    public class SoldierMarchInfo
    {
        public Vector3 position;
        public Vector3 marchStartPosition;
        public Vector3 marchDestinationPosition;
        public int marchStartWave;
        public int marchWavesToReach;
        public int marchTargetWave;
        public string marchDestinationZoneName;
        public string stationedSettlementZoneName;
        public int stationedTroopSlotIndex = -1;
        public int marchDestinationTroopSlotIndex = -1;
        public bool hasReachedExpeditionDestination;
        public AttackMode attackMode = AttackMode.Melee;
        // Tên prefab gốc để Cung/Khiên quay về đúng visual sau SceneBattle,
        // thay vì lấy ngẫu nhiên một UnitController cùng AttackMode.
        public string prefabName;
    }

    public static bool HasData = false;
    public static int EnemyWaveCount = 1;
    // Danh sách prefab chỉ dùng cho trận chinh phục đang mở. Đây là bản sao dữ liệu
    // từ SettlementZone nên không phụ thuộc EnemySpawn hay EnemySpawnManager.prefab.
    public static List<GameObject> ConquestEnemyPrefabs = new List<GameObject>();
    public static bool HasExplicitConquestEnemyComposition = false;
    // Đội raid thật sự được EnemySpawn tạo trên map. Danh sách lưu prefab nguồn
    // của từng Enemy instance để SceneBattle tạo lại đúng loại lẫn số lượng.
    public static List<GameObject> RaidEnemyPrefabs = new List<GameObject>();
    public static bool HasExplicitRaidEnemyComposition = false;
    // Rồng chỉ được bật cho trận phòng thủ lớn do EnemyInvasionManager đánh dấu.
    public static bool SpawnDragonInCurrentBattle = false;
    public static List<BuildingInfo> PlayerBuildings = new List<BuildingInfo>();
    public static int TotalSoldiersInBase = 0;
    public static string MainSceneName = "MainScene";
    public static string TargetedSettlementZoneName = "";

    // Lưu trữ tiến trình Wave & Di chuyển khi chuyển sang SceneBattle
    public static int SavedCurrentWave = 0;
    public static DayNightManager.WaveState SavedWaveState = DayNightManager.WaveState.Preparation;
    public static bool SavedIsWaveActive = false;
    public static List<EnemyMarchInfo> SavedEnemyMarches = new List<EnemyMarchInfo>();
    public static List<SoldierMarchInfo> SavedSoldierMarches = new List<SoldierMarchInfo>();
    // Prologue bắt buộc dùng Hộ Vệ trước khi node research Khiên mở. Cờ này
    // chỉ tồn tại cho đúng trận tutorial đang chuyển cảnh.
    public static bool AllowTutorialShieldTroopsInCurrentBattle = false;

    // Kết quả trận đấu
    public static bool HasResult = false;
    public static bool IsPlayerVictory = false;
    public static bool LastBattleWasVictory = false;
    public static int SurvivingSoldiersCount = 0;

    [System.Serializable]
    public class BattleParticipantInfo
    {
        public AttackMode attackMode;
        public int count;

        public BattleParticipantInfo(AttackMode attackMode, int count = 1)
        {
            this.attackMode = attackMode;
            this.count = count;
        }
    }

    // Chỉ các loại lính thật sự được BattleManager sinh ra mới được đưa vào
    // danh sách này. UI kết quả dùng nó thay cho dữ liệu mẫu trong prefab.
    public static List<BattleParticipantInfo> BattleParticipants = new List<BattleParticipantInfo>();

    // Giá trị mặc định đồng bộ với UISceneBattle.prefab. UISceneBattle sẽ cấu
    // hình lại các giá trị này theo Inspector ngay khi SceneBattle được mở.
    public static int VictoryWoodReward = 125;
    public static int VictoryGoldReward = 5;
    private static bool victoryRewardGranted;

    /// <summary>
    /// Bật cờ này trước khi Reload Scene để ngăn BattleData tự động Load lại file Save.
    /// Dùng cho UILinh.ResetGame() để reset về trạng thái gốc của Scene.
    /// </summary>
    public static bool SkipAutoLoadOnNextSceneLoad = false;

    public static void SetConquestEnemyComposition(SettlementZone zone)
    {
        ClearRaidEnemyComposition();
        ConquestEnemyPrefabs.Clear();
        HasExplicitConquestEnemyComposition = false;

        if (zone == null) return;

        List<GameObject> prefabs = zone.GetConquestEnemyPrefabs();
        if (prefabs == null || prefabs.Count == 0) return;

        ConquestEnemyPrefabs.AddRange(prefabs);
        HasExplicitConquestEnemyComposition = true;
        EnemyWaveCount = ConquestEnemyPrefabs.Count;
    }

    public static void ClearConquestEnemyComposition()
    {
        ConquestEnemyPrefabs.Clear();
        HasExplicitConquestEnemyComposition = false;
    }

    public static void SetRaidEnemyComposition(IEnumerable<GameObject> prefabs)
    {
        RaidEnemyPrefabs.Clear();
        HasExplicitRaidEnemyComposition = false;

        if (prefabs == null) return;
        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null) RaidEnemyPrefabs.Add(prefab);
        }

        if (RaidEnemyPrefabs.Count == 0) return;

        // Một trận chỉ dùng một nguồn đội hình Enemy rõ ràng.
        ClearConquestEnemyComposition();
        HasExplicitRaidEnemyComposition = true;
        EnemyWaveCount = RaidEnemyPrefabs.Count;
    }

    public static void ClearRaidEnemyComposition()
    {
        RaidEnemyPrefabs.Clear();
        HasExplicitRaidEnemyComposition = false;
    }

    public static void ClearBattleParticipants()
    {
        BattleParticipants.Clear();
    }

    public static void BeginNewBattle()
    {
        ClearBattleParticipants();
        HasResult = false;
        IsPlayerVictory = false;
        SurvivingSoldiersCount = 0;
        victoryRewardGranted = false;
    }

    public static void RegisterBattleParticipant(AttackMode attackMode)
    {
        foreach (BattleParticipantInfo participant in BattleParticipants)
        {
            if (participant.attackMode == attackMode)
            {
                participant.count++;
                return;
            }
        }

        BattleParticipants.Add(new BattleParticipantInfo(attackMode));
    }

    public static void ConfigureVictoryRewards(int woodReward, int goldReward)
    {
        VictoryWoodReward = Mathf.Max(0, woodReward);
        VictoryGoldReward = Mathf.Max(0, goldReward);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitSceneLoadedCallback()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "SceneBattle")
        {
            if (SkipAutoLoadOnNextSceneLoad)
            {
                // Reset về trạng thái gốc: không load file Save, để Scene chạy tự nhiên từ dữ liệu ban đầu
                SkipAutoLoadOnNextSceneLoad = false;
                Debug.Log("[BattleData] ⏩ Bỏ qua auto-load Save (Reset Scene được yêu cầu).");
            }
            else
            {
                BattleReturnRestoreRunner.RestoreWhenSceneReady();
            }
        }
    }

    /// <summary>
    /// Ghi nhận trạng thái hiện tại của Scene chính trước khi chuyển sang Battle Scene.
    /// </summary>
    /// <param name="waveEnemyCount">Số lượng Enemy thuộc Wave chuẩn bị giao tranh</param>
    public static void RecordCurrentSceneState(int waveEnemyCount)
    {
        BeginNewBattle();

        Scene currentScene = SceneManager.GetActiveScene();
        if (currentScene.name != "SceneBattle")
        {
            MainSceneName = currentScene.name;
        }

        SettlementZone[] settlementZones = Object.FindObjectsByType<SettlementZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (SettlementZone settlementZone in settlementZones)
        {
            if (settlementZone != null)
            {
                settlementZone.SaveSettlementState();
            }
        }
        PlayerPrefs.Save();
        // 🔥 Lưu toàn bộ công trình hiện có ở Main Scene vào file Save JSON trước khi sang Battle Scene
        BuildingSystem buildingSys = BuildingSystem.Ins != null ? BuildingSystem.Ins : Object.FindFirstObjectByType<BuildingSystem>();
        if (buildingSys != null)
        {
            buildingSys.SaveBuildingsToSlot(1);
        }

        // 1. Lưu trạng thái Wave của DayNightManager
        if (DayNightManager.HasInstance && DayNightManager.Ins != null)
        {
            SavedCurrentWave = DayNightManager.Ins.CurrentWave;
            SavedWaveState = DayNightManager.Ins.CurrentWaveState;
            SavedIsWaveActive = DayNightManager.Ins.IsWaveActive;
        }

        // Khi đang đi chinh phục một căn cứ địch, cuộc tập kích đang nhắm vào
        // ta vẫn phải tiếp tục tồn tại khi quay về Main Map.
        if (IsAttackingExpedition)
        {
            EnemyInvasionManager.CaptureActiveRaidForBattleTransition();
        }
        else
        {
            EnemyInvasionManager.ClearPendingRaidRestore();
        }

        // 2. Lưu tiến trình di chuyển của các đợt EnemyAI
        SavedEnemyMarches.Clear();
        EnemyAI[] activeEnemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (var e in activeEnemies)
        {
            if (e != null && e.gameObject.activeInHierarchy)
            {
                SavedEnemyMarches.Add(new EnemyMarchInfo
                {
                    position = e.transform.position,
                    startSpawnPosition = e.transform.position,
                    spawnWave = e.spawnWave,
                    targetWave = e.targetWave,
                    wavesToReachTarget = e.wavesToReachTarget
                });
            }
        }

        // 3. Lưu tiến trình hành quân của Lính (UnitController) đang xuất trận
        SavedSoldierMarches.Clear();
        string defenseZoneName = TargetedSettlementZoneName;
        if (!IsAttackingExpedition && string.IsNullOrEmpty(defenseZoneName) &&
            EnemyInvasionManager.Ins != null && EnemyInvasionManager.Ins.currentTargetedZone != null)
        {
            defenseZoneName = EnemyInvasionManager.Ins.currentTargetedZone.settlementName;
            TargetedSettlementZoneName = defenseZoneName;
        }

        UnitController[] activeUnits = Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        int battleSoldierCount = 0;
        foreach (var u in activeUnits)
        {
            if (u != null && u.gameObject.activeInHierarchy)
            {
                if (u.isExpeditionMarching || u.hasReachedExpeditionDestination)
                {
                    SavedSoldierMarches.Add(new SoldierMarchInfo
                    {
                        position = u.transform.position,
                        marchStartPosition = u.marchStartPosition,
                        marchDestinationPosition = u.marchDestinationPosition,
                        marchStartWave = u.marchStartWave,
                        marchWavesToReach = u.marchWavesToReach,
                        marchTargetWave = u.marchTargetWave,
                        marchDestinationZoneName = u.marchDestinationZoneName,
                        stationedSettlementZoneName = u.stationedSettlementZoneName,
                        stationedTroopSlotIndex = u.stationedTroopSlotIndex,
                        marchDestinationTroopSlotIndex = u.marchDestinationTroopSlotIndex,
                        hasReachedExpeditionDestination = u.hasReachedExpeditionDestination,
                        attackMode = u.AttackMode,
                        prefabName = NormalizePrefabName(u.gameObject.name)
                    });
                }

                if (IsAttackingExpedition)
                {
                    bool isAttackingTarget = u.hasReachedExpeditionDestination &&
                        u.marchDestinationZoneName == TargetedSettlementZoneName;
                    if (isAttackingTarget)
                    {
                        battleSoldierCount++;
                    }
                }
                else
                {
                    // Chỉ lính đang đóng tại vùng bị địch đánh mới được phòng thủ.
                    // Quân ở vùng khác và quân đang hành quân không được kéo vào trận.
                    if (u.IsStationedInZone(defenseZoneName))
                    {
                        battleSoldierCount++;
                    }
                }
            }
        }

        EnemyWaveCount = Mathf.Max(1, waveEnemyCount);
        PlayerBuildings.Clear();
        TotalSoldiersInBase = 0;

        // 4. Tìm tất cả các công trình UpgradeableBuilding trong scene
        UpgradeableBuilding[] buildings = Object.FindObjectsByType<UpgradeableBuilding>(FindObjectsSortMode.None);

        foreach (var building in buildings)
        {
            if (building == null || !building.gameObject.activeInHierarchy) continue;

            if (!IsAttackingExpedition && !IsBuildingInSettlement(building, defenseZoneName))
            {
                continue;
            }

            BuildingInfo info = new BuildingInfo
            {
                buildingType = building.buildingType,
                level = building.CurrentLevel + 1,
                originalPosition = building.transform.position,
                soldierCount = 0
            };

            // Khi xuất chinh, BattleManager chỉ được spawn đúng đoàn đã chọn ở mục tiêu.
            // Khi phòng thủ, vẫn giữ số quân của từng doanh trại như trước.
            SpawnSoldier spawner = building.GetComponent<SpawnSoldier>();
            if (spawner == null) spawner = building.GetComponentInChildren<SpawnSoldier>();

            if (!IsAttackingExpedition && spawner != null)
            {
                info.soldierCount = spawner.GetCurrentActiveSoldierCount();
            }

            PlayerBuildings.Add(info);
        }

        TotalSoldiersInBase = battleSoldierCount;
        HasData = true;
        Debug.Log($"[BattleData] Đã lưu dữ liệu Trận Đấu: MainScene = {MainSceneName}, CurrentWave = {SavedCurrentWave}, Enemy Wave Count = {EnemyWaveCount}, Quái hành quân = {SavedEnemyMarches.Count}, Lính xuất trận = {SavedSoldierMarches.Count}");
    }

    internal static void RestoreWaveAndMarchProgress()
    {
        // 1. Phục hồi số Wave hiện tại trên DayNightManager
        if (DayNightManager.HasInstance && DayNightManager.Ins != null && SavedCurrentWave > 0)
        {
            DayNightManager.Ins.RestoreWaveState(SavedCurrentWave, SavedWaveState, SavedIsWaveActive);
            Debug.Log($"[BattleData] 🔄 Đã khôi phục Wave: {SavedCurrentWave}");
        }

        // 2. Phục hồi vị trí và số wave còn lại của các đợt EnemyAI
        if (SavedEnemyMarches.Count > 0)
        {
            EnemyAI[] sceneEnemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            for (int i = 0; i < SavedEnemyMarches.Count && i < sceneEnemies.Length; i++)
            {
                var info = SavedEnemyMarches[i];
                var e = sceneEnemies[i];
                if (e != null)
                {
                    e.transform.position = info.position;
                    e.spawnWave = info.spawnWave;
                    e.targetWave = info.targetWave;
                    e.wavesToReachTarget = info.wavesToReachTarget;
                }
            }
        }

        // 3. Phục hồi đoàn Lính đang hành quân/xong hành quân.  Các lính này không
        // được lưu vào quân số của doanh trại nguồn, nên phải tạo lại riêng thay vì
        // ghi đè ngẫu nhiên lên lính mà BuildingSystem vừa spawn.
        if (SavedSoldierMarches.Count > 0)
        {
            List<UnitController> marchingList = new List<UnitController>();

            foreach (SoldierMarchInfo info in SavedSoldierMarches)
            {
                UnitController unit = SpawnRestoredSoldier(info);
                if (unit != null && unit.isExpeditionMarching) marchingList.Add(unit);
            }

            if (marchingList.Count > 0)
            {
                SettlementZone targetZone = null;
                List<UnitController> expeditionMarchers = new List<UnitController>();
                foreach (UnitController unit in marchingList)
                {
                    if (unit == null || string.IsNullOrEmpty(unit.marchDestinationZoneName)) continue;

                    SettlementZone zone = SettlementManager.Ins != null
                        ? SettlementManager.Ins.GetZoneByName(unit.marchDestinationZoneName)
                        : null;
                    if (zone == null)
                    {
                        foreach (SettlementZone candidate in Object.FindObjectsByType<SettlementZone>(FindObjectsSortMode.None))
                        {
                            if (candidate != null && candidate.settlementName == unit.marchDestinationZoneName)
                            {
                                zone = candidate;
                                break;
                            }
                        }
                    }

                    if (zone != null && zone.hasEnemyOutpost)
                    {
                        if (targetZone == null)
                        {
                            targetZone = zone;
                        }

                        if (zone == targetZone)
                        {
                            expeditionMarchers.Add(unit);
                        }
                    }
                }

                Transform targetTr = targetZone != null ? targetZone.GetConquestTargetTransform() : null;

                if (targetTr != null && expeditionMarchers.Count > 0)
                {
                    GameObject runner = new GameObject("ExpeditionBattleTriggerRunner");
                    ExpeditionBattleTrigger trigger = runner.AddComponent<ExpeditionBattleTrigger>();
                    trigger.StartMonitoring(expeditionMarchers, targetTr, "SceneBattle");
                }
            }
        }
    }

    private static bool IsBuildingInSettlement(UpgradeableBuilding building, string settlementZoneName)
    {
        if (building == null || string.IsNullOrEmpty(settlementZoneName)) return false;
        SettlementZone zone = building.GetComponentInParent<SettlementZone>();
        return zone != null && zone.settlementName == settlementZoneName;
    }

    private static UnitController SpawnRestoredSoldier(SoldierMarchInfo info)
    {
        if (info == null) return null;

        UnitController prefabUnit = null;

        // Ưu tiên chính xác prefab mà đơn vị đã dùng trước trận. Điều này quan
        // trọng với Cung/Khiên vì chỉ ghi AttackMode là chưa đủ để giữ model.
        if (!string.IsNullOrEmpty(info.prefabName))
        {
            foreach (UnitController candidate in Resources.FindObjectsOfTypeAll<UnitController>())
            {
                if (candidate != null && candidate.AttackMode == info.attackMode &&
                    !candidate.gameObject.scene.IsValid() &&
                    NormalizePrefabName(candidate.gameObject.name) == info.prefabName)
                {
                    prefabUnit = candidate;
                    break;
                }
            }
        }

        foreach (UnitController candidate in Resources.FindObjectsOfTypeAll<UnitController>())
        {
            if (prefabUnit != null) break;
            if (candidate != null && candidate.AttackMode == info.attackMode && !candidate.gameObject.scene.IsValid())
            {
                prefabUnit = candidate;
                break;
            }
        }
        if (prefabUnit == null)
        {
            Debug.LogWarning($"[BattleData] Không tìm thấy prefab lính {info.attackMode} để khôi phục đoàn hành quân.");
            return null;
        }

        GameObject unitObject = Object.Instantiate(prefabUnit.gameObject, info.position, Quaternion.identity);
        UnitController unit = unitObject.GetComponent<UnitController>();
        if (unit == null) unit = unitObject.GetComponentInChildren<UnitController>();
        if (unit == null)
        {
            Object.Destroy(unitObject);
            return null;
        }

        SettlementZone destinationZone = FindSettlementZone(info.marchDestinationZoneName);
        if (destinationZone != null) unit.transform.SetParent(destinationZone.transform, true);

        unit.marchStartPosition = info.marchStartPosition;
        unit.marchDestinationPosition = info.marchDestinationPosition;
        unit.marchStartWave = info.marchStartWave;
        unit.marchWavesToReach = info.marchWavesToReach;
        unit.marchTargetWave = info.marchTargetWave;
        unit.marchDestinationZoneName = info.marchDestinationZoneName;
        unit.stationedSettlementZoneName = info.stationedSettlementZoneName;
        unit.stationedTroopSlotIndex = info.stationedTroopSlotIndex;
        unit.marchDestinationTroopSlotIndex = info.marchDestinationTroopSlotIndex;
        unit.hasReachedExpeditionDestination = info.hasReachedExpeditionDestination;
        unit.AttackMode = info.attackMode;
        unit.isExpeditionMarching = !info.hasReachedExpeditionDestination;
        unit.currentState = unit.isExpeditionMarching ? UnitState.Moving : UnitState.Idle;
        return unit;
    }

    private static string NormalizePrefabName(string objectName)
    {
        return string.IsNullOrEmpty(objectName)
            ? string.Empty
            : objectName.Replace("(Clone)", string.Empty).Trim();
    }

    private static SettlementZone FindSettlementZone(string settlementZoneName)
    {
        if (string.IsNullOrEmpty(settlementZoneName)) return null;
        if (SettlementManager.Ins != null)
        {
            SettlementZone zone = SettlementManager.Ins.GetZoneByName(settlementZoneName);
            if (zone != null) return zone;
        }
        foreach (SettlementZone zone in Object.FindObjectsByType<SettlementZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (zone != null && zone.settlementName == settlementZoneName) return zone;
        }
        return null;
    }

    /// <summary>
    /// Đặt lại dữ liệu trận đấu
    /// </summary>
    public static void ResetData()
    {
        HasData = false;
        EnemyWaveCount = 1;
        PlayerBuildings.Clear();
        TotalSoldiersInBase = 0;
        HasResult = false;
        LastBattleWasVictory = false;
        SpawnDragonInCurrentBattle = false;
        ClearConquestEnemyComposition();
        ClearRaidEnemyComposition();
        SavedCurrentWave = 0;
        SavedEnemyMarches.Clear();
        SavedSoldierMarches.Clear();
        AllowTutorialShieldTroopsInCurrentBattle = false;
        ClearBattleParticipants();
        victoryRewardGranted = false;
        EnemyInvasionManager.ClearPendingRaidRestore();
    }

    public static bool IsAttackingExpedition = false; // true = Xâm Chiếm / Chinh Phạt | false = Phòng Thủ Căn Cứ Nhà

    /// <summary>
    /// Áp dụng kết quả trận đấu khi quay lại Scene chính
    /// </summary>
    public static void ApplyBattleResultToScene()
    {
        if (!HasResult) return;

        LastBattleWasVictory = IsPlayerVictory;
        Debug.Log($"[BattleData] 🔥 Áp dụng kết quả trận đấu: IsAttackingExpedition = {IsAttackingExpedition}, Victory = {IsPlayerVictory}");

        if (IsPlayerVictory)
        {
            GrantVictoryRewards();
        }

        if (IsAttackingExpedition)
        {
            // === THỜI ĐIỂM ĐI XÂM CHIẾM / CHINH PHẠT CĂN CỨ ĐỊCH ===
            if (IsPlayerVictory)
            {
                ApplyExpeditionVictoryResult();
            }
            else
            {
                ApplyExpeditionDefeatResult();
            }
        }
        else
        {
            // === THỜI ĐIỂM PHÒNG THỦ CĂN CỨ NHÀ CHỐNG ĐỊCH TẤN CÔNG ===
            if (IsPlayerVictory)
            {
                Debug.Log("[BattleData] 🛡️ PHÒNG THỦ THẮNG! Bảo toàn 100% căn cứ và tài nguyên.");
                EnemyInvasionManager.Ins?.TriggerDefenseVictory();
            }
            else
            {
                ApplyDefenseDefeatResult();
            }
        }

        HasResult = false;
        IsAttackingExpedition = false;
        SpawnDragonInCurrentBattle = false;
        AllowTutorialShieldTroopsInCurrentBattle = false;
        ClearRaidEnemyComposition();

        // Lưu lại trạng thái công trình sau trận đấu vào Save Slot 1
        BuildingSystem buildingSys = BuildingSystem.Ins != null ? BuildingSystem.Ins : Object.FindFirstObjectByType<BuildingSystem>();
        if (buildingSys != null)
        {
            buildingSys.SaveBuildingsToSlot(1);
        }
    }

    /// <summary>
    /// Cộng phần thưởng chiến thắng vào đúng kho tài nguyên của Scene chính.
    /// Hàm chỉ được phép thành công một lần cho mỗi trận để không nhân đôi tiền
    /// khi scene bị tải lại hoặc luồng khôi phục chạy lại.
    /// </summary>
    private static void GrantVictoryRewards()
    {
        if (victoryRewardGranted) return;

        JsonDataManager dataManager = JsonDataManager.Ins != null
            ? JsonDataManager.Ins
            : Object.FindFirstObjectByType<JsonDataManager>();

        if (dataManager == null)
        {
            Debug.LogWarning("[BattleData] Chưa tìm thấy JsonDataManager nên chưa thể cộng phần thưởng chiến thắng.");
            return;
        }

        if (VictoryWoodReward > 0) dataManager.AddWood(VictoryWoodReward);
        if (VictoryGoldReward > 0) dataManager.AddGold(VictoryGoldReward);
        dataManager.BroadcastAllResources();

        victoryRewardGranted = true;
        Debug.Log($"[BattleData] 🏆 Đã nhận phần thưởng chiến thắng: +{VictoryWoodReward} Gỗ, +{VictoryGoldReward} Vàng.");
    }

    /// <summary>
    /// Xâm chiếm Thắng: Không mất lính, lính xuất trận trở về an toàn & Giải phóng Vùng đất
    /// </summary>
    private static void ApplyExpeditionVictoryResult()
    {
        // 🔓 CHINH PHỤC VÙNG ĐẤT: Giải phóng Căn cứ Địch trên SettlementZone
        if (string.IsNullOrEmpty(TargetedSettlementZoneName))
        {
            Debug.LogError("[BattleData] Không có tên vùng mục tiêu, hủy xử lý chinh phục để tránh phá nhầm Nhà Chính.");
            return;
        }

        SettlementZone conqueredZone = null;
        SettlementZone[] allZones = Object.FindObjectsByType<SettlementZone>(FindObjectsSortMode.None);
        foreach (var z in allZones)
        {
            if (z != null && z.settlementName == TargetedSettlementZoneName)
            {
                conqueredZone = z;
                break;
            }
        }

        if (conqueredZone == null)
        {
            Debug.LogError($"[BattleData] Không tìm thấy vùng mục tiêu '{TargetedSettlementZoneName}', hủy xử lý chinh phục.");
            TargetedSettlementZoneName = "";
            return;
        }

        conqueredZone.InstantiateEnemyOutpost();
        conqueredZone.OnEnemyOutpostDestroyed();
        conqueredZone.SaveSettlementState();

        UnitController[] activeUnits = Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        foreach (UnitController unit in activeUnits)
        {
            if (unit != null && unit.gameObject.activeInHierarchy &&
                unit.hasReachedExpeditionDestination &&
                unit.marchDestinationZoneName == TargetedSettlementZoneName)
            {
                unit.CompleteExpeditionMarch();
            }
        }
        Debug.Log($"[BattleData] 🏆 XÂM CHIẾM THẮNG! Đã giải phóng vùng đất '{conqueredZone.settlementName}'. Lính xuất trận trở về an toàn!");
        TargetedSettlementZoneName = "";
    }

    /// <summary>
    /// Xâm chiếm Thua: Mất toàn bộ số lính đã cử đi xâm chiếm ngay lập tức khi quay về Scene. Lính ở nhà không bị mất.
    /// </summary>
    private static void ApplyExpeditionDefeatResult()
    {
        // Tiêu diệt các đoàn lính đang hành quân xuất trận
        UnitController[] activeUnits = Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        foreach (var u in activeUnits)
        {
            if (u != null && u.gameObject.activeInHierarchy &&
                u.hasReachedExpeditionDestination &&
                u.marchDestinationZoneName == TargetedSettlementZoneName)
            {
                Object.Destroy(u.gameObject);
            }
        }

        TargetedSettlementZoneName = "";
        Debug.Log("[BattleData] 💀 XÂM CHIẾM THUA! Mất toàn bộ lực lượng lính đã cử đi xâm chiếm. Lính tại căn cứ nhà giữ nguyên.");
    }

    /// <summary>
    /// Phòng thủ Thua: Mất toàn bộ lính thuộc về Vùng đất bị tấn công và MẤT 1 NỬA (50%) TÀI NGUYÊN!
    /// </summary>
    private static void ApplyDefenseDefeatResult()
    {
        SettlementZone targetZone = null;
        if (!string.IsNullOrEmpty(TargetedSettlementZoneName))
        {
            if (SettlementManager.Ins != null) targetZone = SettlementManager.Ins.GetZoneByName(TargetedSettlementZoneName);
        }

        if (targetZone != null)
        {
            foreach (UnitController unit in Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None))
            {
                if (unit != null && unit.gameObject.activeInHierarchy && unit.IsStationedInZone(targetZone.settlementName))
                {
                    Object.Destroy(unit.gameObject);
                }
            }

            SpawnSoldier[] zoneSpawners = targetZone.GetComponentsInChildren<SpawnSoldier>(true);
            foreach (var spawner in zoneSpawners)
            {
                if (spawner != null) spawner.DestroyAllSoldiers();
            }
            if (TroopTrainingManager.Ins != null)
            {
                TroopTrainingManager.Ins.ClearZoneTrainingSlots(targetZone.settlementName);
            }
        }

        if (EnemyInvasionManager.Ins != null)
        {
            EnemyInvasionManager.Ins.TriggerDefenseDefeat(targetZone);
        }
        else
        {
            JsonDataManager jdm = JsonDataManager.Ins != null ? JsonDataManager.Ins : Object.FindFirstObjectByType<JsonDataManager>();
            if (jdm != null)
            {
                jdm.HalveAllResources();
            }
        }

        TargetedSettlementZoneName = "";
        Debug.Log("[BattleData] 💀 PHÒNG THỦ THUA! Đã mất lính của Vùng đất bị tấn công và bị địch cướp 50% tài nguyên!");
    }

    public static bool IsBarracksOrTower(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.BarracksMelee:
            case BuildingType.BarracksArcher:
            case BuildingType.BarracksSpear:
            case BuildingType.ArcherTower:
            case BuildingType.WatchTower:
            case BuildingType.Cannon:
                return true;
            default:
                return false;
        }
    }
}

internal sealed class BattleReturnRestoreRunner : MonoBehaviour
{
    private static BattleReturnRestoreRunner instance;
    private bool restoreQueued;

    public static void RestoreWhenSceneReady()
    {
        if (instance == null)
        {
            GameObject runnerObject = new GameObject(nameof(BattleReturnRestoreRunner));
            DontDestroyOnLoad(runnerObject);
            instance = runnerObject.AddComponent<BattleReturnRestoreRunner>();
        }

        if (instance.restoreQueued) return;

        instance.restoreQueued = true;
        instance.StartCoroutine(instance.RestoreAfterSceneInitialization());
    }

    private System.Collections.IEnumerator RestoreAfterSceneInitialization()
    {
        yield return null;

        BuildingSystem buildingSystem = Object.FindFirstObjectByType<BuildingSystem>();
        if (buildingSystem != null)
        {
            buildingSystem.LoadBuildingsFromSlot(1);
        }
        else
        {
            Debug.LogWarning("[BattleData] Không tìm thấy BuildingSystem để khôi phục công trình sau Battle.");
        }

        BattleData.RestoreWaveAndMarchProgress();
        EnemyInvasionManager.RestorePendingRaidAfterBattleTransition();

        if (BattleData.HasResult)
        {
            BattleData.ApplyBattleResultToScene();
        }

        // Cờ này chỉ cần tồn tại trong lúc bản đồ mới đang khởi tạo để các
        // hệ thống tải dữ liệu nhận biết đây là lần quay về từ SceneBattle.
        // Sau khi đã áp dụng kết quả, phải xóa nó để lần tải scene sau không bị
        // hiểu nhầm là vẫn đang quay về từ trận cũ.
        BattleData.LastBattleWasVictory = false;

        restoreQueued = false;
    }
}
