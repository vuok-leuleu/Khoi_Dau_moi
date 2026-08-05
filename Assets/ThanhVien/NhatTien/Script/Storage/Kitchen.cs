using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

/// <summary>
/// Nhà bếp — nơi Worker vào nghỉ ngơi và tiêu thụ lúa.
/// KHÔNG còn phụ thuộc WarehouseStorage — lấy lúa thẳng từ RiceStorage gần nhất
/// (hoặc từ JsonDataManager trực tiếp nếu không tìm thấy RiceStorage).
/// - Có slot giới hạn số worker bên trong cùng lúc.
/// - Khi vào: tiêu lúa, ẩn model worker.
/// - Khi ra: hiện model worker, giải phóng slot.
/// - Hết lúa hoặc đầy slot: worker đứng ngoài phục hồi chậm.
/// Gán tag "Kitchen" để WorkerStamina tự tìm.
/// </summary>
public class Kitchen : MonoBehaviour
{
    [Header("Capacity")]
    [Tooltip("Số worker tối đa được vào bếp cùng lúc")]
    public int maxCapacity = 3;

    [Header("Penta Dev - Civil Workers Setup")]
    public int[] maxWorkersLevels = new int[] { 3, 5, 8 };
    public int maxWorkerPopulation = 0;
    public int currentWorkersCount = 0;

    [Header("Spawn Settings")]
    public GameObject workerPrefab;
    public Transform spawnPoint;
    public int[] spawnAmountPerLevel = new int[] { 1, 1, 2 };

    [Header("Food Settings")]
    [Tooltip("Số lúa tiêu thụ mỗi lần worker vào bếp nghỉ")]
    public int foodPerWorkerRest = 1;

    [Header("References")]
    [Tooltip("RiceStorage gần nhất — tự tìm nếu bỏ trống.")]
    public RiceStorage riceStorage;

    [Tooltip("Vị trí Cửa bếp để Worker đi tới.")]
    public Transform entrancePoint;

    [Tooltip("Các vị trí đứng bên ngoài bếp (tùy chọn).")]
    public Transform[] restSlots;

    [Header("Events UI Connection")]
    public UnityEvent<int, int> onWorkersChanged;

    private List<WorkerStamina> workersInside = new List<WorkerStamina>();
    private int _nextSlotIndex = 0;
    private int currentLevelIndex = 0;

    public int  WorkerCount         => workersInside.Count;
    public bool IsFull              => workersInside.Count >= maxCapacity;
    public bool HasFood             => GetRice() >= foodPerWorkerRest;
    public Vector3 EntrancePosition => entrancePoint != null ? entrancePoint.position : transform.position;

    // ── Lấy số lúa hiện có ──────────────────────────────
    private int GetRice()
    {
        // Ưu tiên 1: RiceStorage (đọc từ JsonDataManager qua property)
        if (riceStorage == null)
            riceStorage = FindFirstRiceStorage();

        if (riceStorage != null)
            return riceStorage.CurrentAmount;

        // Ưu tiên 2: Fallback đọc thẳng JsonDataManager
        return JsonDataManager.Ins != null ? JsonDataManager.Ins.food : 0;
    }

    private RiceStorage FindFirstRiceStorage()
    {
        // Tìm RiceStorage gần nhất trong Scene
        RiceStorage[] all = FindObjectsByType<RiceStorage>(FindObjectsSortMode.None);
        if (all == null || all.Length == 0) return null;

        RiceStorage nearest = null;
        float minDist = float.MaxValue;
        foreach (var rs in all)
        {
            float d = Vector3.Distance(transform.position, rs.transform.position);
            if (d < minDist) { minDist = d; nearest = rs; }
        }
        return nearest;
    }

    // ── Tiêu thụ lúa ────────────────────────────────────
    private bool ConsumeFood(int amount)
    {
        if (riceStorage != null)
        {
            int taken = riceStorage.ConsumeRice(amount);
            return taken >= amount;
        }
        // Fallback: ghi thẳng vào JsonDataManager
        if (JsonDataManager.Ins != null && JsonDataManager.Ins.food >= amount)
        {
            JsonDataManager.Ins.AddFood(-amount);
            return true;
        }
        return false;
    }

    // ── Level setup ──────────────────────────────────────

    public void SetupLevel(int levelIndex)
    {
        currentLevelIndex = levelIndex;
        if (maxWorkersLevels != null && levelIndex < maxWorkersLevels.Length)
        {
            maxWorkerPopulation = maxWorkersLevels[levelIndex];
            onWorkersChanged?.Invoke(currentWorkersCount, maxWorkerPopulation);
        }
        SpawnWorkersForLevel(levelIndex);
    }

    private void SpawnWorkersForLevel(int levelIndex)
    {
        if (workerPrefab == null || spawnAmountPerLevel == null || levelIndex >= spawnAmountPerLevel.Length) return;
        int amountToSpawn = spawnAmountPerLevel[levelIndex];
        Transform point = spawnPoint != null ? spawnPoint : transform;
        for (int i = 0; i < amountToSpawn; i++)
        {
            if (currentWorkersCount >= maxWorkerPopulation) break;
            Instantiate(workerPrefab, point.position, point.rotation);
            currentWorkersCount++;
            Debug.Log($"[Kitchen Spawn] Tạo worker mới tại Cấp {levelIndex + 1}");
        }
        onWorkersChanged?.Invoke(currentWorkersCount, maxWorkerPopulation);
    }

    public void NotifyWorkerRemoved()
    {
        currentWorkersCount = Mathf.Max(0, currentWorkersCount - 1);
        onWorkersChanged?.Invoke(currentWorkersCount, maxWorkerPopulation);
    }

    // ── Worker vào / ra bếp ──────────────────────────────

    /// <summary>
    /// Worker xin vào bếp.
    /// </summary>
    public bool Enter(WorkerStamina worker, out bool consumedFood)
    {
        consumedFood = false;
        if (worker == null) return false;
        if (workersInside.Contains(worker)) return true;
        if (IsFull) return false;

        workersInside.Add(worker);

        if (HasFood)
        {
            consumedFood = ConsumeFood(foodPerWorkerRest);
            Debug.Log($"[Kitchen] {worker.name} vào bếp nghỉ ngơi {(consumedFood ? "(đã ăn lúa)" : "(nhịn đói)")}. " +
                      $"Slot: {workersInside.Count}/{maxCapacity}");
        }
        else
        {
            Debug.Log($"[Kitchen] {worker.name} vào nhà trú ẩn nhưng nhịn đói (hồi stamina chậm). Kho lúa không đủ.");
        }

        return true;
    }

    public void Exit(WorkerStamina worker)
    {
        if (worker == null) return;
        if (workersInside.Remove(worker))
            Debug.Log($"[Kitchen] {worker.name} no nê đi làm. Slot còn: {maxCapacity - workersInside.Count}/{maxCapacity}");
    }

    /// <summary>
    /// Round-robin — tránh nhiều worker chồng lên cùng 1 slot.
    /// </summary>
    public Vector3 GetRestPosition()
    {
        if (restSlots != null && restSlots.Length > 0)
        {
            for (int i = 0; i < restSlots.Length; i++)
            {
                int idx = (_nextSlotIndex + i) % restSlots.Length;
                if (restSlots[idx] != null)
                {
                    _nextSlotIndex = (idx + 1) % restSlots.Length;
                    return restSlots[idx].position;
                }
            }
        }
        return EntrancePosition;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(EntrancePosition, 2f);
    }
}