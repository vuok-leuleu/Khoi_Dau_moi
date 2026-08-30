using UnityEngine;
using UnityEngine.UI;

/*
 * UIManager.cs
 * Folder: Scripts/Managers/
 * Dự án: KHẨN HOANG (PENTA DEV)
 * Phong cách: Demacia Rising UI Manager & Popup Controller (Cleaned & Optimized)
 */

public class UIManager : Singleton<UIManager>
{
    [Header("=== GIAO DIỆN KIỂU DEMACIA RISING ===")]
    [SerializeField] private GameObject settlementSidePanel;       // Panel Thủ Đô bên góc trái
    [SerializeField] private GameObject buildingShopPopup;          // Cửa sổ Pop-up Shop 2 Cột
    [SerializeField] private GameObject buildingUpgradeSidePanel;   // Bảng Nâng Cấp liền kề

    [Header("=== BOTTOM TOOLBAR & KHUNG HỖ TRỢ ===")]
    [SerializeField] private Button buildButton;
    [SerializeField] private Button toolsButton;
    [SerializeField] private Button settingButton;
    [SerializeField] private GameObject controlHintsGroup;
    [SerializeField] private GameObject settingUI;
    [SerializeField] private GameObject warningUI;

    void Start()
    {
        if (controlHintsGroup != null) controlHintsGroup.SetActive(false);
        if (settingUI != null) settingUI.SetActive(false);

        if (buildButton != null)
        {
            buildButton.onClick.RemoveListener(OpenSettlementPanel);
            buildButton.onClick.AddListener(OpenSettlementPanel);
        }
        if (toolsButton != null)
        {
            toolsButton.onClick.RemoveListener(OnClickToolsButton);
            toolsButton.onClick.AddListener(OnClickToolsButton);
        }
        if (settingButton != null)
        {
            settingButton.onClick.RemoveListener(OnClickSettingButton);
            settingButton.onClick.AddListener(OnClickSettingButton);
        }
    }

    // ====================================================================
    // 1. CHỨC NĂNG PANEL THỦ ĐÔ (SETTLEMENT SIDE PANEL)
    // ====================================================================

    public void OpenSettlementPanel()
    {
        if (MoveModeController.IsMoveModeActive) return;

        CloseAllPopups();

        SettlementZone currentZone = SettlementManager.Ins != null
            ? SettlementManager.Ins.CurrentSettlement
            : null;
        if (currentZone != null && !currentZone.IsConquered)
        {
            // Toolbar không được mở settlement UI khi vùng hiện tại chưa chinh
            // phục (ví dụ vẫn còn căn cứ địch hoặc chưa được mở khóa).
            SettlementSidePanelUI.Ins?.SetMoveButtonVisible(false);
            CloseSettlementPanel();
            return;
        }

        if (BuildTrainingUIManager.Ins != null)
        {
            BuildTrainingUIManager.Ins.ShowSettlementPanel();
            return;
        }

        if (settlementSidePanel != null)
        {
            settlementSidePanel.SetActive(true);
            SettlementSidePanelUI.Ins?.RefreshPanel();
        }
        else if (SettlementSidePanelUI.Ins != null)
        {
            SettlementSidePanelUI.Ins.gameObject.SetActive(true);
            SettlementSidePanelUI.Ins.RefreshPanel();
        }
    }

    public void CloseSettlementPanel()
    {
        // Nút Move có thể nằm cùng SettlementSidePanel trong prefab nên cần
        // ẩn theo panel khi đóng hoàn toàn. Trong lúc chờ xác nhận, luồng
        // điều quân sẽ bật lại chính panel nguồn.
        if (!MoveModeController.IsMoveModeActive)
        {
            SettlementSidePanelUI.Ins?.SetMoveButtonVisible(false);
        }

        if (BuildTrainingUIManager.Ins != null)
        {
            BuildTrainingUIManager.Ins.CloseAllWindows();
            return;
        }

        if (settlementSidePanel != null) settlementSidePanel.SetActive(false);
        if (SettlementSidePanelUI.Ins != null) SettlementSidePanelUI.Ins.gameObject.SetActive(false);
    }

    /// <summary>
    /// Bật/tắt đúng object SettlementSidePanel. Phương thức này được dùng trong
    /// chế độ điều quân để ẩn panel khi bắt điểm đến rồi hiện lại panel nguồn
    /// cho bước xác nhận, không đi qua OpenSettlementPanel (vốn bị chặn khi
    /// MoveMode đang hoạt động).
    /// </summary>
    public void SetSettlementSidePanelVisible(bool visible)
    {
        if (settlementSidePanel != null)
        {
            settlementSidePanel.SetActive(visible);
            return;
        }

        if (SettlementSidePanelUI.Ins != null)
        {
            SettlementSidePanelUI.Ins.gameObject.SetActive(visible);
        }
    }

    // ====================================================================
    // 2. CHỨC NĂNG CỬA SỔ SHOP POP-UP 2 CỘT (BUILDING SHOP)
    // ====================================================================

