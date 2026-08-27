using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*
 * BuildingUpgradeSidePanelUI.cs
 * Folder: Scripts/UI/
 * Dự án: KHẨN HOANG (PENTA DEV)
 * Phong cách: Demacia Rising Side-by-Side Building Upgrade & Details Panel
 */

public class BuildingUpgradeSidePanelUI : MonoBehaviour
{
    public static BuildingUpgradeSidePanelUI Ins { get; private set; }

    [Header("=== THÀNH PHẦN HEADER & ĐÓNG PANEL ===")]
    [SerializeField] private TextMeshProUGUI levelBadgeTMP; // VD: "Lv. 1"
    [SerializeField] private Button closeBtn;

    [Header("=== THÔNG TIN VÀ ẢNH MINH HỌA ===")]
    [SerializeField] private TextMeshProUGUI buildingNameTMP; // VD: "Lumberyard" / "Trại Mộc"
    [SerializeField] private Image artworkImage;

    [Header("=== SO SÁNH CHỈ SỐ CẤP HIỆN TẠI & CẤP TIẾP THEO ===")]
    [SerializeField] private TextMeshProUGUI currentLevelStatTMP; // VD: "15 Lumber per turn"
    [SerializeField] private TextMeshProUGUI nextLevelStatTMP;    // VD: "30 Lumber per turn"

    [Header("=== CẢNH BÁO VÀ CHI PHÍ NÂNG CẤP ===")]
    [SerializeField] private TextMeshProUGUI warningNoticeTMP;    // VD: "Must upgrade Settlement"
    [SerializeField] private TextMeshProUGUI buildDurationTMP;    // VD: "2" (icon ⏳)
    [SerializeField] private TextMeshProUGUI woodCostTMP;
    [SerializeField] private TextMeshProUGUI stoneCostTMP;
    [SerializeField] private TextMeshProUGUI foodCostTMP;

    [Header("=== CÁC NÚT THAO TÁC ===")]
    [SerializeField] private Button upgradeBtn;  // Nút 🔨 UPGRADE
    [SerializeField] private Button demolishBtn; // Nút ❌ Phá dỡ

    [Header("=== XÁC NHẬN PHÁ DỠ ===")]
    [Tooltip("Kéo GameObject gốc của bảng xác nhận vào đây (object chứa Cancel, OK và Text).")]
    [SerializeField] private GameObject demolishConfirmPanel;
    [SerializeField] private Button demolishCancelBtn;
    [SerializeField] private Button demolishConfirmBtn;

    [Header("=== SPRITE TÙY CHỈNH CHO NÚT BẤM ===")]
    [SerializeField] private Sprite upgradeBtnSprite;  // Sprite cho nút NÂNG CẤP
    [SerializeField] private Sprite buildBtnSprite;    // Sprite cho nút XÂY NHÀ CHÍNH / XÂY DỰNG
    [SerializeField] private Sprite repairBtnSprite;   // Sprite cho nút SỬA CHỮA

    [Header("=== TÙY CHỈNH MÀU CHI PHÍ ===")]
    [SerializeField] private Color affordableColor = new Color(0.2f, 0.9f, 0.3f, 1f);   // Xanh lá
    [SerializeField] private Color unaffordableColor = new Color(0.9f, 0.2f, 0.2f, 1f); // Đỏ

    private UpgradeableBuilding targetBuilding;
    private SettlementZone targetEstablishZone;
    private UpgradeableBuilding pendingDemolishBuilding;
    private bool isEstablishMode = false;

    private void Awake()
    {
        if (Ins == null) Ins = this;
        else Destroy(gameObject);

        HideFoodCostDisplay();
    }

    private void OnDestroy()
    {
        if (Ins == this) Ins = null;
    }

    private void HideFoodCostDisplay()
    {
        // Food không còn là chi phí nâng cấp/lập vùng; ẩn cả text lẫn icon con.
        if (foodCostTMP != null) foodCostTMP.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        // ManagerUI có thể đóng panel trực tiếp khi chuyển sang Xây dựng/Huấn luyện.
        // Khi đó cũng phải hủy hộp xác nhận đang mở.
        HideManualDemolishConfirmation();
        BuildingDemolishConfirmUI.Ins?.Hide();
    }

