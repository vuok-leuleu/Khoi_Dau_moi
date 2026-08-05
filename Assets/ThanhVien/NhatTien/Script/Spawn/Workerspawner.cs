using UnityEngine;
using UnityEngine.AI;

/*
 * WorkerSpawner.cs
 * Folder: Scripts/Spawning/
 * Dự án: KHẨN HOANG (PENTA DEV)
 *
 * CHỨC NĂNG:
 * Spawn worker từ prefab và TỰ ĐỘNG setup toàn bộ component/reference cần thiết,
 * để bạn không còn phải kéo-thả tay trên Scene cho từng worker nữa.
 *
 * Script này KHÔNG sửa bất kỳ dòng nào trong House / WorkerStamina / WorkerCarryItem /
 * WorkerFindTree / WorkerEnemyFlee. Nó chỉ đứng bên ngoài, gọi GetComponent/AddComponent
 * và gán field public sẵn có của các script đó sau khi Instantiate.
 *
 * CÁCH DÙNG:
 * 1. Gắn script này lên 1 GameObject quản lý spawn (vd "GameManager" hoặc "WorkerSpawner").
 * 2. Kéo 3 prefab (treeWorkerPrefab / riceWorkerPrefab / stoneWorkerPrefab) vào Inspector.
 * 3. Gọi WorkerSpawner.Instance.SpawnWorker(WorkerType.Tree, originPosition) từ bất kỳ đâu
 *    (vd từ HouseSpawnPanel khi bấm nút UI).
 *
 * YÊU CẦU VỀ PREFAB (để auto-wire hoạt động đúng):
 * - Prefab đã có: NavMeshAgent, Animator, WorkerStamina, WorkerCarryItem,
 *   và ĐÚNG MỘT trong (WorkerFindTree / WorkerFindRice / WorkerFindStone).
 * - Prefab có 1 child Transform tên "HandPoint" (dùng làm handPoint cho WorkerCarryItem).
 * - Nếu muốn worker có khả năng bỏ chạy khi gặp Enemy, tick addEnemyFlee = true,
 *   script sẽ tự AddComponent<WorkerEnemyFlee> nếu prefab chưa có sẵn.
 */
public class WorkerSpawner : MonoBehaviour
{
    public static WorkerSpawner Instance { get; private set; }

    public enum WorkerType { Tree, Rice, Stone }

    [Header("Prefabs theo loại tài nguyên")]
    public GameObject treeWorkerPrefab;
    public GameObject riceWorkerPrefab;
    public GameObject stoneWorkerPrefab;

    [Header("Auto-Setup Options")]
    [Tooltip("Tên child Transform dùng làm điểm cầm đồ trên tay. Phải khớp tên trong prefab.")]
    public string handPointChildName = "HandPoint";

    [Tooltip("Nếu bật, worker spawn ra sẽ tự có thêm WorkerEnemyFlee (nếu prefab chưa gắn sẵn).")]
    public bool addEnemyFlee = true;

    [Tooltip("Bán kính rải vị trí spawn ngẫu nhiên quanh điểm gốc (House).")]
    public float defaultSpawnScatterRadius = 2.5f;

    [Tooltip("Số lần thử tìm vị trí hợp lệ trên NavMesh trước khi fallback về điểm gốc.")]
    public int maxSpawnPositionAttempts = 8;

    void Awake()
    {
        // Cho phép nhiều instance nếu bạn cố tình đặt nhiều spawner ở nhiều khu vực,
        // nhưng vẫn giữ 1 Instance mặc định để gọi nhanh từ UI.
        if (Instance == null) Instance = this;
    }

    /// <summary>
    /// Spawn 1 worker đúng loại, quanh vị trí gốc (thường là House.EntrancePosition),
    /// rải ngẫu nhiên trong bán kính defaultSpawnScatterRadius, và tự setup toàn bộ.
    /// </summary>
    public GameObject SpawnWorker(WorkerType type, Vector3 originPosition)
    {
        return SpawnWorker(type, originPosition, defaultSpawnScatterRadius);
    }

