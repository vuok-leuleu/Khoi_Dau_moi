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

    [Header("=== HÀNH VI ===")]
    [SerializeField] private bool keepSettlementVisible = true;
    [SerializeField] private bool closeSecondaryWindowsOnStart = true;
    [SerializeField] private bool clearSelectedButtonWhenSwitching = true;
    [SerializeField] private bool logMissingReferences = true;

    public ManagedWindow CurrentWindow { get; private set; }

    private bool hasLoggedMissingReferences;

    private void Awake()
    {
        if (Ins != null && Ins != this)
        {
            Destroy(this);
            return;
        }

        Ins = this;
        FindUIReferences();
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
