using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;

public class WorkerCarryRice : MonoBehaviour
{
    public Transform    handPoint;
    public NavMeshAgent agent;
    public Transform riceStoragePoint;   

    private RicePickup   currentRice;
    private RiceStorage  riceStorage;
    private WorkerStamina workerStamina;

    void Start()
    {
        workerStamina = GetComponent<WorkerStamina>();
        riceStorage = FindNearestRiceStorage(out Transform point);
        if (riceStorage != null) riceStoragePoint = point;
    }

    void OnDisable()
    {
        if (currentRice != null)
        {
            ObjectPool pool = currentRice.pool;
            if (pool != null && currentRice.gameObject.activeInHierarchy)
                pool.ReturnObject(currentRice.gameObject);
            else
                Destroy(currentRice.gameObject);
            
            currentRice = null;

            // FIX: đảm bảo Stamina không bị kẹt ở trạng thái "đang ôm hàng"
            // (nếu không, isCarryingResources/isReturnPending có thể bị kẹt true vĩnh viễn
            // khi vật phẩm bị huỷ đột ngột, ví dụ do cơ chế chống-kẹt ép reset carrySystem)
            if (workerStamina != null) workerStamina.OnResourcesDeposited();
        }
    }

    /// <summary>
    /// Quét tất cả RiceStorage bằng Component (không dùng Tag), chọn kho GẦN NHẤT còn chỗ.
    /// Nếu tất cả đều đầy, trả về kho gần nhất (dù đầy) để không bị null.
    /// </summary>
    RiceStorage FindNearestRiceStorage(out Transform chosenPoint)
    {
        chosenPoint = null;

        RiceStorage[] candidates = FindObjectsByType<RiceStorage>(FindObjectsSortMode.None);

        if (candidates == null || candidates.Length == 0)
        {
            // Fallback: dùng điểm được gán thủ công trong Inspector
            if (riceStoragePoint != null)
            {
                RiceStorage rs = riceStoragePoint.GetComponent<RiceStorage>()
                              ?? riceStoragePoint.GetComponentInParent<RiceStorage>()
                              ?? riceStoragePoint.GetComponentInChildren<RiceStorage>();
                if (rs != null) { chosenPoint = riceStoragePoint; return rs; }
            }
            return null;
        }

        RiceStorage bestNotFull = null;
        Transform   bestNotFullPoint = null;
        float       bestNotFullDist  = Mathf.Infinity;

        RiceStorage fallback = null;
        Transform   fallbackPoint = null;
        float       fallbackDist  = Mathf.Infinity;

        foreach (RiceStorage rs in candidates)
        {
            Transform dp = FindDeliveryPoint(rs.transform);
            float d = Vector3.Distance(transform.position, dp.position);

            if (!rs.IsFull && d < bestNotFullDist)
            {
                bestNotFull      = rs;
                bestNotFullPoint = dp;
                bestNotFullDist  = d;
            }
            if (d < fallbackDist)
            {
                fallback      = rs;
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

        // Quét ngược lên root cây phân cấp phòng trường hợp DeliveryPoint ở nhánh khác
        if (storageRoot.root != null)
        {
            foreach (Transform child in storageRoot.root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "DeliveryPoint") return child;
            }
        }

        return storageRoot;
    }

    public bool IsCarrying() => currentRice != null;

    public void PickUpFakeItemForLoad()
    {
        GameObject fakeItem = new GameObject("FakeRice_Loaded");
        fakeItem.transform.SetParent(handPoint);
        fakeItem.transform.localPosition = Vector3.zero;
        currentRice = fakeItem.AddComponent<RicePickup>();
    }

    public void PickupRice(RicePickup rice)
    {
        if (rice == null || rice.IsTaken()) return;
        rice.MarkTaken();
        currentRice = rice;
        currentRice.Pickup(handPoint);
        agent.ResetPath();

        // Chọn kho gần nhất còn chỗ tại thời điểm nhặt xong (1 lần duy nhất cho chuyến này)
        RiceStorage chosen = FindNearestRiceStorage(out Transform point);
        if (chosen != null)
        {
            riceStorage = chosen;
            riceStoragePoint = point;
        }

        if (workerStamina != null) workerStamina.isCarryingResources = true;
    }

    public bool MoveToStorage() 
    {
        if (currentRice == null) return false;

        // Retry tìm kho nếu chưa có hoặc kho đã đầy
        if (riceStorage == null || riceStoragePoint == null || riceStorage.IsFull)
        {
            RiceStorage found = FindNearestRiceStorage(out Transform point);
            if (found != null) { riceStorage = found; riceStoragePoint = point; }
        }

        if (riceStoragePoint == null || !agent.isOnNavMesh)
        {
            // Chưa có kho — dừng agent, chờ đặt kho xong rồi tự chạy lại
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            return false;
        }

        agent.isStopped = false;
        agent.SetDestination(riceStoragePoint.position);
        return true;
    }

    public bool TryDeposit()
    {
        if (currentRice == null) return false;
        if (riceStorage == null) return false; // Chưa có kho — im lặng chờ, không log lỗi
        if (riceStorage.IsFull) return false;

        ObjectPool pool = currentRice.pool;
        if (pool != null) pool.ReturnObject(currentRice.gameObject);
        else currentRice.gameObject.SetActive(false);

        currentRice = null;
        riceStorage.AddRice(1);

        if (workerStamina != null) workerStamina.OnResourcesDeposited();

        return true;
    }
}