    private void Start()
    {
        if (closeBtn != null)
        {
            closeBtn.onClick.RemoveListener(ClosePanel);
            closeBtn.onClick.AddListener(ClosePanel);
        }
        if (upgradeBtn != null)
        {
            upgradeBtn.onClick.RemoveListener(OnClickUpgrade);
            upgradeBtn.onClick.AddListener(OnClickUpgrade);
        }
        if (demolishBtn != null)
        {
            demolishBtn.onClick.RemoveListener(OnClickDemolish);
            demolishBtn.onClick.AddListener(OnClickDemolish);
        }

        ConfigureManualDemolishConfirmation();
    }

    /// <summary>
    /// Hiển thị Bảng Nâng Cấp liền kề bên phải Panel Thủ Đô
    /// </summary>
    public void ShowUpgradePanel(UpgradeableBuilding building)
    {
        if (building == null) return;

        BuildTrainingUIManager.Ins?.ShowUpgradeWindow();

        targetBuilding = building;
        isEstablishMode = false;
        targetEstablishZone = null;

        gameObject.SetActive(true);

        RefreshPanel();
    }

    /// <summary>
    /// Hiển thị Bảng Xây Dựng Nhà Chính cho vùng đất mới chưa có Nhà Chính
    /// </summary>
    public void ShowEstablishTownHallPanel(SettlementZone zone)
    {
        if (zone == null) return;

        BuildTrainingUIManager.Ins?.ShowUpgradeWindow();

        targetEstablishZone = zone;
        isEstablishMode = true;
        targetBuilding = null;

        gameObject.SetActive(true);
        RefreshEstablishPanel();
    }

