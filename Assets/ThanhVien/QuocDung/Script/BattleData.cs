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
    }

    public static bool HasData = false;
    public static int EnemyWaveCount = 1;
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

    // Kết quả trận đấu
    public static bool HasResult = false;
    public static bool IsPlayerVictory = false;
    public static bool LastBattleWasVictory = false;
    public static int SurvivingSoldiersCount = 0;

    /// <summary>
    /// Bật cờ này trước khi Reload Scene để ngăn BattleData tự động Load lại file Save.
    /// Dùng cho UILinh.ResetGame() để reset về trạng thái gốc của Scene.
    /// </summary>
    public static bool SkipAutoLoadOnNextSceneLoad = false;

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
                // 🔥 Tải lại toàn bộ công trình từ file Save JSON khi quay lại Scene chính
                BuildingSystem buildingSys = BuildingSystem.Ins != null ? BuildingSystem.Ins : Object.FindFirstObjectByType<BuildingSystem>();
                if (buildingSys != null)
                {
                    buildingSys.LoadBuildingsFromSlot(1);
                }
            }

            // 🔥 PHỤC HỒI TIẾN TRÌNH WAVE & TIẾN TRÌNH DI CHUYỂN CỦA QUÁI VÀ LÍNH
            RestoreWaveAndMarchProgress();

            if (HasResult)
            {
                ApplyBattleResultToScene();
            }
        }
    }

    /// <summary>
    /// Ghi nhận trạng thái hiện tại của Scene chính trước khi chuyển sang Battle Scene.
    /// </summary>
    /// <param name="waveEnemyCount">Số lượng Enemy thuộc Wave chuẩn bị giao tranh</param>
    public static void RecordCurrentSceneState(int waveEnemyCount)
    {
        Scene currentScene = SceneManager.GetActiveScene();
        if (currentScene.name != "SceneBattle")
        {
            MainSceneName = currentScene.name;
        }

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
        UnitController[] activeUnits = Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        int realActiveSoldierCount = 0;
        foreach (var u in activeUnits)
        {
            if (u != null && u.gameObject.activeInHierarchy)
            {
                if (u.isExpeditionMarching)
                {
                    SavedSoldierMarches.Add(new SoldierMarchInfo
                    {
                        position = u.transform.position,
                        marchStartPosition = u.marchStartPosition,
                        marchDestinationPosition = u.marchDestinationPosition,
                        marchStartWave = u.marchStartWave,
                        marchWavesToReach = u.marchWavesToReach,
                        marchTargetWave = u.marchTargetWave
                    });
                }
                else
                {
                    realActiveSoldierCount++;
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

            BuildingInfo info = new BuildingInfo
            {
                buildingType = building.buildingType,
                level = building.CurrentLevel + 1,
                originalPosition = building.transform.position,
                soldierCount = 0
            };

            // Nếu là Doanh Trại, lấy số lính ĐANG Ở CĂN CỨ (không đi chinh phạt)
            SpawnSoldier spawner = building.GetComponent<SpawnSoldier>();
            if (spawner == null) spawner = building.GetComponentInChildren<SpawnSoldier>();

            if (spawner != null)
            {
                int atBaseCount = 0;
                var soldiersInBuilding = spawner.GetComponentsInChildren<UnitController>();
                foreach (var s in soldiersInBuilding)
                {
                    if (s != null && s.gameObject.activeInHierarchy && !s.isExpeditionMarching)
                    {
                        atBaseCount++;
                    }
                }
                info.soldierCount = atBaseCount;
            }

            PlayerBuildings.Add(info);
        }

        TotalSoldiersInBase = realActiveSoldierCount;
        HasData = true;
        Debug.Log($"[BattleData] Đã lưu dữ liệu Trận Đấu: MainScene = {MainSceneName}, CurrentWave = {SavedCurrentWave}, Enemy Wave Count = {EnemyWaveCount}, Quái hành quân = {SavedEnemyMarches.Count}, Lính xuất trận = {SavedSoldierMarches.Count}");
    }

    private static void RestoreWaveAndMarchProgress()
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

        // 3. Phục hồi đoàn Lính đang hành quân xuất trận
        if (SavedSoldierMarches.Count > 0)
        {
            UnitController[] sceneUnits = Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None);
            int marchIdx = 0;
            List<UnitController> marchingList = new List<UnitController>();

            foreach (var u in sceneUnits)
            {
                if (u != null && marchIdx < SavedSoldierMarches.Count)
                {
                    var info = SavedSoldierMarches[marchIdx];
                    u.transform.position = info.position;
                    u.marchStartPosition = info.marchStartPosition;
                    u.marchDestinationPosition = info.marchDestinationPosition;
                    u.marchStartWave = info.marchStartWave;
                    u.marchWavesToReach = info.marchWavesToReach;
                    u.marchTargetWave = info.marchTargetWave;
                    u.isExpeditionMarching = true;
                    u.currentState = UnitState.Moving;

                    marchingList.Add(u);
                    marchIdx++;
                }
            }

            if (marchingList.Count > 0)
            {
                EnemySpawn enemySpawn = Object.FindFirstObjectByType<EnemySpawn>();
                Transform targetTr = enemySpawn != null ? enemySpawn.transform : null;

                if (targetTr != null)
                {
                    GameObject runner = new GameObject("ExpeditionBattleTriggerRunner");
                    ExpeditionBattleTrigger trigger = runner.AddComponent<ExpeditionBattleTrigger>();
                    trigger.StartMonitoring(marchingList, targetTr, "SceneBattle");
                }
            }
        }
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
        SavedCurrentWave = 0;
        SavedEnemyMarches.Clear();
        SavedSoldierMarches.Clear();
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
            }
            else
            {
                ApplyDefenseDefeatResult();
            }
        }

        HasResult = false;
        IsAttackingExpedition = false;

        // Lưu lại trạng thái công trình sau trận đấu vào Save Slot 1
        BuildingSystem buildingSys = BuildingSystem.Ins != null ? BuildingSystem.Ins : Object.FindFirstObjectByType<BuildingSystem>();
        if (buildingSys != null)
        {
            buildingSys.SaveBuildingsToSlot(1);
        }
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

        if (!conqueredZone.HasValidEnemyOutpostInstance())
        {
            Debug.LogError($"[BattleData] Vùng '{conqueredZone.settlementName}' không có căn cứ địch hợp lệ, hủy xử lý để bảo vệ Nhà Chính.");
            TargetedSettlementZoneName = "";
            return;
        }

        conqueredZone.OnEnemyOutpostDestroyed();
        conqueredZone.SaveSettlementState();
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
            if (u != null && u.gameObject.activeInHierarchy && u.isExpeditionMarching)
            {
                Object.Destroy(u.gameObject);
            }
        }

        // Xóa dữ liệu lính của Spawner và ô slot của vùng xuất trận
        if (!string.IsNullOrEmpty(TargetedSettlementZoneName))
        {
            SettlementZone zone = SettlementManager.Ins != null ? SettlementManager.Ins.GetZoneByName(TargetedSettlementZoneName) : null;
            if (zone != null)
            {
                SpawnSoldier[] spawners = zone.GetComponentsInChildren<SpawnSoldier>(true);
                foreach (var s in spawners)
                {
                    if (s != null) s.DestroyAllSoldiers();
                }
                if (TroopTrainingManager.Ins != null)
                {
                    TroopTrainingManager.Ins.ClearZoneTrainingSlots(zone.settlementName);
                }
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
