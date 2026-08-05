using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;

public class WorkerCarryStone : MonoBehaviour
{
    public Transform    handPoint;
    public NavMeshAgent agent;
    public Transform stoneStoragePoint;   

    private StonePickup  currentStone;
    private StoneStorage stoneStorage;
    private WorkerStamina workerStamina;

    void Start()
    {
        workerStamina = GetComponent<WorkerStamina>();
        stoneStorage = FindNearestStoneStorage(out Transform point);
        if (stoneStorage != null) stoneStoragePoint = point;
    }

    void OnDisable()
    {
        if (currentStone != null)
        {
            ObjectPool pool = currentStone.pool;
            if (pool != null && currentStone.gameObject.activeInHierarchy) 
                pool.ReturnObject(currentStone.gameObject);
            else 
                Destroy(currentStone.gameObject);
            
            currentStone = null;

            // FIX: đảm bảo Stamina không bị kẹt ở trạng thái "đang ôm hàng"
            // (nếu không, isCarryingResources/isReturnPending có thể bị kẹt true vĩnh viễn
            // khi vật phẩm bị huỷ đột ngột, ví dụ do cơ chế chống-kẹt ép reset carrySystem)
            if (workerStamina != null) workerStamina.OnResourcesDeposited();
        }
    }

    /// <summary>
    /// Quét tất cả StoneStorage bằng Component (không dùng Tag), chọn kho GẦN NHẤT còn chỗ.
    /// Nếu tất cả đều đầy, trả về kho gần nhất (dù đầy) để không bị null.
    /// </summary>
    StoneStorage FindNearestStoneStorage(out Transform chosenPoint)
    {
        chosenPoint = null;

        StoneStorage[] candidates = FindObjectsByType<StoneStorage>(FindObjectsSortMode.None);

        if (candidates == null || candidates.Length == 0)
        {
            if (stoneStoragePoint != null)
            {
                StoneStorage ss = stoneStoragePoint.GetComponent<StoneStorage>() 
                               ?? stoneStoragePoint.GetComponentInParent<StoneStorage>() 
                               ?? stoneStoragePoint.GetComponentInChildren<StoneStorage>();
                if (ss != null) { chosenPoint = stoneStoragePoint; return ss; }
            }
            return null;
        }

        StoneStorage bestNotFull = null;
        Transform   bestNotFullPoint = null;
        float       bestNotFullDist  = Mathf.Infinity;

        StoneStorage fallback = null;
        Transform   fallbackPoint = null;
        float       fallbackDist  = Mathf.Infinity;

        foreach (StoneStorage ss in candidates)
        {
            Transform dp = FindDeliveryPoint(ss.transform);
            float d = Vector3.Distance(transform.position, dp.position);

            if (!ss.IsFull && d < bestNotFullDist)
            {
                bestNotFull      = ss;
                bestNotFullPoint = dp;
                bestNotFullDist  = d;
            }
            if (d < fallbackDist)
            {
                fallback      = ss;
                fallbackPoint = dp;
                fallbackDist  = d;
            }
        }

        if (bestNotFull != null) { chosenPoint = bestNotFullPoint; return bestNotFull; }
        chosenPoint = fallbackPoint;
        return fallback;
    }

    /// <summary>
    /// Tìm child Transform tên "DeliveryPoint" bên trong kho (cửa kho, nơi worker thực sự đi tới).
    /// Quét từ kho hiện tại cho tới root cha để đảm bảo không bị sót DeliveryPoint.
    /// </summary>
    Transform FindDeliveryPoint(Transform storageRoot)
    {
        if (storageRoot == null) return null;

        Transform dp = storageRoot.Find("DeliveryPoint");
        if (dp != null) return dp;

        foreach (Transform child in storageRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "DeliveryPoint") return child;
        }

        if (storageRoot.root != null)
        {
            foreach (Transform child in storageRoot.root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "DeliveryPoint") return child;
            }
        }

        return storageRoot;
    }

    public bool IsCarrying() => currentStone != null;

    public void PickUpFakeItemForLoad()
    {
        GameObject fakeItem = new GameObject("FakeStone_Loaded");
        fakeItem.transform.SetParent(handPoint);
        fakeItem.transform.localPosition = Vector3.zero;
        currentStone = fakeItem.AddComponent<StonePickup>();
    }

    public void PickupStone(StonePickup stone)
    {
        if (stone == null || stone.IsTaken()) return;
        stone.MarkTaken();
        currentStone = stone;
        currentStone.Pickup(handPoint);
        agent.ResetPath();

        // Chọn kho gần nhất còn chỗ tại thời điểm nhặt xong (1 lần duy nhất cho chuyến này)
        StoneStorage chosen = FindNearestStoneStorage(out Transform point);
        if (chosen != null)
        {
            stoneStorage = chosen;
            stoneStoragePoint = point;
        }

        if (workerStamina != null) workerStamina.isCarryingResources = true;
    }

    public bool MoveToStorage()
    {
        if (currentStone == null) return false;

        // Retry tìm kho nếu chưa có hoặc kho đã đầy
        if (stoneStorage == null || stoneStoragePoint == null || stoneStorage.IsFull)
        {
            StoneStorage found = FindNearestStoneStorage(out Transform point);
            if (found != null) { stoneStorage = found; stoneStoragePoint = point; }
        }

        if (stoneStoragePoint == null || !agent.isOnNavMesh)
        {
            // Chưa có kho — dừng agent, chờ đặt kho xong rồi tự chạy lại
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            return false;
        }

        agent.isStopped = false;
        agent.SetDestination(stoneStoragePoint.position);
        return true;
    }

    public bool TryDeposit()
    {
        if (currentStone == null) return false;
        if (stoneStorage == null) return false; // Chưa có kho — im lặng chờ, không log lỗi
        if (stoneStorage.IsFull) return false;

        ObjectPool pool = currentStone.pool;
        if (pool != null) pool.ReturnObject(currentStone.gameObject);
        else currentStone.gameObject.SetActive(false);

        currentStone = null;
        stoneStorage.AddStone(1);

        if (workerStamina != null) workerStamina.OnResourcesDeposited();

        return true;
    }
}