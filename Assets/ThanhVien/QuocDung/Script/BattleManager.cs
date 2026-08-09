using UnityEngine;
using System.Collections.Generic;

public class BattleManager : MonoBehaviour
{
    [Header("Spawn Locations")]
    [Tooltip("Vị trí sinh phe Người chơi (BÊN TRÁI)")]
    [SerializeField] private Transform leftSpawnPoint;
    [Tooltip("Vị trí sinh phe Enemy (BÊN PHẢI)")]
    [SerializeField] private Transform rightSpawnPoint;

    [Header("Distance & Grid Spacing Settings")]
    [SerializeField] private float buildingSpacing = 4.0f;
    [SerializeField] private float unitSpacing = 2.0f;
    [SerializeField] private int unitsPerRow = 4;

    [Header("Enemy Prefab Settings")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("Player Soldier Prefabs")]
    [SerializeField] private GameObject soldierPrefab;

    [Header("Player Building Prefabs")]
    [SerializeField] private GameObject barracksPrefab;
    [SerializeField] private GameObject archerTowerPrefab;
    [SerializeField] private GameObject watchTowerPrefab;
    [SerializeField] private GameObject cannonPrefab;

    [System.Serializable]
    public struct CustomBuildingPrefab
    {
        public BuildingType buildingType;
        public GameObject prefab;
    }

    [Header("Custom Building Mapping (Optional)")]
    [SerializeField] private List<CustomBuildingPrefab> customBuildingPrefabs = new List<CustomBuildingPrefab>();

    [Header("Standalone Test Mode (Kích hoạt khi mở trực tiếp BattleScene trong Editor)")]
    [SerializeField] private bool enableTestFallback = true;
    [SerializeField] private int testEnemyWaveCount = 1;
    [SerializeField] private int testBarracksCount = 1;
    [SerializeField] private int testBarracksLevel = 1;
    [SerializeField] private bool testSpawnArcherTower = true;

    [Header("Camera Settings")]
    [Tooltip("Camera chính dùng cho trận đấu (nếu chưa gán sẽ tự lấy Camera.main)")]
    [SerializeField] private Camera battleCamera;
    [Tooltip("Ô đế / Transform để gắn Camera tại vị trí giao tranh")]
    [SerializeField] private Transform battleCameraPoint;
    [Tooltip("Tự động di chuyển Camera đến vị trí giao tranh khi bắt đầu trận")]
    [SerializeField] private bool autoPositionCamera = true;
    [Tooltip("Độ lệch vị trí Camera so với trung tâm điểm giao tranh (nếu không dùng battleCameraPoint)")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 8f, -12f);
    [Tooltip("Góc xoay mặc định của Camera (nếu không dùng battleCameraPoint)")]
    [SerializeField] private Vector3 cameraRotation = new Vector3(30f, 0f, 0f);

    [Header("Battle Result & UI Settings")]
    [SerializeField] private float battleEndDelay = 1.0f;
    [SerializeField] private BattleResultUI battleResultUI;
    [SerializeField] private int rewardCrestPerWave = 5;
    [SerializeField] private int rewardWoodPerWave = 50;
    [SerializeField] private int rewardGoldPerWave = 10;

    private bool isBattleOver = false;
    private int initialPlayerSoldierCount = 0;

    private List<GameObject> spawnedPlayerObjects = new List<GameObject>();
    private List<GameObject> spawnedEnemyObjects = new List<GameObject>();

    public static BattleManager Ins { get; private set; }

    private void Awake()
    {
        Ins = this;
        Time.timeScale = 1f;
    }

    private void Start()
    {
        // 1. Kiểm tra vị trí Spawn mặc định nếu chưa gán trong Inspector
        EnsureSpawnPoints();

        // 2. Kiểm tra dữ liệu truyền từ Scene chính (qua BattleData)
        if (!BattleData.HasData && enableTestFallback)
        {
            SetupFallbackTestData();
        }

        // 3. Tiến hành Spawn theo yêu cầu:
        //    - Phe Người Chơi (Lính & Công trình) bên TRÁI
        //    - Phe Enemy (theo số lượng Wave) bên PHẢI
        SpawnPlayerSide();
        SpawnEnemySide();

        // Đếm tổng số lính ban đầu của phe người chơi
        initialPlayerSoldierCount = CountLivingPlayerSoldiers();

        // 4. Thiết lập vị trí Camera tại giao tranh
        SetupBattleCamera();

        // 5. Cho lính và Enemy lập tức bay vào đánh nhau
        StartCoroutine(TriggerImmediateCombatRoutine());

        // 6. Theo dõi kết quả giao tranh và hiển thị Bảng Kết Quả
        StartCoroutine(MonitorBattleRoutine());

        Debug.Log($"[BattleManager] 🔥 Trận đấu khởi tạo thành công! " +
                  $"Sinh {spawnedPlayerObjects.Count} vật thể Người Chơi (BÊN TRÁI, {initialPlayerSoldierCount} lính) và {spawnedEnemyObjects.Count} Enemy (BÊN PHẢI).");
    }

