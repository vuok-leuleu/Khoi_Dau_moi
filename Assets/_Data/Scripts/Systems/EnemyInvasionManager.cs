using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/*
 * EnemyInvasionManager.cs
 * Quản lý Đợt Tấn Công của Địch nhắm vào Vùng Đất ngẫu nhiên đã xây Nhà Chính.
 * Khoảng cách đỗ quân: ~3m trước căn cứ.
 * Thời gian đếm ngược: 3 Wave (3 Ngày). Nếu quá 3 Ngày không phòng thủ -> Phòng thủ thua (Mất lính vùng đó & 50% tài nguyên).
 */

public class EnemyInvasionManager : MonoBehaviour
{
    public static EnemyInvasionManager Ins { get; private set; }

    [System.Serializable]
    private class RaidEnemySnapshot
    {
        public Vector3 position;
        public Vector3 startSpawnPosition;
        public int spawnWave;
        public int targetWave;
        public int wavesToReachTarget;
        public int remainingWavesToTarget;
        public bool wasWaitingAtTarget;
    }

    [System.Serializable]
    private class RaidSnapshot
    {
        public string targetSettlementName;
        public string spawnPointName;
        public EnemySpawn.RaidDirection spawnDirection;
        public bool usesDragonSpawnPoint;
        public bool hasSpawned;
        public bool enemiesArrivedAtTarget;
        public bool isScripted;
        public bool spawnsDragon;
        public int warningWavesRemaining;
        public int remainingDefenseWaves;
        // Số wave còn lại của đội đang hành quân. Dùng số tương đối thay vì
        // targetWave tuyệt đối vì DayNightManager có thể khởi tạo lại từ Wave 1
        // khi quay về Main Map.
        public int remainingTravelWaves = -1;
        public Vector3 invasionTargetPosition;
        public List<RaidEnemySnapshot> enemies = new List<RaidEnemySnapshot>();
    }

    // Chỉ tồn tại trong lúc chuyển Main Map -> SceneBattle -> Main Map. State
    // này không phải save game dài hạn; nó ngăn một raid đang diễn ra biến mất
    // khi người chơi đi chinh phục một căn cứ địch khác.
    private static RaidSnapshot pendingRaidRestore;

    [Header("Four Fixed Raid Spawn Points")]
    [Tooltip("Bốn EnemySpawn cố định cho hướng Bắc, Đông, Nam, Tây. Manager tự quét cả các điểm đang ẩn. Mốc mở: Bắc = chinh phục Ải 1, Đông = Ải 2, Nam/Tây = Ải 3. Điểm đã mở vẫn ẩn cho đến khi được chọn làm raid.")]
    [SerializeField] private List<EnemySpawn> raidSpawnPoints = new List<EnemySpawn>();
    [Tooltip("Tự tạo raid khi bắt đầu Wave. Chỉ hoạt động khi Scene có đúng bốn Raid Spawn Point hợp lệ.")]
    [SerializeField] private bool automaticallyScheduleRaids = true;
    [Tooltip("1 = raid mới được phát động ngay khi hết Cooldown. Giảm giá trị này nếu muốn có thêm yếu tố may rủi sau cooldown.")]
    [SerializeField, Range(0f, 1f)] private float raidChancePerWave = 1f;
    [Tooltip("Khi một raid mới được chọn, EnemySpawnManager hiện ra ngay rồi chờ đúng số Wave này trước khi spawn quân.")]
    [SerializeField, Min(0)] private int warningWavesBeforeSpawn = 10;
    [SerializeField, Min(0)] private int cooldownWavesAfterRaid = 2;

    [Header("Dragon Raid Spawn Point")]
    [Tooltip("Kéo một EnemySpawnManager riêng cho trận Rồng vào đây. Điểm này không thuộc 4 hướng Bắc/Đông/Nam/Tây, phải tắt Is Raid Spawn Point, và chỉ hiện khi trận Rồng thật sự bắt đầu.")]
    [SerializeField] private EnemySpawn dragonRaidSpawnPoint;
    [Tooltip("Trận Rồng đã xuất phát sẽ mất đúng số Wave này để áp sát thành. Nhãn trên đường sẽ hiện số này.")]
    [SerializeField, Min(1)] private int dragonWavesToReachTarget = 10;
    private string dragonRaidSpawnPointName;