    /// <summary>
    /// Làm mới toàn bộ thông số, so sánh chỉ số và chi phí tài nguyên
    /// </summary>
    public void RefreshPanel()
    {
        if (targetBuilding == null) return;

        int currentLevel = targetBuilding.CurrentLevel + 1;
        int maxLevel = targetBuilding.MaxLevel;
        bool isMaxLevel = currentLevel >= maxLevel;

        // 1. Header & Tên nhà
        if (levelBadgeTMP != null) levelBadgeTMP.text = $"Lv. {currentLevel}";
        if (buildingNameTMP != null) buildingNameTMP.text = targetBuilding.buildingName;

        // 2. Ảnh Art Preview (nếu có Sprite)
        if (artworkImage != null)
        {
            Sprite sp = null;
            if (targetBuilding.BuildingIcons != null && targetBuilding.BuildingIcons.Length > 0)
            {
                int idx = Mathf.Clamp(targetBuilding.CurrentLevel, 0, targetBuilding.BuildingIcons.Length - 1);
                sp = targetBuilding.BuildingIcons[idx];
            }

            if (sp == null && BuildingShopUI.Ins != null)
            {
                var shopItem = BuildingShopUI.Ins.GetShopItem(targetBuilding.buildingType);
                if (shopItem != null) sp = shopItem.artworkSprite;
            }

            if (sp == null)
            {
                var rend = targetBuilding.GetComponentInChildren<SpriteRenderer>();
                if (rend != null) sp = rend.sprite;
            }

            if (sp != null)
            {
                artworkImage.sprite = sp;
                artworkImage.color = Color.white;
                artworkImage.gameObject.SetActive(true);
            }
            else
            {
                artworkImage.gameObject.SetActive(false); // 🔒 Tắt hình vuông trắng khi không có sprite
            }
        }

        // 3. So sánh Chỉ số Cấp hiện tại & Cấp tiếp theo (Lấy dữ liệu thực từ công trình)
        GetBuildingRealStats(targetBuilding, out string curStatText, out string nextStatText);

        if (currentLevelStatTMP != null)
        {
            currentLevelStatTMP.text = curStatText;
        }

        if (nextLevelStatTMP != null)
        {
            nextLevelStatTMP.text = nextStatText;
        }

        // 4. Lấy chi phí nâng cấp hoặc sửa chữa
        bool isRuined = targetBuilding.IsRuined;
        bool isUpgrading = targetBuilding.IsUpgrading;

        int woodCost = 0, stoneCost = 0, foodCost = 0;
        int duration = 1;

        if (isRuined)
        {
            woodCost = targetBuilding.RepairWoodCost;
            stoneCost = targetBuilding.RepairStoneCost;
            foodCost = 0;
            duration = Mathf.RoundToInt(targetBuilding.RepairDuration);
        }
        else
        {
            var nextCost = targetBuilding.GetNextUpgradeCost();
            woodCost = nextCost.woodCost;
            stoneCost = nextCost.stoneCost;
            foodCost = nextCost.foodCost;
            duration = nextCost.upgradeDuration > 0 ? nextCost.upgradeDuration : 1;

            if (woodCost == 0 && stoneCost == 0 && foodCost == 0 && ConstructionManager.Ins != null)
            {
                var costData = ConstructionManager.Ins.GetBuildingCost(targetBuilding.buildingType);
                woodCost = Mathf.RoundToInt(costData.woodCost * 1.5f);
                stoneCost = Mathf.RoundToInt(costData.stoneCost * 1.5f);
                foodCost = Mathf.RoundToInt(costData.foodCost * 1.5f);
            }
        }

        // 5. Kiểm tra Cấp Thủ Đô & Tài nguyên
        SettlementZone currentZone = (SettlementManager.Ins != null) ? SettlementManager.Ins.CurrentSettlement : null;
        int settlementLevel = (currentZone != null) ? currentZone.SettlementLevel : 1;

        bool isTownHall = targetBuilding.buildingType == BuildingType.House || 
                         targetBuilding.buildingName.Contains("Nhà chính") || 
                         targetBuilding.buildingName.Contains("Town Hall");
        bool isProtectedCentralBarracks = IsProtectedCentralBarracks(targetBuilding);

        // Cần Cấp Thủ Đô > Cấp hiện tại của công trình (Trừ chính Nhà Chính)
        bool settlementLevelOk = isTownHall || (targetBuilding.CurrentLevel < settlementLevel);

        bool hasEnoughWood = true, hasEnoughStone = true, hasEnoughFood = true;
        bool canAfford = true;

        if (JsonDataManager.Ins != null)
        {
            hasEnoughWood = JsonDataManager.Ins.wood >= woodCost;
            hasEnoughStone = JsonDataManager.Ins.stone >= stoneCost;
            hasEnoughFood = JsonDataManager.Ins.food >= foodCost;
            canAfford = JsonDataManager.Ins.HasEnoughResources(woodCost, stoneCost, foodCost);
        }

        if (warningNoticeTMP != null)
        {
            if (isUpgrading)
            {
                warningNoticeTMP.text = "⏳ Đang trong quá trình tiến hành...";
                warningNoticeTMP.gameObject.SetActive(true);
            }
            else if (isRuined)
            {
                warningNoticeTMP.text = "⚠️ Công trình bị hỏng! Cần sửa chữa trước!";
                warningNoticeTMP.gameObject.SetActive(true);
            }
            else if (!settlementLevelOk)
            {
                warningNoticeTMP.text = "⚠️ Cần nâng cấp Thủ Đô trước!";
                warningNoticeTMP.gameObject.SetActive(true);
            }
            else if (!canAfford)
            {
                warningNoticeTMP.text = "Không đủ tài nguyên nâng cấp!";
                warningNoticeTMP.gameObject.SetActive(true);
            }
            else
            {
                warningNoticeTMP.gameObject.SetActive(false);
            }
        }

        // Cập nhật chữ chi phí
        if (woodCostTMP != null)
        {
            woodCostTMP.text = woodCost.ToString();
            woodCostTMP.color = hasEnoughWood ? affordableColor : unaffordableColor;
        }

        if (stoneCostTMP != null)
        {
            stoneCostTMP.text = stoneCost.ToString();
            stoneCostTMP.color = hasEnoughStone ? affordableColor : unaffordableColor;
        }

        if (foodCostTMP != null)
        {
            foodCostTMP.text = foodCost.ToString();
            foodCostTMP.color = hasEnoughFood ? affordableColor : unaffordableColor;
        }

        if (buildDurationTMP != null)
        {
            buildDurationTMP.text = duration.ToString();
        }

        // Cập nhật trạng thái & Chữ của nút Nâng Cấp / Sửa Chữa
        if (upgradeBtn != null)
        {
            var btnText = upgradeBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = isRuined ? "SỬA CHỮA" : "NÂNG CẤP";
            }
            upgradeBtn.interactable = canAfford && !isUpgrading && settlementLevelOk && (isRuined || !isMaxLevel);

            Image btnImg = upgradeBtn.GetComponent<Image>();
            if (btnImg != null)
            {
                if (isRuined && repairBtnSprite != null)
                {
                    btnImg.sprite = repairBtnSprite;
                }
                else if (!isRuined && upgradeBtnSprite != null)
                {
                    btnImg.sprite = upgradeBtnSprite;
                }
            }
        }

