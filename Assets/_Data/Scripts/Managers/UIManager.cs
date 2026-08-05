using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

/*
 * UIManager.cs
 * Folder: Scripts/UI/
 * Dự án: KHẨN HOANG (PENTA DEV)
 * Người thực hiện: VŨ + ĐĂNG
 * CHỨC NĂNG: Kết nối dữ liệu từ AttackTowerAI và WatchTowerAI, bóc tách cấu trúc
 * chiso_hientai và chiso_nangcap chứa các Image + Text con để đổ thông số chuẩn xác.
 */

public class UIManager : Singleton<UIManager>
{
    [Header("Old UI Panels")]
    [SerializeField] private GameObject buildMenu;
    [SerializeField] private GameObject warningUI;
    [SerializeField] private GameObject houseSelectionPanel;
    [SerializeField] private GameObject workerStatusPanel;

    [Header("Bottom UI Toolbar (Buttons)")]
    [SerializeField] private Button buildButton;
    [SerializeField] private Button toolsButton;
    [SerializeField] private Button settingButton;

    [Header("Bottom UI Toolbar (Panels)")]
    [SerializeField] private GameObject controlHintsGroup;
    [SerializeField] private GameObject settingUI;

    private Coroutine _fadeWarningCoroutine;
    private Coroutine _hideActionCoroutine;
    private GameObject actionMessageUI;
    private TMP_Text actionMessageText;

    [Header("Upgrade & Move Panel")]
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private TMP_Text buildingNameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TMP_Text upgradeButtonText;
    [SerializeField] private Button moveButton;
    [SerializeField] private Button repairButton; // PENTA DEV - THÊM BIẾN NÀY ĐỂ KÉO THẢ NÚT SỬA CHỮA TRONG INSPECTOR

    [Header("New Features – Preview UI Elements")]
    [SerializeField] private Image currentBuildingPreviewImage;
    [SerializeField] private Image nextBuildingPreviewImage;

    [Header("Cấu trúc Cửa sổ Chỉ Số (chiso_panel)")]
    [SerializeField] private GameObject chiso_panel;          // Panel tổng quản lý hiển thị chỉ số

    [Space(10)]
    [Header("--- Cấu Phần Con Của chiso_hientai ---")]
    [SerializeField] private GameObject chiso_hientai_obj;    // Object cụm Hiện Tại để ẩn/hiện
    [SerializeField] private TMP_Text txt_SatThuong_HienTai;  // Text chứa chỉ số Sát thương hiện tại / Hoặc đổi thành Tên chỉ số 1 dân sự
    [SerializeField] private TMP_Text txt_TamBan_HienTai;     // Text chứa chỉ số Tầm bắn hiện tại / Hoặc đổi thành Tên chỉ số 2 dân sự
    [SerializeField] private TMP_Text txt_TocDo_HienTai;      // Text chứa chỉ số Tốc độ hiện tại

    [Space(10)]
    [Header("--- Cấu Phần Con Của chiso_nangcap ---")]
    [SerializeField] private GameObject chiso_nangcap_obj;   // Object cụm Nâng Cấp để ẩn/hiện (hoặc ẩn khi MAX)
    [SerializeField] private TMP_Text txt_SatThuong_NangCap;  // Text chứa chỉ số Sát thương cấp tiếp theo
    [SerializeField] private TMP_Text txt_TamBan_NangCap;     // Text chứa chỉ số Tầm bắn cấp tiếp theo
    [SerializeField] private TMP_Text txt_TocDo_NangCap;      // Text chứa chỉ số Tốc độ cấp tiếp theo
    [SerializeField] private TMP_Text txt_MaxLevelNotice;     // Text phụ xuất hiện chữ "MAX" hoặc "ĐÃ TỐI ĐA" khi hết cấp

    [Header("Penta Dev - Thêm UI Cho Chỉ Số Dân Sự (Tái sử dụng hoặc tạo riêng)")]
    // Bạn có thể dùng chung cụm txt_SatThuong, txt_TamBan ở trên hoặc gán riêng các Text chuyên dụng dưới đây vào Inspector:
    [SerializeField] private TMP_Text txt_Label_ChiSo1;       // Ví dụ: hiển thị chữ "Dân lao động:" hoặc "Sát thương:"
    [SerializeField] private TMP_Text txt_Label_ChiSo2;       // Ví dụ: hiển thị chữ "Kho tạm chứa:" hoặc "Tầm bắn:"
    [SerializeField] private TMP_Text txt_Label_ChiSo3;       // Ví dụ: hiển thị chữ "Tốc độ:" hoặc ẩn đi khi dùng cho dân sự

