using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Điều phối các cửa sổ Xây dựng, Huấn luyện và Nâng cấp.
/// Bảng khu vực bên trái được giữ lại, còn ba cửa sổ chức năng luôn loại trừ nhau.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-250)]
public sealed class BuildTrainingUIManager : MonoBehaviour
{
    public enum ManagedWindow
    {
        None,
        Build,
        Training,
        Upgrade
    }

    public static BuildTrainingUIManager Ins { get; private set; }

    [Header("=== CÁC UI ĐƯỢC QUẢN LÝ ===")]
    [Tooltip("Có thể để trống. Script sẽ tự tìm cả object đang tắt trong Scene.")]
    [SerializeField] private SettlementSidePanelUI settlementPanel;
    [SerializeField] private BuildingShopUI buildPanel;
    [SerializeField] private TroopSelectionModalUI trainingPanel;
    [SerializeField] private BuildingUpgradeSidePanelUI upgradePanel;

    [Header("=== FOCUS THÀNH ĐANG CHỌN ===")]
    [Tooltip("Có thể để trống. Script tự lấy Camera có tag MainCamera.")]
    [SerializeField] private Camera mapCamera;
    [Tooltip("Có thể để trống. Script tự tìm RTSCameraController trên Main Camera.")]
    [SerializeField] private RTSCameraController rtsCameraController;
    [Tooltip("Kéo FX_Manager vào đây nếu Scene có nhiều DemaciaVFXHoverManager.")]
    [SerializeField] private DemaciaVFXHoverManager settlementVFXManager;
    [SerializeField] private bool focusCameraWhenOpeningSettlement = true;
    [SerializeField, Min(0.05f)] private float focusDuration = 0.55f;
    [SerializeField, Min(1f)] private float focusHorizontalDistance = 18f;
    [SerializeField, Min(1f)] private float focusHeight = 18f;
    [SerializeField] private bool lockRTSInputWhileFocusing = true;
    [SerializeField] private bool enableSettlementVFX = true;
    [SerializeField] private bool restoreCameraWhenSettlementPanelCloses = true;
    [SerializeField, Min(0.05f)] private float returnCameraDuration = 0.5f;

    [Header("=== HÀNH VI ===")]
    [SerializeField] private bool keepSettlementVisible = true;
    [SerializeField] private bool closeSecondaryWindowsOnStart = true;
    [SerializeField] private bool clearSelectedButtonWhenSwitching = true;
    [SerializeField] private bool logMissingReferences = true;

    public ManagedWindow CurrentWindow { get; private set; }
    public bool IsSettlementPanelVisible => IsActive(settlementPanel);

    private bool hasLoggedMissingReferences;
    private Coroutine focusRoutine;
    private bool rtsInputWasEnabled;
    private bool isRTSInputLocked;
    private DemaciaVFXHoverManager activeSettlementVFX;
    private SettlementZone focusedSettlement;
    private bool hasSavedOverviewCameraPose;
    private Vector3 savedOverviewCameraPosition;
    private Quaternion savedOverviewCameraRotation;

    private void Awake()
    {
        if (Ins != null && Ins != this)
        {
            Destroy(this);
            return;
        }

        Ins = this;
        FindUIReferences();
        FindFocusReferences();
    }

    private void Start()
    {
        FindUIReferences();

        if (closeSecondaryWindowsOnStart)
        {
            CloseSecondaryWindows();
        }
        else
        {
            CurrentWindow = GetVisibleWindow();
        }

        ValidateReferences();
    }

    private void OnDestroy()
    {
        StopCameraFocus();
        if (Ins == this) Ins = null;
    }

    /// <summary>
    /// Dùng cho nút toolbar mở bảng khu vực và đóng các cửa sổ phụ.
    /// </summary>
    public void ShowSettlementPanel()
    {
        FindUIReferences();
        SetActive(buildPanel, false);
        SetActive(trainingPanel, false);
        SetActive(upgradePanel, false);
        SetActive(settlementPanel, true);
        CurrentWindow = ManagedWindow.None;
        ClearSelectedButton();
        RefreshSettlement();
        FocusSelectedSettlement();
    }

    public void ShowBuildWindow()
    {
        SwitchTo(ManagedWindow.Build);

        if (buildPanel != null)
        {
            buildPanel.RefreshAllItems();
        }
    }

    public void ShowTrainingWindow()
    {
        SwitchTo(ManagedWindow.Training);
    }

    public void ShowUpgradeWindow()
    {
        SwitchTo(ManagedWindow.Upgrade);
    }

    /// <summary>
    /// Đóng đúng cửa sổ vừa báo đóng mà không làm mất bảng khu vực bên trái.
    /// </summary>
    public void NotifyWindowClosed(ManagedWindow window)
    {
        if (CurrentWindow == window)
        {
            CurrentWindow = ManagedWindow.None;
        }

        ClearSelectedButton();
    }

