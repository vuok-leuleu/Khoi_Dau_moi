using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MoveModeController : MonoBehaviour
{
    public static MoveModeController Ins { get; private set; }
    public static bool IsMoveModeActive => Ins != null && Ins.isSelectingDestination;

    [Header("Move View")]
    [SerializeField] private Camera moveCamera;
    [SerializeField, Min(10f)] private float overviewCameraHeight = 55f;
    [SerializeField, Min(10f)] private float routePreviewCameraHeight = 36f;
    [SerializeField, Min(5f)] private float selectedCameraHeight = 22f;
    [SerializeField] private float cameraMoveSpeed = 7f;
    [SerializeField, Min(0.05f)] private float routePreviewHeight = 0.8f;

    private SettlementZone sourceZone;
    private SettlementZone previewDestination;
    private SoldierPoint routePreview;
    private SoldierPoint routePreviewPrefab;
    private RTSCameraController rtsCameraController;
    private bool isSelectingDestination;
    private Coroutine cameraRoutine;

    public bool HasPreviewDestination => isSelectingDestination && previewDestination != null;

    private void Awake()
    {
        if (Ins == null) Ins = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
        CacheCameraComponents();
    }

    private void OnDestroy()
    {
        if (Ins == this) Ins = null;
    }

    private void Update()
    {
        if (!isSelectingDestination) return;

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelMoveMode();
            return;
        }

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUi())
        {
            TrySelectDestination();
        }
    }

    public void Configure(float overviewHeight, float previewCameraHeight, float selectedHeight, float arrowHeight)
    {
        overviewCameraHeight = Mathf.Max(10f, overviewHeight);
        routePreviewCameraHeight = Mathf.Max(10f, previewCameraHeight);
        selectedCameraHeight = Mathf.Max(5f, selectedHeight);
        routePreviewHeight = Mathf.Max(0.05f, arrowHeight);
    }

    public void BeginMoveMode(SettlementZone zone, SoldierPoint soldierPointUIPrefab)
    {
        if (zone == null) return;

        CacheCameraComponents();
        sourceZone = zone;
        isSelectingDestination = true;
        EnsureRoutePreview(soldierPointUIPrefab);
        if (routePreview == null)
        {
            CancelMoveMode();
            return;
        }

        previewDestination = null;
        SettlementSidePanelUI.Ins?.SetMoveButtonLabel("MOVE");

        // 1. Tìm điểm đến tiếp theo ban đầu và vẽ mũi tên chỉ đến đó
        SettlementZone initialDestination = FindInitialDestination();
        if (initialDestination != null)
        {
            routePreview.Setup(GetRouteAnchor(sourceZone), GetRouteAnchor(initialDestination), (Transform)null, routePreviewHeight);
            routePreview.gameObject.SetActive(true);
        }
        else
        {
            routePreview.gameObject.SetActive(false);
        }

        // 2. Camera chuyển góc nhìn bao quát toàn cảnh (overview)
        GetMapOverview(out Vector3 overviewCenter, out float overviewHeight);
        MoveCameraTo(overviewCenter, overviewHeight);
    }

    public void CancelMoveMode()
    {
        isSelectingDestination = false;
        sourceZone = null;
        previewDestination = null;
        if (routePreview != null) routePreview.gameObject.SetActive(false);
                SettlementSidePanelUI.Ins?.SetMoveButtonLabel("MOVE");
    }

    private void EnsureRoutePreview(SoldierPoint soldierPointUIPrefab)
    {
        if (soldierPointUIPrefab == null)
        {
            Debug.LogWarning("[MoveModeController] SoldierPointUI prefab chưa được gán cho SettlementSidePanelUI.");
            return;
        }

        if (routePreview != null && routePreviewPrefab == soldierPointUIPrefab) return;

        if (routePreview != null) Destroy(routePreview.gameObject);

        routePreview = Instantiate(soldierPointUIPrefab, transform);
        routePreview.gameObject.name = "MoveModeSoldierPointPreview";
        routePreviewPrefab = soldierPointUIPrefab;
    }

    private void TrySelectDestination()
    {
        if (!TryGetSettlementUnderPointer(out SettlementZone destination) || destination == sourceZone)
        {
            CancelMoveMode();
            return;
        }

        SetPreviewDestination(destination);
        // Khi bấm vào vùng đất tiếp theo thì camera zoom vào vùng đất đó và đổi nút sang APPLY
        MoveCameraTo(destination.transform.position, selectedCameraHeight);
    }

    private void SetPreviewDestination(SettlementZone destination)
    {
        if (destination == null || sourceZone == null || routePreview == null) return;

        previewDestination = destination;
        routePreview.Setup(GetRouteAnchor(sourceZone), GetRouteAnchor(destination), (Transform)null, routePreviewHeight);
        routePreview.gameObject.SetActive(true);
        SettlementSidePanelUI.Ins?.SetMoveButtonLabel("APPLY");
    }

    public void ApplySelectedDestination()
    {
        if (!HasPreviewDestination || sourceZone == null) return;

        List<UnitController> availableSoldiers = GetSoldiersStationedInZone(sourceZone);
        if (availableSoldiers.Count == 0)
        {
            Debug.LogWarning($"[MoveModeController] {sourceZone.settlementName} không có lính sẵn sàng để điều quân.");
            return;
        }

        ConfirmDestination(previewDestination, availableSoldiers);
    }

    private void ConfirmDestination(SettlementZone destination, List<UnitController> selectedSoldiers)
    {
        if (destination == null || selectedSoldiers == null || selectedSoldiers.Count == 0) return;

        isSelectingDestination = false;
        SettlementZone selectedZone = destination;
        UnitController routeLeader = SendSoldiersToDestination(sourceZone, selectedZone, selectedSoldiers);
        if (routePreview != null)
        {
            if (routeLeader != null)
            {
                routePreview.Setup(GetRouteAnchor(sourceZone), GetRouteAnchor(selectedZone), routeLeader, routePreviewHeight);
            }
            else
            {
                routePreview.gameObject.SetActive(false);
            }
        }

        if (SettlementManager.Ins != null)
        {
            SettlementManager.Ins.SelectSettlement(selectedZone);
        }

        MoveCameraTo(selectedZone.transform.position, selectedCameraHeight, () =>
        {
            sourceZone = null;
            previewDestination = null;
        });
        SettlementSidePanelUI.Ins?.SetMoveButtonLabel("MOVE");
    }

    private static Transform GetRouteAnchor(SettlementZone zone)
    {
        return zone != null && zone.townHallPoint != null ? zone.townHallPoint : zone.transform;
    }
    private static UnitController SendSoldiersToDestination(
        SettlementZone source,
        SettlementZone destination,
        List<UnitController> selectedSoldiers)
    {
        if (source == null || destination == null || selectedSoldiers == null) return null;

        Transform sourceAnchor = GetRouteAnchor(source);
        Transform destinationAnchor = GetRouteAnchor(destination);
        if (sourceAnchor == null || destinationAnchor == null) return null;

        int wavesToReach = SoldierPoint.CalculateWaveCount(
            Vector3.Distance(sourceAnchor.position, destinationAnchor.position));
        int dispatchedCount = 0;
        UnitController routeLeader = null;

        foreach (UnitController soldier in selectedSoldiers)
        {
            if (soldier == null || !soldier.gameObject.activeInHierarchy) continue;

            soldier.StartExpeditionMarch(
                destinationAnchor.position,
                wavesToReach,
                null,
                destination.settlementName,
                source.settlementName);
            if (routeLeader == null) routeLeader = soldier;
            dispatchedCount++;
        }

        Debug.Log($"[MoveModeController] Đã điều {dispatchedCount} lính từ {source.settlementName} đến {destination.settlementName} trong {wavesToReach} wave.");
        return routeLeader;
    }

    private static List<UnitController> GetSoldiersStationedInZone(SettlementZone zone)
    {
        List<UnitController> soldiers = new List<UnitController>();
        if (zone == null) return soldiers;

        UnitController[] allUnits = FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        foreach (UnitController soldier in allUnits)
        {
            if (soldier == null || !soldier.gameObject.activeInHierarchy) continue;

            if (soldier.hasReachedExpeditionDestination)
            {
                if (soldier.marchDestinationZoneName == zone.settlementName)
                {
                    soldiers.Add(soldier);
                }
                continue;
            }

            if (!string.IsNullOrEmpty(soldier.stationedSettlementZoneName))
            {
                if (soldier.stationedSettlementZoneName == zone.settlementName)
                {
                    soldiers.Add(soldier);
                }
                continue;
            }

            SettlementZone parentZone = soldier.GetComponentInParent<SettlementZone>();
            if (parentZone == zone)
            {
                soldiers.Add(soldier);
            }
        }

        return soldiers;
    }
    private SettlementZone FindInitialDestination()
    {
        SettlementZone[] settlements = SettlementManager.Ins != null && SettlementManager.Ins.AllSettlements.Count > 0
            ? SettlementManager.Ins.AllSettlements.ToArray()
            : FindObjectsByType<SettlementZone>(FindObjectsSortMode.None);

        SettlementZone closestZone = null;
        int bestRelationshipRank = int.MaxValue;
        float shortestDistance = float.MaxValue;
        int sourceTier = sourceZone.GetEffectiveTier();

        foreach (SettlementZone candidate in settlements)
        {
            if (candidate == null || candidate == sourceZone || !candidate.gameObject.activeInHierarchy) continue;

            int candidateTier = candidate.GetEffectiveTier();
            int relationshipRank = (candidate.GetPreviousZone() == sourceZone || candidate.previousTierZone == sourceZone)
                ? 0
                : candidateTier == sourceTier + 1 ? 1 : (candidateTier > sourceTier ? 2 : 3);
            float distance = (candidate.transform.position - sourceZone.transform.position).sqrMagnitude;

            if (relationshipRank < bestRelationshipRank ||
                (relationshipRank == bestRelationshipRank && distance < shortestDistance))
            {
                closestZone = candidate;
                bestRelationshipRank = relationshipRank;
                shortestDistance = distance;
            }
        }

        return closestZone;
    }

    private bool TryGetSettlementUnderPointer(out SettlementZone settlement)
    {
        settlement = null;
        if (moveCamera == null) return false;

        Ray ray = moveCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 2000f, ~0, QueryTriggerInteraction.Collide);
        float closestDistance = float.MaxValue;

        foreach (RaycastHit hit in hits)
        {
            SettlementZone candidate = hit.collider.GetComponentInParent<SettlementZone>();
            if (candidate == null || candidate == sourceZone || hit.distance >= closestDistance) continue;

            settlement = candidate;
            closestDistance = hit.distance;
        }

        return settlement != null;
    }

    private void GetMapOverview(out Vector3 center, out float height)
    {
        CacheCameraComponents();
        float groundHeight = sourceZone != null ? sourceZone.transform.position.y : 0f;

        if (rtsCameraController != null)
        {
            center = new Vector3(
                (rtsCameraController.minX + rtsCameraController.maxX) * 0.5f,
                groundHeight,
                (rtsCameraController.minZ + rtsCameraController.maxZ) * 0.5f);
            height = overviewCameraHeight;
            return;
        }

        SettlementZone[] settlements = FindObjectsByType<SettlementZone>(FindObjectsSortMode.None);
        Bounds bounds = new Bounds(sourceZone.transform.position, Vector3.zero);
        foreach (SettlementZone settlement in settlements)
        {
            if (settlement != null && settlement.gameObject.activeInHierarchy)
            {
                bounds.Encapsulate(settlement.transform.position);
            }
        }

        center = bounds.center;
        center.y = groundHeight;
        height = overviewCameraHeight;
    }

    private Vector3 GetOverviewCenter(SettlementZone destination)
    {
        return (sourceZone.transform.position + destination.transform.position) * 0.5f;
    }


    private void CacheCameraComponents()
    {
        if (moveCamera == null) moveCamera = Camera.main;
        if (moveCamera != null && rtsCameraController == null)
        {
            rtsCameraController = moveCamera.GetComponent<RTSCameraController>();
        }
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void MoveCameraTo(Vector3 worldCenter, float height, Action onComplete = null)
    {
        if (moveCamera == null) return;

        Ray centerRay = moveCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        float downwardAmount = -centerRay.direction.y;
        if (downwardAmount < 0.01f)
        {
            Debug.LogWarning("[MoveModeController] Camera phải hướng xuống để focus khu vực điều quân.");
            return;
        }

        float cameraY = worldCenter.y + height;
        float distance = (cameraY - worldCenter.y) / downwardAmount;
        Vector3 target = worldCenter - centerRay.direction * distance;

        if (rtsCameraController != null && rtsCameraController.maxY < target.y)
        {
            rtsCameraController.maxY = target.y + 1f;
        }

        if (cameraRoutine != null) StopCoroutine(cameraRoutine);
        cameraRoutine = StartCoroutine(MoveCameraRoutine(target, onComplete));
    }

    private IEnumerator MoveCameraRoutine(Vector3 target, Action onComplete)
    {
        while (Vector3.Distance(moveCamera.transform.position, target) > 0.05f)
        {
            moveCamera.transform.position = Vector3.Lerp(moveCamera.transform.position, target, cameraMoveSpeed * Time.deltaTime);
            yield return null;
        }

        moveCamera.transform.position = target;
        cameraRoutine = null;
        onComplete?.Invoke();
    }
}