    private int CountLivingPlayerSoldiers()
    {
        int count = 0;
        foreach (var playerObj in spawnedPlayerObjects)
        {
            if (playerObj != null && playerObj.activeInHierarchy)
            {
                UnitController unit = playerObj.GetComponent<UnitController>();
                if (unit == null) unit = playerObj.GetComponentInChildren<UnitController>();

                if (unit != null)
                {
                    HPSoldier hp = unit.GetComponent<HPSoldier>();
                    if (hp == null) hp = unit.GetComponentInChildren<HPSoldier>();

                    if (hp != null)
                    {
                        if (!hp.IsDead && hp.CurrentHealth > 0f) count++;
                    }
                    else
                    {
                        count++;
                    }
                }
            }
        }
        return count;
    }

    /// <summary>
    /// Coroutine liên tục kiểm tra kết quả trận đấu giữa Phe Lính và Phe Enemy
    /// </summary>
    private System.Collections.IEnumerator MonitorBattleRoutine()
    {
        yield return new WaitForSeconds(1f);

        while (!isBattleOver)
        {
            yield return new WaitForSeconds(0.5f);

            // 1. Đếm số Enemy còn sống trong Battle Scene
            int livingEnemies = 0;
            foreach (var enemyObj in spawnedEnemyObjects)
            {
                if (enemyObj != null && enemyObj.activeInHierarchy)
                {
                    EnemyHealth hp = enemyObj.GetComponent<EnemyHealth>();
                    if (hp == null) hp = enemyObj.GetComponentInChildren<EnemyHealth>();

                    if (hp != null)
                    {
                        if (hp.CurrentHealth > 0f) livingEnemies++;
                    }
                    else
                    {
                        livingEnemies++;
                    }
                }
            }

            // 2. Đếm số Lính (UnitController) còn sống trong Battle Scene
            int livingSoldiers = CountLivingPlayerSoldiers();

            // 3. Đánh giá kết quả giao tranh:
            if (livingEnemies == 0)
            {
                // Phe Lính THẮNG!
                isBattleOver = true;
                BattleData.HasResult = true;
                BattleData.IsPlayerVictory = true;
                BattleData.SurvivingSoldiersCount = livingSoldiers;

                int unitsLost = Mathf.Max(0, initialPlayerSoldierCount - livingSoldiers);
                Debug.Log($"[BattleManager] 🏆 PHE LÍNH THẮNG TRẬN! Số lính sống sót = {livingSoldiers}, Lính hy sinh = {unitsLost}.");

                yield return new WaitForSeconds(battleEndDelay);

                ShowBattleResultPanel(isVictory: true, unitsLost: unitsLost);
                yield break;
            }
            else if (livingSoldiers == 0)
            {
                // Phe Lính THUA!
                isBattleOver = true;
                BattleData.HasResult = true;
                BattleData.IsPlayerVictory = false;
                BattleData.SurvivingSoldiersCount = 0;

                int unitsLost = Mathf.Max(initialPlayerSoldierCount, 1);
                Debug.Log($"[BattleManager] 💀 PHE LÍNH THUA TRẬN! Toàn bộ lính đã ngã xuống ({unitsLost} lính hy sinh).");

                yield return new WaitForSeconds(battleEndDelay);

                ShowBattleResultPanel(isVictory: false, unitsLost: unitsLost);
                yield break;
            }
        }
    }

    private void EnsureBattleResultUI()
    {
        if (battleResultUI == null)
        {
            battleResultUI = Object.FindFirstObjectByType<BattleResultUI>();
        }

        if (battleResultUI == null)
        {
            GameObject uiObj = new GameObject("BattleResultUI_System");
            battleResultUI = uiObj.AddComponent<BattleResultUI>();
        }
    }