    [Header("Upgrade Costs Text Elements")]
    [SerializeField] private TMP_Text woodCostText;
    [SerializeField] private TMP_Text stoneCostText;
    [SerializeField] private TMP_Text foodCostText;

    [Header("Action Notifications")]
    [SerializeField] private TMP_Text warningMessageText;

    private UpgradeableBuilding selectedBuilding;

    // --- BIẾN ĐẾM SỐ LẦN ẤN NÚT NÂNG CẤP ĐƯỢC THÊM VÀO ---
    private int upgradeClickCount = 0;
    private Coroutine _hideWarningCoroutine;

    void Start()
    {
        if (houseSelectionPanel != null) houseSelectionPanel.SetActive(true);
        if (workerStatusPanel != null) workerStatusPanel.SetActive(true);
        if (controlHintsGroup != null) controlHintsGroup.SetActive(false);
        if (settingUI != null) settingUI.SetActive(false);
        if (upgradePanel != null) upgradePanel.SetActive(false);

        if (buildButton != null) buildButton.onClick.AddListener(ToggleBuildMenu);
        if (toolsButton != null) toolsButton.onClick.AddListener(OnClickToolsButton);
        if (settingButton != null) settingButton.onClick.AddListener(OnClickSettingButton);

        if (upgradeButton != null) upgradeButton.onClick.AddListener(OnClickUpgradeButton);
        if (moveButton != null) moveButton.onClick.AddListener(OnClickMoveButton);

        CreateDefaultActionMessageUI();
    }

