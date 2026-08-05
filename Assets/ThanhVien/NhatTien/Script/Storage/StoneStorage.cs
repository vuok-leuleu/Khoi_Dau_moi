using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Kho Đá — kho CHÍNH, ghi thẳng vào JsonDataManager (nguồn thật duy nhất).
/// Worker đào đá nộp vào đây → ghi lên HUD ngay lập tức.
/// Không còn WorkerCarrier / WarehouseStorage làm trung gian.
/// </summary>
public class StoneStorage : MonoBehaviour
{
    [Header("Storage Settings")]
    public int maxCapacity = 9999;

    [Header("Penta Dev - Civil Workers Setup")]
    [Tooltip("Cấu hình số lượng worker tối đa qua từng level")]
    public int[] maxWorkersLevels = new int[] { 2, 4, 6 };
    public int currentWorkersCount = 0;

    [Header("Spawn Settings")]
    public GameObject workerPrefab;
    public Transform spawnPoint;
    [Tooltip("Số worker sẽ spawn tự động tương ứng khi lên từng level")]
    public int[] spawnAmountPerLevel = new int[] { 1, 1, 2 };

    [Header("Events")]
    public UnityEvent      onStorageFull;
    public UnityEvent<int> onStoneAdded;
    public UnityEvent<int, int> onWorkersChanged;
    public UnityEvent<int> onCapacityChanged;

    private int currentLevelIndex = 0;

    void Awake()
    {
        if (maxCapacity < 9999) maxCapacity = 9999;
    }

    // ===== PROPERTIES — đọc thẳng từ JsonDataManager =====
    public int  CurrentAmount => JsonDataManager.Ins != null ? JsonDataManager.Ins.stone : 0;
    public int  MaxCapacity   => maxCapacity;
    public bool IsFull        => CurrentAmount >= maxCapacity;
    public bool IsEmpty       => CurrentAmount <= 0;
    public int  MaxWorkers    => (maxWorkersLevels != null && currentLevelIndex < maxWorkersLevels.Length)
                                  ? maxWorkersLevels[currentLevelIndex] : 0;

    // ===== SETUP LEVEL =====

    public void SetupLevel(int levelIndex)
    {
        currentLevelIndex = levelIndex;
        if (maxWorkersLevels != null && levelIndex < maxWorkersLevels.Length)
            onWorkersChanged?.Invoke(currentWorkersCount, maxWorkersLevels[levelIndex]);
        SpawnWorkersForLevel(levelIndex);
    }

    private void SpawnWorkersForLevel(int levelIndex)
    {
        if (workerPrefab == null || spawnAmountPerLevel == null || levelIndex >= spawnAmountPerLevel.Length) return;
        int amountToSpawn = spawnAmountPerLevel[levelIndex];
        Transform point = spawnPoint != null ? spawnPoint : transform;
        for (int i = 0; i < amountToSpawn; i++)
        {
            if (currentWorkersCount >= MaxWorkers) break;
            Instantiate(workerPrefab, point.position, point.rotation);
            currentWorkersCount++;
            Debug.Log($"[StoneStorage Spawn] Tạo worker mới tại Cấp {levelIndex + 1}");
        }
        onWorkersChanged?.Invoke(currentWorkersCount, MaxWorkers);
    }

    // ===== PUBLIC API =====

    public int AddStone(int amount = 1)
    {
        if (IsFull)
        {
            Debug.Log($"[StoneStorage] '{name}' đã đầy! ({CurrentAmount}/{maxCapacity})");
            return 0;
        }
        int canAdd = Mathf.Min(amount, maxCapacity - CurrentAmount);
        SyncToManager(canAdd);
        Debug.Log($"[StoneStorage] '{name}' +{canAdd} đá → {CurrentAmount}/{maxCapacity}");
        onStoneAdded?.Invoke(CurrentAmount);
        if (IsFull) onStorageFull?.Invoke();
        return canAdd;
    }

    public int TakeStone(int amount = 1)
    {
        if (IsEmpty)
        {
            Debug.LogWarning($"[StoneStorage] '{name}' Kho trống!");
            return 0;
        }
        int canTake = Mathf.Min(amount, CurrentAmount);
        SyncToManager(-canTake);
        Debug.Log($"[StoneStorage] '{name}' -{canTake} đá → {CurrentAmount}/{maxCapacity}");
        return canTake;
    }

    public void ClearStorage()
    {
        SyncToManager(-CurrentAmount);
        Debug.Log($"[StoneStorage] '{name}' Kho đã làm trống.");
    }

    // ===== INTERNAL =====

    private void SyncToManager(int delta)
    {
        if (JsonDataManager.Ins == null)
        {
            Debug.LogError("[StoneStorage] Không tìm thấy JsonDataManager.Ins!");
            return;
        }
        JsonDataManager.Ins.AddStone(delta);
    }

    // ===== GIZMO =====
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.55f, 0.55f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, 1.5f);
    }
}