    /// <summary>
    /// Đóng Xây dựng/Huấn luyện/Nâng cấp nhưng giữ bảng khu vực.
    /// </summary>
    public void CloseSecondaryWindows()
    {
        FindUIReferences();
        SetActive(buildPanel, false);
        SetActive(trainingPanel, false);
        SetActive(upgradePanel, false);
        CurrentWindow = ManagedWindow.None;
        ClearSelectedButton();
    }

    /// <summary>
    /// Đóng toàn bộ nhóm UI, dùng khi click ra ngoài bản đồ hoặc nhấn ESC.
    /// </summary>
    public void CloseAllWindows()
    {
        CloseSecondaryWindows();
        SetActive(settlementPanel, false);
        ClearSettlementVFX();
        ReturnCameraToOverview();
    }

    private void SwitchTo(ManagedWindow targetWindow)
    {
        FindUIReferences();

        if (keepSettlementVisible)
        {
            SetActive(settlementPanel, true);
        }

        SetActive(buildPanel, targetWindow == ManagedWindow.Build);
        SetActive(trainingPanel, targetWindow == ManagedWindow.Training);
        SetActive(upgradePanel, targetWindow == ManagedWindow.Upgrade);

        CurrentWindow = targetWindow;
        ClearSelectedButton();
        RefreshSettlement();

        MonoBehaviour activePanel = GetPanel(targetWindow);
        if (activePanel != null)
        {
            activePanel.transform.SetAsLastSibling();
        }
    }

    private MonoBehaviour GetPanel(ManagedWindow window)
    {
        switch (window)
        {
            case ManagedWindow.Build: return buildPanel;
            case ManagedWindow.Training: return trainingPanel;
            case ManagedWindow.Upgrade: return upgradePanel;
            default: return null;
        }
    }

    private ManagedWindow GetVisibleWindow()
    {
        if (IsActive(trainingPanel)) return ManagedWindow.Training;
        if (IsActive(upgradePanel)) return ManagedWindow.Upgrade;
        if (IsActive(buildPanel)) return ManagedWindow.Build;
        return ManagedWindow.None;
    }

    private void RefreshSettlement()
    {
        if (settlementPanel != null && settlementPanel.gameObject.activeInHierarchy)
        {
            settlementPanel.UpdateHeaderVisual();
            settlementPanel.RefreshPanel();
        }
    }

    private void ClearSelectedButton()
    {
        if (clearSelectedButtonWhenSwitching && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private static void SetActive(MonoBehaviour panel, bool value)
    {
        if (panel != null && panel.gameObject.activeSelf != value)
        {
            panel.gameObject.SetActive(value);
        }
    }

    private static bool IsActive(MonoBehaviour panel)
    {
        return panel != null && panel.gameObject.activeSelf;
    }

    [ContextMenu("Tự tìm các UI trong Scene")]
    public void FindUIReferences()
    {
        if (settlementPanel == null)
        {
            settlementPanel = Object.FindFirstObjectByType<SettlementSidePanelUI>(FindObjectsInactive.Include);
        }

        if (buildPanel == null)
        {
            buildPanel = Object.FindFirstObjectByType<BuildingShopUI>(FindObjectsInactive.Include);
        }

        if (trainingPanel == null)
        {
            trainingPanel = Object.FindFirstObjectByType<TroopSelectionModalUI>(FindObjectsInactive.Include);
        }

        if (upgradePanel == null)
        {
            upgradePanel = Object.FindFirstObjectByType<BuildingUpgradeSidePanelUI>(FindObjectsInactive.Include);
        }
    }

    /// <summary>
    /// Di chuyển camera tới tâm thành đang chọn và bật VFX tương ứng.
    /// Được gọi lúc mở bảng thành, vì vậy cả click trên map lẫn click toolbar
    /// đều cho cùng một trải nghiệm focus.
    /// </summary>
    public void FocusSelectedSettlement()
    {
        SettlementZone selectedZone = SettlementManager.Ins != null
            ? SettlementManager.Ins.CurrentSettlement
            : null;
        if (selectedZone == null) return;

        // Click lặp lại vào chính tòa thành đang mở panel không được reset
        // camera/VFX; nếu không hiệu ứng sẽ chớp tắt gây khó chịu.
        if (focusedSettlement == selectedZone && IsSettlementPanelVisible)
        {
            return;
        }

        Transform focusTarget = selectedZone.townHallPoint != null
            ? selectedZone.townHallPoint
            : selectedZone.transform;

        if (focusCameraWhenOpeningSettlement)
        {
            FocusCamera(focusTarget);
        }

        if (enableSettlementVFX)
        {
            ShowSettlementVFX(selectedZone, focusTarget);
        }

        focusedSettlement = selectedZone;
    }

    private void FindFocusReferences()
    {
        if (mapCamera == null) mapCamera = Camera.main;

        if (rtsCameraController == null && mapCamera != null)
        {
            rtsCameraController = mapCamera.GetComponent<RTSCameraController>();
        }

        if (settlementVFXManager == null)
        {
            settlementVFXManager = Object.FindFirstObjectByType<DemaciaVFXHoverManager>(FindObjectsInactive.Include);
        }
    }

    private void FocusCamera(Transform target)
    {
        if (target == null) return;

        FindFocusReferences();
        if (mapCamera == null) return;

        SaveOverviewCameraPoseIfNeeded();

        Vector3 horizontalForward = mapCamera.transform.forward;
        horizontalForward.y = 0f;
        if (horizontalForward.sqrMagnitude < 0.001f)
        {
            horizontalForward = Vector3.back;
        }
        horizontalForward.Normalize();

        Vector3 destination = target.position - horizontalForward * focusHorizontalDistance + Vector3.up * focusHeight;
        Vector3 lookDirection = target.position - destination;
        Quaternion destinationRotation = lookDirection.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(lookDirection, Vector3.up)
            : mapCamera.transform.rotation;

        StopCameraFocus();
        focusRoutine = StartCoroutine(MoveCameraRoutine(destination, destinationRotation, focusDuration, false));
    }

    private void SaveOverviewCameraPoseIfNeeded()
    {
        if (hasSavedOverviewCameraPose || mapCamera == null) return;

        savedOverviewCameraPosition = mapCamera.transform.position;
        savedOverviewCameraRotation = mapCamera.transform.rotation;
        hasSavedOverviewCameraPose = true;
    }

    private void ReturnCameraToOverview()
    {
        if (!restoreCameraWhenSettlementPanelCloses || !hasSavedOverviewCameraPose) return;

        FindFocusReferences();
        if (mapCamera == null)
        {
            hasSavedOverviewCameraPose = false;
            return;
        }

        StopCameraFocus();
        focusRoutine = StartCoroutine(MoveCameraRoutine(
            savedOverviewCameraPosition,
            savedOverviewCameraRotation,
            returnCameraDuration,
            true));
    }

    private IEnumerator MoveCameraRoutine(Vector3 destination, Quaternion destinationRotation, float duration, bool clearSavedOverviewPoseOnComplete)
    {
        if (lockRTSInputWhileFocusing && rtsCameraController != null)
        {
            rtsInputWasEnabled = rtsCameraController.enabled;
            rtsCameraController.enabled = false;
            isRTSInputLocked = true;
        }

        Vector3 startPosition = mapCamera.transform.position;
        Quaternion startRotation = mapCamera.transform.rotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float easedTime = normalizedTime * normalizedTime * (3f - 2f * normalizedTime);
            mapCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, destination, easedTime),
                Quaternion.Slerp(startRotation, destinationRotation, easedTime));
            yield return null;
        }