    private void CreateDefaultActionMessageUI()
    {
        if (actionMessageUI != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = Object.FindFirstObjectByType<Canvas>();
        }
        if (canvas == null) return;

        GameObject panelGO = new GameObject("ActionMessagePanel", typeof(RectTransform), typeof(Image));
        panelGO.transform.SetParent(canvas.transform, false);

        var panelImage = panelGO.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.65f);

        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.9f);
        panelRect.anchorMax = new Vector2(0.5f, 0.9f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(420f, 40f);
        panelRect.anchoredPosition = Vector2.zero;

        GameObject textGO = new GameObject("ActionMessageText", typeof(RectTransform));
        textGO.transform.SetParent(panelGO.transform, false);
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 5f);
        textRect.offsetMax = new Vector2(-10f, -5f);

        var text = textGO.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 18;
        text.color = Color.white;
        text.enableWordWrapping = true;
        text.text = "";

        actionMessageUI = panelGO;
        actionMessageText = text;
        actionMessageUI.SetActive(false);
    }

    public void ShowActionMessage(string message, float duration = 2f)
    {
        if (actionMessageUI == null)
        {
            CreateDefaultActionMessageUI();
        }

        if (actionMessageText == null && actionMessageUI != null)
        {
            actionMessageText = actionMessageUI.GetComponentInChildren<TMP_Text>(true);
        }

        if (actionMessageText != null)
        {
            actionMessageText.text = message;
        }

        if (actionMessageUI != null)
        {
            actionMessageUI.SetActive(true);
        }

        if (_hideActionCoroutine != null)
        {
            StopCoroutine(_hideActionCoroutine);
        }
        _hideActionCoroutine = StartCoroutine(HideActionAfter(duration));
    }

    public void HideActionMessage()
    {
        if (actionMessageUI != null)
        {
            actionMessageUI.SetActive(false);
        }

        if (_hideActionCoroutine != null)
        {
            StopCoroutine(_hideActionCoroutine);
            _hideActionCoroutine = null;
        }
    }

    private IEnumerator HideActionAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        HideActionMessage();
    }

    // ================= BOTTOM TOOLBAR LOGIC =================

   public void ToggleBuildMenu()
    {
        if (buildMenu != null)
        {
            bool isCurrentActive = buildMenu.activeSelf;
            
            // Tắt hết các bảng khác trước
            CloseAllPopups(); 

            // Đảo ngược trạng thái của Menu Xây Dựng
            buildMenu.SetActive(!isCurrentActive);
        }
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

            // Tắt hết các bảng khác trước
            CloseAllPopups();

            // Đảo ngược trạng thái Cài Đặt
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
        if (buildMenu != null) buildMenu.SetActive(false);
    }

    public void ExitPlacementMode(bool shouldReopenMenu)
    {
        if (controlHintsGroup != null) controlHintsGroup.SetActive(false);
        if (buildMenu != null) buildMenu.SetActive(shouldReopenMenu);
    }

    // ================= WARNING UI LOGIC =================

    public void ShowWarning(string message, float duration = 2f)
    {
        if (warningUI == null) return;

        if (warningMessageText == null)
            warningMessageText = warningUI.GetComponentInChildren<TMP_Text>(true);

        if (warningMessageText != null)
            warningMessageText.text = message;

        warningUI.SetActive(true);

        if (_hideWarningCoroutine != null)
            StopCoroutine(_hideWarningCoroutine);

        _hideWarningCoroutine = StartCoroutine(HideWarningAfter(duration));
    }

    public void HideWarning()
    {
        if (warningUI != null)
            warningUI.SetActive(false);

        if (_hideWarningCoroutine != null)
        {
            StopCoroutine(_hideWarningCoroutine);
            _hideWarningCoroutine = null;
        }
    }

    private IEnumerator HideWarningAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        HideWarning();
    }

    // ================= ON CLICK BUTTONS =================

    public void OnClickHouseButton() => BuildingSystem.Ins.StartPlacing(BuildingType.House);
    public void OnClickMainHouseButton() => BuildingSystem.Ins.StartPlacing(BuildingType.MainHouse);
    public void OnClickWoodCutterButton() => BuildingSystem.Ins.StartPlacing(BuildingType.WoodCutter);
    public void OnClickStoneMineButton() => BuildingSystem.Ins.StartPlacing(BuildingType.StoneMine);
    public void OnClickFarmPlotButton() => BuildingSystem.Ins.StartPlacing(BuildingType.FarmPlot);
    public void OnClickWoodTreeButton() => BuildingSystem.Ins.StartPlacing(BuildingType.WoodTree);
    public void OnClickStoneBoulderButton() => BuildingSystem.Ins.StartPlacing(BuildingType.StoneBoulder);
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

    // ================= UPGRADE & MOVE PANEL LOGIC =================

   [Header("UI Offset")]
    [SerializeField] private Vector3 upgradePanelOffset = new Vector3(0, -150f, 0); // Chỉnh lề xuất hiện (VD: -150px bên dưới công trình)

    public void ShowUpgradePanel(UpgradeableBuilding building)
    {
        if (building == null) return;

        CloseAllPopups();
        selectedBuilding = building;

        if (upgradePanel != null)
        {
            //  1. Chuyển tọa độ 3D của công trình sang tọa độ 2D trên màn hình
            Vector3 buildingWorldPos = building.transform.position;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(buildingWorldPos);

            //  2. Đặt vị trí của Upgrade Panel ngay tại vị trí công trình (+ offset bên dưới)
            RectTransform panelRect = upgradePanel.GetComponent<RectTransform>();
            panelRect.position = screenPos + upgradePanelOffset;

            upgradePanel.SetActive(true);
        }

        upgradeClickCount = 0;
        RefreshUpgradePanel(building);
    }

   public void RefreshUpgradePanel(UpgradeableBuilding building)
    {
        if (building == null) return;

        int currentLevelIdx = building.CurrentLevel;
        int displayLevel = currentLevelIdx + 1;
        bool isMaxLevel = building.CurrentLevel >= building.MaxLevel - 1;
        bool isCurrentlyUpgrading = building.IsUpgrading;

        if (buildingNameText != null) buildingNameText.text = building.buildingName;
        if (levelText != null) levelText.text = $"Cấp {displayLevel} / {building.MaxLevel}";

        // ====================================================================
        // PENTA DEV - LUỒNG XỬ LÝ CHUYỂN ĐỔI NÚT NÂNG CẤP / SỬA CHỮA TỰ ĐỘNG
        // ====================================================================
        if (building.IsRuined) // Nếu nhà đang bị sập thành tàn tích
        {
            // 1. Ẩn nút nâng cấp, hiện nút sửa chữa lên
            if (upgradeButton != null) upgradeButton.gameObject.SetActive(false);
            if (repairButton != null)
            {
                repairButton.gameObject.SetActive(true);
                repairButton.interactable = !isCurrentlyUpgrading;

                // 2. Làm sạch sự kiện cũ và gán lệnh bắt đầu sửa chữa khi nhấn nút
                repairButton.onClick.RemoveAllListeners();
                repairButton.onClick.AddListener(() => {
                    building.StartRepair();
                    HideUpgradePanel(); // Ẩn panel để người chơi thấy thanh đếm giây thi công trên đầu nhà
                });
            }
        }
        else // Nếu nhà bình thường, hoạt động ổn định
        {
            // Hiện lại nút nâng cấp, ẩn nút sửa chữa đi
            if (upgradeButton != null) upgradeButton.gameObject.SetActive(true);
            if (repairButton != null) repairButton.gameObject.SetActive(false);

            if (upgradeButton != null) upgradeButton.interactable = !isMaxLevel && !isCurrentlyUpgrading;
        }
        // ====================================================================

        if (moveButton != null) moveButton.interactable = !isCurrentlyUpgrading;

        if (upgradeButtonText != null)
        {
            if (isMaxLevel) upgradeButtonText.text = "Đã tối đa";
            else if (isCurrentlyUpgrading) upgradeButtonText.text = "Đang nâng cấp...";
            // Đổi chữ nút nâng cấp dựa theo trạng thái click
            else upgradeButtonText.text = (upgradeClickCount == 0) ? "Nâng cấp" : "Xác nhận";
        }

        // --- LUỒNG XỬ LÝ HÌNH ẢNH PREVIEW ---
        if (building.BuildingIcons != null)
        {
            if (currentBuildingPreviewImage != null && currentLevelIdx < building.BuildingIcons.Length)
            {
                currentBuildingPreviewImage.sprite = building.BuildingIcons[currentLevelIdx];
                currentBuildingPreviewImage.gameObject.SetActive(true);
            }

            if (nextBuildingPreviewImage != null)
            {
                if (!isMaxLevel && (currentLevelIdx + 1) < building.BuildingIcons.Length)
                {
                    nextBuildingPreviewImage.sprite = building.BuildingIcons[currentLevelIdx + 1];
                    nextBuildingPreviewImage.gameObject.SetActive(true);
                }
                else
                {
                    nextBuildingPreviewImage.gameObject.SetActive(false);
                }
            }
        }

        // --- LUỒNG HIỂN THỊ CHỈ SỐ (QUÂN SỰ & DÂN SỰ) ---
        bool isDefenseTower = building.buildingType == BuildingType.WatchTower ||
                              building.buildingType == BuildingType.ArcherTower ||
                              building.buildingType == BuildingType.Cannon;

        // Chỉ hiển thị thông số chi tiết khi đã đạt Max cấp HOẶC khi người chơi nhấn nút Nâng cấp lần 1
        // LƯU Ý: Nếu đang sập (IsRuined) thì ẩn panel chỉ số đi để tránh hiển thị nhầm thông số nâng cấp
        if ((isMaxLevel || upgradeClickCount == 1) && !building.IsRuined)
        {
            if (chiso_panel != null) chiso_panel.SetActive(true);

            if (isDefenseTower)
            {
                ResetStatLabels("Sát thương", "Tầm bắn", "Tốc độ");
                if (txt_Label_ChiSo3 != null) txt_Label_ChiSo3.gameObject.SetActive(true);
                if (txt_TocDo_HienTai != null) txt_TocDo_HienTai.gameObject.SetActive(true);

                UpdateDetailedTowerStats(building, currentLevelIdx, isMaxLevel);
            }
            else
            {
                HandleCivilianBuildingUI(building, isMaxLevel);
            }
        }
        else
        {
            if (chiso_panel != null) chiso_panel.SetActive(false);
        }

        // --- LUỒNG HIỂN THỊ CHI PHÍ TÀI NGUYÊN ---
        if (isMaxLevel)
        {
            if (woodCostText != null) woodCostText.text = "-";
            if (stoneCostText != null) stoneCostText.text = "-";
            if (foodCostText != null) foodCostText.text = "-";
        }
        else if (building.IsRuined)
        {
            // NẾU ĐANG SẬP: Hiển thị chi phí sửa chữa cố định (Ví dụ: 30 Gỗ, 30 Đá)
            if (woodCostText != null) woodCostText.text = "30";
            if (stoneCostText != null) stoneCostText.text = "30";
            if (foodCostText != null) foodCostText.text = "0"; // Sửa nhà không tốn lương thực
        }
        else
        {
            // NẾU BÌNH THƯỜNG: Hiện chi phí nâng cấp lên cấp tiếp theo như cũ
            UpgradeableBuilding.UpgradeCost cost = building.GetNextUpgradeCost();
            if (woodCostText != null) woodCostText.text = MathUtility_FormatCost(cost.woodCost);
            if (stoneCostText != null) stoneCostText.text = MathUtility_FormatCost(cost.stoneCost);
            if (foodCostText != null) foodCostText.text = MathUtility_FormatCost(cost.foodCost);
        }
    }

    // Helper đổi tên nhãn tiêu đề chỉ số bên trái panel trực quan
    private void ResetStatLabels(string label1, string label2, string label3 = "")
    {
        if (txt_Label_ChiSo1 != null) txt_Label_ChiSo1.text = label1;
        if (txt_Label_ChiSo2 != null) txt_Label_ChiSo2.text = label2;
        if (txt_Label_ChiSo3 != null)
        {
            if (string.IsNullOrEmpty(label3)) txt_Label_ChiSo3.gameObject.SetActive(false);
            else
            {
                txt_Label_ChiSo3.gameObject.SetActive(true);
                txt_Label_ChiSo3.text = label3;
            }
        }
    }

    private void HandleCivilianBuildingUI(UpgradeableBuilding building, bool isMax)
    {
        int lv = building.CurrentLevel;
        if (chiso_hientai_obj != null) chiso_hientai_obj.SetActive(true);

        // Ẩn bớt hàng thông số thứ 3 (vì dân sự thường chỉ cần Worker và Kho)
        if (txt_Label_ChiSo3 != null) txt_Label_ChiSo3.gameObject.SetActive(false);
        if (txt_TocDo_HienTai != null) txt_TocDo_HienTai.gameObject.SetActive(false);
        if (txt_TocDo_NangCap != null) txt_TocDo_NangCap.text = "";

        // Xử lý khối hiển thị MAX cấp cho cụm Nâng Cấp
        if (isMax)
        {
            if (chiso_nangcap_obj != null) chiso_nangcap_obj.SetActive(false);
            if (txt_MaxLevelNotice != null)
            {
                txt_MaxLevelNotice.gameObject.SetActive(true);
                txt_MaxLevelNotice.text = "CẤP TỐI ĐA";
            }
        }
        else
        {
            if (chiso_nangcap_obj != null) chiso_nangcap_obj.SetActive(true);
            if (txt_MaxLevelNotice != null) txt_MaxLevelNotice.gameObject.SetActive(false);
        }

        switch (building.buildingType)
        {
            case BuildingType.WoodCutter:
                if (building.WoodStorageLevels != null && lv < building.WoodStorageLevels.Length && building.WoodStorageLevels[lv] != null)
                {
                    ResetStatLabels("Thợ chặt gỗ", "Sức chứa kho");
                    var curScript = building.WoodStorageLevels[lv];

                    // Điền thông số Hiện Tại
                    if (txt_SatThuong_HienTai != null) txt_SatThuong_HienTai.text = $"{curScript.currentWorkersCount}/{curScript.MaxWorkers}";
                    if (txt_TamBan_HienTai != null) txt_TamBan_HienTai.text = $"{curScript.CurrentAmount}/{curScript.MaxCapacity}";

                    // Điền thông số Nâng Cấp tiếp theo nếu chưa Max
                    if (!isMax && (lv + 1) < building.WoodStorageLevels.Length && building.WoodStorageLevels[lv + 1] != null)
                    {
                        var nxtScript = building.WoodStorageLevels[lv + 1];
                        if (txt_SatThuong_NangCap != null)
                            txt_SatThuong_NangCap.text = nxtScript.MaxWorkers > curScript.MaxWorkers ? $"<color=green>{nxtScript.MaxWorkers} Max</color>" : $"{nxtScript.MaxWorkers} Max";
                        if (txt_TamBan_NangCap != null)
                            txt_TamBan_NangCap.text = nxtScript.maxCapacity > curScript.maxCapacity ? $"<color=green>{nxtScript.maxCapacity}</color>" : $"{nxtScript.maxCapacity}";
                    }
                }
                break;

            case BuildingType.StoneStorage:
                if (building.StoneStorageLevels != null && lv < building.StoneStorageLevels.Length && building.StoneStorageLevels[lv] != null)
                {
                    ResetStatLabels("Thợ khai thác", "Sức chứa kho");
                    var curScript = building.StoneStorageLevels[lv];

                    if (txt_SatThuong_HienTai != null) txt_SatThuong_HienTai.text = $"{curScript.currentWorkersCount}/{curScript.MaxWorkers}";
                    if (txt_TamBan_HienTai != null) txt_TamBan_HienTai.text = $"{curScript.CurrentAmount}/{curScript.MaxCapacity}";

                    if (!isMax && (lv + 1) < building.StoneStorageLevels.Length && building.StoneStorageLevels[lv + 1] != null)
                    {
                        var nxtScript = building.StoneStorageLevels[lv + 1];
                        if (txt_SatThuong_NangCap != null)
                            txt_SatThuong_NangCap.text = nxtScript.MaxWorkers > curScript.MaxWorkers ? $"<color=green>{nxtScript.MaxWorkers} Max</color>" : $"{nxtScript.MaxWorkers} Max";
                        if (txt_TamBan_NangCap != null)
                            txt_TamBan_NangCap.text = nxtScript.maxCapacity > curScript.maxCapacity ? $"<color=green>{nxtScript.maxCapacity}</color>" : $"{nxtScript.maxCapacity}";
                    }
                }
                break;

            case BuildingType.FoodStorage:
                if (building.RiceStorageLevels != null && lv < building.RiceStorageLevels.Length && building.RiceStorageLevels[lv] != null)
                {
                    ResetStatLabels("Nông dân ruộng", "Sức chứa kho");
                    var curScript = building.RiceStorageLevels[lv];

                    if (txt_SatThuong_HienTai != null) txt_SatThuong_HienTai.text = $"{curScript.currentWorkersCount}/{curScript.MaxWorkers}";
                    if (txt_TamBan_HienTai != null) txt_TamBan_HienTai.text = $"{curScript.CurrentAmount}/{curScript.MaxCapacity}";

                    if (!isMax && (lv + 1) < building.RiceStorageLevels.Length && building.RiceStorageLevels[lv + 1] != null)
                    {
                        var nxtScript = building.RiceStorageLevels[lv + 1];
                        if (txt_SatThuong_NangCap != null)
                            txt_SatThuong_NangCap.text = nxtScript.MaxWorkers > curScript.MaxWorkers ? $"<color=green>{nxtScript.MaxWorkers} Max</color>" : $"{nxtScript.MaxWorkers} Max";
                        if (txt_TamBan_NangCap != null)
                            txt_TamBan_NangCap.text = nxtScript.maxCapacity > curScript.maxCapacity ? $"<color=green>{nxtScript.maxCapacity}</color>" : $"{nxtScript.maxCapacity}";
                    }
                }
                break;

            case BuildingType.Kitchen:
                if (building.KitchenLevels != null && lv < building.KitchenLevels.Length && building.KitchenLevels[lv] != null)
                {
                    ResetStatLabels("Đầu bếp / Thợ", "Sức chứa bếp");
                    var curScript = building.KitchenLevels[lv];

                    if (txt_SatThuong_HienTai != null) txt_SatThuong_HienTai.text = $"{curScript.currentWorkersCount}/{curScript.maxCapacity}"; // Tạm thời dùng maxCapacity làm maxWorkers vì là chỗ nghỉ
                    if (txt_TamBan_HienTai != null) txt_TamBan_HienTai.text = $"{curScript.WorkerCount}/{curScript.maxCapacity}";

                    if (!isMax && (lv + 1) < building.KitchenLevels.Length && building.KitchenLevels[lv + 1] != null)
                    {
                        var nxtScript = building.KitchenLevels[lv + 1];
                        if (txt_SatThuong_NangCap != null)
                            txt_SatThuong_NangCap.text = nxtScript.maxCapacity > curScript.maxCapacity ? $"<color=green>{nxtScript.maxCapacity} Slot</color>" : $"{nxtScript.maxCapacity} Slot";
                        if (txt_TamBan_NangCap != null)
                            txt_TamBan_NangCap.text = nxtScript.maxCapacity > curScript.maxCapacity ? $"<color=green>{nxtScript.maxCapacity}</color>" : $"{nxtScript.maxCapacity}";
                    }
                }
                break;

            case BuildingType.House:
                if (building.HouseLevels != null && lv < building.HouseLevels.Length && building.HouseLevels[lv] != null)
                {
                    // Đổi tên nhãn hiển thị bên trái cột thông số
                    ResetStatLabels("Sức chứa nhà", "Tốc độ khai thác");
                    var curScript = building.HouseLevels[lv];

                    // 1. Điền thông số hiện tại (Cột bên trái)
                    // Hiện số worker hiện tại đang ngủ / Sức chứa tối đa cấp này
                    if (txt_SatThuong_HienTai != null) 
                        txt_SatThuong_HienTai.text = $"{curScript.WorkerCount}/{curScript.maxCapacity}";
                    
                    // Hiện tốc độ khai thác của cấp này
                    if (txt_TamBan_HienTai != null) 
                        txt_TamBan_HienTai.text = $"{curScript.gatherSpeed:F1}s";

                    // 2. Điền thông số cấp tiếp theo nếu chưa đạt cấp tối đa (Cột bên phải)
                    if (!isMax && (lv + 1) < building.HouseLevels.Length && building.HouseLevels[lv + 1] != null)
                    {
                        var nxtScript = building.HouseLevels[lv + 1];
                        
                        // Nếu tăng sức chứa tối đa thì hiển thị chữ màu xanh green
                        if (txt_SatThuong_NangCap != null) 
                            txt_SatThuong_NangCap.text = nxtScript.maxCapacity > curScript.maxCapacity 
                                ? $"<color=green>{nxtScript.maxCapacity} Worker</color>" 
                                : $"{nxtScript.maxCapacity} Worker";

                        // Nếu tốc độ khai thác được tối ưu (giảm thời gian xuống) thì hiện màu xanh
                        if (txt_TamBan_NangCap != null) 
                            txt_TamBan_NangCap.text = nxtScript.gatherSpeed < curScript.gatherSpeed 
                                ? $"<color=green>{nxtScript.gatherSpeed:F1}s</color>" 
                                : $"{nxtScript.gatherSpeed:F1}s";
                    }
                }
                break;
            default:
                if (chiso_panel != null) chiso_panel.SetActive(false);
                break;
        }
    }

    private string MathUtility_FormatCost(int rawCost)
    {
        return rawCost.ToString();
    }

    /// <summary>
    /// Bóc tách thông số tháp phòng thủ và điền chuẩn xác vào từng thành phần TMP_Text con bên trong panel chỉ số
    /// </summary>
    private void UpdateDetailedTowerStats(UpgradeableBuilding building, int currentLv, bool isMax)
    {
        float curDamage = 0, nxtDamage = 0;
        float curRange = 0, nxtRange = 0;
        float curSpeed = 0, nxtSpeed = 0;

        AttackTowerAI currentAttackAI = null;
        if (building.TowerLevelScripts != null && currentLv >= 0 && currentLv < building.TowerLevelScripts.Length)
        {
            currentAttackAI = building.TowerLevelScripts[currentLv];
        }

        WatchTowerAI watchAI = building.GetComponent<WatchTowerAI>();

        if (chiso_hientai_obj != null) chiso_hientai_obj.SetActive(true);

        if (currentAttackAI != null)
        {
            curSpeed = currentAttackAI.fireRate;
            curRange = currentAttackAI.AttackRange;

            if (currentLv == 0) curDamage = currentAttackAI.damageLv1;
            else if (currentLv == 1) curDamage = currentAttackAI.damageLv2;
            else curDamage = currentAttackAI.damageLv3;

            if (txt_SatThuong_HienTai != null) txt_SatThuong_HienTai.text = curDamage.ToString();
            if (txt_TamBan_HienTai != null) txt_TamBan_HienTai.text = $"{curRange}m";
            if (txt_TocDo_HienTai != null) txt_TocDo_HienTai.text = $"{curSpeed}/s";

            if (isMax)
            {
                if (chiso_nangcap_obj != null) chiso_nangcap_obj.SetActive(false);
                if (txt_MaxLevelNotice != null)
                {
                    txt_MaxLevelNotice.gameObject.SetActive(true);
                    txt_MaxLevelNotice.text = "CẤP TỐI ĐA";
                }
            }
            else
            {
                if (chiso_nangcap_obj != null) chiso_nangcap_obj.SetActive(true);
                if (txt_MaxLevelNotice != null) txt_MaxLevelNotice.gameObject.SetActive(false);

                int nextLv = currentLv + 1;
                AttackTowerAI nextAttackAI = null;

                if (building.TowerLevelScripts != null && nextLv < building.TowerLevelScripts.Length)
                {
                    nextAttackAI = building.TowerLevelScripts[nextLv];
                }

                if (nextAttackAI != null)
                {
                    nxtSpeed = nextAttackAI.fireRate;
                    nxtRange = nextAttackAI.AttackRange;

                    if (nextLv == 1) nxtDamage = nextAttackAI.damageLv2;
                    else nxtDamage = nextAttackAI.damageLv3;
                }
                else
                {
                    nxtDamage = curDamage;
                    nxtRange = curRange;
                    nxtSpeed = curSpeed;
                }

                if (txt_SatThuong_NangCap != null)
                    txt_SatThuong_NangCap.text = nxtDamage > curDamage ? $"<color=green>{nxtDamage}</color>" : nxtDamage.ToString();

                if (txt_TamBan_NangCap != null)
                    txt_TamBan_NangCap.text = nxtRange > curRange ? $"<color=green>{nxtRange}m</color>" : $"{nxtRange}m";

                if (txt_TocDo_NangCap != null)
                    txt_TocDo_NangCap.text = nxtSpeed > curSpeed ? $"<color=green>{nxtSpeed}/s</color>" : $"{nxtSpeed}/s";
            }
        }
        else if (watchAI != null)
        {
            curRange = watchAI.detectRadius;
            curSpeed = watchAI.scanInterval;

            if (txt_SatThuong_HienTai != null) txt_SatThuong_HienTai.text = "-";
            if (txt_TamBan_HienTai != null) txt_TamBan_HienTai.text = $"{curRange}m";
            if (txt_TocDo_HienTai != null) txt_TocDo_HienTai.text = $"{curSpeed}s";

            if (isMax)
            {
                if (chiso_nangcap_obj != null) chiso_nangcap_obj.SetActive(false);
                if (txt_MaxLevelNotice != null)
                {
                    txt_MaxLevelNotice.gameObject.SetActive(true);
                    txt_MaxLevelNotice.text = "CẤP TỐI ĐA";
                }
            }
            else
            {
                if (chiso_nangcap_obj != null) chiso_nangcap_obj.SetActive(true);
                if (txt_MaxLevelNotice != null) txt_MaxLevelNotice.gameObject.SetActive(false);

                float nxtWatchRange = curRange + 5f;

                if (txt_SatThuong_NangCap != null) txt_SatThuong_NangCap.text = "-";
                if (txt_TamBan_NangCap != null) txt_TamBan_NangCap.text = $"<color=green>{nxtWatchRange}m</color>";
                if (txt_TocDo_NangCap != null) txt_TocDo_NangCap.text = $"{curSpeed}s";
            }
        }
    }

    public void HideUpgradePanel()
    {
        selectedBuilding = null;
        upgradeClickCount = 0;
        if (upgradePanel != null) upgradePanel.SetActive(false);
    }

    public void OnClickUpgradeButton()
    {
        if (selectedBuilding == null || selectedBuilding.IsUpgrading) return;

        if (upgradeClickCount == 0)
        {
            upgradeClickCount = 1;
            RefreshUpgradePanel(selectedBuilding);
            return;
        }

        UpgradeableBuilding.UpgradeCost cost = selectedBuilding.GetNextUpgradeCost();
        if (DialogNPC.Instance != null && !DialogNPC.Instance.Consume(cost.woodCost, cost.foodCost, cost.stoneCost)) return;

        selectedBuilding.StartUpgradeProcess();
        upgradeClickCount = 0;
        RefreshUpgradePanel(selectedBuilding);
    }

    public void OnClickMoveButton()
    {
        if (selectedBuilding == null) return;

        if (BuildingSystem.Ins != null)
        {
            BuildingSystem.Ins.StartMoving(selectedBuilding);
        }

        HideUpgradePanel();
    }

    public void CloseUpgradePanel()
    {
        selectedBuilding = null;
        upgradeClickCount = 0;
        if (upgradePanel != null)
        {
            upgradePanel.SetActive(false);
            Debug.Log("[UIManager] ❌ Đã đóng Cửa sổ Nâng cấp / Di chuyển công trình.");
        }
    }

    public void CloseBuildMenu()
    {
        if (buildMenu != null)
        {
            buildMenu.SetActive(false);
            Debug.Log("[UIManager] ❌ Đã đóng Menu Xây dựng.");
        }
    }

