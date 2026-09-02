using UnityEngine;
using UnityEngine.AI;

/*
 * AttackTowerAI.cs
 * Folder: Scripts/AI/
 * Dự án: KHẨN HOANG (PENTA DEV)
 * Người thực hiện: VŨ + ĐĂNG
 * CHỨC NĂNG: Điều khiển tháp tấn công (Cung, Pháo) bắn quái, tính toán quỹ đạo ném bom ballistic,
 * đồng thời mở cổng property "AttackRange" để UIManager.cs bóc tách dữ liệu UI chuẩn xác.
 */

public enum AttackTowerType { Archer, Cannon }

public class AttackTowerAI : MonoBehaviour
{
    [Header("Cấu hình Loại Tháp")]
    public AttackTowerType towerType;
    public float fireRate = 1f;          // Tốc độ bắn (số phát / giây)
    public Transform firePoint;          // Kéo Object trống ở đầu nòng/họng pháo vào đây
    public GameObject projectilePrefab;  // Prefab Mũi tên (Arrow) hoặc Quả bom (Bomb)
    
    [Header("Projectile")]
    public float projectileSpeed = 20f; // speed applied if projectile has Rigidbody
    [Tooltip("Yaw offset (degrees) to apply so projectile model faces correctly. Common: 270")]
    public float projectileYawOffset = 0f;
    [Tooltip("Vertical spawn height above target for AoE bombs (meters). Lower to reduce high arc.")]
    public float bombSpawnHeight = 6f;
    [Tooltip("Distance forward from the firePoint to spawn the projectile to avoid overlapping the muzzle.")]
    public float muzzleOffset = 0.5f;

    [Header("Cấu hình Nâng cấp (Upgrade)")]
    public float damageLv1 = 10f;
    public float damageLv2 = 15f;
    public float damageLv3 = 20f;

    [Header("Cấu hình Vùng Cháy (Lv3)")]
    public float burnRadius = 3f;
    public float burnDamagePerSec = 5f;
    public float burnDuration = 3f;
    public GameObject fireVfxPrefab;

    [Header("Cấu hình Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string attackParamName = "IsAttack";

    [Header("Hệ thống nhắm mục tiêu (Targeting)")]
    [SerializeField] private float attackRange = 8f; 

    [Header("Cấu hình Tấn Công Tự Động")]
    [Tooltip("Tự động tấn công khi có mục tiêu. Mặc định với Pháo (Cannon) sẽ là FALSE để ngắt tự động đánh.")]
    [SerializeField] private bool autoAttack = true;
    public bool AutoAttack { get => autoAttack; set => autoAttack = value; }

    [Header("Cấu hình Xoay (Rotation)")]
    public float rotationSpeed = 10f;
    [Tooltip("Kéo phần đầu hoặc thân tháp cần xoay vào đây. Nếu để trống, sẽ xoay toàn bộ tháp.")]
    public Transform partToRotate;

    [Header("Di chuyển trong Scene Battle")]
    [Tooltip("Cho Tháp Nỏ/Pháo tiến lên đến tầm bắn khi ở SceneBattle. Không ảnh hưởng vị trí công trình ở Map.")]
    [SerializeField] private bool moveInBattleScene = true;
    [SerializeField, Min(0.1f)] private float battleMoveSpeed = 3f;
    [Tooltip("Khoảng cách tối thiểu với địch trước khi dừng. Để 0 sẽ dùng 90% tầm đánh.")]
    [SerializeField, Min(0f)] private float battleStoppingDistance = 0f;
    [Tooltip("Ưu tiên NavMeshAgent nếu prefab có sẵn; nếu không có sẽ đi thẳng trên mặt phẳng battle.")]
    [SerializeField] private bool useNavMeshAgentWhenAvailable = true;

    // --- CỔNG KẾT NỐI PUBLIC ĐỂ UIManager ĐỌC DỮ LIỆU (KHÔNG LÀM MẤT PRIVATE BIẾN GỐC) ---
    public float AttackRange => attackRange;

    private UpgradeableBuilding upgradeableBuilding;
    private HPTower hpTower;
    private NavMeshAgent navMeshAgent;
    private Transform currentTarget;
    private float nextFireTime;

    private void Awake()
    {
        autoAttack = false;
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null) navMeshAgent = GetComponentInParent<NavMeshAgent>();
    }

    public bool IsDestroyed()
    {
        if (hpTower == null)
        {
            hpTower = GetComponent<HPTower>();
            if (hpTower == null) hpTower = GetComponentInParent<HPTower>();
            if (hpTower == null) hpTower = GetComponentInChildren<HPTower>();
        }

        if (hpTower != null)
        {
            if (hpTower.IsDestroyed || hpTower.CurrentHealth <= 0) return true;
        }

        return false;
    }

    private void Start()
    {
        hpTower = GetComponent<HPTower>();
        if (hpTower == null) hpTower = GetComponentInParent<HPTower>();
        if (hpTower == null) hpTower = GetComponentInChildren<HPTower>();

        upgradeableBuilding = GetComponent<UpgradeableBuilding>();
        UpdateAnimatorReference();
        if (firePoint == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>())
            {
                string nameLower = child.name.ToLower();
                if (nameLower.Contains("firepoint") || nameLower.Contains("muzzle") || nameLower.Contains("spawn") || nameLower.Contains("shoot"))
                {
                    firePoint = child;
                    break;
                }
            }
            if (firePoint == null)
            {
                firePoint = transform;
                Debug.LogWarning($"[AttackTowerAI] {name}: Không tìm thấy firePoint, tự động dùng chính tháp làm firePoint!");
            }
        }
    }