    private void ShowBattleResultPanel(bool isVictory, int unitsLost)
    {
        EnsureBattleResultUI();

        List<BattleRewardData> rewards = new List<BattleRewardData>();

        if (isVictory)
        {
            int waveMultiplier = Mathf.Max(1, BattleData.EnemyWaveCount);
            int crestAmount = rewardCrestPerWave * waveMultiplier;
            int woodAmount = rewardWoodPerWave * waveMultiplier;
            int goldAmount = rewardGoldPerWave * waveMultiplier;

            Sprite crestIcon = battleResultUI != null ? battleResultUI.RewardCrestIcon : null;
            Sprite woodIcon = battleResultUI != null ? battleResultUI.RewardWoodIcon : null;
            Sprite goldIcon = battleResultUI != null ? battleResultUI.RewardGoldIcon : null;

            rewards.Add(new BattleRewardData { rewardName = "Crest", amount = crestAmount, icon = crestIcon });
            rewards.Add(new BattleRewardData { rewardName = "Wood", amount = woodAmount, icon = woodIcon });

            if (goldAmount > 0)
            {
                rewards.Add(new BattleRewardData { rewardName = "Gold", amount = goldAmount, icon = goldIcon });
            }

            // Cộng tài nguyên vào JsonDataManager
            if (JsonDataManager.Ins != null)
            {
                if (woodAmount > 0) JsonDataManager.Ins.AddWood(woodAmount);
                if (goldAmount > 0) JsonDataManager.Ins.AddGold(goldAmount);
            }
        }

        battleResultUI.ShowResult(isVictory, unitsLost, rewards, OnReturnToMainSceneClicked);
    }

    private void OnReturnToMainSceneClicked()
    {
        Time.timeScale = 1f;
        string returnScene = string.IsNullOrEmpty(BattleData.MainSceneName) ? "MainScene" : BattleData.MainSceneName;
        Debug.Log($"[BattleManager] 🚪 Returning to scene: {returnScene}");
        UnityEngine.SceneManagement.SceneManager.LoadScene(returnScene);
    }