/// <summary>
    /// Tắt tất cả các cửa sổ Popup đang mở để tránh chồng lấp UI
    /// </summary>
    public void CloseAllPopups()
    {
        if (buildMenu != null) buildMenu.SetActive(false);
        if (upgradePanel != null) upgradePanel.SetActive(false);
        if (settingUI != null) settingUI.SetActive(false);
        if (controlHintsGroup != null) controlHintsGroup.SetActive(false);

        // Nếu bảng thông tin công nhân/người chơi cũng là dạng Popup muốn ẩn khi mở menu khác:
        // if (workerStatusPanel != null) workerStatusPanel.SetActive(false);

        selectedBuilding = null;
        upgradeClickCount = 0;
    }
    public void CloseAllActiveWindows()
    {
        if (upgradePanel != null) upgradePanel.SetActive(false);
        if (buildMenu != null) buildMenu.SetActive(false);
        if (settingUI != null) settingUI.SetActive(false);
        if (houseSelectionPanel != null) houseSelectionPanel.SetActive(false);
        if (workerStatusPanel != null) workerStatusPanel.SetActive(false);

        selectedBuilding = null;
        upgradeClickCount = 0;
        Debug.Log("[UIManager] 🧹 Đã dọn dẹp và ẩn toàn bộ giao diện cửa sổ popup.");
    }
}