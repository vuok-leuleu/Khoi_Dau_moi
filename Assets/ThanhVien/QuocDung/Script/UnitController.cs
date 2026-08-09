using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

// 1. Định nghĩa các Trạng thái (State Machine)
public enum UnitState
{
    Idle,
    Moving,
    Attacking
}

public enum AttackMode
{
    Melee,
    Ranged
}

[RequireComponent(typeof(NavMeshAgent))]
public class UnitController : MonoBehaviour
{
    private static readonly Dictionary<int, UnitController> claimedEnemies = new Dictionary<int, UnitController>();

    // 2. Khai báo các thành phần cần thiết
    private NavMeshAgent agent;
    private HPSoldier hpSoldier;

    [Header("Animation Config")]
    [SerializeField] private Animator animator;
    [SerializeField] private string moveBoolParam = "IsMove";
    [SerializeField] private string attackBoolParam = "IsAttack";
    [SerializeField] private string shootBoolParam = "IsShoot";

    [Header("Combat Config")]
    [SerializeField] float attackDamage = 15f;
    [SerializeField] float attackRate = 1.5f;
    [SerializeField] GameObject projectilePrefab;
    [SerializeField] Transform firePoint;
    [SerializeField] float projectileSpeed = 15f;
    [SerializeField] float projectileSpawnDelay = 0.35f;
    private float nextAttackTime;
    public UnitState currentState = UnitState.Idle;
    public GameObject currentTarget;
    public float scanFrequency = 0.25f;
    [SerializeField] AttackMode attackMode = AttackMode.Melee;
    public AttackMode AttackMode => attackMode;
    [SerializeField] float attackRange = 2f;
    [SerializeField] float rangedAttackRange = 5f;
    
    [Header("Area Scan Config")]
    [SerializeField] float detectRadius = 25f;
    [SerializeField] LayerMask enemyLayer = ~0;
    
    // Giữ lại các biến cũ để tránh mất cấu hình ở Inspector (tương thích ngược)
    [SerializeField] LayerMask targetLayerMask = ~0;
    [SerializeField] float raycastDistance = 6f;
    [SerializeField] float raycastHeight = 0.8f;
    [SerializeField] float visionAngle = 180f;
    [SerializeField] int visionRayCount = 7;
    
    [SerializeField] float destinationUpdateThreshold = 0.5f;
    [SerializeField] string enemyTag = "Enemy";

    // Internal caches
    private Coroutine lowFreqCoroutine;
    private Vector3 lastDestinationPos = Vector3.positiveInfinity;
    private int currentTargetInstanceId = 0;

    [Header("Warning Response State")]
    [SerializeField] private bool autoAggro = false; // Đặt false để lính đứng yên, không tự chạy tới đánh khi quái xuất hiện
    [SerializeField] private bool isRespondingToWarning = false;
    [SerializeField] private bool isReturning = false;
    [SerializeField] private Vector3 returnPosition;
    [SerializeField] private Vector3 warningPosition;

    [Header("Expedition Wave Marching")]
    public bool isExpeditionMarching = false;
    public int marchStartWave = 1;
    public int marchWavesToReach = 3;
    public int marchTargetWave = 4;
    public Vector3 marchStartPosition;
    public Vector3 marchDestinationPosition;

    public bool isMarchingToEnemyBase => isExpeditionMarching || isRespondingToWarning || isReturning;

    [Header("Mũi Tên Dưới Chân (Ground Arrow)")]
    [Tooltip("Bật/Tắt hiển thị mũi tên di chuyển dưới chân Soldier")]
    public bool showGroundArrow = true;
    public static bool globalShowSoldierGroundArrow = true;

    public SoldierGroundArrow EnsureSoldierGroundArrow()
    {
        SoldierGroundArrow arrow = GetComponentInChildren<SoldierGroundArrow>();
        if (arrow == null)
        {
            arrow = SoldierGroundArrow.Create(transform);
        }
        if (arrow != null)
        {
            arrow.showGroundArrow = showGroundArrow;
        }
        return arrow;
    }

