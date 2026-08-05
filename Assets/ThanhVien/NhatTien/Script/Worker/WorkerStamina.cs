using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

public class WorkerStamina : MonoBehaviour
{
    [Header("Stamina Settings")]
    public float maxStamina = 100f;
    public float drainPerSecond = 5f;
    public float recoverPerSecond = 20f;
    public float restThreshold = 20f;
    public float resumeThreshold = 80f;

    [Header("Locations Settings")]
    public Kitchen kitchen;
    // House không còn được dùng để nghỉ ngơi/ngủ nữa — chỉ giữ lại để WorkerEnemyFlee
    // và WorkerSpawner dùng làm điểm spawn / trú ẩn khi bị enemy đuổi.
    public House house;
    [Range(0f, 1f)] public float hungryRecoverMultiplier = 0.3f;

    [Header("Visual Settings")]
    public GameObject workerModel;
    public GameObject[] extraModelsToHide;
    public float interactionRadius = 2.0f;
    public float waitingScatterRadius = 2.5f;
    public Transform restSpot;

    [Header("Events")]
    public UnityEvent onStaminaDepleted;
    public UnityEvent onStaminaRecovered;

    [HideInInspector] public bool isCarryingResources = false;
    
    private bool isReturnPending = false;

    private Vector3 targetRestPosition;
    private bool hasTargetRestPosition = false;
    private float currentStamina;
    private NavMeshAgent agent;
    private bool isResting = false;
    private bool isDraining = false;
    private bool isInsideKitchen = false;
    private bool isHungryInside = false;
    private Vector3 personalOffset = Vector3.zero;
    private bool _hasPersonalOffset = false;

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public bool IsResting => isResting;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        currentStamina = maxStamina;
    }

    void Start()
    {
        FindKitchen();
        FindHouse();
        FindRestSpot();
    }

    void OnEnable()
    {
        if (isInsideKitchen) kitchen?.Exit(this);

        ShowModel();
        isInsideKitchen = false;
        isHungryInside = false;
        hasTargetRestPosition = false;
        isDraining = false;
    }

    void OnDisable()
    {
        if (isInsideKitchen) kitchen?.Exit(this);
    }

    void Update()
    {
        if (isResting)
        {
            HandleResting();
        }
        else
        {
            if (isDraining && !isReturnPending)
            {
                currentStamina -= drainPerSecond * Time.deltaTime;
                currentStamina = Mathf.Max(currentStamina, 0f);
                if (currentStamina <= restThreshold)
                {
                    if (isCarryingResources) isReturnPending = true;
                    else StartResting();
                }
            }
        }
    }

    public void SetDraining(bool drain)
    {
        if (isResting) { isDraining = false; return; }
        isDraining = drain;
    }

    public bool CanWork()
    {
        return !isResting && !isReturnPending;
    }

    private void HandleResting()
    {
        HandleKitchenLogic();
    }

    private void HandleKitchenLogic()
    {
        if (kitchen == null) FindKitchen();
        if (kitchen == null)
        {
            HandleFallback();
            return;
        }

        if (isInsideKitchen)
        {
            float multiplier = isHungryInside ? hungryRecoverMultiplier : 1f;
            currentStamina = Mathf.Min(currentStamina + recoverPerSecond * multiplier * Time.deltaTime, maxStamina);
            if (CanStopResting()) StopResting();
            return;
        }

        if (kitchen.IsFull)
        {
            MoveToWaitingArea(kitchen.EntrancePosition);
            currentStamina = Mathf.Min(currentStamina + recoverPerSecond * hungryRecoverMultiplier * Time.deltaTime, maxStamina);
            if (CanStopResting()) StopResting();
            return;
        }

        ClearOffset();
        if (!hasTargetRestPosition)
        {
            targetRestPosition = kitchen.GetRestPosition();
            hasTargetRestPosition = true;
        }

        MoveToPosition(targetRestPosition);
        if (Vector3.Distance(transform.position, targetRestPosition) <= interactionRadius)
        {
            if (kitchen.Enter(this, out bool consumedFood))
            {
                isInsideKitchen = true;
                isHungryInside = !consumedFood;
                hasTargetRestPosition = false;
                HideModel();
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            }
        }
    }

    private void HandleFallback()
    {
        if (restSpot != null)
        {
            MoveToPosition(restSpot.position);
            float stopDist = (agent != null ? agent.stoppingDistance : 0.5f) + 0.5f;
            if (Vector3.Distance(transform.position, restSpot.position) <= stopDist)
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        }
        currentStamina = Mathf.Min(currentStamina + recoverPerSecond * Time.deltaTime, maxStamina);

        if (CanStopResting())
        {
            StopResting();
        }
    }

    public void OnResourcesDeposited()
    {
        isCarryingResources = false;
        if (isReturnPending)
        {
            isReturnPending = false;
            StartResting();
        }
    }

    private bool CanStopResting()
    {
        if (currentStamina < resumeThreshold) return false;
        return true;
    }

    private void StartResting()
    {
        if (isResting) return;
        isResting = true;
        isDraining = false;
        hasTargetRestPosition = false;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        onStaminaDepleted?.Invoke();
    }

    private void StopResting()
    {
        ShowModel();
        if (isInsideKitchen)
        {
            kitchen?.Exit(this);
            isInsideKitchen = false;
            isHungryInside = false;
        }
        isResting = false;
        isDraining = false;
        hasTargetRestPosition = false;
        ClearOffset();
        if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
        onStaminaRecovered?.Invoke();
    }

    private void FindKitchen()
    {
        if (kitchen != null) return;

        Kitchen[] kitchens = FindObjectsByType<Kitchen>(FindObjectsSortMode.None);
        float closestDist = Mathf.Infinity;
        Kitchen best = null;
        Kitchen fallback = null;
        foreach (var k in kitchens)
        {
            if (fallback == null) fallback = k;
            if (!k.IsFull)
            {
                float d = Vector3.Distance(transform.position, k.transform.position);
                if (d < closestDist) { closestDist = d; best = k; }
            }
        }
        kitchen = best != null ? best : fallback;
    }

    private void FindHouse()
    {
        if (house != null) return;

        House[] houses = FindObjectsByType<House>(FindObjectsSortMode.None);
        float closestDist = Mathf.Infinity;
        House best = null;
        House fallback = null;
        foreach (var h in houses)
        {
            if (fallback == null) fallback = h;
            if (!h.IsFull)
            {
                float d = Vector3.Distance(transform.position, h.transform.position);
                if (d < closestDist) { closestDist = d; best = h; }
            }
        }
        house = best != null ? best : fallback;
    }

    private void FindRestSpot()
    {
        if (restSpot != null) return;
        // Tìm Kitchen gần nhất và dùng EntrancePosition làm điểm nghỉ
        FindKitchen();
        if (kitchen != null) restSpot = kitchen.entrancePoint != null
            ? kitchen.entrancePoint
            : kitchen.transform;
    }

    private void MoveToWaitingArea(Vector3 center)
    {
        if (!_hasPersonalOffset)
        {
            Vector2 rand = Random.insideUnitCircle * waitingScatterRadius;
            personalOffset = new Vector3(rand.x, 0f, rand.y);
            _hasPersonalOffset = true;
        }
        MoveToPosition(center + personalOffset);
    }

    private void ClearOffset()
    {
        if (_hasPersonalOffset)
        {
            personalOffset = Vector3.zero;
            _hasPersonalOffset = false;
        }
    }

    private void MoveToPosition(Vector3 pos)
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(pos);
        }
    }

    private void ShowModel()
    {
        if (workerModel != null) workerModel.SetActive(true);
        if (extraModelsToHide != null)
            foreach (var obj in extraModelsToHide) if (obj != null) obj.SetActive(true);
    }

    private void HideModel()
    {
        if (workerModel != null) workerModel.SetActive(false);
        if (extraModelsToHide != null)
            foreach (var obj in extraModelsToHide) if (obj != null) obj.SetActive(false);
    }
}