    public void OpenBuildMenu()
    {
        Debug.Log("[UIManager] 🛒 OpenBuildMenu được gọi!");
        HideUpgradePanel();

        if (BuildTrainingUIManager.Ins != null)
        {
            BuildTrainingUIManager.Ins.ShowBuildWindow();
            return;
        }

        if (buildingShopPopup != null)
        {
            buildingShopPopup.SetActive(true);
            BuildingShopUI.Ins?.RefreshAllItems();
        }
        else if (BuildingShopUI.Ins != null)
        {
            BuildingShopUI.Ins.gameObject.SetActive(true);
            BuildingShopUI.Ins.RefreshAllItems();
        }
        else
        {
            Debug.LogWarning("[UIManager] ⚠️ buildingShopPopup và BuildingShopUI.Ins đều đang NULL! Vui lòng gán BuildCanvas vào UIManager trong Inspector.");
        }
    }

    public void CloseBuildMenu()
    {
        if (buildingShopPopup != null) buildingShopPopup.SetActive(false);
        if (BuildingShopUI.Ins != null) BuildingShopUI.Ins.gameObject.SetActive(false);
        BuildTrainingUIManager.Ins?.NotifyWindowClosed(BuildTrainingUIManager.ManagedWindow.Build);
    }

    public void ToggleBuildMenu()
    {
        bool isActive = (buildingShopPopup != null && buildingShopPopup.activeSelf) || 
                         (BuildingShopUI.Ins != null && BuildingShopUI.Ins.gameObject.activeSelf);
        if (isActive) CloseBuildMenu();
        else OpenBuildMenu();
    }

    // ====================================================================
    // 3. CHỨC NĂNG BẢNG NÂNG CẤP LIỀN KỀ (BUILDING UPGRADE)
    // ====================================================================

    public void ShowUpgradePanel(UpgradeableBuilding building)
    {
        Debug.Log($"[UIManager] ⚡ ShowUpgradePanel được gọi cho công trình: {(building != null ? building.buildingName : "null")}");
        if (building == null) return;

        CloseBuildMenu();
        BuildTrainingUIManager.Ins?.ShowUpgradeWindow();

        // 1. Đảm bảo Bảng Thủ Đô (Settlement Panel) bên trái LUÔN hiển thị song song!
        if (settlementSidePanel != null && !settlementSidePanel.activeSelf)
        {
            settlementSidePanel.SetActive(true);
            SettlementSidePanelUI.Ins?.RefreshPanel();
        }
        else if (SettlementSidePanelUI.Ins != null && !SettlementSidePanelUI.Ins.gameObject.activeSelf)
        {
            SettlementSidePanelUI.Ins.gameObject.SetActive(true);
            SettlementSidePanelUI.Ins.RefreshPanel();
        }

        // 2. Mở Bảng Nâng Cấp Side Panel bên phải
        if (BuildingUpgradeSidePanelUI.Ins != null)
        {
            BuildingUpgradeSidePanelUI.Ins.ShowUpgradePanel(building);
        }
        else if (buildingUpgradeSidePanel != null)
        {
            buildingUpgradeSidePanel.SetActive(true);
            var sideUI = buildingUpgradeSidePanel.GetComponent<BuildingUpgradeSidePanelUI>();
            if (sideUI != null) sideUI.ShowUpgradePanel(building);
        }
        else
        {
            Debug.LogWarning("[UIManager] ⚠️ buildingUpgradeSidePanel và BuildingUpgradeSidePanelUI.Ins đều đang NULL! Vui lòng gán UpgradeCanvas vào UIManager trong Inspector.");
        }
    }

    public void HideUpgradePanel()
    {
        if (BuildingUpgradeSidePanelUI.Ins != null) BuildingUpgradeSidePanelUI.Ins.ClosePanel();
        if (buildingUpgradeSidePanel != null) buildingUpgradeSidePanel.SetActive(false);
        BuildTrainingUIManager.Ins?.NotifyWindowClosed(BuildTrainingUIManager.ManagedWindow.Upgrade);
    }

    public void CloseUpgradePanel() => HideUpgradePanel();

    public void OpenEstablishTownHallPanel(SettlementZone zone)
    {
        if (zone == null) return;

        CloseBuildMenu();
        BuildTrainingUIManager.Ins?.ShowUpgradeWindow();

        // 1. Đảm bảo Bảng Thủ Đô bên trái luôn hiển thị
        if (settlementSidePanel != null && !settlementSidePanel.activeSelf)
        {
            settlementSidePanel.SetActive(true);
            SettlementSidePanelUI.Ins?.RefreshPanel();
        }
        else if (SettlementSidePanelUI.Ins != null && !SettlementSidePanelUI.Ins.gameObject.activeSelf)
        {
            SettlementSidePanelUI.Ins.gameObject.SetActive(true);
            SettlementSidePanelUI.Ins.RefreshPanel();
        }

        // 2. Mở Bảng Nâng Cấp / Xây Dựng Side Panel bên phải
        if (buildingUpgradeSidePanel != null && !buildingUpgradeSidePanel.activeSelf)
        {
            buildingUpgradeSidePanel.SetActive(true);
        }

        var sideUI = BuildingUpgradeSidePanelUI.Ins;
        if (sideUI == null && buildingUpgradeSidePanel != null)
        {
            sideUI = buildingUpgradeSidePanel.GetComponent<BuildingUpgradeSidePanelUI>();
        }

        if (sideUI != null)
        {
            sideUI.gameObject.SetActive(true);
            sideUI.ShowEstablishTownHallPanel(zone);
        }
        else
        {
            Debug.LogWarning("[UIManager] ⚠️ Không tìm thấy BuildingUpgradeSidePanelUI để mở bảng Xây Nhà Chính!");
        }
    }