        mapCamera.transform.SetPositionAndRotation(destination, destinationRotation);
        focusRoutine = null;
        RestoreRTSInput();

        if (clearSavedOverviewPoseOnComplete)
        {
            hasSavedOverviewCameraPose = false;
        }
    }

    private void StopCameraFocus()
    {
        if (focusRoutine != null)
        {
            StopCoroutine(focusRoutine);
            focusRoutine = null;
        }

        RestoreRTSInput();
    }

    private void RestoreRTSInput()
    {
        if (isRTSInputLocked && rtsCameraController != null)
        {
            rtsCameraController.enabled = rtsInputWasEnabled;
        }

        isRTSInputLocked = false;
    }

    private void ShowSettlementVFX(SettlementZone selectedZone, Transform focusTarget)
    {
        // Ưu tiên VFX đặt trực tiếp trong từng thành; nếu không có thì dùng FX_Manager chung.
        DemaciaVFXHoverManager zoneVFX = selectedZone.GetComponentInChildren<DemaciaVFXHoverManager>(true);
        DemaciaVFXHoverManager vfxManager = zoneVFX != null ? zoneVFX : settlementVFXManager;
        if (vfxManager == null) return;

        if (activeSettlementVFX != null && activeSettlementVFX != vfxManager)
        {
            activeSettlementVFX.ClearSettlementFocus();
        }

        if (!vfxManager.gameObject.activeSelf)
        {
            vfxManager.gameObject.SetActive(true);
        }

        vfxManager.ShowSettlementFocus(focusTarget);
        activeSettlementVFX = vfxManager;
    }

    private void ClearSettlementVFX()
    {
        if (activeSettlementVFX != null)
        {
            activeSettlementVFX.ClearSettlementFocus();
            activeSettlementVFX = null;
        }

        focusedSettlement = null;
    }

    private void ValidateReferences()
    {
        if (!logMissingReferences || hasLoggedMissingReferences) return;

        string missing = string.Empty;
        if (settlementPanel == null) missing += " SettlementSidePanelUI";
        if (buildPanel == null) missing += " BuildingShopUI";
        if (trainingPanel == null) missing += " TroopSelectionModalUI";
        if (upgradePanel == null) missing += " BuildingUpgradeSidePanelUI";

        if (!string.IsNullOrEmpty(missing))
        {
            hasLoggedMissingReferences = true;
            Debug.LogWarning($"[BuildTrainingUIManager] Thiếu UI:{missing}. " +
                             "Hãy đặt các prefab UI vào Scene hoặc gán chúng trong Inspector.", this);
        }
    }
}
