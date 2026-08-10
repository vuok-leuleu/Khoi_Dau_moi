using UnityEngine;
using System.Collections.Generic;

public class EnemySpawn : MonoBehaviour
{
    [Header("Căn Cứ Địch - Số Lượng Enemy")]
    [Tooltip("Số lượng Enemy tại căn cứ này khi vào SceneBattle (Có thể chỉnh tùy ý cho từng vị trí trong Inspector).")]
    public int enemyCountInBase = 5;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private Transform[] spawnPoints;
    [Tooltip("Khoảng cách đẩy vị trí spawn ra phía trước căn cứ (mét) để tránh bị vướng vào nhà/căn cứ.")]
    [SerializeField] private float spawnForwardOffset = 3.5f;

    /// <summary>
    /// Danh sách các điểm spawn
    /// </summary>
    public Transform[] SpawnPoints => spawnPoints;

    /// <summary>
    /// Lấy vị trí Spawn chính của EnemySpawn (ưu tiên điểm trong spawnPoints, nếu không có lấy vị trí transform)
    /// </summary>
    public Vector3 GetSpawnPosition()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            foreach (Transform p in spawnPoints)
            {
                if (p != null) return p.position;
            }
        }
        return transform.position;
    }

    /// <summary>
    /// Lấy Transform điểm Spawn chính của EnemySpawn
    /// </summary>
    public Transform GetSpawnPoint()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            foreach (Transform p in spawnPoints)
            {
                if (p != null) return p;
            }
        }
        Transform childSpawn = transform.Find("Spawn");
        if (childSpawn != null) return childSpawn;
        return transform;
    }

    [Header("Attack Target (Optional)")]
    [SerializeField] public Transform attackTarget;

    [Header("Grid Spawn Settings")]
    [SerializeField] private bool useGridSpawn = false;
    [SerializeField] private int rows = 1;
    [SerializeField] private int cols = 1;
    [SerializeField] private float spacingX = 2f;
    [SerializeField] private float spacingZ = 2f;

    [Header("Time & Wave Spawning Settings")]
    [SerializeField] private bool useWaveSpawn = true;
    [SerializeField] private float waveInterval = 30f;
    [Tooltip("Tự động spawn Enemy theo chu kỳ Wave độc lập khi TẮT Tutorial")]
    [SerializeField] private bool autoSpawnWaveAlways = true;

    [Header("Warning Icon & Attack UI Settings")]
    [SerializeField] private bool showAttackButton = true;
    [SerializeField] private GameObject warningIconPrefab;
    [SerializeField] private float warningIconHeightOffset = 3f;

    [Header("Cài Đặt Kích Thước Mũi Tên & Cảnh Báo (Spawn Warning Arrow)")]
    [Tooltip("Điều chỉnh chiều rộng mũi tên dưới chân Enemy")]
    [Range(0.1f, 5f)] public float warningArrowSize = 1.0f;

    [Tooltip("Điều chỉnh độ dài kéo dài của mũi tên (1.0 = duỗi đúng tới mục tiêu, >1.0 = dài hơn, <1.0 = ngắn hơn)")]
    [Range(0.1f, 5f)] public float warningArrowLengthMultiplier = 1.0f;

    [Tooltip("Độ dài cộng thêm cố định (mét) cho mũi tên")]
    public float warningArrowExtraLength = 0.0f;

    [Tooltip("Điều chỉnh kích thước chữ đếm ngược")]
    [Range(0.1f, 5f)] public float warningTimerTextScale = 1.0f;

    [Tooltip("Độ cao chữ đếm ngược trên đầu/thân Enemy")]
    [Range(0.5f, 5f)] public float warningTextHeightOffset = 1.8f;

    [Header("Exit Play Mode Settings")]
    [Tooltip("Khi tích chọn, nếu tất cả công trình/tháp bị phá hủy thì game sẽ tự động thoát chế độ Play.")]
    [SerializeField] private bool exitPlayModeWhenNoBuildings = false;

    private Coroutine waveSpawnCoroutine;

    private bool IsTutorialActive()
    {
        if (PlayerPrefs.GetInt("TutorialCompleted", 0) == 0) return true;

        if (CampaignTutorialManager.Ins != null && CampaignTutorialManager.Ins.gameObject.activeInHierarchy && CampaignTutorialManager.Ins.currentStage != DemaciaTutorialStage.Completed) return true;

        GameObject tutCanvas = GameObject.Find("TutorialCanvas");
        if (tutCanvas != null && tutCanvas.activeInHierarchy) return true;

        return false;
    }

    private void OnEnable()
    {
        SubscribeToWaveEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromWaveEvents();
        StopWaveSpawning();
    }

    private void SubscribeToWaveEvents()
    {
        if (DayNightManager.HasInstance && DayNightManager.Ins != null)
        {
            DayNightManager.Ins.OnWaveStart -= OnWaveStartHandler;
            DayNightManager.Ins.OnWaveStart += OnWaveStartHandler;
        }
    }

    private void UnsubscribeFromWaveEvents()
    {
        if (DayNightManager.HasInstance && DayNightManager.Ins != null)
        {
            DayNightManager.Ins.OnWaveStart -= OnWaveStartHandler;
        }
    }

    private void OnWaveStartHandler(int waveIndex)
    {
        // Tự động Spawn đợt Quái mới phù hợp với hệ thống Wave của DayNightManager (xuất hiện ở Wave 1, Wave 4, Wave 7, ...)
        if (waveIndex == 1 || (waveIndex > 1 && (waveIndex - 1) % 3 == 0))
        {
            if (!IsTutorialActive())
            {
                Debug.Log($"[EnemySpawn] 🔥 DayNightManager phát sự kiện Wave {waveIndex}! Tự động Spawn đợt Quái mới.");
                SpawnEnemy();
            }
        }
    }

    private void Start()
    {
        SubscribeToWaveEvents();
        GetOrFindAttackTarget();

        // 1. Spawn quái khởi đầu ở Wave 1 nếu spawnOnStart = true (tuyệt đối KHÔNG spawn khi đang trong Tutorial)
        if (spawnOnStart && !IsTutorialActive())
        {
            int currentWave = (DayNightManager.HasInstance && DayNightManager.Ins != null) ? DayNightManager.Ins.CurrentWave : 1;
            if (currentWave <= 1)
            {
                SpawnEnemy();
            }
        }

        // Tắt hoàn toàn timer đếm ngược 30s tự động (xóa thời gian spawn theo thời gian)
        useWaveSpawn = false;
        autoSpawnWaveAlways = false;
        StopWaveSpawning();
    }

    /// <summary>
    /// Spawn duy nhất 1 con Quái Địch dành riêng cho Stage 3 Tutorial ("Kẻ địch phát hiện ra ta")
    /// </summary>
    public void SpawnSingleEnemy()
    {
        if (enemyPrefab == null) return;
        Transform source = GetSpawnPoint();
        Vector3 spawnPos = (source != null) ? source.position : transform.position;
        Quaternion spawnRot = (source != null) ? source.rotation : transform.rotation;

        List<EnemyAI> squadList = new List<EnemyAI>();
        List<GameObject> spawnedWaveEnemies = new List<GameObject>();
        SpawnAtPosition(spawnPos, spawnRot, squadList, spawnedWaveEnemies, true);
        Debug.Log("[EnemySpawn] 👾 Đã Spawn duy nhất 1 quái địch cho Stage 3 Tutorial.");
    }

    private void OnMouseDown()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
        UIEnemyWaveButton.CreateButton(GetSpawnPoint());
    }

    private void Update()
    {
        // Đảm bảo event listener luôn được kết nối nếu DayNightManager khởi tạo trễ
        if (DayNightManager.HasInstance && DayNightManager.Ins != null)
        {
            SubscribeToWaveEvents();
        }
        StopWaveSpawning();

        // 🎯 Lắng nghe click chuột vào Căn cứ Địch để hiện nút TẤN CÔNG
        if (Input.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 200f))
                {
                    if (hit.transform == transform || hit.transform.IsChildOf(transform) || hit.transform.name.Contains("Chal") || hit.transform.name.Contains("Enemy") || hit.transform.name.Contains("Spawn"))
                    {
                        UIEnemyWaveButton.CreateButton(GetSpawnPoint());
                    }
                }
            }
        }
    }

    private void StartWaveSpawning()
    {
        if (waveSpawnCoroutine == null)
        {
            waveSpawnCoroutine = StartCoroutine(WaveSpawnRoutine());
            Debug.Log("[EnemySpawn] Started wave spawning.");
        }
    }

    private void StopWaveSpawning()
    {
        if (waveSpawnCoroutine != null)
        {
            StopCoroutine(waveSpawnCoroutine);
            waveSpawnCoroutine = null;
            Debug.Log("[EnemySpawn] Stopped wave spawning.");
        }
    }

    private System.Collections.IEnumerator WaveSpawnRoutine()
    {
        float interval = waveInterval > 0f ? waveInterval : 30f;
        while (true)
        {
            yield return new WaitForSeconds(interval);
            if (!IsTutorialActive())
            {
                SpawnEnemy();
            }
        }
    }

    public void SpawnEnemy()
{
    if (enemyPrefab == null)
    {
        Debug.LogWarning("EnemySpawn: Enemy Prefab is not assigned!", this);
        return;
    }

    List<Transform> sources = new List<Transform>();
    
    // 🎯 ƯU TIÊN 1: Lấy các điểm trong mảng spawnPoints
    if (spawnPoints != null && spawnPoints.Length > 0)
    {
        foreach (Transform p in spawnPoints)
        {
            if (p != null) sources.Add(p);
        }
    }

    // 🎯 ƯU TIÊN 2: Nếu spawnPoints rỗng, tự tìm con tên "Spawn" bên trong Manager này!
    if (sources.Count == 0)
    {
        Transform childSpawn = GetSpawnPoint();
        if (childSpawn != null && childSpawn != transform)
        {
            sources.Add(childSpawn);
        }
        else
        {
            sources.Add(transform);
        }
    }

    List<GameObject> spawnedWaveEnemies = new List<GameObject>();

    foreach (Transform source in sources)
    {
        List<EnemyAI> squadList = new List<EnemyAI>();

        // Kiểm tra xem vị trí này có phải là Transform gốc không để quyết định có dùng spawnForwardOffset hay không
        bool isRootTransform = (source == transform);

        if (useGridSpawn)
        {
            SpawnGridAt(source.position, source.rotation, squadList, spawnedWaveEnemies);
        }
        else
        {
            SpawnAtPosition(source.position, source.rotation, squadList, spawnedWaveEnemies, isRootTransform);
        }
    }

    // Gắn Mũi Tên & Cảnh Báo cho con Thủ Lĩnh (Lead Enemy)
    if (showAttackButton && spawnedWaveEnemies.Count > 0)
    {
        Quaternion spawnRot = (sources.Count > 0 && sources[0] != null) ? sources[0].rotation : transform.rotation;
        Vector3 spawnCenter = (sources.Count > 0 && sources[0] != null) ? sources[0].position : transform.position;

        GameObject leadObj = GetFrontCenterEnemy(spawnedWaveEnemies, spawnRot, spawnCenter);
        Transform leadEnemy = (leadObj != null) ? leadObj.transform : spawnedWaveEnemies[0].transform;

        EnemyAI leadAI = leadEnemy.GetComponent<EnemyAI>();
        if (leadAI != null && leadAI.squadEnemies != null)
        {
            leadAI.squadEnemies.Remove(leadAI);
            leadAI.squadEnemies.Insert(0, leadAI);
        }

        EnemySpawnWarningArrow arrow = EnemySpawnWarningArrow.Create(leadEnemy);
        if (arrow != null)
        {
            arrow.arrowSize = warningArrowSize;
            arrow.arrowLengthMultiplier = warningArrowLengthMultiplier;
            arrow.arrowExtraLength = warningArrowExtraLength;
            arrow.timerTextScale = warningTimerTextScale;
            arrow.textHeightOffset = warningTextHeightOffset;
            arrow.UpdateVisuals();
        }
    }
}

    /// <summary>
    /// Tìm con quái nằm ở HÀNG ĐẦU TIÊN (front row) và CHÍNH GIỮA (center column) của đội hình Wave
    /// </summary>
    private GameObject GetFrontCenterEnemy(List<GameObject> enemies, Quaternion spawnRotation, Vector3 spawnCenter)
    {
        if (enemies == null || enemies.Count == 0) return null;

        Vector3 forward = spawnRotation * Vector3.forward;
        Vector3 right = spawnRotation * Vector3.right;

        GameObject bestEnemy = null;
        float maxFrontDist = float.MinValue;
        float minCenterDist = float.MaxValue;

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null) continue;

            Vector3 toEnemy = enemy.transform.position - spawnCenter;
            float frontDist = Vector3.Dot(toEnemy, forward);
            float sideDist = Mathf.Abs(Vector3.Dot(toEnemy, right));

            if (frontDist > maxFrontDist + 0.1f)
            {
                maxFrontDist = frontDist;
                minCenterDist = sideDist;
                bestEnemy = enemy;
            }
            else if (Mathf.Abs(frontDist - maxFrontDist) <= 0.1f)
            {
                if (sideDist < minCenterDist)
                {
                    minCenterDist = sideDist;
                    bestEnemy = enemy;
                }
            }
        }

        return bestEnemy;
    }

    private void SpawnGridAt(Vector3 center, Quaternion rotation, List<EnemyAI> squadList, List<GameObject> spawnedWaveEnemies = null)
    {
        Vector3 forward = rotation * Vector3.forward;
        Vector3 right = rotation * Vector3.right;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float offsetX = (c - (cols - 1) * 0.5f) * spacingX;
                float offsetZ = (r - (rows - 1) * 0.5f) * spacingZ;

                Vector3 spawnPos = center + right * offsetX + forward * offsetZ;
                SpawnAtPosition(spawnPos, rotation, squadList, spawnedWaveEnemies);
            }
        }
    }

    /// <summary>
    /// Tìm hoặc lấy mục tiêu tấn công là một Nhà Chính (Town Hall Prefab) ngẫu nhiên trong Scene (KHÔNG tấn công townHallPoint)
    /// </summary>
    public Transform GetOrFindAttackTarget()
    {
        if (attackTarget != null && attackTarget.gameObject.activeInHierarchy)
        {
            return attackTarget;
        }

        List<Transform> validTownHallPrefabs = new List<Transform>();

        // 1. Quét qua tất cả SettlementZone trong Scene có Nhà Chính đã được xây dựng
        SettlementZone[] allZones = Object.FindObjectsByType<SettlementZone>(FindObjectsSortMode.None);
        foreach (var zone in allZones)
        {
            if (zone != null && zone.gameObject.activeInHierarchy)
            {
                if (zone.townHallBuilding != null && zone.townHallBuilding.gameObject.activeInHierarchy)
                {
                    if (!validTownHallPrefabs.Contains(zone.townHallBuilding.transform))
                    {
                        validTownHallPrefabs.Add(zone.townHallBuilding.transform);
                    }
                }
                else
                {
                    UpgradeableBuilding[] localUbs = zone.GetComponentsInChildren<UpgradeableBuilding>(true);
                    foreach (var ub in localUbs)
                    {
                        if (ub != null && ub.gameObject.activeInHierarchy && (ub.buildingType == BuildingType.House || SettlementZone.IsTownHallBuilding(ub, zone)))
                        {
                            if (!validTownHallPrefabs.Contains(ub.transform))
                            {
                                validTownHallPrefabs.Add(ub.transform);
                            }
                        }
                    }
                }
            }
        }

        // 2. Quét bổ sung các UpgradeableBuilding dạng House trong Scene
        if (validTownHallPrefabs.Count == 0)
        {
            UpgradeableBuilding[] allUbs = Object.FindObjectsByType<UpgradeableBuilding>(FindObjectsSortMode.None);
            foreach (var ub in allUbs)
            {
                if (ub != null && ub.gameObject.activeInHierarchy && ub.buildingType == BuildingType.House)
                {
                    if (!validTownHallPrefabs.Contains(ub.transform))
                    {
                        validTownHallPrefabs.Add(ub.transform);
                    }
                }
            }
        }

        // 3. Quét bổ sung các GameObject theo tên "Nhachinhs" hoặc "Nhachinh" hoặc "TownHall" trong Scene
        if (validTownHallPrefabs.Count == 0)
        {
            GameObject[] sceneObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var go in sceneObjects)
            {
                if (go != null && go.activeInHierarchy)
                {
                    string n = go.name.ToLower();
                    if (n.Contains("nhachinh") || n.Contains("townhall"))
                    {
                        if (!validTownHallPrefabs.Contains(go.transform))
                        {
                            validTownHallPrefabs.Add(go.transform);
                        }
                    }
                }
            }
        }

        // 🎲 Nếu có ít nhất 1 Nhà Chính Prefab ➔ Chọn ngẫu nhiên 1 Nhà Chính Prefab làm mục tiêu tấn công
        if (validTownHallPrefabs.Count > 0)
        {
            int randomIndex = Random.Range(0, validTownHallPrefabs.Count);
            attackTarget = validTownHallPrefabs[randomIndex];
            Debug.Log($"[EnemySpawn] 🎯 Đã chọn ngẫu nhiên Nhà Chính Prefab ({attackTarget.name}) làm mục tiêu tấn công (Tổng số Nhà Chính: {validTownHallPrefabs.Count}).");
            return attackTarget;
        }

        return null;
    }

    private GameObject SpawnAtPosition(Vector3 position, Quaternion rotation, List<EnemyAI> squadList, List<GameObject> spawnedWaveEnemies = null, bool applyForwardOffset = true)
{
    Transform target = GetOrFindAttackTarget();

    Vector3 finalSpawnPos = position;

    // Chỉ cộng thêm spawnForwardOffset khi sinh tại gốc transform (chưa có điểm Spawn riêng)
    if (applyForwardOffset && spawnForwardOffset > 0f)
    {
        Vector3 spawnDir = (target != null) ? (target.position - position).normalized : (rotation * Vector3.forward);
        spawnDir.y = 0f;
        if (spawnDir.sqrMagnitude < 0.001f) spawnDir = rotation * Vector3.forward;
        spawnDir.Normalize();

        finalSpawnPos = position + spawnDir * spawnForwardOffset;
    }

    Quaternion finalSpawnRot = rotation;
    if (target != null)
    {
        Vector3 dirToTarget = (target.position - finalSpawnPos).normalized;
        dirToTarget.y = 0f;
        if (dirToTarget.sqrMagnitude > 0.001f)
        {
            finalSpawnRot = Quaternion.LookRotation(dirToTarget);
        }
    }

    // Khớp vị trí spawn lên NavMesh gần nhất
    if (UnityEngine.AI.NavMesh.SamplePosition(finalSpawnPos, out UnityEngine.AI.NavMeshHit hit, 5.0f, UnityEngine.AI.NavMesh.AllAreas))
    {
        finalSpawnPos = hit.position;
    }

    Debug.Log($"[EnemySpawn] Spawning enemy at exact position: {finalSpawnPos}");
    GameObject enemy = Instantiate(enemyPrefab, finalSpawnPos, finalSpawnRot);
    if (spawnedWaveEnemies != null) spawnedWaveEnemies.Add(enemy);

    Collider[] spawnerColliders = GetComponentsInChildren<Collider>();
    Collider[] enemyColliders = enemy.GetComponentsInChildren<Collider>();
    if (spawnerColliders != null && enemyColliders != null)
    {
        foreach (var sCol in spawnerColliders)
        {
            foreach (var eCol in enemyColliders)
            {
                if (sCol != null && eCol != null)
                    Physics.IgnoreCollision(sCol, eCol, true);
            }
        }
    }

    EnemyAI enemyAI = enemy.GetComponent<EnemyAI>();
    if (enemyAI != null)
    {
        enemyAI.exitPlayModeWhenNoBuildings = exitPlayModeWhenNoBuildings;

        int curWave = (DayNightManager.HasInstance && DayNightManager.Ins != null) ? DayNightManager.Ins.CurrentWave : 1;
        enemyAI.InitializeWaveArrival(curWave, 3);

        if (target != null)
        {
            enemyAI.villageCenter = target;
        }

        if (squadList != null)
        {
            squadList.Add(enemyAI);
            enemyAI.squadEnemies = squadList;
        }
    }

    UnityEngine.AI.NavMeshAgent agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
    if (agent != null)
    {
        if (agent.isOnNavMesh)
        {
            agent.Warp(finalSpawnPos);
        }
    }

    return enemy;
}
}