    /// <summary>
    /// Kích hoạt cho cả Lính và Enemy lập tức xông vào đánh nhau khi mở Battle Scene
    /// </summary>
    private System.Collections.IEnumerator TriggerImmediateCombatRoutine()
    {
        yield return new WaitForEndOfFrame();

        Vector3 enemyTargetPos = (rightSpawnPoint != null) ? rightSpawnPoint.position : (transform.position + Vector3.right * 15f);

        // 1. Kích hoạt Lính người chơi lao vào đánh Enemy ở bên Phải
        UnitController[] playerUnits = Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        foreach (var unit in playerUnits)
        {
            if (unit != null && unit.gameObject.activeInHierarchy)
            {
                unit.EnableCombat(enemyTargetPos);
            }
        }

        // 2. Kích hoạt Enemy lao vào đánh Lính người chơi ở bên Trái
        EnemyAI[] enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                enemy.EnableCombat();
            }
        }

        Debug.Log("[BattleManager] ⚔️ Cả lính và Enemy đã lập tức bay vào đánh nhau!");
    }

    /// <summary>
    /// Định vị Camera tại vị trí ô đế giao tranh (battleCameraPoint) hoặc điểm trung tâm trận đấu
    /// </summary>
    private void SetupBattleCamera()
    {
        if (battleCamera == null)
        {
            battleCamera = Camera.main;
        }

        if (battleCamera == null)
        {
            battleCamera = Object.FindFirstObjectByType<Camera>();
        }

        if (battleCamera == null || !autoPositionCamera) return;

        if (battleCameraPoint != null)
        {
            battleCamera.transform.position = battleCameraPoint.position;
            battleCamera.transform.rotation = battleCameraPoint.rotation;
            Debug.Log($"[BattleManager] Đã gắn Camera vào ô đế battleCameraPoint: {battleCameraPoint.position}");
        }
        else
        {
            // Tính vị trí trung tâm giữa phe Người chơi (Trái) và Enemy (Phải)
            Vector3 centerPos = (leftSpawnPoint.position + rightSpawnPoint.position) * 0.5f;
            battleCamera.transform.position = centerPos + cameraOffset;
            battleCamera.transform.rotation = Quaternion.Euler(cameraRotation);
            Debug.Log($"[BattleManager] Đã tự động di chuyển Camera đến tâm điểm giao tranh: {centerPos}");
        }
    }

    /// <summary>
    /// Đảm bảo tự tạo Spawn Point bên TRÁI và BÊN PHẢI nếu chưa gán trong Inspector
    /// </summary>
    private void EnsureSpawnPoints()
    {
        if (leftSpawnPoint == null)
        {
            GameObject leftObj = GameObject.Find("LeftSpawnPoint");
            if (leftObj != null)
            {
                leftSpawnPoint = leftObj.transform;
            }
            else
            {
                leftObj = new GameObject("LeftSpawnPoint_Player");
                leftObj.transform.position = transform.position + Vector3.left * 15f;
                leftSpawnPoint = leftObj.transform;
            }
        }

        if (rightSpawnPoint == null)
        {
            GameObject rightObj = GameObject.Find("RightSpawnPoint");
            if (rightObj != null)
            {
                rightSpawnPoint = rightObj.transform;
            }
            else
            {
                rightObj = new GameObject("RightSpawnPoint_Enemy");
                rightObj.transform.position = transform.position + Vector3.right * 15f;
                rightSpawnPoint = rightObj.transform;
            }
        }
    }

    /// <summary>
    /// Cài đặt dữ liệu giả lập cho chế độ Test độc lập
    /// </summary>
    private void SetupFallbackTestData()
    {
        BattleData.EnemyWaveCount = Mathf.Max(1, testEnemyWaveCount);
        BattleData.PlayerBuildings.Clear();

        // Tạo Doanh trại Test
        for (int i = 0; i < testBarracksCount; i++)
        {
            int lvl = Mathf.Clamp(testBarracksLevel, 1, 3);
            int soldiers = (lvl == 1) ? 4 : (lvl == 2 ? 6 : 8);

            BattleData.PlayerBuildings.Add(new BattleData.BuildingInfo
            {
                buildingType = BuildingType.BarracksMelee,
                level = lvl,
                soldierCount = soldiers,
                originalPosition = Vector3.zero
            });
        }

        // Tạo Tháp cung Test
        if (testSpawnArcherTower)
        {
            BattleData.PlayerBuildings.Add(new BattleData.BuildingInfo
            {
                buildingType = BuildingType.ArcherTower,
                level = 1,
                soldierCount = 0,
                originalPosition = Vector3.zero
            });
        }

        BattleData.HasData = true;
        Debug.Log("[BattleManager] Đã tự động tạo dữ liệu Test cho BattleScene.");
    }

    private bool IsCombatBuilding(BuildingType type)
    {
        return type == BuildingType.BarracksMelee ||
               type == BuildingType.BarracksArcher ||
               type == BuildingType.BarracksSpear ||
               type == BuildingType.ArcherTower ||
               type == BuildingType.WatchTower ||
               type == BuildingType.Cannon;
    }

    private void SetupSpawnedBuildingState(GameObject spawnedBuilding, int level)
    {
        if (spawnedBuilding == null) return;

        // Tắt HOÀN TOÀN tất cả SpawnSoldier trên công trình ở SceneBattle để tránh tự động spawn lính thừa!
        SpawnSoldier[] spawners = spawnedBuilding.GetComponentsInChildren<SpawnSoldier>(true);
        foreach (var spawner in spawners)
        {
            if (spawner != null)
            {
                spawner.enabled = false;
            }
        }

        // Cập nhật Cấp độ và ĐẶT TRẠNG THÁI ĐÃ XÂY XONG cho UpgradeableBuilding
        UpgradeableBuilding ub = spawnedBuilding.GetComponent<UpgradeableBuilding>();
        if (ub == null) ub = spawnedBuilding.GetComponentInChildren<UpgradeableBuilding>();
        if (ub != null)
        {
            int targetLevel = Mathf.Max(0, level - 1);
            ub.LoadBuildingData(targetLevel, isRuinedState: false, isInitialBuildNeededState: false);
        }

        // Cập nhật trạng thái cho BuildingCtrl nếu có
        BuildingCtrl buildingCtrl = spawnedBuilding.GetComponent<BuildingCtrl>();
        if (buildingCtrl == null) buildingCtrl = spawnedBuilding.GetComponentInChildren<BuildingCtrl>();
    }

    /// <summary>
    /// Spawn toàn bộ Công trình và Lính của Người Chơi ở BÊN TRÁI
    /// </summary>
    private void SpawnPlayerSide()
    {
        if (leftSpawnPoint == null) return;

        Vector3 originLeft = leftSpawnPoint.position;

        // Phân loại công trình: Tháp Canh (Đứng sau cùng) và Tháp Tấn Công / Doanh Trại (Đứng phía trước Tháp Canh)
        List<BattleData.BuildingInfo> watchTowers = new List<BattleData.BuildingInfo>();
        List<BattleData.BuildingInfo> attackBuildings = new List<BattleData.BuildingInfo>();

        foreach (var b in BattleData.PlayerBuildings)
        {
            if (b.buildingType == BuildingType.WatchTower)
            {
                watchTowers.Add(b);
            }
            else if (IsCombatBuilding(b.buildingType))
            {
                attackBuildings.Add(b);
            }
        }

        Vector3 playerAreaCenter = originLeft + Vector3.right * 6f;

        // Tháp Canh (WatchTower) đứng ở HÀNG SAU CÙNG
        Vector3 watchTowerOrigin = playerAreaCenter + Vector3.left * 3f;

        // Các công trình tấn công (Tháp Cung, Pháo, Doanh Trại) đứng PHÍA TRƯỚC Tháp Canh
        Vector3 attackBuildingOrigin = watchTowers.Count > 0 ? (playerAreaCenter + Vector3.right * 2f) : playerAreaCenter;

        // Lính đứng PHÍA TRƯỚC toàn bộ công trình
        Vector3 soldierFrontOrigin = attackBuildingOrigin + Vector3.right * 5f;

        float actualBuildingSpacing = Mathf.Max(6.0f, buildingSpacing);

        // 1. Spawn Tháp Canh (Hàng sau cùng)
        for (int i = 0; i < watchTowers.Count; i++)
        {
            var info = watchTowers[i];
            GameObject prefab = GetBuildingPrefab(info.buildingType);
            if (prefab != null)
            {
                float offsetZ = (i - (watchTowers.Count - 1) * 0.5f) * actualBuildingSpacing;
                Vector3 pos = watchTowerOrigin + Vector3.forward * offsetZ;
                Quaternion rot = Quaternion.Euler(0, 90, 0);

                GameObject spawned = Instantiate(prefab, pos, rot);
                spawned.name = $"Player_{info.buildingType}_Lv{info.level}";
                spawnedPlayerObjects.Add(spawned);

                SetupSpawnedBuildingState(spawned, info.level);
            }
        }

        // 2. Spawn các Công trình Tấn Công (ĐỨNG PHÍA TRƯỚC THÁP CANH)
        int buildingsPerRow = 3;
        for (int i = 0; i < attackBuildings.Count; i++)
        {
            var info = attackBuildings[i];
            GameObject prefab = GetBuildingPrefab(info.buildingType);
            if (prefab != null)
            {
                int bRow = i / buildingsPerRow;
                int bCol = i % buildingsPerRow;
                int countInRow = Mathf.Min(buildingsPerRow, attackBuildings.Count - bRow * buildingsPerRow);

                float offsetZ = (bCol - (countInRow - 1) * 0.5f) * actualBuildingSpacing;
                Vector3 pos = attackBuildingOrigin + Vector3.right * (bRow * 4f) + Vector3.forward * offsetZ;
                Quaternion rot = Quaternion.Euler(0, 90, 0);

                GameObject spawned = Instantiate(prefab, pos, rot);
                spawned.name = $"Player_{info.buildingType}_Lv{info.level}";
                spawnedPlayerObjects.Add(spawned);

                SetupSpawnedBuildingState(spawned, info.level);
            }
        }

        // 3. Spawn chính xác số Lính của Người Chơi (Bằng đúng số lính thực tế từ MainScene)
        int targetTotalSoldiers = BattleData.TotalSoldiersInBase;
        if (targetTotalSoldiers <= 0)
        {
            targetTotalSoldiers = 0;
            foreach (var bInfo in BattleData.PlayerBuildings)
            {
                targetTotalSoldiers += bInfo.soldierCount;
            }
            if (targetTotalSoldiers <= 0) targetTotalSoldiers = 3; // Fallback mặc định duy nhất nếu trống dữ liệu
        }

        for (int i = 0; i < targetTotalSoldiers; i++)
        {
            if (soldierPrefab != null)
            {
                int row = i / unitsPerRow;
                int col = i % unitsPerRow;

                float sOffsetZ = (col - (unitsPerRow - 1) * 0.5f) * unitSpacing;
                Vector3 soldierPos = soldierFrontOrigin + Vector3.left * (row * unitSpacing) + Vector3.forward * sOffsetZ;
                Quaternion soldierRot = Quaternion.Euler(0, 90, 0);

                GameObject spawnedSoldier = Instantiate(soldierPrefab, soldierPos, soldierRot);
                spawnedSoldier.name = $"Player_Soldier_{i + 1}";
                spawnedPlayerObjects.Add(spawnedSoldier);
            }
        }

        Debug.Log($"[BattleManager] 🔥 Đã sinh chính xác {targetTotalSoldiers} lính cho SceneBattle (Khớp 100% với MainScene).");
    }

    /// <summary>
    /// Spawn toàn bộ Enemy thuộc Wave ở BÊN PHẢI
    /// </summary>
    private void SpawnEnemySide()
    {
        if (rightSpawnPoint == null || enemyPrefab == null)
        {
            Debug.LogWarning("[BattleManager] Chưa cài đặt rightSpawnPoint hoặc enemyPrefab!");
            return;
        }

        int count = Mathf.Max(1, BattleData.EnemyWaveCount);
        Vector3 originRight = rightSpawnPoint.position;

        for (int i = 0; i < count; i++)
        {
            int row = i / unitsPerRow;
            int col = i % unitsPerRow;

            Vector3 enemyPos = originRight + Vector3.right * (row * unitSpacing) + Vector3.forward * (col * unitSpacing - 1.5f);
            Quaternion enemyRot = Quaternion.Euler(0, -90, 0); // Quay mặt về phía bên Trái (Player)

            GameObject spawnedEnemy = Instantiate(enemyPrefab, enemyPos, enemyRot);
            spawnedEnemy.name = $"Enemy_WaveUnit_{i + 1}";
            spawnedEnemyObjects.Add(spawnedEnemy);

            // Kích hoạt AI giao tranh cho Enemy nếu có
            EnemyAI enemyAI = spawnedEnemy.GetComponent<EnemyAI>();
            if (enemyAI != null)
            {
                enemyAI.EnableCombat();
            }
        }
    }

    private GameObject FindBuildingPrefabByType(BuildingType type)
    {
        var allBuildings = Resources.FindObjectsOfTypeAll<UpgradeableBuilding>();
        foreach (var b in allBuildings)
        {
            if (b != null && b.buildingType == type)
            {
                // Ưu tiên các Prefab Asset hoặc thực thể mẫu chưa nằm trực tiếp trong Scene giao tranh
                if (!b.gameObject.scene.IsValid() || b.gameObject.name.ToLower().Contains("prefab"))
                {
                    return b.gameObject;
                }
            }
        }

        // Fallback: Thử tìm bất kỳ mẫu công trình cùng loại nào đang có sẵn
        foreach (var b in allBuildings)
        {
            if (b != null && b.buildingType == type)
            {
                return b.gameObject;
            }
        }

        return null;
    }

    /// <summary>
    /// Tìm Prefab công trình chuẩn xác dựa theo BuildingType
    /// </summary>
    private GameObject GetBuildingPrefab(BuildingType type)
    {
        // 1. Kiểm tra mảng Custom mapping trong Inspector
        foreach (var custom in customBuildingPrefabs)
        {
            if (custom.buildingType == type && custom.prefab != null)
            {
                return custom.prefab;
            }
        }

        // 2. Kiểm tra các field được gán thủ công trong Inspector
        switch (type)
        {
            case BuildingType.ArcherTower:
                if (archerTowerPrefab != null) return archerTowerPrefab;
                break;

            case BuildingType.WatchTower:
                if (watchTowerPrefab != null) return watchTowerPrefab;
                break;

            case BuildingType.Cannon:
                if (cannonPrefab != null) return cannonPrefab;
                break;

            case BuildingType.BarracksMelee:
            case BuildingType.BarracksArcher:
            case BuildingType.BarracksSpear:
                if (barracksPrefab != null) return barracksPrefab;
                break;
        }

        // 3. Tự động truy tìm Prefab chuẩn xác của loại nhà đó trong Project/Memory
        GameObject foundDynamic = FindBuildingPrefabByType(type);
        if (foundDynamic != null)
        {
            return foundDynamic;
        }

        // 4. Dự phòng cuối cùng nếu hoàn toàn không tìm thấy
        if (archerTowerPrefab != null) return archerTowerPrefab;
        if (watchTowerPrefab != null) return watchTowerPrefab;
        if (cannonPrefab != null) return cannonPrefab;
        return barracksPrefab;
    }
}
