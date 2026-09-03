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
    private readonly List<TroopTrainingSlotUI> selectedTroopSlots = new List<TroopTrainingSlotUI>();
    private SoldierPoint routePreview;
    private SoldierPoint routePreviewPrefab;
    private RTSCameraController rtsCameraController;
    private bool isSelectingDestination;
    private Coroutine cameraRoutine;

    private sealed class TroopDispatchGroup
    {
        public TroopTrainingSlotUI sourceSlot;
        public List<UnitController> soldiers;
    }

    public bool HasPreviewDestination => isSelectingDestination && previewDestination != null;
    public bool HasSelectedTroopSlot => isSelectingDestination && selectedTroopSlots.Count > 0;

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

        if (Input.GetMouseButtonUp(0) &&
            !RTSCameraController.IsMouseDragging &&
            !RTSCameraController.WasMouseDragThisPress &&
            !IsPointerOverUi())
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
        if (zone == null || !zone.IsConquered) return;

        CacheCameraComponents();
        sourceZone = zone;
        isSelectingDestination = true;
        ClearTroopSlotSelection();
        // Nếu chế độ điều quân được mở lại sau một lần chọn trước đó, bảo đảm
        // panel nguồn đang hiển thị để người chơi còn chọn/đổi ô lính.
        SettlementSidePanelUI.Ins?.SetMoveDestinationSelectionView(false);
        EnsureRoutePreview(soldierPointUIPrefab);
        if (routePreview == null)
        {
            CancelMoveMode();
            return;
        }

        previewDestination = null;
        SettlementSidePanelUI.Ins?.SetMoveButtonLabel("CHỌN Ô LÍNH");

        // Chưa chọn slot thì chưa cho xem/đặt điểm đến, tránh điều nhầm toàn bộ quân.
        routePreview.gameObject.SetActive(false);

        // 2. Camera chuyển góc nhìn bao quát toàn cảnh (overview)
        GetMapOverview(out Vector3 overviewCenter, out float overviewHeight);
        MoveCameraTo(overviewCenter, overviewHeight);
    }

    public void CancelMoveMode()
    {
        isSelectingDestination = false;
        sourceZone = null;
        previewDestination = null;
        ClearTroopSlotSelection();
        if (routePreview != null) routePreview.gameObject.SetActive(false);
        SettlementSidePanelUI.Ins?.SetMoveDestinationSelectionView(false);
        SettlementSidePanelUI.Ins?.SetMoveButtonLabel("ĐIỀU QUÂN");
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
        if (!HasSelectedTroopSlot)
        {
            SettlementSidePanelUI.Ins?.SetMoveButtonLabel("CHỌN Ô LÍNH");
            return;
        }

        if (!TryGetSettlementUnderPointer(out SettlementZone destination))
        {
            return;
        }

        TrySelectDestination(destination);
    }

    /// <summary>
    /// SettlementZoneClickHandler gọi trực tiếp hàm này. Cách này vẫn chọn được
    /// vùng đích khi collider của mô hình/công trình nằm trên bề mặt vùng đất.
    /// </summary>
    public void TrySelectDestination(SettlementZone destination)
    {
        if (!isSelectingDestination || !HasSelectedTroopSlot ||
            destination == null || destination == sourceZone)
        {
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
        // Tắt đúng SettlementSidePanel trong lúc chốt vùng đích, sau đó bật lại
        // panel nguồn để người chơi có thể bấm XÁC NHẬN.
        SettlementSidePanelUI.Ins?.SetMoveDestinationSelectionView(true);
        SettlementSidePanelUI.Ins?.ShowMoveConfirmationPanel();
        SettlementSidePanelUI.Ins?.SetMoveButtonLabel("XÁC NHẬN");
    }

    public void ToggleTroopSlotSelection(TroopTrainingSlotUI slot)
    {
        if (!isSelectingDestination || slot == null || slot.MoveZone != sourceZone) return;

        if (!slot.IsMoveSlotCompleted)
        {
            UIManager.Ins?.ShowWarning("Ô lính này chưa có quân sẵn sàng để điều đi.");
            return;
        }

        if (selectedTroopSlots.Contains(slot))
        {
            selectedTroopSlots.Remove(slot);
            slot.SetMoveSelected(false);
            previewDestination = null;
            routePreview?.gameObject.SetActive(false);
            SettlementSidePanelUI.Ins?.SetMoveButtonLabel(
                selectedTroopSlots.Count > 0 ? "CHỌN ĐIỂM ĐẾN" : "CHỌN Ô LÍNH");
            return;
        }

        selectedTroopSlots.Add(slot);
        slot.SetMoveSelected(true);
        previewDestination = null;
        routePreview?.gameObject.SetActive(false);
        // Vẫn giữ panel nguồn khi mới chọn ô lính để người chơi có thể chọn
        // thêm hoặc bỏ bớt nhiều nhóm trước khi chọn điểm đến.
        SettlementSidePanelUI.Ins?.SetMoveButtonLabel("CHỌN ĐIỂM ĐẾN");
    }

    public void RestoreSelectedTroopSlotVisual()
    {
        if (isSelectingDestination)
        {
            foreach (TroopTrainingSlotUI slot in selectedTroopSlots)
            {
                slot?.SetMoveSelected(true);
            }
        }
    }

    private void ClearTroopSlotSelection()
    {
        foreach (TroopTrainingSlotUI slot in selectedTroopSlots)
        {
            slot?.SetMoveSelected(false);
        }
        selectedTroopSlots.Clear();
    }

    public void ApplySelectedDestination()
    {
        if (!HasPreviewDestination || sourceZone == null) return;

        if (selectedTroopSlots.Count == 0)
        {
            UIManager.Ins?.ShowWarning("Hãy chọn ít nhất một ô lính trước khi xác nhận điều quân.");
            SettlementSidePanelUI.Ins?.SetMoveButtonLabel("CHỌN Ô LÍNH");
            return;
        }

        List<TroopDispatchGroup> dispatchGroups = new List<TroopDispatchGroup>();
        HashSet<UnitController> alreadySelected = new HashSet<UnitController>();
        foreach (TroopTrainingSlotUI slot in selectedTroopSlots)
        {
            List<UnitController> soldiers = GetSoldiersForTroopSlot(sourceZone, slot, alreadySelected);
            if (soldiers.Count == 0)
            {
                UIManager.Ins?.ShowWarning($"Ô lính {slot.MoveSlotIndex + 1} không còn quân sẵn sàng để điều đi.");
                return;
            }

            dispatchGroups.Add(new TroopDispatchGroup
            {
                sourceSlot = slot,
                soldiers = soldiers
            });
            foreach (UnitController soldier in soldiers) alreadySelected.Add(soldier);
        }

        if (TroopTrainingManager.Ins == null ||
            !TroopTrainingManager.Ins.TryGetAvailableGarrisonSlotIndices(
                previewDestination,
                dispatchGroups.Count,
                out List<int> destinationSlotIndices))
        {
            UIManager.Ins?.ShowWarning("Không đủ chỗ ở vùng đích để điều quân.");
            Debug.LogWarning($"[MoveModeController] Vùng đích không đủ {dispatchGroups.Count} ô trống cho các nhóm quân đã chọn.");
            return;
        }

        ConfirmDestination(previewDestination, dispatchGroups, destinationSlotIndices);
    }

    private void ConfirmDestination(
        SettlementZone destination,
        List<TroopDispatchGroup> dispatchGroups,
        List<int> destinationSlotIndices)
    {
        if (destination == null || dispatchGroups == null || dispatchGroups.Count == 0 ||
            destinationSlotIndices == null || destinationSlotIndices.Count != dispatchGroups.Count) return;

        isSelectingDestination = false;
        SettlementZone selectedZone = destination;
        UnitController routeLeader = null;
        List<UnitController> dispatchedSoldiers = new List<UnitController>();
        for (int i = 0; i < dispatchGroups.Count; i++)
        {
            TroopDispatchGroup group = dispatchGroups[i];
            UnitController groupLeader = SendSoldiersToDestination(
                sourceZone,
                selectedZone,
                group.soldiers,
                destinationSlotIndices[i]);
            if (routeLeader == null) routeLeader = groupLeader;
            foreach (UnitController soldier in group.soldiers)
            {
                if (soldier != null && !dispatchedSoldiers.Contains(soldier))
                {
                    dispatchedSoldiers.Add(soldier);
                }
            }
        }

        List<int> sourceSlotIndices = new List<int>();
        foreach (TroopDispatchGroup group in dispatchGroups)
        {
            if (group.sourceSlot != null) sourceSlotIndices.Add(group.sourceSlot.MoveSlotIndex);
        }
        TroopTrainingManager.Ins?.MarkSlotsDispatched(sourceZone, sourceSlotIndices);
        ClearTroopSlotSelection();
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

        // Không chọn settlement đích ở đây. Nếu gọi SelectSettlement, panel của
        // vùng đích sẽ bật lên ngay sau khi người chơi vừa xác nhận điều quân.
        SettlementSidePanelUI.Ins?.HideSettlementPanelAfterMove();

        MoveCameraTo(selectedZone.transform.position, selectedCameraHeight, () =>
        {
            sourceZone = null;
            previewDestination = null;
        });
        SettlementSidePanelUI.Ins?.SetMoveButtonLabel("ĐIỀU QUÂN");

        // Theo dõi đoàn quân ngay lúc người chơi xác nhận điều quân tới căn
        // cứ địch. Nút Tấn Công chỉ được tạo khi toàn bộ đoàn thật sự đến nơi.
        Transform enemyTarget = destination.hasEnemyOutpost
            ? destination.GetConquestTargetTransform()
            : null;
        if (enemyTarget != null && dispatchedSoldiers.Count > 0)
        {
            // Nếu một nhóm đã tới trước, nút TẤN CÔNG cũ không được phép giữ
            // trạng thái hợp lệ trong lúc có thêm quân đang hành quân tới đây.
            UIEnemyWaveButton.RemoveArrivalButton(enemyTarget);
            GameObject runner = new GameObject("ExpeditionBattleTriggerRunner");
            ExpeditionBattleTrigger trigger = runner.AddComponent<ExpeditionBattleTrigger>();
            trigger.StartMonitoring(dispatchedSoldiers, enemyTarget, "SceneBattle");
        }
    }

    private static Transform GetRouteAnchor(SettlementZone zone)
    {
        return zone != null && zone.townHallPoint != null ? zone.townHallPoint : zone.transform;
    }
    private static UnitController SendSoldiersToDestination(
        SettlementZone source,
        SettlementZone destination,
        List<UnitController> selectedSoldiers,
        int destinationSlotIndex)
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
                source.settlementName,
                destinationSlotIndex);
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

            // A marching squad has already left its origin.  It cannot be selected
            // again until it reaches a settlement.
            if (soldier.isExpeditionMarching) continue;

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

    private static List<UnitController> GetSoldiersForTroopSlot(
        SettlementZone zone,
        TroopTrainingSlotUI slot,
        HashSet<UnitController> alreadySelected)
    {
        List<UnitController> selected = new List<UnitController>();
        if (zone == null || slot == null) return selected;

        List<UnitController> available = GetSoldiersStationedInZone(zone);
        int targetCount = slot.MoveUnitCount;
        if (targetCount <= 0) targetCount = 3;

        // Ưu tiên các lính đã có đúng slot index. Đây là đường chính cho các
        // nhóm mới và bảo đảm hai ô cùng loại không bị trộn vào nhau.
        foreach (UnitController soldier in available)
        {
            if (soldier == null || alreadySelected.Contains(soldier) ||
                soldier.stationedTroopSlotIndex != slot.MoveSlotIndex ||
                TroopTrainingManager.GetTroopType(soldier) != slot.MoveTroopType) continue;
            selected.Add(soldier);
            if (selected.Count >= targetCount) break;
        }

        // Tương thích với lính được tạo trước khi hệ thống slot được bổ sung:
        // chỉ dùng lính chưa có index, rồi gán chúng vào slot đang chọn.
        if (selected.Count < targetCount)
        {
            foreach (UnitController soldier in available)
            {
                if (soldier == null || alreadySelected.Contains(soldier) ||
                    soldier.stationedTroopSlotIndex >= 0 ||
                    TroopTrainingManager.GetTroopType(soldier) != slot.MoveTroopType) continue;
                soldier.stationedTroopSlotIndex = slot.MoveSlotIndex;
                selected.Add(soldier);
                if (selected.Count >= targetCount) break;
            }
        }

        return selected;
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
