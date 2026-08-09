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

    public static bool HasData = false;
    public static int EnemyWaveCount = 1;
    public static List<BuildingInfo> PlayerBuildings = new List<BuildingInfo>();
    public static int TotalSoldiersInBase = 0;
    public static string MainSceneName = "MainScene";

    // Kết quả trận đấu
    public static bool HasResult = false;
    public static bool IsPlayerVictory = false;
    public static bool LastBattleWasVictory = false;
    public static int SurvivingSoldiersCount = 0;

    [System.Serializable]
    public class SavedEnemyData
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 startSpawnPosition;
        public int spawnWave;
        public int targetWave;
        public int wavesToReachTarget;
        public int squadId;
    }

    public static List<SavedEnemyData> SavedRemainingEnemies = new List<SavedEnemyData>();

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
            else if (HasResult)
            {
                // Khi quay lại từ BattleScene có kết quả trận đấu -> ApplyBattleResultToScene sẽ tự động xử lý và lưu game!
                ApplyBattleResultToScene();
            }
            else
            {
                // Chỉ Load từ slot save khi khởi động bình thường
                BuildingSystem buildingSys = BuildingSystem.Ins != null ? BuildingSystem.Ins : Object.FindFirstObjectByType<BuildingSystem>();
                if (buildingSys != null)
                {
                    buildingSys.LoadBuildingsFromSlot(1);
                }

                if (JsonDataManager.Ins != null)
                {
                    JsonDataManager.Ins.LoadGame(1);
                }
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

        // 🔥 Lưu toàn bộ công trình và tài nguyên hiện có ở Main Scene vào file Save JSON trước khi sang Battle Scene
        BuildingSystem buildingSys = BuildingSystem.Ins != null ? BuildingSystem.Ins : Object.FindFirstObjectByType<BuildingSystem>();
        if (buildingSys != null)
        {
            buildingSys.SaveBuildingsToSlot(1);
        }

        // 🔥 Lưu dữ liệu lính & công trình trong UILinh
        UILinh uiLinh = Object.FindFirstObjectByType<UILinh>();
        if (uiLinh != null)
        {
            uiLinh.SaveGame();
        }

        // 🔥 Lưu Ngày/Wave hiện tại vào PlayerPrefs
        if (DayNightManager.HasInstance && DayNightManager.Ins != null)
        {
            PlayerPrefs.SetInt("SavedCurrentWave", DayNightManager.Ins.CurrentWave);
            PlayerPrefs.Save();
        }

        EnemyWaveCount = Mathf.Max(1, waveEnemyCount);
        PlayerBuildings.Clear();
        TotalSoldiersInBase = 0;

        // 1. Đếm chính xác số lính thực tế thuộc phe Người chơi đang có mặt trên map
        UnitController[] activeUnits = Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        int realActiveSoldierCount = 0;
        foreach (var u in activeUnits)
        {
            if (u != null && u.gameObject.activeInHierarchy)
            {
                // Chỉ đếm Lính thật của Người Chơi (Không đếm EnemyAI)
                if (u.GetComponent<EnemyAI>() == null && u.GetComponentInParent<EnemyAI>() == null)
                {
                    realActiveSoldierCount++;
                }
            }
        }

        // 2. Tìm tất cả các công trình UpgradeableBuilding trong scene
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

            // Nếu là Doanh Trại, lấy số lính ĐANG HOẠT ĐỘNG THỰC TẾ của công trình đó
            SpawnSoldier spawner = SpawnSoldier.GetActiveSpawnerForBuilding(building);

            if (spawner != null)
            {
                info.soldierCount = spawner.GetActiveSoldiersCount();
            }

            PlayerBuildings.Add(info);
        }

        // Đảm bảo TotalSoldiersInBase phản ánh đúng số lính thực tế
        TotalSoldiersInBase = realActiveSoldierCount;

        HasData = true;
        Debug.Log($"[BattleData] Đã lưu dữ liệu Trận Đấu: MainScene = {MainSceneName}, Enemy Wave = {EnemyWaveCount}, Tổng số công trình = {PlayerBuildings.Count}, Tổng lính thực tế = {TotalSoldiersInBase}");
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
        SavedRemainingEnemies.Clear();
    }

    /// <summary>
    /// Áp dụng kết quả trận đấu khi quay lại Scene chính
    /// </summary>
    public static void ApplyBattleResultToScene()
    {
        if (!HasResult) return;

        LastBattleWasVictory = IsPlayerVictory;
        Debug.Log($"[BattleData] 🔥 Đang áp dụng kết quả trận đấu: IsPlayerVictory = {IsPlayerVictory}, SurvivingSoldiersCount = {SurvivingSoldiersCount}");

        // 🔥 XÓA SẠCH TẤT CẢ LÍNH CŨ / MỒ CÔI TRÊN MAP TRƯỚC KHI TẠO LÍNH MỚI
        GameObject[] oldSoldiers = GameObject.FindGameObjectsWithTag("Soldier");
        foreach (var s in oldSoldiers)
        {
            if (s != null)
            {
                s.tag = "Untagged";
                s.SetActive(false);
                if (Application.isPlaying) Object.Destroy(s);
                else Object.DestroyImmediate(s);
            }
        }

        UpgradeableBuilding[] buildings = Object.FindObjectsByType<UpgradeableBuilding>(FindObjectsSortMode.None);
        int remainingSurvivingSoldiers = SurvivingSoldiersCount;

        foreach (var b in buildings)
        {
            if (b == null || !b.gameObject.activeInHierarchy) continue;

            if (IsBarracksOnly(b.buildingType))
            {
                SpawnSoldier spawner = SpawnSoldier.GetActiveSpawnerForBuilding(b);
                int maxCapacity = spawner != null ? spawner.GetMaxSoldiersForLevel(b.CurrentLevel) : 3;

                int assignedSurviving = Mathf.Min(remainingSurvivingSoldiers, maxCapacity);
                remainingSurvivingSoldiers = Mathf.Max(0, remainingSurvivingSoldiers - assignedSurviving);

                // QUY TẮC:
                // 1. Nếu có ít nhất 1 lính của Barracks sống sót -> Barracks GIỮ NGUYÊN và duy trì đủ 3 lính!
                // 2. Nếu cả 3 lính của Barracks chết hết -> PHÁ HỦY CÔNG TRÌNH BARRACKS ĐÓ!
                if (assignedSurviving > 0 || (IsPlayerVictory && SurvivingSoldiersCount > 0))
                {
                    if (spawner != null)
                    {
                        if (!spawner.gameObject.activeSelf) spawner.gameObject.SetActive(true);
                        spawner.enabled = true;
                        spawner.LoadAndSpawnSoldiers(maxCapacity, b.CurrentLevel - 1);
                    }
                }
                else
                {
                    Debug.Log($"[BattleData] 💀 Toàn bộ 3 lính của công trình {b.gameObject.name} đã chết -> PHÁ HỦY BARRACKS!");
                    b.TriggerDestructionSequence();

                    if (spawner != null)
                    {
                        spawner.LoadAndSpawnSoldiers(0, b.CurrentLevel - 1);
                    }
                }
            }
        }

        BuildingSystem buildingSys = BuildingSystem.Ins != null ? BuildingSystem.Ins : Object.FindFirstObjectByType<BuildingSystem>();
        if (buildingSys != null)
        {
            buildingSys.SaveBuildingsToSlot(1);
        }

        UILinh uiLinh = Object.FindFirstObjectByType<UILinh>();
        if (uiLinh != null)
        {
            uiLinh.SaveGame();
        }

        HasResult = false;
    }

    public static bool IsBarracksOnly(BuildingType type)
    {
        return type == BuildingType.BarracksMelee ||
               type == BuildingType.BarracksArcher ||
               type == BuildingType.BarracksSpear;
    }

    public static void SaveRemainingEnemiesState(List<EnemyAI> attackedSquad)
    {
        SavedRemainingEnemies.Clear();

        if (attackedSquad == null) attackedSquad = new List<EnemyAI>();

        EnemyAI[] allEnemiesInScene = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        Dictionary<List<EnemyAI>, int> squadToIdMap = new Dictionary<List<EnemyAI>, int>();
        int currentSquadId = 1;

        foreach (var enemy in allEnemiesInScene)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy && !attackedSquad.Contains(enemy))
            {
                int sId = 1;
                if (enemy.squadEnemies != null && enemy.squadEnemies.Count > 0)
                {
                    if (!squadToIdMap.TryGetValue(enemy.squadEnemies, out sId))
                    {
                        sId = currentSquadId++;
                        squadToIdMap[enemy.squadEnemies] = sId;
                    }
                }
                else
                {
                    sId = currentSquadId++;
                }

                SavedEnemyData data = new SavedEnemyData
                {
                    position = enemy.transform.position,
                    rotation = enemy.transform.rotation,
                    startSpawnPosition = enemy.startSpawnPosition,
                    spawnWave = enemy.spawnWave,
                    targetWave = enemy.targetWave,
                    wavesToReachTarget = enemy.wavesToReachTarget,
                    squadId = sId
                };
                SavedRemainingEnemies.Add(data);
            }
        }
        Debug.Log($"[BattleData] 🔥 Đã lưu {SavedRemainingEnemies.Count} enemy chưa tham chiến thuộc {squadToIdMap.Count} đợt quái khác.");
    }

    public static void RestoreRemainingEnemies()
    {
        if (SavedRemainingEnemies == null || SavedRemainingEnemies.Count == 0) return;

        EnemySpawn spawner = EnemySpawn.Ins != null ? EnemySpawn.Ins : Object.FindFirstObjectByType<EnemySpawn>();
        GameObject prefab = spawner != null ? spawner.EnemyPrefab : null;
        Transform attackTarget = spawner != null ? spawner.attackTarget : null;

        if (prefab == null)
        {
            EnemyAI sample = Object.FindFirstObjectByType<EnemyAI>();
            if (sample != null) prefab = sample.gameObject;
        }

        if (prefab == null) return;

        Dictionary<int, List<EnemyAI>> squadMap = new Dictionary<int, List<EnemyAI>>();

        foreach (var data in SavedRemainingEnemies)
        {
            GameObject enemyObj = Object.Instantiate(prefab, data.position, data.rotation);

            EnemyAI ai = enemyObj.GetComponent<EnemyAI>();
            if (ai != null)
            {
                ai.RestoreWaveData(data.spawnWave, data.targetWave, data.wavesToReachTarget, data.startSpawnPosition);
                if (attackTarget != null) ai.villageCenter = attackTarget;

                if (!squadMap.ContainsKey(data.squadId))
                {
                    squadMap[data.squadId] = new List<EnemyAI>();
                }
                squadMap[data.squadId].Add(ai);
            }
        }

        foreach (var kvp in squadMap)
        {
            List<EnemyAI> squad = kvp.Value;
            foreach (var ai in squad)
            {
                ai.squadEnemies = squad;
            }

            if (squad.Count > 0)
            {
                Transform leadEnemy = squad[0].transform;
                EnemySpawnWarningArrow arrow = EnemySpawnWarningArrow.Create(leadEnemy);
                if (arrow != null && spawner != null)
                {
                    arrow.arrowSize = spawner.warningArrowSize;
                    arrow.arrowLengthMultiplier = spawner.warningArrowLengthMultiplier;
                    arrow.arrowExtraLength = spawner.warningArrowExtraLength;
                    arrow.timerTextScale = spawner.warningTimerTextScale;
                    arrow.textHeightOffset = spawner.warningTextHeightOffset;
                    arrow.UpdateVisuals();
                }
            }
        }

        Debug.Log($"[BattleData] 🔥 Đã phục hồi {SavedRemainingEnemies.Count} Enemy thuộc {squadMap.Count} đợt quái còn lại trên Main Scene.");
        SavedRemainingEnemies.Clear();
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