    /// <summary>
    /// Overload cho phép chỉ định bán kính rải vị trí riêng cho lần spawn này.
    /// </summary>
    public GameObject SpawnWorker(WorkerType type, Vector3 originPosition, float scatterRadius)
    {
        GameObject prefab = GetPrefabFor(type);
        if (prefab == null)
        {
            Debug.LogError($"[WorkerSpawner] Chưa gán prefab cho loại {type} trong Inspector.");
            return null;
        }

        Vector3 spawnPos = ResolveSpawnPosition(originPosition, scatterRadius);

        GameObject worker = Instantiate(prefab, spawnPos, Quaternion.identity);
        SetupWorker(worker, type, originPosition);
        
        if (WorkerManager.Ins != null)
        {
            WorkerManager.Ins.RegisterWorker(worker, type.ToString());
        }

        // Tự động xóa khỏi Manager khi worker bị Destroy (vd: về nhà hoặc bị quái giết)
        var destroyer = worker.AddComponent<WorkerDestroyNotifier>();
        destroyer.workerType = type.ToString();

        return worker;
    }

    GameObject GetPrefabFor(WorkerType type)
    {
        switch (type)
        {
            case WorkerType.Tree:  return treeWorkerPrefab;
            case WorkerType.Rice:  return riceWorkerPrefab;
            case WorkerType.Stone: return stoneWorkerPrefab;
            default: return null;
        }
    }

    /// <summary>
    /// Tìm 1 điểm ngẫu nhiên trong bán kính quanh originPosition mà nằm trên NavMesh.
    /// Nếu thử nhiều lần không ra, fallback về chính originPosition.
    /// </summary>
    Vector3 ResolveSpawnPosition(Vector3 originPosition, float scatterRadius)
    {
        for (int i = 0; i < maxSpawnPositionAttempts; i++)
        {
            Vector2 rand = Random.insideUnitCircle * scatterRadius;
            Vector3 candidate = originPosition + new Vector3(rand.x, 0f, rand.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, scatterRadius + 1f, NavMesh.AllAreas))
                return hit.position;
        }

        // Fallback: thử chính originPosition trên NavMesh, nếu vẫn fail thì dùng nguyên
        if (NavMesh.SamplePosition(originPosition, out NavMeshHit originHit, scatterRadius + 1f, NavMesh.AllAreas))
            return originHit.position;