    public void SetGroundArrowVisible(bool visible)
    {
        showGroundArrow = visible;
        SoldierGroundArrow arrow = GetComponentInChildren<SoldierGroundArrow>();
        if (arrow != null)
        {
            arrow.showGroundArrow = visible;
        }
    }

    public static void ToggleAllSoldierGroundArrows(bool enable)
    {
        globalShowSoldierGroundArrow = enable;
        SoldierGroundArrow.globalShowSoldierGroundArrow = enable;
        UnitController[] soldiers = Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        foreach (var s in soldiers)
        {
            if (s != null) s.SetGroundArrowVisible(enable);
        }
    }

    float GetAttackStopDistance()
    {
        return attackMode == AttackMode.Ranged ? rangedAttackRange : attackRange;
    }

    bool IsEnemyAlive(GameObject enemy)
    {
        if (enemy == null || !enemy.activeInHierarchy) return false;
        
        var enemyHealth = enemy.GetComponentInParent<EnemyHealth>();
        if (enemyHealth != null)
        {
            return enemyHealth.CurrentHealth > 0f;
        }

        var damageable = enemy.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            return damageable.CurrentHealth > 0f;
        }

        return true;
    }

    bool IsEnemyClaimedByOther(GameObject enemy)
    {
        if (enemy == null) return false;
        int id = enemy.GetInstanceID();
        if (claimedEnemies.TryGetValue(id, out UnitController owner))
        {
            return owner != null && owner != this;
        }
        return false;
    }

    bool ClaimEnemy(GameObject enemy)
    {
        if (enemy == null) return false;

        int id = enemy.GetInstanceID();
        claimedEnemies[id] = this;
        currentTargetInstanceId = id;
        return true;
    }

    void ReleaseCurrentTargetClaim()
    {
        int id = currentTargetInstanceId;
        if (id == 0 && currentTarget != null)
        {
            id = currentTarget.GetInstanceID();
        }

        if (id == 0)
        {
            return;
        }

        if (claimedEnemies.TryGetValue(id, out UnitController owner) && owner == this)
        {
            claimedEnemies.Remove(id);
        }

        currentTargetInstanceId = 0;
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        hpSoldier = GetComponent<HPSoldier>();

        if (agent == null)
        {
            Debug.LogError("UnitController requires a NavMeshAgent component.");
            enabled = false;
            return;
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        // Khởi tạo tương thích ngược cho detectRadius và enemyLayer
        if (detectRadius == 25f && raycastDistance != 6f)
        {
            detectRadius = raycastDistance;
        }
        
        // Đảm bảo bán kính quét tối thiểu là 20f để hoạt động theo vùng hiệu quả
        if (detectRadius < 15f)
        {
            detectRadius = 20f;
        }

        if (enemyLayer.value == ~0 && targetLayerMask.value != ~0)
        {
            enemyLayer = targetLayerMask;
        }

        // Tự động warp agent lên NavMesh nếu bị lệch khi spawn
        if (agent.isActiveAndEnabled && !agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        lowFreqCoroutine = StartCoroutine(LowFrequencyUpdate());

        if (currentState == UnitState.Attacking && currentTarget != null)
        {
            SetDestination(currentTarget.transform.position);
            lastDestinationPos = currentTarget.transform.position;
        }
    }

    private void SetDestination(Vector3 dest)
    {
        if (agent != null && agent.isActiveAndEnabled)
        {
            if (!agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
            }

            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;

                if (Vector3.Distance(agent.destination, dest) > 0.25f)
                {
                    agent.SetDestination(dest);
                }
                return;
            }
        }

        // Fallback di chuyển trực tiếp Transform nếu không có NavMesh trong Scene
        float speed = (agent != null && agent.speed > 0.1f) ? agent.speed : 3.5f;
        Vector3 moveDir = (dest - transform.position);
        moveDir.y = 0f;

        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 360f * Time.deltaTime);
            transform.position = Vector3.MoveTowards(transform.position, transform.position + moveDir.normalized, speed * Time.deltaTime);
        }
    }

    IEnumerator LowFrequencyUpdate()
    {
        while (true)
        {
            yield return new WaitForSeconds(scanFrequency);

            if (currentState == UnitState.Attacking && currentTarget != null)
            {
                if (!IsEnemyAlive(currentTarget))
                {
                    ReleaseCurrentTargetClaim();
                    currentTarget = null;
                    currentState = UnitState.Idle;
                    if (agent != null && agent.isOnNavMesh)
                    {
                        agent.isStopped = true;
                    }
                    continue;
                }

                Vector3 targetPos = currentTarget.transform.position;
                float stopDistance = GetAttackStopDistance();

                if ((transform.position - targetPos).sqrMagnitude > stopDistance * stopDistance)
                {
                    if ((targetPos - lastDestinationPos).sqrMagnitude > destinationUpdateThreshold * destinationUpdateThreshold)
                    {
                        SetDestination(targetPos);
                        lastDestinationPos = targetPos;
                    }
                }
            }
        }
    }

    void Update()
    {
        if (hpSoldier != null && hpSoldier.IsDead) return;

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        bool isSceneBattle = currentScene.ToLower().Contains("battle");

        if (isSceneBattle)
        {
            isExpeditionMarching = false;
        }

        // 🔥 Xử lý Lính di chuyển từng nấc theo từng Wave (chỉ khi ở Main Scene)
        if (isExpeditionMarching && !isSceneBattle)
        {
            int currentWave = (DayNightManager.HasInstance && DayNightManager.Ins != null) ? DayNightManager.Ins.CurrentWave : marchStartWave;
            int elapsedWaves = Mathf.Max(0, currentWave - marchStartWave);
            float progress = Mathf.Clamp01((float)elapsedWaves / (float)marchWavesToReach);

            Vector3 targetStepPos = Vector3.Lerp(marchStartPosition, marchDestinationPosition, progress);

            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                if (Vector3.Distance(transform.position, targetStepPos) > 0.05f)
                {
                    agent.Warp(Vector3.MoveTowards(transform.position, targetStepPos, 8f * Time.deltaTime));
                }
            }
            else
            {
                transform.position = Vector3.MoveTowards(transform.position, targetStepPos, 8f * Time.deltaTime);
            }

            Vector3 lookDir = marchDestinationPosition - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(lookDir), 360f * Time.deltaTime);
            }

            UpdateAnimationState();
            return;
        }

        // Chỉ quét tự động tìm mục tiêu nếu autoAggro = true HOẶC đang trong chế độ phản công theo nút bấm (isRespondingToWarning)
        if (autoAggro || isRespondingToWarning)
        {
            if (currentTarget == null || !currentTarget.CompareTag(enemyTag) || !IsEnemyAlive(currentTarget))
            {
                if (TryAcquireEnemyInArea())
                {
                    // Tìm thấy mục tiêu mới và bắt đầu đuổi/tấn công
                }
                else
                {
                    // Không thấy kẻ địch nào -> Về trạng thái Idle và dừng agent
                    if (currentState == UnitState.Attacking)
                    {
                        ReleaseCurrentTargetClaim();
                        currentTarget = null;
                        if (!isRespondingToWarning && !isReturning)
                        {
                            currentState = UnitState.Idle;
                            if (agent != null && agent.isOnNavMesh)
                            {
                                agent.isStopped = true;
                            }
                        }
                    }
                }
            }
        }

        // Cập nhật và xử lý logic Cảnh báo
        if (isRespondingToWarning)
        {
            if (currentTarget == null)
            {
                if (!AnyEnemyAlive())
                {
                    isRespondingToWarning = false;
                    isReturning = true;
                    currentState = UnitState.Moving;
                    SetDestination(returnPosition);
                }
                else
                {
                    // Nếu vẫn còn địch trên bản đồ nhưng đã mất dấu địch hiện tại,
                    // và chúng ta chưa tới điểm cảnh báo, hãy tiếp tục di chuyển tới đó
                    float distToWarning = Vector3.Distance(transform.position, warningPosition);
                    if (distToWarning > GetAttackStopDistance() && agent != null && agent.isOnNavMesh && Vector3.Distance(agent.destination, warningPosition) > 0.5f)
                    {
                        SetDestination(warningPosition);
                        currentState = UnitState.Moving;
                    }
                }
            }
        }
        else if (isReturning)
        {
            if (currentTarget == null)
            {
                // Kiểm tra xem đã về tới vị trí cũ chưa
                if (agent != null && agent.isOnNavMesh && !agent.pathPending && (agent.remainingDistance <= agent.stoppingDistance || !agent.hasPath))
                {
                    isReturning = false;
                    currentState = UnitState.Idle;
                }
                else if (agent != null && agent.isOnNavMesh && Vector3.Distance(agent.destination, returnPosition) > 0.5f)
                {
                    // Đảm bảo vẫn đang di chuyển về vị trí cũ
                    SetDestination(returnPosition);
                    currentState = UnitState.Moving;
                }
            }
        }

        switch (currentState)
        {
            case UnitState.Attacking:
                HandleAttacking();
                break;
            case UnitState.Moving:
                HandleMovement();
                break;
        }

        UpdateAnimationState();
    }

    public void SetNewTarget(GameObject target)
    {
        if (target == null) return;
        if (!target.CompareTag(enemyTag)) return;
        if (currentTarget == target) return;

        ReleaseCurrentTargetClaim();
        if (!ClaimEnemy(target)) return;

        currentTarget = target;
        currentState = UnitState.Attacking;

        SetDestination(target.transform.position);
        lastDestinationPos = target.transform.position;
    }

    public void RespondToWarning(Vector3 targetPosition)
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene.ToLower().Contains("battle"))
        {
            isExpeditionMarching = false;
            isRespondingToWarning = true;
            isReturning = false;
            autoAggro = true;
            currentState = UnitState.Moving;

            SetDestination(targetPosition);
            return;
        }

        StartExpeditionMarch(targetPosition, -1);
    }

    public void StartExpeditionMarch(Vector3 destinationPos, int wavesToReach = -1)
    {
        if (!isExpeditionMarching)
        {
            marchStartPosition = transform.position;
        }
        marchDestinationPosition = destinationPos;
        marchStartWave = (DayNightManager.HasInstance && DayNightManager.Ins != null) ? DayNightManager.Ins.CurrentWave : 1;

        if (wavesToReach <= 0)
        {
            float dist = Vector3.Distance(marchStartPosition, marchDestinationPosition);
            // 🔥 Tự động tính số Wave cần thiết dựa theo khoảng cách thực tế (khoảng 15m / Wave)
            wavesToReach = Mathf.Max(1, Mathf.RoundToInt(dist / 15f));
        }

        marchWavesToReach = wavesToReach;
        marchTargetWave = marchStartWave + marchWavesToReach;
        isExpeditionMarching = true;
        isRespondingToWarning = true;
        isReturning = false;
        currentState = UnitState.Moving;

        if (showGroundArrow && globalShowSoldierGroundArrow)
        {
            EnsureSoldierGroundArrow().SetTargetDestination(destinationPos);
        }
    }

    public void EnableCombat(Vector3 enemyTargetPos)
    {
        autoAggro = true;
        RespondToWarning(enemyTargetPos);
    }

    private bool AnyEnemyAlive()
    {
        // 1. Tag check
        try
        {
            GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
            foreach (var enemy in enemies)
            {
                if (enemy != null && enemy.activeInHierarchy)
                {
                    var hp = enemy.GetComponentInParent<EnemyHealth>();
                    if (hp == null || hp.CurrentHealth > 0f) return true;
                }
            }
        }
        catch {}

        // 2. EnemyAI check fallback
        EnemyAI[] enemyAIs = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (var enemy in enemyAIs)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                var hp = enemy.GetComponent<EnemyHealth>();
                if (hp == null || hp.CurrentHealth > 0f) return true;
            }
        }

        return false;
    }

    void HandleAttacking()
    {
        if (currentTarget == null)
        {
            ReleaseCurrentTargetClaim();
            currentState = UnitState.Idle;
            return;
        }

        float sqrDistance = (transform.position - currentTarget.transform.position).sqrMagnitude;
        float stopDistance = GetAttackStopDistance();

        if (sqrDistance <= stopDistance * stopDistance)
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
            
            // Xoay mượt về phía kẻ địch khi đứng tấn công
            Vector3 lookDir = (currentTarget.transform.position - transform.position);
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 360f * Time.deltaTime);
            }

            // Thực hiện tấn công theo chu kỳ
            if (Time.time >= nextAttackTime)
            {
                ExecuteAttack(currentTarget.transform);
                nextAttackTime = Time.time + attackRate;
            }
        }
        else
        {
            if (agent != null && agent.isOnNavMesh && agent.isStopped)
            {
                agent.isStopped = false;
            }
        }
    }

    void HandleMovement()
    {
        if (agent != null && agent.isOnNavMesh && !agent.pathPending)
        {
            if (agent.hasPath && agent.remainingDistance <= agent.stoppingDistance)
            {
                currentState = UnitState.Idle;
            }
        }
    }

    private bool IsEnemyCollider(Collider col, out GameObject enemyRoot)
    {
        enemyRoot = null;
        if (col == null) return false;

        if (col.tag == enemyTag || 
            col.GetComponentInParent<EnemyHealth>() != null || 
            col.name.ToLower().Contains("enemy"))
        {
            var health = col.GetComponentInParent<EnemyHealth>();
            enemyRoot = (health != null) ? health.gameObject : col.gameObject;
            return IsEnemyAlive(enemyRoot);
        }
        return false;
    }

    bool TryAcquireEnemyInArea()
    {
        Collider[] colliders = null;
        if (enemyLayer.value != 0)
        {
            colliders = Physics.OverlapSphere(transform.position, detectRadius, enemyLayer);
        }

        List<GameObject> validEnemies = new List<GameObject>();

        if (colliders != null)
        {
            foreach (var col in colliders)
            {
                if (col == null || !col.gameObject.activeInHierarchy) continue;
                if (IsEnemyCollider(col, out GameObject enemyRoot))
                {
                    if (!validEnemies.Contains(enemyRoot)) validEnemies.Add(enemyRoot);
                }
            }
        }

        // Fallback 1: Nếu LayerMask không tìm thấy gì, quét lại toàn bộ các Layer trong bán kính
        if (validEnemies.Count == 0)
        {
            Collider[] fallbackColliders = Physics.OverlapSphere(transform.position, detectRadius);
            foreach (var col in fallbackColliders)
            {
                if (col == null || !col.gameObject.activeInHierarchy) continue;
                if (IsEnemyCollider(col, out GameObject enemyRoot))
                {
                    if (!validEnemies.Contains(enemyRoot)) validEnemies.Add(enemyRoot);
                }
            }
        }

        // Fallback 2: Quét toàn bộ Scene theo Tag "Enemy" trong bán kính
        if (validEnemies.Count == 0)
        {
            GameObject[] taggedEnemies = null;
            try { taggedEnemies = GameObject.FindGameObjectsWithTag(enemyTag); } catch { taggedEnemies = null; }

            if (taggedEnemies != null)
            {
                float detectRadiusSqr = detectRadius * detectRadius;
                foreach (var go in taggedEnemies)
                {
                    if (go == null || !go.activeInHierarchy) continue;

                    float sqrDist = (go.transform.position - transform.position).sqrMagnitude;
                    if (sqrDist <= detectRadiusSqr)
                    {
                        var health = go.GetComponentInParent<EnemyHealth>();
                        GameObject enemyRoot = (health != null) ? health.gameObject : go;

                        if (IsEnemyAlive(enemyRoot) && !validEnemies.Contains(enemyRoot))
                        {
                            validEnemies.Add(enemyRoot);
                        }
                    }
                }
            }
        }

        // Fallback 3 (Dành cho chế độ phản công): Mở rộng quét TOÀN BỘ BẢN ĐỒ tìm kẻ địch còn sống
        if (validEnemies.Count == 0 && (isRespondingToWarning || autoAggro))
        {
            EnemyAI[] allEnemyAIs = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            foreach (var enemyAI in allEnemyAIs)
            {
                if (enemyAI != null && enemyAI.gameObject.activeInHierarchy && IsEnemyAlive(enemyAI.gameObject))
                {
                    if (!validEnemies.Contains(enemyAI.gameObject))
                    {
                        validEnemies.Add(enemyAI.gameObject);
                    }
                }
            }

            try
            {
                GameObject[] taggedEnemies = GameObject.FindGameObjectsWithTag(enemyTag);
                if (taggedEnemies != null)
                {
                    foreach (var go in taggedEnemies)
                    {
                        if (go == null || !go.activeInHierarchy) continue;
                        var health = go.GetComponentInParent<EnemyHealth>();
                        GameObject enemyRoot = (health != null) ? health.gameObject : go;
                        if (IsEnemyAlive(enemyRoot) && !validEnemies.Contains(enemyRoot))
                        {
                            validEnemies.Add(enemyRoot);
                        }
                    }
                }
            }
            catch {}
        }

        if (validEnemies.Count == 0)
        {
            return false;
        }

        // In log giúp theo dõi trong Console
        // Debug.Log($"[UnitController Scan] {gameObject.name} tìm thấy {validEnemies.Count} mục tiêu hợp lệ trong bán kính {detectRadius}.");

        // 1. Tìm kẻ địch gần nhất chưa bị ai chiếm (claim)
        GameObject bestTarget = null;
        float minDistance = float.MaxValue;

        foreach (var enemy in validEnemies)
        {
            if (IsEnemyClaimedByOther(enemy)) continue;

            float dist = (enemy.transform.position - transform.position).sqrMagnitude;
            if (dist < minDistance)
            {
                minDistance = dist;
                bestTarget = enemy;
            }
        }

        // 2. Fallback: Nếu tất cả kẻ địch trong tầm đều đã bị chiếm, chọn kẻ địch gần nhất (chấp nhận chia sẻ mục tiêu)
        if (bestTarget == null)
        {
            minDistance = float.MaxValue;
            foreach (var enemy in validEnemies)
            {
                float dist = (enemy.transform.position - transform.position).sqrMagnitude;
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestTarget = enemy;
                }
            }
        }

        if (bestTarget != null)
        {
            if (currentTarget == bestTarget)
            {
                return true;
            }

            ReleaseCurrentTargetClaim();
            ClaimEnemy(bestTarget);

            currentTarget = bestTarget;
            currentState = UnitState.Attacking;

            SetDestination(currentTarget.transform.position);
            lastDestinationPos = currentTarget.transform.position;
            return true;
        }

        return false;
    }

    private void ExecuteAttack(Transform target)
    {
        PlayAttackAnimation();

        if (attackMode == AttackMode.Melee)
        {
            IDamageable damageable = target.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(attackDamage, target.position);
            }
        }
        else if (attackMode == AttackMode.Ranged)
        {
            StartCoroutine(SpawnProjectileDelayed(target, projectileSpawnDelay));
        }
    }

    private IEnumerator SpawnProjectileDelayed(Transform target, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (target != null && target.gameObject.activeInHierarchy && IsEnemyAlive(target.gameObject))
        {
            if (projectilePrefab != null)
            {
                Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.5f;
                Collider targetCollider = target.GetComponentInChildren<Collider>();
                Vector3 targetCenter = targetCollider != null ? targetCollider.bounds.center : target.position + Vector3.up * 1f;
                Vector3 direction = (targetCenter - spawnPos).normalized;
                Quaternion spawnRot = Quaternion.LookRotation(direction);

                GameObject proj = Instantiate(projectilePrefab, spawnPos, spawnRot);
                
                Arrow arrowComp = proj.GetComponent<Arrow>();
                if (arrowComp != null)
                {
                    arrowComp.SetLauncher(gameObject);
                    arrowComp.SetDamage(attackDamage);
                    arrowComp.SetTarget(target, projectileSpeed);
                }
                else
                {
                    Rigidbody rb = proj.GetComponent<Rigidbody>();
                    if (rb == null) rb = proj.AddComponent<Rigidbody>();
                    rb.linearVelocity = direction * projectileSpeed;
                }
            }
        }
    }

    public void PlayAttackAnimation()
    {
        if (animator == null) return;

        string paramToSet = (attackMode == AttackMode.Ranged) ? shootBoolParam : attackBoolParam;
        if (!string.IsNullOrWhiteSpace(paramToSet))
        {
            if (HasAnimatorParameter(animator, paramToSet, AnimatorControllerParameterType.Trigger))
            {
                animator.SetTrigger(paramToSet);
            }
            else
            {
                if (attackMode == AttackMode.Ranged)
                {
                    animator.SetBool(paramToSet, true);
                }
                else
                {
                    StartCoroutine(TriggerBoolAnimation(paramToSet));
                }
            }
        }
    }

    private bool HasAnimatorParameter(Animator anim, string paramName, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter param in anim.parameters)
        {
            if (param.name == paramName && param.type == type)
                return true;
        }
        return false;
    }

    private System.Collections.IEnumerator TriggerBoolAnimation(string paramName)
    {
        animator.SetBool(paramName, true);
        yield return new WaitForSeconds(0.1f);
        animator.SetBool(paramName, false);
    }

    private bool IsShooting()
    {
        if (attackMode != AttackMode.Ranged) return false;
        if (currentTarget == null || !currentTarget.activeInHierarchy) return false;
        if (!IsEnemyAlive(currentTarget)) return false;

        float stopDistance = GetAttackStopDistance();
        float sqrDistance = (transform.position - currentTarget.transform.position).sqrMagnitude;
        if (sqrDistance > stopDistance * stopDistance) return false;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && !agent.isStopped) return false;

        return true;
    }

    private void UpdateAnimationState()
    {
        if (animator == null) return;
        if (hpSoldier != null && hpSoldier.IsDead) return;

        bool isMoving = false;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            bool hasMeaningfulPath = !agent.isStopped && agent.hasPath && !agent.pathPending && agent.remainingDistance > agent.stoppingDistance;
            bool hasMeaningfulVelocity = !agent.isStopped && agent.velocity.sqrMagnitude > 0.01f;
            isMoving = hasMeaningfulPath || hasMeaningfulVelocity;
        }
        else
        {
            isMoving = false;
        }

        animator.SetBool(moveBoolParam, isMoving);

        if (!string.IsNullOrWhiteSpace(shootBoolParam))
        {
            bool isShooting = IsShooting();
            if (isShooting)
            {
                // Force the animator to stay in the "Shoot" state by looping it manually
                var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.IsName("Shoot"))
                {
                    if (stateInfo.normalizedTime >= 0.9f && !animator.IsInTransition(0))
                    {
                        animator.Play("Shoot", 0, 0f);
                    }
                }
                else
                {
                    if (!animator.IsInTransition(0) || !animator.GetNextAnimatorStateInfo(0).IsName("Shoot"))
                    {
                        animator.Play("Shoot", 0, 0f);
                    }
                }

                // Set/Keep trigger or bool state active
                if (HasAnimatorParameter(animator, shootBoolParam, AnimatorControllerParameterType.Trigger))
                {
                    animator.SetTrigger(shootBoolParam);
                }
                else if (HasAnimatorParameter(animator, shootBoolParam, AnimatorControllerParameterType.Bool))
                {
                    animator.SetBool(shootBoolParam, true);
                }
            }
            else
            {
                // Reset trigger or bool state when not shooting
                if (HasAnimatorParameter(animator, shootBoolParam, AnimatorControllerParameterType.Trigger))
                {
                    animator.ResetTrigger(shootBoolParam);
                }
                else if (HasAnimatorParameter(animator, shootBoolParam, AnimatorControllerParameterType.Bool))
                {
                    animator.SetBool(shootBoolParam, false);
                }
            }
        }
    }

    public void SetAttackDamage(float damage)
    {
        attackDamage = damage;
    }

    void OnDisable()
    {
        ReleaseCurrentTargetClaim();

        if (lowFreqCoroutine != null)
        {
            StopCoroutine(lowFreqCoroutine);
            lowFreqCoroutine = null;
        }
    }

    void OnDrawGizmosSelected()
    {
        // Vòng đỏ: Tầm quét kẻ địch theo vùng
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectRadius);

        // Vòng vàng: Tầm dừng tấn công
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, GetAttackStopDistance());
    }
}