    public bool CanAttack()
    {
        if (IsDestroyed()) return false;

        // 🔥 Ở Scene Main Game: TOÀN BỘ tháp (Cung & Pháo) ĐỀU KHÔNG ĐƯỢC BẮN
        // (Để quái đứng tại hàng rào chờ người chơi bấm nút Tấn Công sang SceneBattle).
        // Chỉ khi vào bên trong SceneBattle (IsInBattleScene() == true) mới cho phép bắn!
        if (!IsInBattleScene())
        {
            return false;
        }

        if (upgradeableBuilding == null)
        {
            upgradeableBuilding = GetComponent<UpgradeableBuilding>();
            if (upgradeableBuilding == null) upgradeableBuilding = GetComponentInParent<UpgradeableBuilding>();
            if (upgradeableBuilding == null) upgradeableBuilding = GetComponentInChildren<UpgradeableBuilding>();
        }

        if (upgradeableBuilding != null)
        {
            if (upgradeableBuilding.IsInitialBuildNeeded || upgradeableBuilding.IsUpgrading || upgradeableBuilding.IsRuined)
            {
                return false;
            }
        }

        BuildingCtrl buildingCtrl = GetComponent<BuildingCtrl>();
        if (buildingCtrl == null) buildingCtrl = GetComponentInParent<BuildingCtrl>();
        if (buildingCtrl != null && !buildingCtrl.IsBuilt)
        {
            return false;
        }

        return true;
    }

    // Hàm nhận lệnh tấn công do Tháp Canh truyền mục tiêu sang
    public void CommandAttack(Transform target)
    {
        if (!CanAttack())
        {
            currentTarget = null;
            return;
        }

        currentTarget = target;
        Debug.Log($"[AttackTowerAI] CommandAttack received. Target={(target == null ? "null" : target.name)}");
    }

    /// <summary>
    /// Hàm bắn thủ công trên Pháo/Tháp (Bỏ qua kiểm tra autoAttack), dành cho việc điều khiển Cannon bắn theo lệnh người chơi
    /// </summary>
    public void ManualAttack(Transform target)
    {
        if (IsDestroyed()) return;
        if (target == null || !target.gameObject.activeInHierarchy) return;

        currentTarget = target;
        RotateTowardsTarget();
        ExecuteAttack();
    }