        if (demolishBtn != null)
        {
            demolishBtn.gameObject.SetActive(!isTownHall && !isProtectedCentralBarracks);
        }
    }

    private void RefreshEstablishPanel()
    {
        if (targetEstablishZone == null) return;

        if (levelBadgeTMP != null) levelBadgeTMP.text = "Lv. 0";
        if (buildingNameTMP != null) buildingNameTMP.text = "Nhà Chính (Town Hall)";

        if (currentLevelStatTMP != null) currentLevelStatTMP.text = "Chưa có Nhà Chính";
        if (nextLevelStatTMP != null) nextLevelStatTMP.text = $"Mở khóa và kích hoạt lãnh thổ {targetEstablishZone.settlementName}";

        int woodCost = targetEstablishZone.establishWoodCost;
        int stoneCost = targetEstablishZone.establishStoneCost;
        int foodCost = 0;

        bool hasEnoughWood = true, hasEnoughStone = true, hasEnoughFood = true;
        bool canAfford = true;

        if (JsonDataManager.Ins != null)
        {
            hasEnoughWood = JsonDataManager.Ins.wood >= woodCost;
            hasEnoughStone = JsonDataManager.Ins.stone >= stoneCost;
            hasEnoughFood = JsonDataManager.Ins.food >= foodCost;
            canAfford = JsonDataManager.Ins.HasEnoughResources(woodCost, stoneCost, foodCost);
        }

        if (warningNoticeTMP != null)
        {
            if (!canAfford)
            {
                warningNoticeTMP.text = "Không đủ tài nguyên xây Nhà Chính!";
                warningNoticeTMP.gameObject.SetActive(true);
            }
            else
            {
                warningNoticeTMP.gameObject.SetActive(false);
            }
        }

        if (woodCostTMP != null)
        {
            woodCostTMP.text = woodCost.ToString();
            woodCostTMP.color = hasEnoughWood ? affordableColor : unaffordableColor;
        }

        if (stoneCostTMP != null)
        {
            stoneCostTMP.text = stoneCost.ToString();
            stoneCostTMP.color = hasEnoughStone ? affordableColor : unaffordableColor;
        }

        if (foodCostTMP != null)
        {
            foodCostTMP.text = foodCost.ToString();
            foodCostTMP.color = hasEnoughFood ? affordableColor : unaffordableColor;
        }

        if (buildDurationTMP != null)
        {
            buildDurationTMP.text = "1";
        }

        if (upgradeBtn != null)
        {
            var btnText = upgradeBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = "XÂY NHÀ CHÍNH";
            upgradeBtn.interactable = canAfford;

            Image btnImg = upgradeBtn.GetComponent<Image>();
            if (btnImg != null && buildBtnSprite != null)
            {
                btnImg.sprite = buildBtnSprite;
            }
        }

        if (demolishBtn != null)
        {
            demolishBtn.gameObject.SetActive(false);
        }
    }

    private void OnClickUpgrade()
    {
        if (isEstablishMode && targetEstablishZone != null)
        {
            bool success = targetEstablishZone.EstablishTownHall();
            if (success)
            {
                isEstablishMode = false;
                if (targetEstablishZone.townHallBuilding != null)
                {
                    ShowUpgradePanel(targetEstablishZone.townHallBuilding);
                }
                else
                {
                    ClosePanel();
                }

                if (SettlementSidePanelUI.Ins != null)
                {
                    SettlementSidePanelUI.Ins.RefreshPanel();
                }
            }
            return;
        }

        if (targetBuilding == null) return;

        if (targetBuilding.IsRuined)
        {
            targetBuilding.StartRepair();
        }
        else
        {
            targetBuilding.Upgrade();
        }

        RefreshPanel();

        if (SettlementSidePanelUI.Ins != null)
        {
            SettlementSidePanelUI.Ins.UpdateHeaderVisual();
            SettlementSidePanelUI.Ins.RefreshPanel();
        }
    }

    private void OnClickDemolish()
    {
        if (targetBuilding == null) return;

        if (IsTownHall(targetBuilding))
        {
            if (UIManager.Ins != null) UIManager.Ins.ShowWarning("Không thể phá dỡ Nhà Chính!");
            return;
        }

        if (IsProtectedCentralBarracks(targetBuilding))
        {
            if (UIManager.Ins != null) UIManager.Ins.ShowWarning("Không thể phá dỡ Trại Lính trung tâm!");
            return;
        }

        if (ShowManualDemolishConfirmation(targetBuilding))
        {
            return;
        }

        // Fallback cho Scene chưa dựng bảng xác nhận riêng.
        BuildingDemolishConfirmUI demolishConfirmUI = BuildingDemolishConfirmUI.Ins;
        if (demolishConfirmUI == null)
        {
            demolishConfirmUI = FindFirstObjectByType<BuildingDemolishConfirmUI>(FindObjectsInactive.Include);
        }

        if (demolishConfirmUI == null || !demolishConfirmUI.Show(targetBuilding, DemolishBuilding))
        {
            Debug.LogWarning("[BuildingUpgradeSidePanelUI] Không thể mở hộp xác nhận phá dỡ.", this);
        }
    }

    private void DemolishBuilding(UpgradeableBuilding building)
    {
        if (building == null || IsTownHall(building) || IsProtectedCentralBarracks(building)) return;

        SettlementZone currentZone = (SettlementManager.Ins != null) ? SettlementManager.Ins.CurrentSettlement : null;
        if (currentZone != null && currentZone.builtStructures != null)
        {
            currentZone.builtStructures.Remove(building);
        }

        if (BuildingManager.Ins != null)
        {
            var bCtrl = building.GetComponent<BuildingCtrl>();
            if (bCtrl != null) BuildingManager.Ins.RemoveBuilding(bCtrl);
        }

        building.gameObject.SetActive(false);
        Destroy(building.gameObject);
        ClosePanel();

        if (SettlementSidePanelUI.Ins != null)
        {
            SettlementSidePanelUI.Ins.RefreshPanel();
        }
    }

    private static bool IsTownHall(UpgradeableBuilding building)
    {
        if (building == null) return false;

        string buildingName = building.buildingName ?? string.Empty;
        return building.buildingType == BuildingType.House ||
               buildingName.IndexOf("Nhà chính", StringComparison.OrdinalIgnoreCase) >= 0 ||
               buildingName.IndexOf("Town Hall", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsProtectedCentralBarracks(UpgradeableBuilding building)
    {
        if (building == null || !TroopTrainingManager.IsCentralBarracksType(building.buildingType)) return false;

        SettlementZone owningZone = building.GetComponentInParent<SettlementZone>();
        if (owningZone == null) return false;

        // Cờ gán tay có ưu tiên; Zone Tier 0 là fallback an toàn cho Scene cũ.
        return owningZone.isStartingSettlement || owningZone.zoneTier == 0;
    }

    private void ConfigureManualDemolishConfirmation()
    {
        if (demolishConfirmPanel == null) return;

        FindManualDemolishReferences();

        if (demolishCancelBtn != null)
        {
            demolishCancelBtn.onClick.RemoveListener(HideManualDemolishConfirmation);
            demolishCancelBtn.onClick.AddListener(HideManualDemolishConfirmation);
        }

        if (demolishConfirmBtn != null)
        {
            demolishConfirmBtn.onClick.RemoveListener(ConfirmManualDemolish);
            demolishConfirmBtn.onClick.AddListener(ConfirmManualDemolish);
        }

        demolishConfirmPanel.SetActive(false);
    }

    private bool ShowManualDemolishConfirmation(UpgradeableBuilding building)
    {
        if (demolishConfirmPanel == null) return false;

        FindManualDemolishReferences();
        if (demolishCancelBtn == null || demolishConfirmBtn == null)
        {
            Debug.LogWarning("[BuildingUpgradeSidePanelUI] Bảng hủy công trình cần có Button tên Cancel và OK (hoặc gán hai Button trong Inspector).", this);
            return false;
        }

        pendingDemolishBuilding = building;
        demolishConfirmPanel.SetActive(true);
        demolishConfirmPanel.transform.SetAsLastSibling();
        return true;
    }

    private void ConfirmManualDemolish()
    {
        UpgradeableBuilding building = pendingDemolishBuilding;
        HideManualDemolishConfirmation();
        DemolishBuilding(building);
    }

    private void HideManualDemolishConfirmation()
    {
        if (demolishConfirmPanel != null) demolishConfirmPanel.SetActive(false);
        pendingDemolishBuilding = null;
    }

    private void FindManualDemolishReferences()
    {
        if (demolishConfirmPanel == null) return;

        Button[] buttons = demolishConfirmPanel.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            string buttonName = button.name.ToLowerInvariant();
            if (demolishCancelBtn == null && (buttonName.Contains("cancel") || buttonName.Contains("huy")))
            {
                demolishCancelBtn = button;
            }
            else if (demolishConfirmBtn == null && (buttonName == "ok" || buttonName.Contains("confirm") || buttonName.Contains("xac nhan")))
            {
                demolishConfirmBtn = button;
            }
        }

    }

    /// <summary>
    /// Tính toán và định dạng thông số tăng sản lượng / sức chứa thực tế theo từng Cấp công trình
    /// </summary>
    private void GetBuildingRealStats(UpgradeableBuilding building, out string currentStatStr, out string nextStatStr)
    {
        if (building == null)
        {
            currentStatStr = "Cấp 1: Bình thường";
            nextStatStr = "Cấp 2: + Sản lượng";
            return;
        }

        int curLvl = building.CurrentLevel; // 0-indexed
        int nextLvl = curLvl + 1;
        bool isMax = nextLvl >= building.MaxLevel;

        BuildingType type = building.buildingType;

        switch (type)
        {
            case BuildingType.WoodCutter:
                {
                    var ws = building.GetComponentInChildren<WoodStorage>();
                    int curWorkers = (ws != null && ws.maxWorkersLevels != null && curLvl < ws.maxWorkersLevels.Length) ? ws.maxWorkersLevels[curLvl] : (curLvl + 1) * 2;
                    int nextWorkers = (ws != null && ws.maxWorkersLevels != null && nextLvl < ws.maxWorkersLevels.Length) ? ws.maxWorkersLevels[nextLvl] : (nextLvl + 1) * 2;
                    int diff = nextWorkers - curWorkers;

                    currentStatStr = $"{curWorkers * 15} Gỗ/lượt";
                    nextStatStr = isMax ? "ĐÃ ĐẠT CẤP TỐI ĐA" : $"+{diff * 15} Gỗ/lượt";
                }
                break;

            case BuildingType.StoneMine:
            case BuildingType.StoneStorage:
                {
                    var ss = building.GetComponentInChildren<StoneStorage>();
                    int curWorkers = (ss != null && ss.maxWorkersLevels != null && curLvl < ss.maxWorkersLevels.Length) ? ss.maxWorkersLevels[curLvl] : (curLvl + 1) * 2;
                    int nextWorkers = (ss != null && ss.maxWorkersLevels != null && nextLvl < ss.maxWorkersLevels.Length) ? ss.maxWorkersLevels[nextLvl] : (nextLvl + 1) * 2;
                    int diff = nextWorkers - curWorkers;

                    currentStatStr = $"{curWorkers * 15} Đá/lượt";
                    nextStatStr = isMax ? "ĐÃ ĐẠT CẤP TỐI ĐA" : $"+{diff * 15} Đá/lượt";
                }
                break;

            case BuildingType.Kitchen:
            case BuildingType.FoodStorage:
                {
                    var rs = building.GetComponentInChildren<RiceStorage>();
                    var kit = building.GetComponentInChildren<Kitchen>();
                    int curWorkers = (rs != null && rs.maxWorkersLevels != null && curLvl < rs.maxWorkersLevels.Length) ? rs.maxWorkersLevels[curLvl] : ((kit != null && kit.maxWorkersLevels != null && curLvl < kit.maxWorkersLevels.Length) ? kit.maxWorkersLevels[curLvl] : (curLvl + 1) * 3);
                    int nextWorkers = (rs != null && rs.maxWorkersLevels != null && nextLvl < rs.maxWorkersLevels.Length) ? rs.maxWorkersLevels[nextLvl] : ((kit != null && kit.maxWorkersLevels != null && nextLvl < kit.maxWorkersLevels.Length) ? kit.maxWorkersLevels[nextLvl] : (nextLvl + 1) * 3);
                    int diff = nextWorkers - curWorkers;

                    currentStatStr = $"{curWorkers * 15} Lương/lượt";
                    nextStatStr = isMax ? "ĐÃ ĐẠT CẤP TỐI ĐA" : $"+{diff * 15} Lương/lượt";
                }
                break;

            case BuildingType.House:
                {
                    int curCap = (curLvl + 1) * 4;
                    int nextCap = (nextLvl + 1) * 4;
                    int diff = nextCap - curCap;

                    currentStatStr = $"{curCap} Dân làng";
                    nextStatStr = isMax ? "ĐÃ ĐẠT CẤP TỐI ĐA" : $"+{diff} Dân làng";
                }
                break;

            case BuildingType.BarracksMelee:
            case BuildingType.BarracksArcher:
                {
                    int curSoldiers = (curLvl + 1) * 5;
                    int nextSoldiers = (nextLvl + 1) * 5;
                    int diff = nextSoldiers - curSoldiers;

                    currentStatStr = $"{curSoldiers} Lính";
                    nextStatStr = isMax ? "ĐÃ ĐẠT CẤP TỐI ĐA" : $"+{diff} Lính";
                }
                break;

            case BuildingType.WatchTower:
            case BuildingType.ArcherTower:
            case BuildingType.Cannon:
                {
                    float curDmg = 20f * (curLvl + 1);
                    float nextDmg = 20f * (nextLvl + 1);

                    var towerAI = building.GetComponentInChildren<AttackTowerAI>();
                    if (towerAI != null)
                    {
                        if (curLvl == 0) curDmg = towerAI.damageLv1;
                        else if (curLvl == 1) curDmg = towerAI.damageLv2;
                        else if (curLvl == 2) curDmg = towerAI.damageLv3;

                        if (nextLvl == 0) nextDmg = towerAI.damageLv1;
                        else if (nextLvl == 1) nextDmg = towerAI.damageLv2;
                        else if (nextLvl == 2) nextDmg = towerAI.damageLv3;
                    }

                    float diffDmg = nextDmg - curDmg;

                    currentStatStr = $"{curDmg} DP";
                    nextStatStr = isMax ? "ĐÃ ĐẠT CẤP TỐI ĐA" : $"+{diffDmg} DP";
                }
                break;

            default:
                {
                    currentStatStr = $"Cấp {curLvl + 1}: Hiệu suất bình thường";
                    nextStatStr = isMax ? "ĐÃ ĐẠT CẤP TỐI ĐA" : $"Cấp {nextLvl + 1}: +100% Hiệu suất & Độ bền";
                }
                break;
        }
    }

    public void ClosePanel()
    {
        BuildingDemolishConfirmUI.Ins?.Hide();
        gameObject.SetActive(false);
        BuildTrainingUIManager.Ins?.NotifyWindowClosed(BuildTrainingUIManager.ManagedWindow.Upgrade);
    }
}
