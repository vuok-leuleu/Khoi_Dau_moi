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

    /// <summary>
    /// Áp dụng kết quả trận đấu khi quay lại Scene chính
    /// </summary>
    public static void ApplyBattleResultToScene()
    {
        if (!HasResult) return;

        LastBattleWasVictory = IsPlayerVictory;
        Debug.Log($"[BattleData] 🔥 Đang áp dụng kết quả trận đấu vào Scene chính ({MainSceneName}): Victory = {IsPlayerVictory}, SurvivingSoldiers = {SurvivingSoldiersCount}");

        if (IsPlayerVictory)
        {
            ApplyVictoryResult(SurvivingSoldiersCount);
        }
        else
        {
            ApplyDefeatResult();
        }

        HasResult = false;

        // 🔥 Lưu lại trạng thái công trình sau trận đấu vào Save Slot 1
        BuildingSystem buildingSys = BuildingSystem.Ins != null ? BuildingSystem.Ins : Object.FindFirstObjectByType<BuildingSystem>();
        if (buildingSys != null)
        {
            buildingSys.SaveBuildingsToSlot(1);
        }
    }

    private static void ApplyVictoryResult(int survivingCount)
    {
        SpawnSoldier[] spawners = Object.FindObjectsByType<SpawnSoldier>(FindObjectsSortMode.None);
        HashSet<GameObject> processedBuildings = new HashSet<GameObject>();
        int remainingToAssign = survivingCount;

        foreach (var spawner in spawners)
        {
            if (spawner == null || !spawner.gameObject.activeInHierarchy) continue;

            UpgradeableBuilding building = spawner.GetComponent<UpgradeableBuilding>();
            if (building == null) building = spawner.GetComponentInParent<UpgradeableBuilding>();
            if (building == null) building = spawner.GetComponentInChildren<UpgradeableBuilding>();

            GameObject bObj = building != null ? building.gameObject : spawner.transform.root.gameObject;
            if (processedBuildings.Contains(bObj)) continue;
            processedBuildings.Add(bObj);

            if (building != null && building.IsRuined)
            {
                spawner.LoadAndSpawnSoldiers(0, building.CurrentLevel);
                continue;
            }

            int level = spawner.CurrentLevel;
            int maxForLevel = spawner.GetMaxSoldiersForLevel(level);

            int assignCount = Mathf.Min(remainingToAssign, maxForLevel);
            spawner.LoadAndSpawnSoldiers(assignCount, level - 1);
            remainingToAssign -= assignCount;
        }

        UnitController[] activeUnits = Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        if (activeUnits.Length > survivingCount)
        {
            int extraToRemove = activeUnits.Length - survivingCount;
            for (int i = activeUnits.Length - 1; i >= 0 && extraToRemove > 0; i--)
            {
                if (activeUnits[i] != null && activeUnits[i].gameObject.activeInHierarchy)
                {
                    if (activeUnits[i].isExpeditionMarching) continue;
                    Object.Destroy(activeUnits[i].gameObject);
                    extraToRemove--;
                }
            }
        }

        // 🔥 CHINH PHỤC VÙNG ĐẤT: TIÊU DIỆT CĂN CỨ ĐỊCH TRÊN SETTLEMENT ZONE KHI GIẢI PHÓNG THÀNH CÔNG
        SettlementZone conqueredZone = null;
        if (!string.IsNullOrEmpty(TargetedSettlementZoneName))
        {
            SettlementZone[] allZones = Object.FindObjectsByType<SettlementZone>(FindObjectsSortMode.None);
            foreach (var z in allZones)
            {
                if (z != null && z.settlementName == TargetedSettlementZoneName)
                {
                    conqueredZone = z;
                    break;
                }
            }
        }

        if (conqueredZone == null)
        {
            SettlementZone[] allZones = Object.FindObjectsByType<SettlementZone>(FindObjectsSortMode.None);
            foreach (var z in allZones)
            {
                if (z != null && z.hasEnemyOutpost)
                {
                    conqueredZone = z;
                    break;
                }
            }
        }

        if (conqueredZone != null)
        {
            conqueredZone.OnEnemyOutpostDestroyed();
            conqueredZone.SaveSettlementState();
            Debug.Log($"[BattleData] 🏆 CHINH PHỤC THÀNH CÔNG! Đã giải phóng vùng đất '{conqueredZone.settlementName}'. Người chơi hiện có thể xây dựng công trình trên ô đất tại đây!");
        }

        TargetedSettlementZoneName = "";
        Debug.Log($"[BattleData] 🏆 Thắng trận! Đã cập nhật {survivingCount} lính còn sống trên Scene chính.");
    }

    private static void ApplyDefeatResult()
    {
        UpgradeableBuilding[] buildings = Object.FindObjectsByType<UpgradeableBuilding>(FindObjectsSortMode.None);

        foreach (var b in buildings)
        {
            if (b == null || !b.gameObject.activeInHierarchy) continue;

            if (IsBarracksOrTower(b.buildingType))
            {
                b.TriggerDestructionSequence();

                SpawnSoldier spawner = b.GetComponent<SpawnSoldier>();
                if (spawner == null) spawner = b.GetComponentInChildren<SpawnSoldier>();
                if (spawner != null)
                {
                    spawner.LoadAndSpawnSoldiers(0, b.CurrentLevel);
                }

                HPTower hpTower = b.GetComponent<HPTower>();
                if (hpTower == null) hpTower = b.GetComponentInChildren<HPTower>();
                if (hpTower != null)
                {
                    hpTower.SetRuinedHealth();
                }
            }
        }

        UnitController[] activeUnits = Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        foreach (var u in activeUnits)
        {
            if (u != null && u.gameObject.activeInHierarchy)
            {
                Object.Destroy(u.gameObject);
            }
        }

        Debug.Log("[BattleData] 💀 Thua trận! Tất cả công trình Barracks & Tower đã bị phá hủy bên Scene chính.");
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
