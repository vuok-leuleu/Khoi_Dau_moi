using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class WorkerFindStone : MonoBehaviour
{
    public static List<Stone> Registry = new List<Stone>(); 

    public NavMeshAgent     agent;
    public WorkerCarryStone carrySystem;
    public Animator         animator;
    public WorkerStamina    stamina;

    public float mineDistance = 1.8f;
    public float mineTime     = 1.5f;

    [Header("Animation Settings")]
    public string mineTriggerName = "Mine"; 

    [Header("Idle/Wander Settings")]
    public float wanderRadius = 5f;
    public float wanderInterval = 3f;
    private float wanderTimer = 0f;
    private Vector3 anchorPosition;

    [Header("Settings Nâng Cấp")]
    public float stuckTimeout = 2.0f;
    private float stuckTimer = 0f;
    private float depositRetryTimer = 0f;
    private float totalWaitTimer = 0f; 

    [Header("Lift/Pickup Settings")]
    public string liftTriggerName = "Lift";
    public float  liftTime        = 1f;   // tổng thời gian animation Lift chạy (khớp với độ dài clip)
    public float  liftGrabRatio   = 0.6f; // % thời gian khi tay chạm đá để gắn vào tay

    private bool        isLifting            = false;
    private float       liftTimer            = 0f;
    private bool        hasGrabbedDuringLift = false;
    private StonePickup pendingStone         = null;

    private Stone targetStone;
    private float mineTimer            = 0f;
    private bool  hasTriggeredMineAnim = false;
    private bool  wasResting           = false;
    private float findStoneCooldown    = 0f;
    private const float FIND_STONE_INTERVAL = 0.5f;

    private bool isHeadingToStone   = false;
    private bool isHeadingToDeposit = false;

    void Start()
    {
        if (stamina == null) stamina = GetComponent<WorkerStamina>();
        anchorPosition = transform.position;
    }

    void Update()
    {
        UpdateAnimationSpeed();
        CheckStuck();

        if (isLifting)
        {
            HandleLifting();
            return;
        }

        if (carrySystem.IsCarrying())
        {
            isHeadingToStone = false;
            HandleCarrying();
            return;
        }

        if (stamina != null && !stamina.CanWork())
        {
            if (!wasResting)
            {
                wasResting = true;
                ReleaseCurrentStone();
                if (animator != null) animator.ResetTrigger(mineTriggerName);
                hasTriggeredMineAnim = false;
                mineTimer            = 0f;
                isHeadingToStone     = false;
                isHeadingToDeposit   = false;
            }
            return; 
        }

        wasResting = false;
        isHeadingToDeposit = false;

        if (targetStone == null || !targetStone.gameObject.activeInHierarchy || targetStone.GetCurrentHealth() <= 0)
        {
            if (targetStone != null) ReleaseCurrentStone();
            
            HandleFindStone();
            
            if (targetStone == null)
            {
                stamina?.SetDraining(false); 
                HandleWander();
                return;
            }
        }

        stamina?.SetDraining(true);

        float dist = Vector3.Distance(transform.position, targetStone.GetMinePoint(transform.position));
        if (dist > mineDistance)
        {
            HandleMoveToStone();
            return;
        }

        HandleMining();
    }

    void HandleWander()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        wanderTimer += Time.deltaTime;
        if (wanderTimer >= wanderInterval)
        {
            wanderTimer = 0f;
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
            {
                Vector3 randDir = Random.insideUnitSphere * wanderRadius + anchorPosition;
                if (NavMesh.SamplePosition(randDir, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
                {
                    agent.isStopped = false;
                    agent.SetDestination(hit.position);
                }
            }
        }
    }

    void UpdateAnimationSpeed()
    {
        if (animator == null || agent == null) return;
        float speed = agent.isStopped ? 0f : (agent.speed > 0f ? agent.velocity.magnitude / agent.speed : 0f);
        animator.SetFloat("Speed", speed, 0.05f, Time.deltaTime);
    }

    void HandleCarrying()
    {
        if (!isHeadingToDeposit)
        {
            bool moved = carrySystem.MoveToStorage();
            if (moved)
            {
                isHeadingToDeposit = true;
            }
            else
            {
                // Chưa có kho → đứng yên chờ, thử lại sau
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            }
            return;
        }

        bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f;
        if (arrived)
        {
            agent.isStopped = true;
            depositRetryTimer -= Time.deltaTime;
            totalWaitTimer += Time.deltaTime;

            if (depositRetryTimer <= 0f)
            {
                if (carrySystem.TryDeposit())
                {
                    depositRetryTimer = 0f;
                    totalWaitTimer = 0f;
                    isHeadingToDeposit = false;
                }
                else
                {
                    depositRetryTimer = 2.5f;
                    if (totalWaitTimer >= 15f)
                    {
                        // Kho đầy hoặc mất — tìm kho khác, KHÔNG drop item
                        totalWaitTimer = 0f;
                        depositRetryTimer = 0f;
                        isHeadingToDeposit = false;
                        if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
                    }
                }
            }
        }
        else
        {
            depositRetryTimer = 0f;
            totalWaitTimer = 0f;
        }
    }

    void HandleFindStone()
    {
        findStoneCooldown -= Time.deltaTime;
        if (findStoneCooldown <= 0f)
        {
            findStoneCooldown = FIND_STONE_INTERVAL;
            FindNearestStoneOptimized();
        }
    }

    void FindNearestStoneOptimized()
    {
        float minDist = Mathf.Infinity;
        Stone best = null;

        // BUG FIX: dùng Registry (của WorkerFindStone) thay vì Stone.Registry (của class Stone)
        for (int i = Registry.Count - 1; i >= 0; i--)
        {
            Stone stone = Registry[i];
            if (stone == null || !stone.gameObject.activeInHierarchy || stone.GetCurrentHealth() <= 0) { Registry.RemoveAt(i); continue; }
            if (!stone.TryClaim()) continue;

            float dist = Vector3.Distance(transform.position, stone.transform.position);
            if (dist < minDist) { if (best != null) best.Release(); minDist = dist; best = stone; }
            else stone.Release();
        }

        if (best == null)
        {
            Stone[] stones = GameObject.FindObjectsOfType<Stone>();
            foreach (var stone in stones)
            {
                if (stone == null || !stone.gameObject.activeInHierarchy || stone.GetCurrentHealth() <= 0) continue;
                if (!stone.TryClaim()) continue;
                float dist = Vector3.Distance(transform.position, stone.transform.position);
                if (dist < minDist) { if (best != null) best.Release(); minDist = dist; best = stone; }
                else stone.Release();
            }
        }

        if (best != null)
        {
            targetStone = best;
            mineTimer = 0f;
            hasTriggeredMineAnim = false;
            isHeadingToStone = false;
        }
    }

    void HandleMoveToStone()
    {
        if (agent.isOnNavMesh && !isHeadingToStone)
        {
            isHeadingToStone = true;
            agent.isStopped = false;
            agent.SetDestination(targetStone.GetMinePoint(transform.position));
        }
        hasTriggeredMineAnim = false;
    }

    void HandleMining()
    {
        agent.isStopped = true;
        isHeadingToStone = false; 

        if (!hasTriggeredMineAnim)
        {
            hasTriggeredMineAnim = true;
            if (animator != null) { animator.ResetTrigger(mineTriggerName); animator.SetTrigger(mineTriggerName); }
        }

        mineTimer += Time.deltaTime;
        if (mineTimer < mineTime) return;

        mineTimer            = 0f;
        hasTriggeredMineAnim = false;

        StonePickup[] drops = targetStone.TakeDamage(1);
        if (drops != null && drops.Length > 0)
        {
            // Đá đã vỡ: nhả claim ngay, rồi chơi animation Lift trước khi gắn đá vào tay
            ReleaseCurrentStone();
            StartLifting(drops[0]);
        }
    }

    void StartLifting(StonePickup stone)
    {
        pendingStone         = stone;
        isLifting            = true;
        liftTimer            = 0f;
        hasGrabbedDuringLift = false;

        if (animator != null) { animator.ResetTrigger(liftTriggerName); animator.SetTrigger(liftTriggerName); }
    }

    void HandleLifting()
    {
        agent.isStopped = true;
        liftTimer += Time.deltaTime;

        // Gắn đá vào tay đúng lúc tay chạm xuống trong animation Lift
        if (!hasGrabbedDuringLift && liftTimer >= liftTime * liftGrabRatio)
        {
            hasGrabbedDuringLift = true;
            if (pendingStone != null) carrySystem.PickupStone(pendingStone);
        }

        if (liftTimer < liftTime) return;

        isLifting    = false;
        pendingStone = null;
    }

    void ReleaseCurrentStone()
    {
        if (targetStone != null)
        {
            targetStone.Release();
            targetStone = null;
        }
        isHeadingToStone = false;
    }

    void CheckStuck()
    {
        bool isResting = stamina != null && !stamina.CanWork();
        if (agent == null || agent.isStopped || !agent.hasPath || isResting) return;

        if (agent.velocity.sqrMagnitude < 0.01f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= stuckTimeout)
            {
                stuckTimer = 0f;
                agent.ResetPath();
                if (carrySystem.IsCarrying()) carrySystem.MoveToStorage();
                isHeadingToStone = false;
                isHeadingToDeposit = false;
            }
        }
        else stuckTimer = 0f;
    }
}