        return originPosition;
    }

    /// <summary>
    /// Đây là phần cốt lõi: tự động nối toàn bộ reference giữa các component
    /// trên worker vừa spawn, thay cho việc bạn phải kéo tay trong Inspector.
    /// Hỗ trợ đầy đủ 3 loại worker: Tree (CarryItem), Rice (CarryRice), Stone (CarryStone), Carrier.
    /// </summary>
    void SetupWorker(GameObject worker, WorkerType type, Vector3 nearSearchOrigin)
    {
        NavMeshAgent agent = worker.GetComponent<NavMeshAgent>();
        if (agent == null) agent = worker.AddComponent<NavMeshAgent>();

        // Đặt agent đúng vị trí trên NavMesh ngay khi spawn để tránh warp lỗi
        if (!agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(worker.transform.position, out hit, 5f, NavMesh.AllAreas))
                agent.Warp(hit.position);
        }

        Animator animator = worker.GetComponent<Animator>();

        WorkerStamina stamina = worker.GetComponent<WorkerStamina>();
        if (stamina == null) stamina = worker.AddComponent<WorkerStamina>();

        // --- Tự tìm House/Kitchen gần nhất trong scene và gán cho WorkerStamina ---
        // Chỉ tự tìm khi prefab CHƯA gán sẵn trong Inspector — tránh ghi đè giá trị đã đúng.
        if (stamina.house == null)    stamina.house    = FindNearestHouse(nearSearchOrigin);
        if (stamina.kitchen == null)  stamina.kitchen  = FindNearestKitchen(nearSearchOrigin);

        // --- Tìm/tạo HandPoint dùng chung cho tất cả carry system ---
        Transform handPoint = ResolveHandPoint(worker);

        // =====================================================================
        // WORKER GỖ — WorkerFindTree dùng WorkerCarryItem
        // =====================================================================
        WorkerFindTree findTree = worker.GetComponent<WorkerFindTree>();
        if (findTree != null)
        {
            WorkerCarryItem carryItem = worker.GetComponent<WorkerCarryItem>();
            if (carryItem == null) carryItem = worker.AddComponent<WorkerCarryItem>();

            if (carryItem.handPoint == null) carryItem.handPoint = handPoint;
            carryItem.agent = agent;

            findTree.agent       = agent;
            findTree.carrySystem = carryItem;
            findTree.animator    = animator;
            findTree.stamina     = stamina;
        }

        // =====================================================================
        // WORKER LÚA — WorkerFindRice dùng WorkerCarryRice
        // =====================================================================
        WorkerFindRice findRice = worker.GetComponent<WorkerFindRice>();
        if (findRice != null)
        {
            WorkerCarryRice carryRice = worker.GetComponent<WorkerCarryRice>();
            if (carryRice == null) carryRice = worker.AddComponent<WorkerCarryRice>();

            if (carryRice.handPoint == null) carryRice.handPoint = handPoint;
            carryRice.agent = agent;

            findRice.agent       = agent;
            findRice.carrySystem = carryRice;
            findRice.animator    = animator;
            findRice.stamina     = stamina;
        }

        // =====================================================================
        // WORKER ĐÁ — WorkerFindStone dùng WorkerCarryStone
        // =====================================================================
        WorkerFindStone findStone = worker.GetComponent<WorkerFindStone>();
        if (findStone != null)
        {
            WorkerCarryStone carryStone = worker.GetComponent<WorkerCarryStone>();
            if (carryStone == null) carryStone = worker.AddComponent<WorkerCarryStone>();

            if (carryStone.handPoint == null) carryStone.handPoint = handPoint;
            carryStone.agent = agent;

            findStone.agent       = agent;
            findStone.carrySystem = carryStone;
            findStone.animator    = animator;
            findStone.stamina     = stamina;
        }

        // --- WorkerEnemyFlee (tùy chọn) ---
        if (addEnemyFlee)
        {
            WorkerEnemyFlee flee = worker.GetComponent<WorkerEnemyFlee>();
            if (flee == null) flee = worker.AddComponent<WorkerEnemyFlee>();
            flee.house              = stamina.house;
            flee.animator           = animator;
            flee.workerModel        = stamina.workerModel;
            flee.extraModelsToHide  = stamina.extraModelsToHide;
        }
    }

    /// <summary>
    /// Tìm hoặc tạo child Transform làm điểm cầm đồ trên tay worker.
    /// Ưu tiên child tên handPointChildName, fallback tạo mới nếu không có.
    /// </summary>
    Transform ResolveHandPoint(GameObject worker)
    {
        Transform handPoint = FindDeepChild(worker.transform, handPointChildName);
        if (handPoint != null) return handPoint;

        Debug.LogWarning($"[WorkerSpawner] Không tìm thấy child '{handPointChildName}' trên prefab {worker.name}. " +
                          $"Tạo tạm 1 điểm rỗng tại gốc worker, bạn nên thêm child này vào prefab để đúng vị trí tay.");
        GameObject fallbackHand = new GameObject(handPointChildName);
        fallbackHand.transform.SetParent(worker.transform);
        fallbackHand.transform.localPosition = Vector3.up * 1.2f;
        return fallbackHand.transform;
    }

    House FindNearestHouse(Vector3 fromPosition)
    {
        House[] houses = FindObjectsByType<House>(FindObjectsSortMode.None);
        House best = null;
        float bestDist = Mathf.Infinity;
        foreach (var h in houses)
        {
            float d = Vector3.Distance(fromPosition, h.transform.position);
            if (d < bestDist) { bestDist = d; best = h; }
        }
        return best;
    }

    Kitchen FindNearestKitchen(Vector3 fromPosition)
    {
        Kitchen[] kitchens = FindObjectsByType<Kitchen>(FindObjectsSortMode.None);
        Kitchen best = null;
        float bestDist = Mathf.Infinity;
        foreach (var k in kitchens)
        {
            float d = Vector3.Distance(fromPosition, k.transform.position);
            if (d < bestDist) { bestDist = d; best = k; }
        }
        return best;
    }

    /// <summary>
    /// Tìm child Transform theo tên ở bất kỳ độ sâu nào (không chỉ con trực tiếp).
    /// </summary>
    Transform FindDeepChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name) return child;
        }
        return null;
    }
}

public class WorkerDestroyNotifier : MonoBehaviour
{
    public string workerType;
    private bool isQuitting = false;

    void OnApplicationQuit()
    {
        isQuitting = true;
    }

    void OnDestroy()
    {
        if (isQuitting) return;

        // Dùng FindObjectOfType thay vì .Ins để tránh việc class Singleton 
        // tự động đẻ ra 1 object rác lúc game đang tắt.
        var manager = FindObjectOfType<WorkerManager>();
        if (manager != null)
        {
            manager.UnregisterWorker(gameObject);
        }
    }
}