    // ====================================================================
    // 4. CHỨC NĂNG CẢNH BÁO VÀ TOOLBAR
    // ====================================================================

    public void ShowWarning(string message)
    {
        if (warningUI != null) warningUI.SetActive(true);
    }

    public void HideWarning()
    {
        if (warningUI != null) warningUI.SetActive(false);
    }

    public void OnClickToolsButton()
    {
        CloseAllPopups();
        if (controlHintsGroup != null) controlHintsGroup.SetActive(true);
    }

    public void OnClickSettingButton()
    {
        if (settingUI != null)
        {
            bool isCurrentActive = settingUI.activeSelf;
            CloseAllPopups();
            settingUI.SetActive(!isCurrentActive);
        }
    }

    public void ExitActionModes()
    {
        if (controlHintsGroup != null) controlHintsGroup.SetActive(false);
    }

    public void EnterPlacementMode()
    {
        if (controlHintsGroup != null) controlHintsGroup.SetActive(true);
        CloseBuildMenu();
    }

    public void ExitPlacementMode(bool shouldReopenMenu)
    {
        if (controlHintsGroup != null) controlHintsGroup.SetActive(false);
        if (shouldReopenMenu) OpenBuildMenu();
    }

    // ====================================================================
    // 5. DELEGATES XÂY NHÀ TỰ ĐỘNG
    // ====================================================================

    public void OnClickHouseButton() => BuildingSystem.Ins.StartPlacing(BuildingType.House);
    public void OnClickWoodCutterButton() => BuildingSystem.Ins.StartPlacing(BuildingType.WoodCutter);
    public void OnClickStoneMineButton() => BuildingSystem.Ins.StartPlacing(BuildingType.StoneMine);
    public void OnClickKitchenButton() => BuildingSystem.Ins.StartPlacing(BuildingType.Kitchen);
    public void OnClickFoodStorageButton() => BuildingSystem.Ins.StartPlacing(BuildingType.FoodStorage);
    public void OnClickStoneStorageButton() => BuildingSystem.Ins.StartPlacing(BuildingType.StoneStorage);
    public void OnClickWarehouseButton() => BuildingSystem.Ins.StartPlacing(BuildingType.Warehouse);

    public void OnClickWatchTowerButton() => BuildingSystem.Ins.StartPlacing(BuildingType.WatchTower);
    public void OnClickArcherTowerButton() => BuildingSystem.Ins.StartPlacing(BuildingType.ArcherTower);
    public void OnClickCannonButton() => BuildingSystem.Ins.StartPlacing(BuildingType.Cannon);

    public void OnClickBarracksMeleeButton() => BuildingSystem.Ins.StartPlacing(BuildingType.BarracksMelee);
    public void OnClickBarracksArcherButton() => BuildingSystem.Ins.StartPlacing(BuildingType.BarracksArcher);
    public void OnClickBarracksSpearButton() => BuildingSystem.Ins.StartPlacing(BuildingType.BarracksSpear);

    public void OnClickWoodButton() => BuildingSystem.Ins.StartPlacing(BuildingType.Wood);
    public void OnClickRiceButton() => BuildingSystem.Ins.StartPlacing(BuildingType.Rice);
    public void OnClickStoneButton() => BuildingSystem.Ins.StartPlacing(BuildingType.Stone);

    // ====================================================================
    // 6. DỌN DẸP TOÀN BỘ POPUP DỪNG XUNG ĐỘT GIAO DIỆN
    // ====================================================================

    public void CloseAllPopups()
    {
        BuildTrainingUIManager.Ins?.CloseSecondaryWindows();

        if (buildingShopPopup != null) buildingShopPopup.SetActive(false);
        if (BuildingShopUI.Ins != null) BuildingShopUI.Ins.gameObject.SetActive(false);

        if (buildingUpgradeSidePanel != null) buildingUpgradeSidePanel.SetActive(false);
        if (BuildingUpgradeSidePanelUI.Ins != null) BuildingUpgradeSidePanelUI.Ins.ClosePanel();

        if (settingUI != null) settingUI.SetActive(false);
        if (controlHintsGroup != null) controlHintsGroup.SetActive(false);
    }

    public void CloseAllActiveWindows()
    {
        CloseAllPopups();
        CloseSettlementPanel();
        Debug.Log("[UIManager] 🧹 Đã dọn dẹp và ẩn toàn bộ giao diện cửa sổ popup.");
    }
}
