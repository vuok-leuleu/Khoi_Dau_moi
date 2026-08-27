using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Implement this on an enemy that must finish a death animation before its
/// EnemyHealth component removes the GameObject.
/// </summary>
public interface IDeathAnimationHandler
{
    /// <returns>How long EnemyHealth should keep the object alive, in seconds.</returns>
    float PlayDeathAnimation();
}

/// <summary>
/// Controls the dragon's entrance and ground combat.
/// Attack order: Attack 1, Attack 1, Attack 2, then repeats.
/// </summary>
[DisallowMultipleComponent]
public class Dragon : MonoBehaviour, IDeathAnimationHandler
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform villageCenter;
    [SerializeField] private NavMeshAgent agent;

    [Header("Spawn presentation")]
    [SerializeField] private bool playSpawnPresentation = true;
    [SerializeField, Min(0f)] private float idleLandingDuration = 2f;
    [SerializeField, Min(0f)] private float breatheFireDuration = 2.5f;

    [Header("Targeting - like EnemyAI")]
    [SerializeField] private bool autoStartCombat = true;
    [SerializeField, Min(1f)] private float targetSearchRange = 250f;
    [SerializeField, Min(0.05f)] private float targetRefreshInterval = 0.25f;
    [SerializeField] private LayerMask targetLayers = ~0;
    [Tooltip("When enabled, the dragon ignores soldiers/towers and goes straight to the Main target.")]
    [SerializeField] private bool attackMainDirectly;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 4f;
    [SerializeField, Min(0.1f)] private float stoppingDistance = 3f;
    [SerializeField, Min(0f)] private float turnSpeed = 720f;

    [Header("Combat")]
    [SerializeField, Min(0f)] private float attackDamage = 25f;
    [SerializeField, Min(0.05f)] private float attackCooldown = 1.5f;

    [Header("Animator state names")]
    [Tooltip("Names of the states in the Animator window, not necessarily the imported clip names.")]
    [SerializeField] private string idleState = "Idle";
    [SerializeField] private string idleLandingState = "Idle Landing";
    [SerializeField] private string breatheFireState = "Breathe Fire";
    [SerializeField] private string attack1State = "Attack 1";
    [SerializeField] private string attack2State = "Attack 2";
    [SerializeField] private string deathState = "Death";
    [SerializeField, Range(0f, 0.5f)] private float stateBlendDuration = 0.1f;
    [SerializeField, Min(0f)] private float deathFallbackDuration = 3f;

    [Header("Animator triggers")]
    [SerializeField] private string idleLandTrigger = "idleLand";
    [SerializeField] private string breatheFireTrigger = "breatheFire";
    [SerializeField] private string attack1Trigger = "attack1";
    [SerializeField] private string attack2Trigger = "attack2";
    [SerializeField] private string deathTrigger = "death";

    private Transform currentTarget;
    private float nextTargetRefreshTime;
    private float nextAttackTime;
    private int attackStep;
    private bool spawnPresentationFinished;
    private bool isAttacking;
    private bool isDead;
    private bool isPrimaryDragon;
    private bool hasLandingGroundPosition;
    private Vector3 landingGroundPosition;
    private Coroutine spawnRoutine;
    private Coroutine attackRoutine;

    /// <summary>
    /// Called immediately after the dragon is spawned above the battlefield.
    /// Start runs on the next frame, so this target is ready for Idle Landing.
    /// </summary>
    public void SetLandingGroundPosition(Vector3 groundPosition)
    {
        landingGroundPosition = groundPosition;
        hasLandingGroundPosition = true;
    }

    private void Awake()
    {
        // The current prefab accidentally contains this component twice. Keep
        // only the first one active so the entrance and attacks never run twice.
        Dragon[] dragons = GetComponents<Dragon>();
        isPrimaryDragon = dragons.Length == 0 || dragons[0] == this;
        if (!isPrimaryDragon)
        {
            enabled = false;
            return;
        }

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = stoppingDistance;
            agent.angularSpeed = turnSpeed;
        }
    }

    private void Start()
    {
        if (!isPrimaryDragon) return;

        spawnRoutine = StartCoroutine(SpawnPresentationRoutine());
    }

    private IEnumerator SpawnPresentationRoutine()
    {
        if (playSpawnPresentation)
        {
            if (!SetTrigger(idleLandTrigger)) PlayState(idleLandingState);
            float landingDuration = GetStateDuration(idleLandingState, idleLandingDuration);
            yield return PlayLandingMovement(landingDuration);

            if (!SetTrigger(breatheFireTrigger)) PlayState(breatheFireState);
            yield return new WaitForSeconds(GetStateDuration(breatheFireState, breatheFireDuration));
        }

        spawnPresentationFinished = true;
        spawnRoutine = null;
    }

    private IEnumerator PlayLandingMovement(float duration)
    {
        if (!hasLandingGroundPosition || duration <= 0f)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        Vector3 startPosition = transform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.position = Vector3.Lerp(startPosition, landingGroundPosition, progress);
            yield return null;
        }

        transform.position = landingGroundPosition;
    }

    private void Update()
    {
        if (!isPrimaryDragon || isDead || !spawnPresentationFinished || !autoStartCombat) return;

        RefreshTargetIfNeeded();
        if (!IsValidTarget(currentTarget)) return;

        float distance = GetDistanceToTarget(currentTarget);
        if (distance > stoppingDistance)
        {
            MoveTo(currentTarget);
            return;
        }

        StopMoving();
        FaceTarget(currentTarget.position);

        if (!isAttacking && Time.time >= nextAttackTime)
        {
            attackRoutine = StartCoroutine(AttackRoutine(currentTarget));
        }
    }

    /// <summary>Allows a spawner or wave manager to start the dragon manually.</summary>
    public void EnableCombat()
    {
        autoStartCombat = true;
    }

    /// <summary>Allows a spawner or cutscene to pause the dragon's combat AI.</summary>
    public void DisableCombat()
    {
        autoStartCombat = false;
        StopMoving();
        if (!isDead) PlayState(idleState);
    }

    public Transform GetCurrentTarget()
    {
        return currentTarget;
    }

    private IEnumerator AttackRoutine(Transform target)
    {
        isAttacking = true;
        nextAttackTime = Time.time + attackCooldown;

        // 0 -> Attack 1, 1 -> Attack 1, 2 -> Attack 2, repeat.
        bool useAttack2 = attackStep == 2;
        string stateToPlay = useAttack2 ? attack2State : attack1State;
        string triggerToSet = useAttack2 ? attack2Trigger : attack1Trigger;
        attackStep = (attackStep + 1) % 3;
        if (!SetTrigger(triggerToSet)) PlayState(stateToPlay);

        // EnemyAI applies the damage as soon as an attack begins, so the dragon
        // does the same. Use animation events later if a frame-perfect hit is needed.
        DamageTarget(target);

        yield return new WaitForSeconds(GetStateDuration(stateToPlay, attackCooldown));

        isAttacking = false;
        attackRoutine = null;
    }

    private void RefreshTargetIfNeeded()
    {
        if (IsValidTarget(currentTarget) && Time.time < nextTargetRefreshTime) return;

        nextTargetRefreshTime = Time.time + targetRefreshInterval;
        if (IsValidTarget(currentTarget)) return;

        currentTarget = FindBestTarget();
    }

    private Transform FindBestTarget()
    {
        if (attackMainDirectly) return FindMainTarget();

        Transform closestSoldier = null;
        Transform closestTower = null;
        float closestSoldierDistance = float.MaxValue;
        float closestTowerDistance = float.MaxValue;

        Collider[] candidates = Physics.OverlapSphere(
            transform.position,
            targetSearchRange,
            targetLayers,
            QueryTriggerInteraction.Ignore);

        foreach (Collider candidate in candidates)
        {
            if (candidate == null || candidate.transform == transform || candidate.transform.IsChildOf(transform)) continue;

            IDamageable damageable = candidate.GetComponentInParent<IDamageable>();
            Component damageableComponent = damageable as Component;
            if (damageableComponent == null || damageable.CurrentHealth <= 0f) continue;

            Transform targetRoot = damageableComponent.transform;
            float distance = GetDistanceToTarget(targetRoot);

            if (IsSoldier(targetRoot.gameObject) && distance < closestSoldierDistance)
            {
                closestSoldier = targetRoot;
                closestSoldierDistance = distance;
            }
            else if (IsTower(targetRoot.gameObject) && distance < closestTowerDistance)
            {
                closestTower = targetRoot;
                closestTowerDistance = distance;
            }
        }

        // Match EnemyAI's broad priority: units first, then defensive towers,
        // then the main settlement.
        if (closestSoldier != null) return closestSoldier;
        if (closestTower != null) return closestTower;
        return FindMainTarget();
    }

    private Transform FindMainTarget()
    {
        if (IsValidTarget(villageCenter)) return villageCenter;

        try
        {
            foreach (GameObject main in GameObject.FindGameObjectsWithTag("Main"))
            {
                if (main != null && main.activeInHierarchy)
                {
                    villageCenter = main.transform;
                    return villageCenter;
                }
            }
        }
        catch (UnityException)
        {
            // The project may not define the Main tag in every test scene.
        }

        GameObject namedMain = GameObject.Find("Nhachinhs") ?? GameObject.Find("Nhachinh");
        if (namedMain != null && namedMain.activeInHierarchy)
        {
            villageCenter = namedMain.transform;
        }

        return villageCenter;
    }

    private void MoveTo(Transform target)
    {
        Vector3 destination = GetClosestTargetPoint(target);

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = stoppingDistance;
            agent.isStopped = false;
            agent.SetDestination(destination);
            return;
        }

        // A NavMeshAgent is optional so the dragon can still be previewed in a
        // scene that has not baked a NavMesh yet.
        Vector3 direction = destination - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f) return;

        FaceTarget(destination);
        transform.position += direction.normalized * moveSpeed * Time.deltaTime;
    }

    private void StopMoving()
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            if (agent.hasPath) agent.ResetPath();
        }
    }

    private void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f) return;

        Quaternion desiredRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            desiredRotation,
            turnSpeed * Time.deltaTime);
    }

    private void DamageTarget(Transform target)
    {
        if (!IsValidTarget(target)) return;

        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(attackDamage, target.position);
        }
    }

    private bool IsValidTarget(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;

        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        return damageable == null || damageable.CurrentHealth > 0f;
    }

    private float GetDistanceToTarget(Transform target)
    {
        if (target == null) return float.MaxValue;

        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        float closestDistance = float.MaxValue;
        foreach (Collider targetCollider in colliders)
        {
            if (targetCollider == null || !targetCollider.enabled || targetCollider.isTrigger) continue;
            closestDistance = Mathf.Min(
                closestDistance,
                Vector3.Distance(transform.position, targetCollider.ClosestPoint(transform.position)));
        }

        return closestDistance == float.MaxValue
            ? Vector3.Distance(transform.position, target.position)
            : closestDistance;
    }

    private Vector3 GetClosestTargetPoint(Transform target)
    {
        if (target == null) return transform.position;

        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        Vector3 closestPoint = target.position;
        float closestDistance = float.MaxValue;
        foreach (Collider targetCollider in colliders)
        {
            if (targetCollider == null || !targetCollider.enabled || targetCollider.isTrigger) continue;
            Vector3 point = targetCollider.ClosestPoint(transform.position);
            float distance = Vector3.Distance(transform.position, point);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPoint = point;
            }
        }

        return closestPoint;
    }

    private static bool IsSoldier(GameObject target)
    {
        return target != null && (target.tag == "Soldier" || target.GetComponentInParent<HPSoldier>() != null);
    }

    private static bool IsTower(GameObject target)
    {
        return target != null &&
               (target.tag == "Tower" ||
                target.tag == "DefenseTower" ||
                target.GetComponentInParent<HPTower>() != null);
    }

    private bool PlayState(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName)) return false;

        if (!TryGetStateHash(stateName, out int stateHash))
        {
            Debug.LogWarning($"[Dragon] State '{stateName}' was not found in {animator.runtimeAnimatorController?.name}.", this);
            return false;
        }

        animator.CrossFadeInFixedTime(stateHash, stateBlendDuration, 0, 0f);
        return true;
    }

    private bool SetTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName)) return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == triggerName && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(triggerName);
                return true;
            }
        }

        Debug.LogWarning($"[Dragon] Trigger '{triggerName}' was not found in {animator.runtimeAnimatorController?.name}.", this);
        return false;
    }

    private float GetStateDuration(string stateName, float fallbackDuration)
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (string possibleStateName in GetPossibleStateNames(stateName))
            {
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                {
                    if (clip != null && clip.name == possibleStateName)
                    {
                        return clip.length;
                    }
                }
            }
        }

        return fallbackDuration;
    }

    private bool TryGetStateHash(string stateName, out int stateHash)
    {
        foreach (string possibleStateName in GetPossibleStateNames(stateName))
        {
            stateHash = Animator.StringToHash(possibleStateName);
            if (animator.HasState(0, stateHash)) return true;

            // Animator.HasState expects the full path for controllers that do
            // not resolve a short state name. The shown states are in Base Layer.
            stateHash = Animator.StringToHash($"Base Layer.{possibleStateName}");
            if (animator.HasState(0, stateHash)) return true;
        }

        stateHash = 0;
        return false;
    }

    // The package's original controller uses names without spaces, while the
    // states dragged into the Animator in this scene use friendly names.
    private static string[] GetPossibleStateNames(string stateName)
    {
        switch (stateName)
        {
            case "Idle": return new[] { "Idle", "Ground" };
            case "Breathe Fire": return new[] { "Breathe Fire", "BreatheFire" };
            case "Attack 1": return new[] { "Attack 1", "Attack01" };
            case "Attack 2": return new[] { "Attack 2", "Attack02" };
            default: return new[] { stateName };
        }
    }

    /// <summary>
    /// Called by EnemyHealth through IDeathAnimationHandler when health reaches zero.
    /// </summary>
    public float PlayDeathAnimation()
    {
        if (isDead) return 0f;

        isDead = true;
        autoStartCombat = false;
        currentTarget = null;

        if (spawnRoutine != null) StopCoroutine(spawnRoutine);
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        StopMoving();
        if (!SetTrigger(deathTrigger)) PlayState(deathState);

        return GetStateDuration(deathState, deathFallbackDuration);
    }
}