    private void Update()
    {
        // Nếu không thể tấn công (đang xây dựng, nâng cấp, hư hỏng, hoặc bị phá hủy) -> Không bắn và hủy mục tiêu
        if (!CanAttack())
        {
            if (currentTarget != null) currentTarget = null;
            return;
        }

        // Nếu không có mục tiêu (hoặc mục tiêu đã chết/hủy) -> Tự động tìm kẻ địch gần nhất trong SceneBattle hoặc trong tầm bắn
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy || IsTargetDead(currentTarget))
        {
            currentTarget = FindNearestTarget();
        }

        if (currentTarget == null) return;

        // Tháp chiến đấu là đối tượng di động duy nhất trong SceneBattle. Khi
        // ngoài tầm, tiến tới mục tiêu; ở Map nó luôn đứng yên như công trình.
        if (MoveIntoBattleRange()) return;

        // Xoay tháp về phía mục tiêu
        RotateTowardsTarget();

        // Kiểm tra giãn cách thời gian giữa các loạt bắn
        if (Time.time >= nextFireTime)
        {
            ExecuteAttack();
            nextFireTime = Time.time + 1f / fireRate;
        }
    }

    private bool IsTargetDead(Transform target)
    {
        if (target == null) return true;
        var hp = target.GetComponentInParent<EnemyHealth>();
        if (hp != null && (hp.IsDead || hp.CurrentHealth <= 0)) return true;

        var ai = target.GetComponentInParent<EnemyAI>();
        if (ai != null && !ai.gameObject.activeInHierarchy) return true;

        return false;
    }

    private Transform FindNearestTarget()
    {
        bool inBattle = IsInBattleScene();
        float checkRadius = inBattle ? 50f : Mathf.Max(attackRange, 40f);
        float minDistance = float.MaxValue;
        Transform bestTarget = null;

        Collider[] colliders = Physics.OverlapSphere(transform.position, checkRadius);
        foreach (var col in colliders)
        {
            if (col == null || !col.gameObject.activeInHierarchy) continue;

            bool isEnemy = col.CompareTag("Enemy") || col.name.ToLower().Contains("enemy") || col.GetComponentInParent<EnemyHealth>() != null || col.GetComponentInParent<EnemyAI>() != null;
            if (isEnemy)
            {
                Transform enemyTrans = col.transform;
                var hp = col.GetComponentInParent<EnemyHealth>();
                if (hp != null)
                {
                    if (hp.IsDead || hp.CurrentHealth <= 0) continue;
                    enemyTrans = hp.transform;
                }

                var ai = col.GetComponentInParent<EnemyAI>();
                if (ai != null)
                {
                    if (!ai.gameObject.activeInHierarchy) continue;
                    enemyTrans = ai.transform;
                }

                float dist = Vector3.Distance(transform.position, enemyTrans.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestTarget = enemyTrans;
                }
            }
        }

        return bestTarget;
    }

    private bool IsInBattleScene()
    {
        if (BattleManager.Ins != null) return true;
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        return sceneName.ToLower().Contains("battle");
    }

    private bool MoveIntoBattleRange()
    {
        if (!moveInBattleScene || !IsInBattleScene() || currentTarget == null) return false;

        float stoppingDistance = battleStoppingDistance > 0f
            ? battleStoppingDistance
            : Mathf.Max(1f, attackRange * 0.9f);
        Vector3 targetPosition = currentTarget.position;
        targetPosition.y = transform.position.y;

        if (Vector3.Distance(transform.position, targetPosition) <= stoppingDistance)
        {
            StopBattleMovement();
            return false;
        }

        if (useNavMeshAgentWhenAvailable && navMeshAgent != null &&
            navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.speed = battleMoveSpeed;
            navMeshAgent.stoppingDistance = stoppingDistance;
            navMeshAgent.isStopped = false;
            navMeshAgent.SetDestination(targetPosition);
        }
        else
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                battleMoveSpeed * Time.deltaTime);
        }

        RotateTowardsTarget();
        return true;
    }

    private void StopBattleMovement()
    {
        if (navMeshAgent != null && navMeshAgent.isActiveAndEnabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = true;
        }
    }

    private void RotateTowardsTarget()
    {
        if (currentTarget == null) return;

        Transform rotateTransform = partToRotate != null ? partToRotate : transform;
        Vector3 targetDirection = currentTarget.position - rotateTransform.position;
        targetDirection.y = 0f; // Chỉ xoay quanh trục Y

        if (targetDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            rotateTransform.rotation = Quaternion.Slerp(rotateTransform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }

    private void ExecuteAttack()
    {
        // Kiểm tra lại xem tháp còn nguyên vẹn và mục tiêu còn sống/tồn tại không trước khi bắn
        if (IsDestroyed()) { currentTarget = null; return; }
        if (currentTarget == null) { Debug.Log("[AttackTowerAI] ExecuteAttack called but currentTarget is null"); return; }

        PlayAttackAnimation();

        if (towerType == AttackTowerType.Archer)
        {
            Debug.Log($"[ArcherTower] 🏹 Bắn cung vào mục tiêu: {currentTarget.name} (Tọa độ: {currentTarget.position})");
            SpawnArrow();
        }
        else if (towerType == AttackTowerType.Cannon)
        {
            Debug.Log($"[Cannon] 💣 Dội bom/Pháo kích vào vị trí: {currentTarget.position}");
            SpawnAoEBomb();
        }
    }

    private void PlayAttackAnimation()
    {
        UpdateAnimatorReference();
        if (animator == null)
        {
            Debug.LogWarning($"[AttackTowerAI] {name}: Animator component is missing/null!");
            return;
        }
        if (!animator.enabled)
        {
            Debug.LogWarning($"[AttackTowerAI] {name}: Animator component is disabled!");
            return;
        }
        StartCoroutine(TriggerAttackAnimationRoutine());
    }

    private void UpdateAnimatorReference()
    {
        if (upgradeableBuilding != null)
        {
            int currentLevel = upgradeableBuilding.CurrentLevel;
            var visualModels = upgradeableBuilding.VisualModels;
            if (visualModels != null && currentLevel >= 0 && currentLevel < visualModels.Length)
            {
                GameObject activeModel = visualModels[currentLevel];
                if (activeModel != null)
                {
                    Animator activeModelAnimator = activeModel.GetComponent<Animator>();
                    if (activeModelAnimator == null)
                    {
                        activeModelAnimator = activeModel.GetComponentInChildren<Animator>();
                    }

                    if (activeModelAnimator != null)
                    {
                        animator = activeModelAnimator;
                        return;
                    }
                }
            }
        }

        Animator rootAnimator = GetComponent<Animator>();
        if (rootAnimator != null)
        {
            animator = rootAnimator;
            return;
        }

        Animator activeChildAnimator = GetComponentInChildren<Animator>(false);
        if (activeChildAnimator != null)
        {
            animator = activeChildAnimator;
        }
    }

    private System.Collections.IEnumerator TriggerAttackAnimationRoutine()
    {
        AnimatorControllerParameter param = GetParameter(animator, attackParamName);
        if (param != null)
        {
            if (param.type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(attackParamName);
                Debug.Log($"[AttackTowerAI] {name}: Set Animator Trigger parameter '{attackParamName}'.");
            }
            else if (param.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(attackParamName, true);
                Debug.Log($"[AttackTowerAI] {name}: Set Animator bool parameter '{attackParamName}' to true.");
                yield return new WaitForSeconds(0.2f);
                if (animator != null)
                {
                    animator.SetBool(attackParamName, false);
                    Debug.Log($"[AttackTowerAI] {name}: Set Animator bool parameter '{attackParamName}' to false.");
                }
            }
            else
            {
                Debug.LogWarning($"[AttackTowerAI] {name}: Parameter '{attackParamName}' is of type {param.type}, which is not supported (only Bool or Trigger are supported).");
            }
        }
        else
        {
            Debug.LogWarning($"[AttackTowerAI] {name}: Parameter '{attackParamName}' was NOT found in the Animator Controller!");
        }
    }

    private AnimatorControllerParameter GetParameter(Animator anim, string paramName)
    {
        if (anim == null) return null;
        foreach (AnimatorControllerParameter param in anim.parameters)
        {
            if (param.name == paramName) return param;
        }
        return null;
    }

    private void SpawnArrow()
    {
        if (projectilePrefab == null || firePoint == null)
        {
            Debug.LogWarning("[AttackTowerAI] SpawnArrow aborted: projectilePrefab or firePoint is null");
            return;
        }

        int level = upgradeableBuilding != null ? upgradeableBuilding.CurrentLevel : 0;
        float damage = damageLv1;
        if (level == 1) damage = damageLv2;
        else if (level == 2) damage = damageLv3;

        if (level == 0)
        {
            SpawnSingleArrow(currentTarget, 0f, level, damage);
        }
        else
        {
            System.Collections.Generic.List<Transform> enemiesInRange = new System.Collections.Generic.List<Transform>();
            if (currentTarget != null) enemiesInRange.Add(currentTarget);

            float checkRadius = 25f;
            Collider[] colls = Physics.OverlapSphere(transform.position, checkRadius);
            foreach (var col in colls)
            {
                if (col == null || !col.gameObject.activeInHierarchy) continue;
                
                bool isEnemy = col.CompareTag("Enemy") || col.name.ToLower().Contains("enemy") || col.GetComponentInParent<EnemyHealth>() != null;
                if (isEnemy)
                {
                    var health = col.GetComponentInParent<EnemyHealth>();
                    Transform enemyTrans = (health != null) ? health.transform : col.transform;
                    if (!enemiesInRange.Contains(enemyTrans))
                    {
                        enemiesInRange.Add(enemyTrans);
                    }
                }
            }

            enemiesInRange.Sort((a, b) => {
                if (a == currentTarget) return -1;
                if (b == currentTarget) return 1;
                float distA = (a.position - transform.position).sqrMagnitude;
                float distB = (b.position - transform.position).sqrMagnitude;
                return distA.CompareTo(distB);
            });

            if (enemiesInRange.Count > 0)
            {
                SpawnSingleArrow(enemiesInRange[0], 0f, level, damage);

                Transform target2 = enemiesInRange.Count > 1 ? enemiesInRange[1] : enemiesInRange[0];
                SpawnSingleArrow(target2, -15f, level, damage);

                Transform target3 = enemiesInRange.Count > 2 ? enemiesInRange[2] : (enemiesInRange.Count > 1 ? enemiesInRange[0] : enemiesInRange[0]);
                SpawnSingleArrow(target3, 15f, level, damage);
            }
            else
            {
                SpawnSingleArrow(currentTarget, 0f, level, damage);
                SpawnSingleArrow(null, -15f, level, damage);
                SpawnSingleArrow(null, 15f, level, damage);
            }
        }
    }

    private void SpawnSingleArrow(Transform target, float yawOffset, int level, float damage)
    {
        Vector3 dirToTarget = (target != null) ? (target.position - firePoint.position) : firePoint.forward;
        dirToTarget.y = 0f;
        if (dirToTarget.sqrMagnitude < 0.0001f) dirToTarget = firePoint.forward;

        float baseYaw = Mathf.Atan2(dirToTarget.x, dirToTarget.z) * Mathf.Rad2Deg;
        float finalYaw = baseYaw + yawOffset + projectileYawOffset;
        
        Quaternion spawnRot = Quaternion.Euler(0f, finalYaw, 0f);
        Vector3 spawnPos = firePoint.position + spawnRot * Vector3.forward * muzzleOffset;

        GameObject arrow = ArrowPool.Instance != null ? ArrowPool.Instance.Spawn(projectilePrefab, spawnPos, spawnRot) : Instantiate(projectilePrefab, spawnPos, spawnRot);

        var arrowComp = arrow.GetComponent<Arrow>();
        if (arrowComp != null)
        {
            arrowComp.SetLauncher(gameObject);
            arrowComp.SetDamage(damage);
            
            if (level == 2 && towerType == AttackTowerType.Archer)
            {
                arrowComp.SetFireArrow(true, burnRadius, burnDamagePerSec, burnDuration, fireVfxPrefab);
            }

            if (target != null)
            {
                arrowComp.SetTarget(target, projectileSpeed);
                arrowComp.AdjustZByHeightAndDistance(firePoint.position, target.position);
                arrowComp.AdjustYToFaceTarget(firePoint.position, target.position, projectileYawOffset);
            }
            else
            {
                arrowComp.SetTarget(null, projectileSpeed);
            }
        }
        else
        {
            Rigidbody rb = arrow.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = (spawnRot * Vector3.forward) * projectileSpeed;
            }
        }
    }

    private void SpawnAoEBomb()
    {
        if (projectilePrefab == null || currentTarget == null)
        {
            Debug.LogWarning("[AttackTowerAI] SpawnAoEBomb aborted: projectilePrefab or currentTarget is null");
            return;
        }

        int level = upgradeableBuilding != null ? upgradeableBuilding.CurrentLevel : 0;
        float damage = damageLv1;
        if (level == 1) damage = damageLv2;
        else if (level == 2) damage = damageLv3;

        Vector3 spawnPos;
        Quaternion bombRot;
        if (firePoint != null)
        {
            spawnPos = firePoint.position;
            bombRot = firePoint.rotation * projectilePrefab.transform.rotation;
        }
        else
        {
            spawnPos = currentTarget.position + Vector3.up * bombSpawnHeight;
            bombRot = projectilePrefab.transform.rotation;
        }

        if (firePoint != null)
            spawnPos += firePoint.forward * muzzleOffset;

        GameObject bomb = ArrowPool.Instance != null ? ArrowPool.Instance.Spawn(projectilePrefab, spawnPos, bombRot) : Instantiate(projectilePrefab, spawnPos, bombRot);

        var canonComp = bomb.GetComponent<Canon>();
        if (canonComp != null)
        {
            canonComp.SetLauncher(gameObject);
            canonComp.SetLevel(level + 1);
            canonComp.SetDamage(damage);

            if (level == 2)
            {
                canonComp.SetZoneConfig(burnRadius, burnDamagePerSec, burnDuration, fireVfxPrefab);
            }
        }

        Rigidbody rb = bomb.GetComponent<Rigidbody>();
        if (rb != null)
        {
            bomb.transform.SetParent(null);
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            Vector3 toTarget = currentTarget.position - spawnPos;
            Vector3 toTargetXZ = new Vector3(toTarget.x, 0f, toTarget.z);
            float dx = toTargetXZ.magnitude;
            float dy = toTarget.y;

            float v = projectileSpeed;
            float v2 = v * v;
            float g = Mathf.Abs(Physics.gravity.y);

            float underSqrt = v2 * v2 - g * (g * dx * dx + 2f * dy * v2);

            if (underSqrt < 0f)
            {
                Vector3 vel = (toTarget.normalized) * v;
                rb.linearVelocity = vel;
            }
            else
            {
                float root = Mathf.Sqrt(underSqrt);
                float tanTheta = (v2 - root) / (g * dx);
                float angle = Mathf.Atan(tanTheta);

                float vy = v * Mathf.Sin(angle);
                float vx = v * Mathf.Cos(angle);

                Vector3 vel = toTargetXZ.normalized * vx + Vector3.up * vy;
                rb.linearVelocity = vel;
                if (vel.sqrMagnitude > 0.001f)
                    bomb.transform.rotation = Quaternion.LookRotation(vel.normalized);
            }
        }
    }
}