    [HideInInspector] public EnemySpawn currentRaidSpawnPoint;
    public EnemySpawn CurrentRaidSpawnPoint => currentRaidSpawnPoint;
    public bool CurrentRaidSpawnsDragon => currentRaidSpawnsDragon;

    [Header("Current Invasion Info")]
    public SettlementZone currentTargetedZone;
    public bool isInvasionActive = false;
    public bool isEnemiesArrivedAtTarget = false;
    public int remainingDefenseWaves = 3;
    public Vector3 invasionTargetPosition;

    private DayNightManager subscribedDayNightManager;
    private bool currentRaidHasSpawned;
    private bool currentRaidIsScripted;
    private bool currentRaidSpawnsDragon;
    private bool automaticRaidsPaused;
    private int warningWavesRemaining;
    private int nextRaidEligibleWave;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (Ins != null) return;

        EnemyInvasionManager existing = Object.FindFirstObjectByType<EnemyInvasionManager>();
        if (existing != null) return;

        GameObject managerObject = new GameObject("EnemyInvasionManager");
        managerObject.AddComponent<EnemyInvasionManager>();
    }

    private void Awake()
    {
        if (Ins == null)
        {
            Ins = this;
            DontDestroyOnLoad(gameObject);
            CacheDragonSpawnPointName();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        EnsureWaveSubscription();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (subscribedDayNightManager != null)
        {
            subscribedDayNightManager.OnWaveStart -= OnWaveStartHandler;
        }
        subscribedDayNightManager = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        CacheDragonSpawnPointName();
        RefreshRaidSpawnPoints();
        UpdateRaidSpawnPointVisibility();
        int currentWave = DayNightManager.HasInstance && DayNightManager.Ins != null
            ? DayNightManager.Ins.CurrentWave
            : 1;
        nextRaidEligibleWave = currentWave + Mathf.Max(0, cooldownWavesAfterRaid);
    }

    private void Update()
    {
        EnsureWaveSubscription();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(RefreshRaidSpawnPointsAfterSceneLoad());
    }

    private IEnumerator RefreshRaidSpawnPointsAfterSceneLoad()
    {
        // Chờ SettlementZone nạp PlayerPrefs xong trước khi xét ải nào đã chinh phục.
        yield return null;
        UpdateRaidSpawnPointVisibility();
    }

    private void EnsureWaveSubscription()
    {
        DayNightManager activeDayNightManager = DayNightManager.HasInstance ? DayNightManager.Ins : null;
        if (subscribedDayNightManager == activeDayNightManager) return;

        if (subscribedDayNightManager != null)
        {
            subscribedDayNightManager.OnWaveStart -= OnWaveStartHandler;
            subscribedDayNightManager = null;
        }

        if (activeDayNightManager == null) return;

        activeDayNightManager.OnWaveStart -= OnWaveStartHandler;
        activeDayNightManager.OnWaveStart += OnWaveStartHandler;
        subscribedDayNightManager = activeDayNightManager;
    }

    /// <summary>
    /// Lấy bốn spawn point raid đã đánh dấu trong Scene. Chỉ EnemySpawn có
    /// Is Raid Spawn Point mới được đưa vào danh sách này.
    /// </summary>
    public void RefreshRaidSpawnPoints()
    {
        RebindDragonRaidSpawnPoint();

        // Điểm Rồng là điểm thứ năm độc lập. Dù scene cũ vô tình lưu cờ
        // IsRaidSpawnPoint của nó là true, tuyệt đối không để nó lẫn vào
        // danh sách bốn hướng raid thường.
        raidSpawnPoints.RemoveAll(spawn => spawn == null || spawn == dragonRaidSpawnPoint || !spawn.IsRaidSpawnPoint);

        // Các điểm chưa mở bị SetActive(false), nên bắt buộc quét cả inactive.
        EnemySpawn[] sceneSpawns = Object.FindObjectsByType<EnemySpawn>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (EnemySpawn spawn in sceneSpawns)
        {
            if (spawn != null && spawn != dragonRaidSpawnPoint && spawn.IsRaidSpawnPoint && !raidSpawnPoints.Contains(spawn))
            {
                raidSpawnPoints.Add(spawn);
            }
        }
    }

    private void CacheDragonSpawnPointName()
    {
        if (dragonRaidSpawnPoint != null)
        {
            dragonRaidSpawnPointName = dragonRaidSpawnPoint.name;
        }
    }

    private void RebindDragonRaidSpawnPoint()
    {
        if (dragonRaidSpawnPoint != null || string.IsNullOrEmpty(dragonRaidSpawnPointName)) return;

        EnemySpawn[] sceneSpawns = Object.FindObjectsByType<EnemySpawn>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (EnemySpawn spawn in sceneSpawns)
        {
            if (spawn != null && spawn.name == dragonRaidSpawnPointName)
            {
                dragonRaidSpawnPoint = spawn;
                return;
            }
        }
    }

    /// <summary>
    /// Lưu raid đang chạy trước khi người chơi đi chinh phục căn cứ khác. Chỉ
    /// gọi cho trận tấn công căn cứ địch, không áp dụng với trận phòng thủ.
    /// </summary>
    public static void CaptureActiveRaidForBattleTransition()
    {
        if (Ins == null || !Ins.isInvasionActive || Ins.currentTargetedZone == null || Ins.currentRaidSpawnPoint == null)
        {
            pendingRaidRestore = null;
            return;
        }

        pendingRaidRestore = Ins.CreateRaidSnapshot();
    }

    public static void ClearPendingRaidRestore()
    {
        pendingRaidRestore = null;
    }

    /// <summary>
    /// Nối lại spawn point, vùng mục tiêu và đội địch của raid sau khi Main Map
    /// được tạo mới. Được gọi sau khi BuildingSystem đã tải công trình xong.
    /// </summary>
    public static void RestorePendingRaidAfterBattleTransition()
    {
        if (pendingRaidRestore == null) return;

        RaidSnapshot snapshot = pendingRaidRestore;
        pendingRaidRestore = null;

        if (Ins == null || !Ins.RestoreRaidSnapshot(snapshot))
        {
            Debug.LogWarning("[EnemyInvasionManager] Không thể khôi phục cuộc tập kích sau SceneBattle.");
        }
    }

    private RaidSnapshot CreateRaidSnapshot()
    {
        int currentWave = GetCurrentWave();
        RaidSnapshot snapshot = new RaidSnapshot
        {
            targetSettlementName = currentTargetedZone.settlementName,
            spawnPointName = currentRaidSpawnPoint.name,
            spawnDirection = currentRaidSpawnPoint.Direction,
            usesDragonSpawnPoint = currentRaidSpawnPoint == dragonRaidSpawnPoint,
            hasSpawned = currentRaidHasSpawned,
            enemiesArrivedAtTarget = isEnemiesArrivedAtTarget,
            isScripted = currentRaidIsScripted,
            spawnsDragon = currentRaidSpawnsDragon,
            warningWavesRemaining = warningWavesRemaining,
            remainingDefenseWaves = remainingDefenseWaves,
            invasionTargetPosition = invasionTargetPosition
        };

        foreach (EnemyAI enemy in currentRaidSpawnPoint.GetActiveWaveEnemies())
        {
            int remainingWaves = Mathf.Max(0, enemy.targetWave - currentWave);
            if (snapshot.remainingTravelWaves < 0)
            {
                snapshot.remainingTravelWaves = remainingWaves;
            }

            snapshot.enemies.Add(new RaidEnemySnapshot
            {
                position = enemy.transform.position,
                startSpawnPosition = enemy.StartSpawnPosition,
                spawnWave = enemy.spawnWave,
                targetWave = enemy.targetWave,
                wavesToReachTarget = enemy.wavesToReachTarget,
                remainingWavesToTarget = remainingWaves,
                wasWaitingAtTarget = enemy.isWaitingAtTarget
            });
        }

        return snapshot;
    }

    private bool RestoreRaidSnapshot(RaidSnapshot snapshot)
    {
        if (snapshot == null || string.IsNullOrEmpty(snapshot.targetSettlementName)) return false;

        RefreshRaidSpawnPoints();
        SettlementZone targetZone = FindSettlementZone(snapshot.targetSettlementName);
        EnemySpawn spawnPoint = FindRaidSpawnPoint(snapshot);
        if (targetZone == null || spawnPoint == null) return false;

        currentTargetedZone = targetZone;
        currentRaidSpawnPoint = spawnPoint;
        isInvasionActive = true;
        isEnemiesArrivedAtTarget = snapshot.enemiesArrivedAtTarget;
        remainingDefenseWaves = snapshot.remainingDefenseWaves;
        currentRaidHasSpawned = snapshot.hasSpawned;
        currentRaidIsScripted = snapshot.isScripted;
        currentRaidSpawnsDragon = snapshot.spawnsDragon;
        warningWavesRemaining = snapshot.warningWavesRemaining;
        invasionTargetPosition = snapshot.invasionTargetPosition;
        UpdateRaidSpawnPointVisibility();

        if (!currentRaidHasSpawned) return true;

        Transform target = targetZone.townHallBuilding != null
            ? targetZone.townHallBuilding.transform
            : (targetZone.townHallPoint != null ? targetZone.townHallPoint : targetZone.transform);
        spawnPoint.SetAttackTarget(target);
        // Không tạo lại một hành trình 10 Wave. Raid đã đi được bao xa thì chỉ
        // tạo phần đường còn lại, kể cả khi Wave của map vừa bị khởi tạo lại.
        int waveArrivalOverride = snapshot.remainingTravelWaves >= 0
            ? Mathf.Max(1, snapshot.remainingTravelWaves)
            : (currentRaidSpawnsDragon ? dragonWavesToReachTarget : -1);
        spawnPoint.SpawnEnemy(waveArrivalOverride);

        List<EnemyAI> restoredEnemies = spawnPoint.GetActiveWaveEnemies();
        int restoreCount = Mathf.Min(restoredEnemies.Count, snapshot.enemies.Count);
        int restoredCurrentWave = GetCurrentWave();
        for (int i = 0; i < restoreCount; i++)
        {
            RaidEnemySnapshot enemySnapshot = snapshot.enemies[i];
            int remainingWaves = Mathf.Max(0, enemySnapshot.remainingWavesToTarget);
            int totalTravelWaves = Mathf.Max(1, enemySnapshot.wavesToReachTarget);
            int restoredTargetWave = restoredCurrentWave + remainingWaves;
            int restoredSpawnWave = Mathf.Max(1, restoredCurrentWave - (totalTravelWaves - remainingWaves));
            restoredEnemies[i].RestoreWaveArrival(
                enemySnapshot.position,
                enemySnapshot.startSpawnPosition,
                restoredSpawnWave,
                restoredTargetWave,
                totalTravelWaves,
                enemySnapshot.wasWaitingAtTarget);
        }

        return true;
    }

    private static int GetCurrentWave()
    {
        return DayNightManager.HasInstance && DayNightManager.Ins != null
            ? DayNightManager.Ins.CurrentWave
            : 1;
    }

    private EnemySpawn FindRaidSpawnPoint(RaidSnapshot snapshot)
    {
        if (snapshot.usesDragonSpawnPoint)
        {
            RebindDragonRaidSpawnPoint();
            if (dragonRaidSpawnPoint != null) return dragonRaidSpawnPoint;
        }

        foreach (EnemySpawn spawn in raidSpawnPoints)
        {
            if (spawn != null && spawn.Direction == snapshot.spawnDirection) return spawn;
        }

        EnemySpawn[] sceneSpawns = Object.FindObjectsByType<EnemySpawn>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (EnemySpawn spawn in sceneSpawns)
        {
            if (spawn != null && spawn.name == snapshot.spawnPointName) return spawn;
        }

        return null;
    }

    private static SettlementZone FindSettlementZone(string settlementName)
    {
        if (SettlementManager.Ins != null)
        {
            SettlementZone managedZone = SettlementManager.Ins.GetZoneByName(settlementName);
            if (managedZone != null) return managedZone;
        }

        foreach (SettlementZone zone in Object.FindObjectsByType<SettlementZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (zone != null && zone.settlementName == settlementName) return zone;
        }

        return null;
    }

    private EnemySpawn SelectRandomRaidSpawnPoint()
    {
        RefreshRaidSpawnPoints();
        HashSet<EnemySpawn.RaidDirection> directions = new HashSet<EnemySpawn.RaidDirection>();
        foreach (EnemySpawn spawn in raidSpawnPoints)
        {
            if (spawn != null) directions.Add(spawn.Direction);
        }

        if (raidSpawnPoints.Count != 4 || directions.Count != 4)
        {
            Debug.LogWarning($"[EnemyInvasionManager] Cần đúng bốn Raid Spawn Point khác hướng Bắc/Đông/Nam/Tây; hiện có {raidSpawnPoints.Count} điểm và {directions.Count} hướng.");
            return null;
        }

        int highestConqueredTier = GetHighestConqueredSettlementTier();
        List<EnemySpawn> unlockedSpawnPoints = new List<EnemySpawn>();
        foreach (EnemySpawn spawn in raidSpawnPoints)
        {
            if (IsRaidSpawnPointUnlocked(spawn, highestConqueredTier))
            {
                unlockedSpawnPoints.Add(spawn);
            }
        }

        if (unlockedSpawnPoints.Count == 0)
        {
            Debug.Log("[EnemyInvasionManager] Chưa có điểm Raid nào được mở. Hãy chinh phục Ải 1 để mở điểm đầu tiên.");
            return null;
        }

        return unlockedSpawnPoints[Random.Range(0, unlockedSpawnPoints.Count)];
    }

    /// <summary>
    /// Tạm dừng raid ngẫu nhiên khi kịch bản thuyết trình đang điều khiển trận đánh.
    /// Raid đã được gọi bằng StartScriptedRaid vẫn hoạt động bình thường.
    /// </summary>
    public void SetAutomaticRaidsPaused(bool paused)
    {
        automaticRaidsPaused = paused;
    }

    /// <summary>
    /// Các điểm raid đã mở chỉ là danh sách có thể chọn, không hiển thị thường
    /// trực trên map. Chỉ điểm của raid đang chờ/đang đánh được bật; sau khi
    /// raid kết thúc nó lại biến mất.
    /// </summary>
    public void UpdateRaidSpawnPointVisibility()
    {
        RefreshRaidSpawnPoints();

        foreach (EnemySpawn spawn in raidSpawnPoints)
        {
            if (spawn == null) continue;

            bool shouldBeVisible = isInvasionActive && currentRaidSpawnPoint == spawn;
            if (spawn.gameObject.activeSelf != shouldBeVisible)
            {
                spawn.gameObject.SetActive(shouldBeVisible);
            }
        }

        // Điểm Rồng là điểm thứ năm riêng, cũng phải ẩn lúc bình thường để
        // không xuất hiện song song với các raid thông thường.
        if (dragonRaidSpawnPoint != null)
        {
            bool shouldBeVisible = isInvasionActive && currentRaidSpawnPoint == dragonRaidSpawnPoint;
            if (dragonRaidSpawnPoint.gameObject.activeSelf != shouldBeVisible)
            {
                dragonRaidSpawnPoint.gameObject.SetActive(shouldBeVisible);
            }
        }
    }

    /// <summary>
    /// SettlementZone gọi ngay sau khi phá căn cứ địch để mở điểm raid đúng mốc.
    /// </summary>
    public void NotifySettlementConquered(SettlementZone conqueredZone)
    {
        if (conqueredZone == null) return;

        UpdateRaidSpawnPointVisibility();
        Debug.Log($"[EnemyInvasionManager] Đã chinh phục Ải {conqueredZone.GetEffectiveTier()}; mở thêm điểm Raid vào danh sách có thể được chọn.");
    }

    private int GetHighestConqueredSettlementTier()
    {
        int highestTier = 0;
        SettlementZone[] zones = Object.FindObjectsByType<SettlementZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (SettlementZone zone in zones)
        {
            if (zone == null || zone.isStartingSettlement || zone.GetEffectiveTier() <= 0) continue;

            int tier = zone.GetEffectiveTier();
            bool wasConqueredThisRun = PlayerPrefs.GetInt($"Settlement_{zone.settlementName}_Conquered", 0) == 1;
            // Dự phòng cho save cũ, trước khi key "Conquered" được thêm vào.
            bool isConqueredInLegacySave = zone.IsConquered;
            if (wasConqueredThisRun || isConqueredInLegacySave)
            {
                highestTier = Mathf.Max(highestTier, tier);
            }
        }

        return highestTier;
    }

    private static int GetRequiredConqueredTier(EnemySpawn.RaidDirection direction)
    {
        switch (direction)
        {
            case EnemySpawn.RaidDirection.North:
                return 1;
            case EnemySpawn.RaidDirection.East:
                return 2;
            case EnemySpawn.RaidDirection.South:
            case EnemySpawn.RaidDirection.West:
                return 3;
            default:
                return int.MaxValue;
        }
    }

    private static bool IsRaidSpawnPointUnlocked(EnemySpawn spawn, int highestConqueredTier)
    {
        return spawn != null && highestConqueredTier >= GetRequiredConqueredTier(spawn.Direction);
    }

    /// <summary>
    /// Khởi động một đợt tấn công ngẫu nhiên nhắm vào Vùng Đất đã có Nhà Chính
    /// </summary>
    public SettlementZone PickRandomEstablishedTargetZone()
    {
        if (isInvasionActive) return currentTargetedZone;

        List<SettlementZone> establishedZones = new List<SettlementZone>();

        if (SettlementManager.Ins != null && SettlementManager.Ins.AllSettlements != null)
        {
            foreach (var z in SettlementManager.Ins.AllSettlements)
            {
                if (z != null && z.isTownHallEstablished && z.isUnlocked && !z.hasEnemyOutpost)
                {
                    establishedZones.Add(z);
                }
            }
        }

        if (establishedZones.Count == 0)
        {
            SettlementZone[] sceneZones = Object.FindObjectsByType<SettlementZone>(FindObjectsSortMode.None);
            foreach (var z in sceneZones)
            {
                if (z != null && z.isTownHallEstablished && z.isUnlocked && !z.hasEnemyOutpost)
                {
                    establishedZones.Add(z);
                }
            }
        }

        if (establishedZones.Count > 0)
        {
            int index = Random.Range(0, establishedZones.Count);
            currentTargetedZone = establishedZones[index];
        }
        else
        {
            currentTargetedZone = null;
        }

        if (currentTargetedZone != null &&
            !BeginRaid(currentTargetedZone, false, false, warningWavesBeforeSpawn))
        {
            currentTargetedZone = null;
        }

        return currentTargetedZone;
    }

    /// <summary>
    /// Gọi một đợt phòng thủ theo kịch bản. Dùng cho hai trận trình diễn nhỏ/lớn;
    /// chỉ đợt có spawnDragon = true mới sinh Rồng trong SceneBattle.
    /// </summary>
    public bool StartScriptedRaid(SettlementZone targetZone, bool spawnDragon, int warningWaves = 0)
    {
        if (targetZone == null || isInvasionActive) return false;
        return BeginRaid(targetZone, true, spawnDragon, warningWaves);
    }

    /// <summary>
    /// Trận Rồng chỉ dùng EnemySpawnManager do Designer chỉ định, tuyệt đối
    /// không chọn ngẫu nhiên một trong bốn điểm raid thông thường.
    /// </summary>
    public bool StartScriptedDragonRaid(SettlementZone targetZone, int warningWaves = 0)
    {
        if (targetZone == null || isInvasionActive) return false;

        if (dragonRaidSpawnPoint == null)
        {
            Debug.LogWarning("[EnemyInvasionManager] Chưa gán Dragon Raid Spawn Point. Hãy đặt một EnemySpawnManager riêng vào field này.");
            return false;
        }

        return BeginRaid(targetZone, true, true, warningWaves, dragonRaidSpawnPoint);
    }

    private bool BeginRaid(
        SettlementZone targetZone,
        bool isScripted,
        bool spawnDragon,
        int warningWaves,
        EnemySpawn forcedSpawnPoint = null)
    {
        if (targetZone == null || isInvasionActive) return false;

        currentRaidSpawnPoint = forcedSpawnPoint != null ? forcedSpawnPoint : SelectRandomRaidSpawnPoint();
        if (currentRaidSpawnPoint == null) return false;

        currentTargetedZone = targetZone;
        isInvasionActive = true;
        isEnemiesArrivedAtTarget = false;
        remainingDefenseWaves = 3;
        currentRaidHasSpawned = false;
        currentRaidIsScripted = isScripted;
        currentRaidSpawnsDragon = spawnDragon;
        warningWavesRemaining = Mathf.Max(0, warningWaves);
        UpdateRaidSpawnPointVisibility();

        // Đặt mục tiêu đỗ quân cách khoảng 3m phía trước Vùng Đất.
        Vector3 zonePos = currentTargetedZone.transform.position;
        Vector3 fwd = currentTargetedZone.transform.forward;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        invasionTargetPosition = zonePos + fwd * 3.0f;

        string dragonText = spawnDragon ? " cùng RỒNG" : string.Empty;
        string originText = forcedSpawnPoint != null
            ? $"tại điểm {currentRaidSpawnPoint.name}"
            : $"từ hướng {currentRaidSpawnPoint.Direction}";
        int displayedWaves = spawnDragon && warningWavesRemaining <= 0
            ? dragonWavesToReachTarget
            : warningWavesRemaining;
        string msg = $"⚔️ CẢNH BÁO: Địch{dragonText} đang tập kết {originText} và sẽ tấn công Vùng Đất {currentTargetedZone.settlementName} sau {displayedWaves} wave!";
        if (UIManager.Ins != null) UIManager.Ins.ShowWarning(msg);
        Debug.Log($"[EnemyInvasionManager] {msg}");

        if (warningWavesRemaining <= 0)
        {
            StartCurrentRaid();
        }

        return true;
    }

    /// <summary>
    /// Spawn đúng một raid từ điểm đã được chọn. Không spawn từ SettlementZone hay EnemyTower.
    /// </summary>
    public bool StartCurrentRaid()
    {
        if (!isInvasionActive || currentRaidHasSpawned || currentTargetedZone == null || currentRaidSpawnPoint == null)
        {
            return false;
        }

        Transform target = currentTargetedZone.townHallBuilding != null
            ? currentTargetedZone.townHallBuilding.transform
            : (currentTargetedZone.townHallPoint != null ? currentTargetedZone.townHallPoint : currentTargetedZone.transform);

        currentRaidSpawnPoint.SetAttackTarget(target);
        int waveArrivalOverride = currentRaidSpawnsDragon ? dragonWavesToReachTarget : -1;
        currentRaidSpawnPoint.SpawnEnemy(waveArrivalOverride);
        currentRaidHasSpawned = true;
        Debug.Log($"[EnemyInvasionManager] Raid xuất phát từ {currentRaidSpawnPoint.Direction} tới {currentTargetedZone.settlementName}.");
        return true;
    }

    /// <summary>
    /// Gọi khi kẻ địch đã hành quân đến vị trí cách 3m trước Vùng đất mục tiêu
    /// </summary>
    public void NotifyEnemiesArrivedAtTarget()
    {
        if (!isInvasionActive || isEnemiesArrivedAtTarget) return;

        isEnemiesArrivedAtTarget = true;
        remainingDefenseWaves = 3;

        string zoneName = currentTargetedZone != null ? currentTargetedZone.settlementName : "Căn Cứ";
        string msg = $"🚨 Kẻ địch đã áp sát và bao vây Vùng Đất {zoneName}! Còn {remainingDefenseWaves} Ngày để cử lính xuất trận phòng thủ!";
        if (UIManager.Ins != null) UIManager.Ins.ShowWarning(msg);
        Debug.Log($"[EnemyInvasionManager] {msg}");
    }

    /// <summary>
    /// Đếm ngược số Wave / Ngày trôi qua khi kẻ địch đang bao vây
    /// </summary>
    private void OnWaveStartHandler(int waveIndex)
    {
        if (!isInvasionActive)
        {
            bool tutorialIsStillRunning = CampaignTutorialManager.Ins != null && !CampaignTutorialManager.Ins.IsTutorialCompleted();
            if (!tutorialIsStillRunning && !automaticRaidsPaused && automaticallyScheduleRaids &&
                waveIndex >= nextRaidEligibleWave && Random.value <= raidChancePerWave)
            {
                PickRandomEstablishedTargetZone();
            }
            return;
        }

        if (!currentRaidHasSpawned)
        {
            warningWavesRemaining--;
            if (warningWavesRemaining <= 0)
            {
                StartCurrentRaid();
            }
            return;
        }

        if (!isEnemiesArrivedAtTarget || currentTargetedZone == null) return;

        remainingDefenseWaves--;
        string zoneName = currentTargetedZone.settlementName;

        if (remainingDefenseWaves > 0)
        {
            string msg = $"⚠️ Vùng Đất {zoneName} đang bị bao vây! Còn {remainingDefenseWaves} Ngày để phòng thủ!";
            if (UIManager.Ins != null) UIManager.Ins.ShowWarning(msg);
            Debug.Log($"[EnemyInvasionManager] {msg}");
        }
        else
        {
            // Quá 3 Wave không cử lính ra phòng thủ -> PHÒNG THỦ THUA!
            TriggerDefenseDefeat(currentTargetedZone);
        }
    }

    /// <summary>
    /// Xử lý sự kiện Phòng Thủ Thua do không ra phòng thủ hoặc đánh thua tại Vùng đất này
    /// </summary>
    public void TriggerDefenseDefeat(SettlementZone zone)
    {
        bool dragonRaid = currentRaidSpawnsDragon;
        if (zone == null) zone = currentTargetedZone;
        string zoneName = zone != null ? zone.settlementName : "Căn Cứ";

        Debug.Log($"[EnemyInvasionManager] 💥 Phòng thủ THUA tại Vùng Đất {zoneName}! Mất toàn bộ lính của vùng và 50% tài nguyên.");

        // 1. Tiêu diệt toàn bộ lính thuộc về Doanh trại của Vùng đất này
        if (zone != null)
        {
            foreach (UnitController unit in Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None))
            {
                if (unit != null && unit.gameObject.activeInHierarchy && unit.IsStationedInZone(zone.settlementName))
                {
                    Destroy(unit.gameObject);
                }
            }

            SpawnSoldier[] zoneSpawners = zone.GetComponentsInChildren<SpawnSoldier>(true);
            foreach (var spawner in zoneSpawners)
            {
                if (spawner != null)
                {
                    spawner.DestroyAllSoldiers();
                }
            }
        }

        // 2. Trừ 50% tài nguyên tích lũy
        if (JsonDataManager.Ins != null)
        {
            JsonDataManager.Ins.HalveAllResources();
        }

        string warnMsg = $"💥 Vùng Đất {zoneName} PHÒNG THỦ THẤT BẠI! Đã mất lính của vùng và 50% tài nguyên!";
        if (UIManager.Ins != null) UIManager.Ins.ShowWarning(warnMsg);

        ResetCurrentRaid();

        // EnemyInvasionManager của BuildMap được tạo lại sau khi quay về từ
        // SceneBattle, nên cờ currentRaidIsScripted không còn đáng tin ở thời
        // điểm nhận kết quả. CampaignTutorialManager tự đối chiếu phase đã
        // lưu để chỉ xử lý đúng trận phòng thủ kịch bản đang chờ kết quả.
        CampaignTutorialManager.Ins?.OnScriptedDefenseDefeat(dragonRaid);
    }

    /// <summary>
    /// Gọi khi người chơi Phòng Thủ Thành Công (Đánh bại đợt quái xâm lược)
    /// </summary>
    public void TriggerDefenseVictory()
    {
        bool dragonRaid = currentRaidSpawnsDragon;
        string zoneName = currentTargetedZone != null ? currentTargetedZone.settlementName : "Căn Cứ";
        string msg = $"🏆 PHÒNG THỦ THÀNH CÔNG! Đã đánh đuổi kẻ địch khỏi Vùng Đất {zoneName}. Bảo toàn 100% lính và tài nguyên!";
        if (UIManager.Ins != null) UIManager.Ins.ShowWarning(msg);
        Debug.Log($"[EnemyInvasionManager] {msg}");

        ResetCurrentRaid();

        // Xem chú thích ở TriggerDefenseDefeat: sau round-trip SceneBattle,
        // chỉ phase chiến dịch còn được lưu. Luôn thông báo để Campaign
        // chuyển FirstDefenseActive -> DragonCountdown ngay sau khi thắng.
        CampaignTutorialManager.Ins?.OnScriptedDefenseVictory(dragonRaid);
    }

    private void ResetCurrentRaid()
    {
        isInvasionActive = false;
        isEnemiesArrivedAtTarget = false;
        currentTargetedZone = null;
        currentRaidSpawnPoint = null;
        currentRaidHasSpawned = false;
        currentRaidIsScripted = false;
        currentRaidSpawnsDragon = false;
        nextRaidEligibleWave = GetNextEligibleWave();
        UpdateRaidSpawnPointVisibility();
    }

    private int GetNextEligibleWave()
    {
        int currentWave = DayNightManager.HasInstance && DayNightManager.Ins != null
            ? DayNightManager.Ins.CurrentWave
            : 1;
        return currentWave + Mathf.Max(0, cooldownWavesAfterRaid);
    }
}
