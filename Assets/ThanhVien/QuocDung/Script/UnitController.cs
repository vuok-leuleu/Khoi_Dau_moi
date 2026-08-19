using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum AttackMode
{
    Melee,
    Ranged,
    Tank
}

public enum UnitState
{
    Idle,
    Moving,
    Attacking
}

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
    [SerializeField] private string shieldBoolParam = "IsShield";

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

    public void SetHomePosition(Vector3 pos)
    {
        returnPosition = pos;
    }

    [Header("Expedition Wave Marching")]
    public bool isExpeditionMarching = false;
    public int marchStartWave = 1;
    public int marchWavesToReach = 3;
    public int marchTargetWave = 4;
    public Vector3 marchStartPosition;
    public Vector3 marchDestinationPosition;


    public bool isMarchingToEnemyBase => isExpeditionMarching || isRespondingToWarning || isReturning;
    public bool isDead => hpSoldier != null && hpSoldier.IsDead;

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
            if (owner != null && owner != this && owner.gameObject.activeInHierarchy && owner.currentTarget == enemy)
            {
                return true;
            }
            claimedEnemies.Remove(id);
        }
        return false;
    }

    void ClaimEnemy(GameObject enemy)
    {
        if (enemy == null) return;
        ReleaseCurrentTargetClaim();
        int id = enemy.GetInstanceID();
        claimedEnemies[id] = this;
        currentTargetInstanceId = id;
    }

    void ReleaseCurrentTargetClaim()
    {
        if (currentTargetInstanceId != 0)
        {
            if (claimedEnemies.TryGetValue(currentTargetInstanceId, out UnitController owner) && owner == this)
            {
                claimedEnemies.Remove(currentTargetInstanceId);
            }
            currentTargetInstanceId = 0;
        }
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        hpSoldier = GetComponent<HPSoldier>();
        if (hpSoldier == null)
        {
            hpSoldier = GetComponentInParent<HPSoldier>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        
        if (returnPosition == Vector3.zero)
        {
            returnPosition = transform.position;
        }
    }

    void Start()
    {
        if (agent != null && !agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }
        
        lowFreqCoroutine = StartCoroutine(LowFrequencyScanRoutine());
    }

    IEnumerator LowFrequencyScanRoutine()
    {
        while (true)
        {
            float currentFrequency = (currentState == UnitState.Moving && currentTarget != null) 
                ? Mathf.Max(0.05f, scanFrequency * 0.5f) 
                : scanFrequency;

            yield return new WaitForSeconds(currentFrequency);
            PerformAreaScan();
        }
    }

    void PerformAreaScan()
    {
        if (currentState == UnitState.Attacking && currentTarget != null) return;

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectRadius, enemyLayer);
        if (hitColliders.Length == 0) return;

        GameObject bestTarget = null;
        float minDistance = float.MaxValue;

        for (int i = 0; i < hitColliders.Length; i++)
        {
            Collider col = hitColliders[i];
            if (col == null || !col.gameObject.activeInHierarchy) continue;

            if (col.CompareTag(enemyTag) || (col.transform.parent != null && col.transform.parent.CompareTag(enemyTag)))
            {
                GameObject enemyObj = col.gameObject;
                if (!IsEnemyAlive(enemyObj)) continue;

                float dist = Vector3.Distance(transform.position, enemyObj.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestTarget = enemyObj;
                }
            }
        }

        if (bestTarget != null && bestTarget != currentTarget)
        {
            currentTarget = bestTarget;
            ClaimEnemy(bestTarget);
            currentState = UnitState.Moving;
        }
    }

    void Update()
    {
        if (hpSoldier != null && hpSoldier.IsDead)
        {
            ReleaseCurrentTargetClaim();
            if (agent != null && agent.isOnNavMesh && !agent.isStopped)
            {
                agent.isStopped = true;
            }
            return;
        }

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        bool isSceneBattle = currentScene.ToLower().Contains("battle");

        if (isSceneBattle)
        {
            isExpeditionMarching = false;
        }

        // Giữ lính tại vùng xuất phát trong thời gian hành quân theo wave.
        if (isExpeditionMarching && !isSceneBattle)
        {
            int currentWave = DayNightManager.HasInstance && DayNightManager.Ins != null
                ? DayNightManager.Ins.CurrentWave
                : marchStartWave;
            bool hasReachedDestination = currentWave >= marchTargetWave;

            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }

            if (hasReachedDestination)
            {
                transform.position = marchDestinationPosition;
                isExpeditionMarching = false;
                isRespondingToWarning = false;
            }

            currentState = UnitState.Idle;
            UpdateAnimation();
            return;
        }
        // Chỉ quét tự động tìm mục tiêu nếu autoAggro = true HOẶC đang trong chế độ phản công theo nút bấm (isRespondingToWarning)
        if (autoAggro || isRespondingToWarning)
        {
            if (currentTarget == null || !currentTarget.CompareTag(enemyTag) || !IsEnemyAlive(currentTarget))
            {
                GameObject enemy = FindClosestEnemy();
                if (enemy != null)
                {
                    currentTarget = enemy;
                    ClaimEnemy(enemy);
                    currentState = UnitState.Moving;
                }
                else
                {
                    if (currentTarget != null)
                    {
                        ReleaseCurrentTargetClaim();
                        currentTarget = null;
                        if (!isRespondingToWarning && !isReturning)
                        {
                            currentState = UnitState.Idle;
                        }
                    }
                }
            }
        }
        else
        {
            ReleaseCurrentTargetClaim();
            currentTarget = null;
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
                    float distToWarning = Vector3.Distance(transform.position, warningPosition);
                    if (distToWarning > GetAttackStopDistance() && agent != null && agent.isOnNavMesh && Vector3.Distance(agent.destination, warningPosition) > 0.5f)
                    {
                        SetDestination(warningPosition);
                        currentState = UnitState.Moving;
                    }
                }
            }
        }

        if (isReturning)
        {
            if (Vector3.Distance(transform.position, returnPosition) <= 0.8f)
            {
                isReturning = false;
                autoAggro = false;
                currentState = UnitState.Idle;
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            }
            else
            {
                SetDestination(returnPosition);
                currentState = UnitState.Moving;
            }
        }

        // State Machine
        switch (currentState)
        {
            case UnitState.Idle:
                if (agent != null && agent.isOnNavMesh && !agent.isStopped)
                {
                    agent.isStopped = true;
                }
                break;

            case UnitState.Moving:
                if (currentTarget != null)
                {
                    float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
                    if (distanceToTarget <= GetAttackStopDistance())
                    {
                        currentState = UnitState.Attacking;
                        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                    }
                    else
                    {
                        if (Vector3.Distance(lastDestinationPos, currentTarget.transform.position) > destinationUpdateThreshold)
                        {
                            SetDestination(currentTarget.transform.position);
                            lastDestinationPos = currentTarget.transform.position;
                        }
                    }
                }
                break;

            case UnitState.Attacking:
                if (currentTarget != null && IsEnemyAlive(currentTarget))
                {
                    float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
                    if (distanceToTarget > GetAttackStopDistance() * 1.15f)
                    {
                        currentState = UnitState.Moving;
                        SetDestination(currentTarget.transform.position);
                    }
                    else
                    {
                        Vector3 targetLookPos = currentTarget.transform.position;
                        targetLookPos.y = transform.position.y;
                        Vector3 dir = (targetLookPos - transform.position).normalized;
                        if (dir != Vector3.zero)
                        {
                            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);
                        }

                        if (Time.time >= nextAttackTime)
                        {
                            PerformAttack();
                            nextAttackTime = Time.time + attackRate;
                        }
                    }
                }
                else
                {
                    ReleaseCurrentTargetClaim();
                    currentTarget = null;
                    currentState = UnitState.Idle;
                }
                break;
        }

        UpdateAnimation();
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

    public void StartExpeditionMarch(Vector3 destinationPos, int wavesToReach = -1, Transform targetBuilding = null)
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
            wavesToReach = Mathf.Max(1, Mathf.RoundToInt(dist / 15f));
        }

        marchWavesToReach = wavesToReach;
        marchTargetWave = marchStartWave + marchWavesToReach;
        isExpeditionMarching = true;
        isRespondingToWarning = true;
        isReturning = false;
        currentState = UnitState.Moving;

    }


    public void EnableCombat(Vector3 enemyTargetPos)
    {
        autoAggro = true;
        RespondToWarning(enemyTargetPos);
    }

    private bool AnyEnemyAlive()
    {
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

        EnemyAI[] enemyAIs = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (var e in enemyAIs)
        {
            if (e != null && e.gameObject.activeInHierarchy)
            {
                var hp = e.GetComponentInParent<EnemyHealth>();
                if (hp == null || hp.CurrentHealth > 0f) return true;
            }
        }

        return false;
    }

    void SetDestination(Vector3 targetPos)
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(targetPos);
        }
    }

    void PerformAttack()
    {
        if (attackMode == AttackMode.Melee)
        {
            PlayAnimationTrigger(attackBoolParam);
            StartCoroutine(ApplyMeleeDamageAfterDelay(0.35f));
        }
        else if (attackMode == AttackMode.Ranged)
        {
            PlayAnimationTrigger(shootBoolParam);
            StartCoroutine(SpawnRangedProjectileAfterDelay(projectileSpawnDelay));
        }
        else if (attackMode == AttackMode.Tank)
        {
            PlayAnimationTrigger(shieldBoolParam);
            StartCoroutine(ApplyMeleeDamageAfterDelay(0.35f));
        }
    }

    private void PlayAnimationTrigger(string paramName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(paramName)) return;

        if (HasAnimatorParameter(animator, paramName, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(paramName);
        }
        else if (HasAnimatorParameter(animator, paramName, AnimatorControllerParameterType.Bool))
        {
            StartCoroutine(TriggerBoolAnimation(paramName));
        }
    }

    private IEnumerator TriggerBoolAnimation(string paramName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(paramName)) yield break;
        animator.SetBool(paramName, true);
        yield return new WaitForSeconds(0.15f);
        if (animator != null)
        {
            animator.SetBool(paramName, false);
        }
    }

    private bool HasAnimatorParameter(Animator anim, string paramName, AnimatorControllerParameterType paramType)
    {
        if (anim == null || string.IsNullOrWhiteSpace(paramName)) return false;
        foreach (var p in anim.parameters)
        {
            if (p.type == paramType && p.name == paramName)
            {
                return true;
            }
        }
        return false;
    }

    IEnumerator ApplyMeleeDamageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (currentTarget != null && IsEnemyAlive(currentTarget))
        {
            float dist = Vector3.Distance(transform.position, currentTarget.transform.position);
            if (dist <= GetAttackStopDistance() * 1.35f)
            {
                var hp = currentTarget.GetComponentInParent<EnemyHealth>();
                if (hp != null)
                {
                    hp.TakeDamage(attackDamage, currentTarget.transform.position);
                }
                else
                {
                    var dmg = currentTarget.GetComponentInParent<IDamageable>();
                    if (dmg != null) dmg.TakeDamage(attackDamage, currentTarget.transform.position);
                }
            }
        }
    }

    IEnumerator SpawnRangedProjectileAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (currentTarget != null && IsEnemyAlive(currentTarget) && projectilePrefab != null)
        {
            Transform target = currentTarget.transform;
            Vector3 spawnPos = firePoint != null ? firePoint.position : (transform.position + Vector3.up * 1.2f);
            Vector3 aimDir = (target.position - spawnPos).normalized;
            Quaternion spawnRot = aimDir != Vector3.zero ? Quaternion.LookRotation(aimDir) : transform.rotation;

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
                if (rb != null)
                {
                    rb.linearVelocity = aimDir * projectileSpeed;
                }
            }
        }
    }

    GameObject FindClosestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        GameObject closest = null;
        float minDistance = float.MaxValue;
        
        List<GameObject> validEnemies = new List<GameObject>();
        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject e = enemies[i];
            if (e != null && e.activeInHierarchy && IsEnemyAlive(e))
            {
                validEnemies.Add(e);
            }
        }

        if (validEnemies.Count == 0 && (isRespondingToWarning || autoAggro))
        {
            EnemyAI[] allEnemyAIs = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            foreach (var ai in allEnemyAIs)
            {
                if (ai != null && ai.gameObject.activeInHierarchy && IsEnemyAlive(ai.gameObject))
                {
                    validEnemies.Add(ai.gameObject);
                }
            }
        }

        for (int i = 0; i < validEnemies.Count; i++)
        {
            GameObject enemy = validEnemies[i];
            if (IsEnemyClaimedByOther(enemy)) continue;

            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = enemy;
            }
        }

        if (closest == null)
        {
            for (int i = 0; i < validEnemies.Count; i++)
            {
                GameObject enemy = validEnemies[i];
                float dist = Vector3.Distance(transform.position, enemy.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closest = enemy;
                }
            }
        }

        return closest;
    }

    bool IsShooting()
    {
        if (attackMode != AttackMode.Ranged) return false;
        if (currentState == UnitState.Attacking) return true;
        if (animator == null) return false;

        var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName("Shoot");
    }

    bool IsShielding()
    {
        if (attackMode != AttackMode.Tank) return false;
        if (currentState == UnitState.Attacking) return true;
        if (animator == null) return false;

        var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName("Shield");
    }

    void UpdateAnimation()
    {
        if (animator == null) return;

        bool isMoving = false;

        if (isExpeditionMarching)
        {
            isMoving = true;
        }
        else if (currentState == UnitState.Moving && agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            isMoving = agent.velocity.sqrMagnitude > 0.1f && !agent.isStopped;
        }
        else if (isReturning)
        {
            isMoving = true;
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

        if (!string.IsNullOrWhiteSpace(shieldBoolParam))
        {
            bool isShielding = IsShielding();
            if (isShielding)
            {
                var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.IsName("Shield"))
                {
                    if (stateInfo.normalizedTime >= 0.9f && !animator.IsInTransition(0))
                    {
                        animator.Play("Shield", 0, 0f);
                    }
                }
                else
                {
                    if (!animator.IsInTransition(0) || !animator.GetNextAnimatorStateInfo(0).IsName("Shield"))
                    {
                        animator.Play("Shield", 0, 0f);
                    }
                }

                if (HasAnimatorParameter(animator, shieldBoolParam, AnimatorControllerParameterType.Trigger))
                {
                    animator.SetTrigger(shieldBoolParam);
                }
                else if (HasAnimatorParameter(animator, shieldBoolParam, AnimatorControllerParameterType.Bool))
                {
                    animator.SetBool(shieldBoolParam, true);
                }
            }
            else
            {
                if (HasAnimatorParameter(animator, shieldBoolParam, AnimatorControllerParameterType.Trigger))
                {
                    animator.ResetTrigger(shieldBoolParam);
                }
                else if (HasAnimatorParameter(animator, shieldBoolParam, AnimatorControllerParameterType.Bool))
                {
                    animator.SetBool(shieldBoolParam, false);
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

    void OnDestroy()
    {
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, GetAttackStopDistance());
    }
}
