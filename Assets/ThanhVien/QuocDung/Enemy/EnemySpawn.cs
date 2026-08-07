using UnityEngine;
using System.Collections.Generic;

public class EnemySpawn : MonoBehaviour
{
    public static EnemySpawn Ins { get; private set; }

    [Header("Spawn Settings")]
    [SerializeField] private GameObject enemyPrefab;
    public GameObject EnemyPrefab => enemyPrefab;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private Transform[] spawnPoints;

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
        // 1. Nếu đã hoàn thành Tutorial (TutorialCompleted = 1) -> Trả về false để Enemy có thể spawn bình thường
        if (PlayerPrefs.GetInt("TutorialCompleted", 0) == 1) return false;

        // 2. Kiểm tra CampaignTutorialManager hiện tại
        if (CampaignTutorialManager.Ins != null)
        {
            if (CampaignTutorialManager.Ins.currentStage == TutorialStage.Stage7_Complete) return false;
            if (CampaignTutorialManager.Ins.currentStage != TutorialStage.None) return true;
        }

        // 3. Kiểm tra các TutorialManager legacy khác
        TutorialManager tut = Object.FindFirstObjectByType<TutorialManager>();
        if (tut != null && tut.gameObject.activeInHierarchy && tut.enabled) return true;

        return false;
    }

    private void Awake()
    {
        Ins = this;
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
        // Sau khi hoàn thành Tutorial, tự động Spawn đợt Quái mới cứ sau 3 Wave (xuất hiện ở Wave 3, Wave 6, Wave 9, Wave 12, Wave 15...)
        if (waveIndex > 0 && (waveIndex % 3 == 0 || (waveIndex - 1) % 3 == 0 || waveIndex == 1))
        {
            if (!IsTutorialActive())
            {
                Debug.Log($"[EnemySpawn] 🔥 DayNightManager phát sự kiện Wave {waveIndex}! Tự động Spawn đợt Quái mới sau 3 Wave.");
                SpawnEnemy();
            }
        }
    }

    private void Start()
    {
        SubscribeToWaveEvents();

        // 1. Phục hồi các đợt quái chưa tham chiến từ trận đánh trước (nếu có)
        if (BattleData.SavedRemainingEnemies != null && BattleData.SavedRemainingEnemies.Count > 0)
        {
            BattleData.RestoreRemainingEnemies();
        }
        else if (spawnOnStart)
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

    private void Update()
    {
        // Đảm bảo event listener luôn được kết nối nếu DayNightManager khởi tạo trễ
        if (DayNightManager.HasInstance && DayNightManager.Ins != null)
        {
            SubscribeToWaveEvents();
        }
        StopWaveSpawning();
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
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            foreach (Transform p in spawnPoints)
            {
                if (p != null) sources.Add(p);
            }
        }

        if (sources.Count == 0)
        {
            sources.Add(transform);
        }

        List<GameObject> spawnedWaveEnemies = new List<GameObject>();

        foreach (Transform source in sources)
        {
            List<EnemyAI> squadList = new List<EnemyAI>();

            if (useGridSpawn)
            {
                SpawnGridAt(source.position, source.rotation, squadList, spawnedWaveEnemies);
            }
            else
            {
                SpawnAtPosition(source.position, source.rotation, squadList, spawnedWaveEnemies);
            }
        }

        // Gắn Mũi Tên & Cảnh Báo cho con Thủ Lĩnh (Lead Enemy) - Luôn nằm ở HÀNG ĐẦU VÀ VỊ TRÍ Ở GIỮA
        if (showAttackButton && spawnedWaveEnemies.Count > 0)
        {
            Quaternion spawnRot = (sources.Count > 0 && sources[0] != null) ? sources[0].rotation : transform.rotation;
            Vector3 spawnCenter = (sources.Count > 0 && sources[0] != null) ? sources[0].position : transform.position;

            GameObject leadObj = GetFrontCenterEnemy(spawnedWaveEnemies, spawnRot, spawnCenter);
            Transform leadEnemy = (leadObj != null) ? leadObj.transform : spawnedWaveEnemies[0].transform;

            // Đảm bảo con Thủ Lĩnh đứng ở vị trí index 0 trong squadEnemies của các quái cùng Wave
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

    private GameObject SpawnAtPosition(Vector3 position, Quaternion rotation, List<EnemyAI> squadList, List<GameObject> spawnedWaveEnemies = null)
    {
        Debug.Log($"[EnemySpawn] Spawning enemy at position: {position}");
        GameObject enemy = Instantiate(enemyPrefab, position, rotation);
        if (spawnedWaveEnemies != null) spawnedWaveEnemies.Add(enemy);

        EnemyAI enemyAI = enemy.GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.exitPlayModeWhenNoBuildings = exitPlayModeWhenNoBuildings;

            int curWave = (DayNightManager.HasInstance && DayNightManager.Ins != null) ? DayNightManager.Ins.CurrentWave : 1;
            enemyAI.InitializeWaveArrival(curWave, 3);

            if (attackTarget != null)
            {
                enemyAI.villageCenter = attackTarget;
            }

            if (squadList != null)
            {
                squadList.Add(enemyAI);
                enemyAI.squadEnemies = squadList;
            }
        }
        return enemy;
    }
}
