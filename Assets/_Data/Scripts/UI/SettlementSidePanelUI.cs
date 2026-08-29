using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*
 * SettlementSidePanelUI.cs
 * Folder: Scripts/UI/
 * Dự án: KHẨN HOANG (PENTA DEV)
 * Phong cách: Demacia Rising Capital/Settlement Left Side Panel (Multi-Settlement Enabled)
 */

public class SettlementSidePanelUI : MonoBehaviour
{
    public static SettlementSidePanelUI Ins { get; private set; }

    [Header("=== CẤU HÌNH TIÊU ĐỀ THỦ ĐÔ ===")]
    [SerializeField] private string defaultSettlementName = "ZEFFIRA";
    [SerializeField] private int defaultSettlementLevel = 1;
    [SerializeField] private TextMeshProUGUI settlementNameTMP;
    [SerializeField] private TextMeshProUGUI settlementLevelTMP;
    [SerializeField] private Button upgradeSettlementBtn;
    [SerializeField] private TextMeshProUGUI upgradeBtnTextTMP;
    [Header("=== ĐIỀU QUÂN ===")]
    [SerializeField] private Button moveButton;
    [SerializeField] private TextMeshProUGUI moveButtonTextTMP;
    [SerializeField] private Color moveButtonNormalColor = new Color(0.08f, 0.35f, 0.62f, 1f);
    [SerializeField] private Color moveButtonHighlightedColor = new Color(0.13f, 0.5f, 0.85f, 1f);

    [Header("=== SPRITE TÙY CHỈNH CHO NÚT HEADER ===")]
    [SerializeField] private Sprite upgradeHeaderSprite;   // Sprite nút Nâng cấp
    [SerializeField] private Sprite buildHeaderSprite;     // Sprite nút Xây Nhà Chính
    [SerializeField] private Sprite attackHeaderSprite;    // Sprite nút Tấn Công / Chiếm Đóng

    [Header("=== CONTAINER CHỨA LƯỚI CÁC Ô CÔNG TRÌNH ===")]
    [SerializeField] private Transform slotsContainer;
    [SerializeField] private GameObject slotItemPrefab;
    [SerializeField] private int totalSlotsCount = 12; // Tổng số ô hiển thị trong Panel

    [Header("=== CONTAINER CHỨA 8 Ô HUẤN LUYỆN LÍNH ===")]
    [SerializeField] private Transform troopTrainingContainer;
    [SerializeField] private GameObject troopTrainingSlotPrefab;
    [SerializeField] private SoldierPoint soldierPointUIPrefab;

    [Header("=== MOVE CAMERA / MŨI TÊN ===")]
    [SerializeField, Min(10f)] private float moveOverviewCameraHeight = 55f;
    [SerializeField, Min(10f)] private float moveRoutePreviewCameraHeight = 36f;
    [SerializeField, Min(5f)] private float moveSelectedCameraHeight = 22f;
    [SerializeField, Min(0.05f)] private float moveArrowHeightAboveTerrain = 0.8f;

    private List<SettlementSlotItemUI> activeSlotUIItems = new List<SettlementSlotItemUI>();
    private List<TroopTrainingSlotUI> activeTrainingSlotUIItems = new List<TroopTrainingSlotUI>();
    private bool movePanelHidden;

    private void OnDestroy()
    {
        if (Ins == this) Ins = null;
    }

    private void Awake()
    {
        if (Ins == null) Ins = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (upgradeSettlementBtn != null)
        {
            upgradeSettlementBtn.interactable = true;
            upgradeSettlementBtn.onClick.RemoveListener(OnClickUpgradeSettlement);
            upgradeSettlementBtn.onClick.AddListener(OnClickUpgradeSettlement);
        }

        ConfigureMoveButton();

        UpdateHeaderVisual();
        RefreshPanel();
    }

    private void OnEnable()
    {
        RestoreSettlementInfoAfterMove();

        if (upgradeSettlementBtn != null)
        {
            upgradeSettlementBtn.interactable = true;
        }
        ConfigureMoveButton();
        UpdateHeaderVisual();
        RefreshPanel();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            SynchronizeTroopTrainingSlotAppearance();
        }
    }
#endif

    private void ConfigureMoveButton()
    {
        if (moveButton == null) moveButton = FindMoveButton();
        if (moveButton == null) return;

        moveButton.onClick.RemoveListener(OnClickMove);
        moveButton.onClick.AddListener(OnClickMove);

        Image background = moveButton.targetGraphic as Image;
        if (background == null) background = moveButton.GetComponent<Image>();
        if (background != null) background.color = moveButtonNormalColor;

        ColorBlock colors = moveButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(
            moveButtonHighlightedColor.r / moveButtonNormalColor.r,
            moveButtonHighlightedColor.g / moveButtonNormalColor.g,
            moveButtonHighlightedColor.b / moveButtonNormalColor.b,
            1f);
        colors.pressedColor = new Color(0.8f, 0.9f, 1f, 1f);
        colors.selectedColor = colors.highlightedColor;
        moveButton.colors = colors;

        if (moveButtonTextTMP == null) moveButtonTextTMP = moveButton.GetComponentInChildren<TextMeshProUGUI>(true);

        if (moveButtonTextTMP != null)
        {
            moveButtonTextTMP.fontStyle = FontStyles.Bold;
            moveButtonTextTMP.alignment = TextAlignmentOptions.Center;
        }

        if (MoveModeController.IsMoveModeActive && MoveModeController.Ins != null)
        {
            SetMoveButtonLabel(MoveModeController.Ins.HasPreviewDestination
                ? "XÁC NHẬN"
                : MoveModeController.Ins.HasSelectedTroopSlot ? "CHỌN ĐIỂM ĐẾN" : "CHỌN Ô LÍNH");
        }
        else
        {
            SetMoveButtonLabel("ĐIỀU QUÂN");
        }
    }

    public void OnClickMove()
    {
        if (MoveModeController.IsMoveModeActive && MoveModeController.Ins != null)
        {
            if (MoveModeController.Ins.HasPreviewDestination)
            {
                MoveModeController.Ins.ApplySelectedDestination();
            }
            else
            {
                MoveModeController.Ins.CancelMoveMode();
            }
            return;
        }

        SettlementZone currentZone = SettlementManager.Ins != null ? SettlementManager.Ins.CurrentSettlement : null;
        if (currentZone == null) currentZone = Object.FindFirstObjectByType<SettlementZone>();
        if (currentZone == null) return;

        if (!currentZone.IsConquered)
        {
            if (moveButton != null) moveButton.gameObject.SetActive(false);
            return;
        }

        if (MoveModeController.Ins == null)
        {
            GameObject managerObject = new GameObject("MoveModeController");
            managerObject.AddComponent<MoveModeController>();
        }

        MoveModeController.Ins.Configure(
            moveOverviewCameraHeight,
            moveRoutePreviewCameraHeight,
            moveSelectedCameraHeight,
            moveArrowHeightAboveTerrain);
        MoveModeController.Ins.BeginMoveMode(currentZone, soldierPointUIPrefab);
        SetMoveButtonLabel("CHỌN Ô LÍNH");
    }

    /// <summary>
    /// Ẩn toàn bộ SettlementSidePanel khi đang chốt điểm đến. Sau đó panel
    /// được bật lại bằng ShowMoveConfirmationPanel() để bấm XÁC NHẬN.
    /// </summary>
    public void SetMoveDestinationSelectionView(bool hideSettlementInfo)
    {
        if (!hideSettlementInfo)
        {
            RestoreSettlementInfoAfterMove();
            UIManager.Ins?.SetSettlementSidePanelVisible(true);
            return;
        }

        // Ẩn chính object SettlementSidePanel thay vì chỉ ẩn vài child của nó.
        // Nút XÁC NHẬN sẽ được bật lại bởi ShowMoveConfirmationPanel().
        movePanelHidden = true;
        UIManager.Ins?.SetSettlementSidePanelVisible(false);
        if (UIManager.Ins == null && gameObject.activeSelf) gameObject.SetActive(false);
    }

    /// <summary>
    /// Hiện lại SettlementSidePanel nguồn sau khi đã chọn điểm đến để người
    /// chơi bấm nút XÁC NHẬN. SettlementManager.CurrentSettlement vẫn là vùng
    /// nguồn nên panel không bị đổi sang vùng đích.
    /// </summary>
    public void ShowMoveConfirmationPanel()
    {
        RestoreSettlementInfoAfterMove();
        UIManager.Ins?.SetSettlementSidePanelVisible(true);

        if (UIManager.Ins == null && !gameObject.activeSelf) gameObject.SetActive(true);

        ConfigureMoveButton();
        UpdateHeaderVisual();
        RefreshPanel();
        MoveModeController.Ins?.RestoreSelectedTroopSlotVisual();
        SetMoveButtonLabel("XÁC NHẬN");
    }

    /// <summary>
    /// Khôi phục settlement info sau khi hủy/hoàn tất chế độ điều quân.
    /// </summary>
    public void RestoreSettlementInfoAfterMove()
    {
        if (movePanelHidden)
        {
            movePanelHidden = false;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Đóng panel nguồn sau khi đoàn quân đã được điều đi. Khi mở lại,
    /// panel sẽ được RefreshPanel theo settlement hiện tại.
    /// </summary>
    public void HideSettlementPanelAfterMove()
    {
        RestoreSettlementInfoAfterMove();
        SetMoveButtonVisible(false);
        if (UIManager.Ins != null) UIManager.Ins.SetSettlementSidePanelVisible(false);
        else gameObject.SetActive(false);
    }

    /// <summary>
    /// Điều khiển riêng nút Điều quân khi panel bị đóng hoàn toàn.
    /// </summary>
    public void SetMoveButtonVisible(bool visible)
    {
        if (moveButton != null) moveButton.gameObject.SetActive(visible);
    }

    public void SetMoveButtonLabel(string label)
    {
        if (moveButtonTextTMP == null && moveButton != null)
        {
            moveButtonTextTMP = moveButton.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (moveButtonTextTMP != null)
        {
            moveButtonTextTMP.text = string.IsNullOrWhiteSpace(label) ? "ĐIỀU QUÂN" : label;
        }
    }

    private Button FindMoveButton()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button != null && button.name.Equals("Move", System.StringComparison.OrdinalIgnoreCase)) return button;
        }
        return null;
    }

    /// <summary>
    /// Cập nhật thông tin Header Thủ Đô (Tên & Cấp độ / Trạng thái Nhà Chính)
    /// </summary>
    public void UpdateHeaderVisual()
    {
        SettlementZone currentZone = (SettlementManager.Ins != null) ? SettlementManager.Ins.CurrentSettlement : null;

        string currentName = (currentZone != null) ? currentZone.settlementName : defaultSettlementName;
        int currentLevel = (currentZone != null) ? currentZone.SettlementLevel : defaultSettlementLevel;
        bool hasEnemy = (currentZone != null) && currentZone.hasEnemyOutpost;
        bool isTownHallBuilt = (currentZone == null) || currentZone.isTownHallEstablished;

        // Chỉ settlement đã chinh phục mới được phép điều quân.
        if (moveButton != null)
        {
            SetMoveButtonVisible(currentZone == null || currentZone.IsConquered);
        }

        if (settlementNameTMP != null) settlementNameTMP.text = currentName;

        if (settlementLevelTMP != null)
        {
            if (hasEnemy)
            {
                settlementLevelTMP.text = "<color=red>CHINH PHỤC</color>";
            }
            else if (isTownHallBuilt)
            {
                settlementLevelTMP.text = $"Lv. {currentLevel}";
            }
            else
            {
                settlementLevelTMP.text = "<color=orange>CHƯA CHIẾM ĐÓNG</color>";
            }
        }

        if (upgradeBtnTextTMP != null)
        {
            if (hasEnemy)
            {
                upgradeBtnTextTMP.text = "CHIẾM ĐÓNG";
            }
            else if (isTownHallBuilt)
            {
                upgradeBtnTextTMP.text = "Nâng cấp";
            }
            else
            {
                upgradeBtnTextTMP.text = "XÂY NHÀ CHÍNH";
            }
        }

        if (upgradeSettlementBtn != null)
        {
            Image btnImg = upgradeSettlementBtn.GetComponent<Image>();
            if (btnImg != null)
            {
                if (hasEnemy && attackHeaderSprite != null)
                {
                    btnImg.sprite = attackHeaderSprite;
                }
                else if (!isTownHallBuilt && buildHeaderSprite != null)
                {
                    btnImg.sprite = buildHeaderSprite;
                }
                else if (isTownHallBuilt && upgradeHeaderSprite != null)
                {
                    btnImg.sprite = upgradeHeaderSprite;
                }
            }
        }
    }

    /// <summary>
    /// Làm mới toàn bộ lưới các ô Slot công trình theo thời gian thực
    /// </summary>
    public void RefreshPanel()
    {
        if (slotsContainer == null) return;

        UpdateHeaderVisual();

        SettlementZone currentZone = (SettlementManager.Ins != null) ? SettlementManager.Ins.CurrentSettlement : null;
        bool isTownHallBuilt = (currentZone == null) || currentZone.isTownHallEstablished;

        // 1. Thu thập danh sách các ô slot hiện tại dưới slotsContainer
        activeSlotUIItems.Clear();
        activeSlotUIItems.AddRange(slotsContainer.GetComponentsInChildren<SettlementSlotItemUI>(true));

        // Helper lọc bỏ Nhà Chính khỏi danh sách Slot thông thường
        bool IsTownHallBuilding(UpgradeableBuilding ub)
        {
            return SettlementZone.IsTownHallBuilding(ub, currentZone);
        }

        // 2. Lấy danh sách các nhà công trình (TRỪ NHÀ CHÍNH) CHỈ THUỘC VỀ VÙNG ĐẤT NÀY
        List<UpgradeableBuilding> builtStructures = new List<UpgradeableBuilding>();

        if (currentZone != null)
        {
            currentZone.Update3DSlotVisibility();

            // 🔥 Tự động quét và đăng ký lại tất cả công trình thuộc đúng Transform của Vùng đất hiện tại
            var allBuildingsInScene = Object.FindObjectsByType<UpgradeableBuilding>(FindObjectsSortMode.None);
            foreach (var ub in allBuildingsInScene)
            {
                if (ub != null && ub.gameObject.activeSelf && !IsTownHallBuilding(ub))
                {
                    bool belongsToCurrentZone = ub.transform.IsChildOf(currentZone.transform) || (ub.GetComponentInParent<SettlementZone>() == currentZone);
                    if (belongsToCurrentZone)
                    {
                        currentZone.RegisterBuilding(ub);
                    }
                }
            }

            // Lấy duy nhất từ danh sách công trình đã đăng ký chuẩn của Vùng đất hiện tại (loại bỏ công trình ngoại bang)
            if (currentZone.builtStructures != null)
            {
                for (int i = currentZone.builtStructures.Count - 1; i >= 0; i--)
                {
                    var ub = currentZone.builtStructures[i];
                    bool belongs = ub != null && (ub.transform.IsChildOf(currentZone.transform) || ub.GetComponentInParent<SettlementZone>() == currentZone);

                    if (ub == null || !ub.gameObject.activeSelf || !belongs)
                    {
                        currentZone.builtStructures.RemoveAt(i);
                        continue;
                    }

                    if (!IsTownHallBuilding(ub) && !builtStructures.Contains(ub))
                    {
                        builtStructures.Add(ub);
                    }
                }
            }
        }
        else
        {
            var allInScene = FindObjectsByType<UpgradeableBuilding>(FindObjectsSortMode.None);
            foreach (var ub in allInScene)
            {
                if (ub != null && ub.gameObject.activeSelf && !IsTownHallBuilding(ub) && !builtStructures.Contains(ub)) builtStructures.Add(ub);
            }
        }

        if (currentZone != null) currentZone.AlignBuildingsToSlotPositions();

        // 3. Hiển thị danh sách các ô Slot trong Panel (ĐÚNG BẰNG SỐ SLOT ĐĂNG KÝ TRONG INSPECTOR)
        int occupiedCount = builtStructures.Count;
        int registeredSlotCount = (currentZone != null && currentZone.slotPoints.Count > 0) ? currentZone.slotPoints.Count : totalSlotsCount;
        int unlockedCount = (currentZone != null) ? currentZone.GetUnlockedSlotCount() : registeredSlotCount;
        int totalCount = registeredSlotCount;

        for (int i = 0; i < totalCount; i++)
        {
            SettlementSlotItemUI slotUI = GetOrCreateSlotUI(i);
            if (slotUI == null) continue;

            slotUI.gameObject.SetActive(true);

            // Nếu vùng đất CHƯA CÓ NHÀ CHÍNH, khóa toàn bộ các ô slot con bên dưới!
            if (!isTownHallBuilt)
            {
                slotUI.SetLockedSlot();
                continue;
            }

            Vector3 slotPos = (currentZone != null) 
                ? currentZone.GetSlotWorldPosition(i) 
                : ((BuildingSystem.Ins != null) ? BuildingSystem.Ins.SelectedSlotPos : Vector3.zero);

            // Tìm công trình chuẩn thuộc về ô Slot [i] của Vùng đất này
            UpgradeableBuilding buildingAtSlot = (currentZone != null) ? currentZone.GetBuildingAtSlot(i) : null;
            if (buildingAtSlot == null)
            {
                foreach (var b in builtStructures)
                {
                    if (b != null && b.gameObject.activeSelf && (b.slotIndex == i || Vector3.Distance(b.transform.position, slotPos) < 3.5f))
                    {
                        buildingAtSlot = b;
                        b.slotIndex = i;
                        break;
                    }
                }
            }

            if (buildingAtSlot != null)
            {
                // Ô ĐÃ CÓ NHÀ TẠI VỊ TRÍ SLOT 3D [i]
                slotUI.SetOccupiedSlot(buildingAtSlot);
            }
            else if (i < unlockedCount)
            {
                // Ô TRỐNG MỞ KHÓA TẠI VỊ TRÍ SLOT 3D [i]
                slotUI.SetEmptySlot(slotPos);
            }
            else
            {
                // Ô BỊ KHÓA 🔒 (Cần nâng cấp Cấp độ Vùng Đất / Nhà Chính)
                slotUI.SetLockedSlot();
            }
        }

        // Ẩn các slot dư thừa ngoài số lượng đăng ký
        for (int i = totalCount; i < activeSlotUIItems.Count; i++)
        {
            if (activeSlotUIItems[i] != null) activeSlotUIItems[i].gameObject.SetActive(false);
        }

        // Cập nhật hiển thị 8 Ô Huấn Luyện Lính
        RefreshTroopTrainingSlots();
    }

    /// <summary>
    /// Làm mới hiển thị cho khu vực 8 Ô Huấn Luyện Lính
    /// </summary>
    public void RefreshTroopTrainingSlots()
    {
        if (troopTrainingContainer == null) return;

        SynchronizeTroopTrainingSlotAppearance();

        SettlementZone currentZone = (SettlementManager.Ins != null) ? SettlementManager.Ins.CurrentSettlement : null;

        // Tự động bảo đảm singleton TroopTrainingManager tồn tại
        if (TroopTrainingManager.Ins == null)
        {
            GameObject managerObj = new GameObject("TroopTrainingManager");
            managerObj.AddComponent<TroopTrainingManager>();
        }

        if (TroopTrainingManager.Ins == null) return;

        if (currentZone == null) currentZone = TroopTrainingManager.Ins.CentralSettlement;
        if (currentZone == null) return;

        // Thành đầu tiên dùng khung này để huấn luyện; thành khác dùng để xem
        // quân đồn trú vừa được điều đến.
        troopTrainingContainer.gameObject.SetActive(true);
        Transform parent = troopTrainingContainer.parent;
        if (parent != null)
        {
            Transform titleBackground = parent.Find("BottomHeaderGroup");
            if (titleBackground != null) titleBackground.gameObject.SetActive(true);

            Transform titleText = parent.Find("SectionTitle_TMP");
            if (titleText != null)
            {
                titleText.gameObject.SetActive(true);
                TextMeshProUGUI titleTMP = titleText.GetComponent<TextMeshProUGUI>();
                if (titleTMP != null && currentZone != null && TroopTrainingManager.Ins != null)
                {
                    titleTMP.text = TroopTrainingManager.Ins.IsCentralTrainingSettlement(currentZone)
                        ? "HUẤN LUYỆN LÍNH"
                        : "QUÂN ĐỒN TRÚ";
                }
            }
        }

        TroopTrainingSlotData[] slots = TroopTrainingManager.Ins.GetSlotsForZone(currentZone);
        if (slots == null) return;

        for (int i = 0; i < TroopTrainingManager.MAX_TRAINING_SLOTS; i++)
        {
            TroopTrainingSlotUI slotUI = GetOrCreateTrainingSlotUI(i);
            if (slotUI == null) continue;

            slotUI.gameObject.SetActive(true);
            slotUI.SetData(slots[i], currentZone);
        }

        for (int i = TroopTrainingManager.MAX_TRAINING_SLOTS; i < activeTrainingSlotUIItems.Count; i++)
        {
            if (activeTrainingSlotUIItems[i] != null)
            {
                activeTrainingSlotUIItems[i].gameObject.SetActive(false);
            }
        }
    }

    [ContextMenu("Đồng bộ giao diện ô huấn luyện từ ô đầu tiên")]
    private void SynchronizeTroopTrainingSlotAppearance()
    {
        if (troopTrainingContainer == null || troopTrainingContainer.childCount < 2) return;

        TroopTrainingSlotUI template = troopTrainingContainer.GetChild(0).GetComponent<TroopTrainingSlotUI>();
        if (template == null)
        {
            template = troopTrainingContainer.GetChild(0).gameObject.AddComponent<TroopTrainingSlotUI>();
        }

        int slotCount = Mathf.Min(TroopTrainingManager.MAX_TRAINING_SLOTS, troopTrainingContainer.childCount);
        for (int i = 1; i < slotCount; i++)
        {
            Transform child = troopTrainingContainer.GetChild(i);
            TroopTrainingSlotUI slot = child.GetComponent<TroopTrainingSlotUI>();
            if (slot == null) slot = child.gameObject.AddComponent<TroopTrainingSlotUI>();

            slot.CopyVisualStyleFrom(template);
        }
    }

    private TroopTrainingSlotUI GetOrCreateTrainingSlotUI(int index)
    {
        if (index >= 0 && index < activeTrainingSlotUIItems.Count && activeTrainingSlotUIItems[index] != null)
        {
            return activeTrainingSlotUIItems[index];
        }

        if (troopTrainingContainer == null) return null;

        // 1. Nếu đã có các child GameObject sẵn trong Hierarchy (TroopTrainingSlotItem_01 (0..7))
        if (index < troopTrainingContainer.childCount)
        {
            Transform child = troopTrainingContainer.GetChild(index);
            if (child != null)
            {
                child.gameObject.SetActive(true);
                TroopTrainingSlotUI itemUI = child.GetComponent<TroopTrainingSlotUI>();
                if (itemUI == null) itemUI = child.gameObject.AddComponent<TroopTrainingSlotUI>();

                if (!activeTrainingSlotUIItems.Contains(itemUI))
                {
                    activeTrainingSlotUIItems.Add(itemUI);
                }
                return itemUI;
            }
        }

        // 2. Nếu thiếu child GameObject và có Prefab gán sẵn
        if (troopTrainingSlotPrefab != null)
        {
            GameObject obj = Instantiate(troopTrainingSlotPrefab, troopTrainingContainer);
            TroopTrainingSlotUI itemUI = obj.GetComponent<TroopTrainingSlotUI>();
            if (itemUI == null) itemUI = obj.AddComponent<TroopTrainingSlotUI>();

            if (!activeTrainingSlotUIItems.Contains(itemUI))
            {
                activeTrainingSlotUIItems.Add(itemUI);
            }
            return itemUI;
        }

        return null;
    }

    private SettlementSlotItemUI GetOrCreateSlotUI(int index)
    {
        if (index >= 0 && index < activeSlotUIItems.Count && activeSlotUIItems[index] != null)
        {
            return activeSlotUIItems[index];
        }

        if (slotItemPrefab != null && slotsContainer != null)
        {
            GameObject obj = Instantiate(slotItemPrefab, slotsContainer);
            SettlementSlotItemUI itemUI = obj.GetComponent<SettlementSlotItemUI>();
            if (itemUI != null)
            {
                activeSlotUIItems.Add(itemUI);
                return itemUI;
            }
        }

        return null;
    }

    public void OnClickUpgradeSettlement()
    {
        SettlementZone currentZone = (SettlementManager.Ins != null) ? SettlementManager.Ins.CurrentSettlement : null;
        if (currentZone == null)
        {
            currentZone = Object.FindFirstObjectByType<SettlementZone>();
        }

        if (currentZone == null)
        {
            Debug.LogWarning("[SettlementSidePanelUI] ⚠️ Không tìm thấy SettlementZone nào trong Scene!");
            return;
        }

        if (currentZone.hasEnemyOutpost)
        {
            if (UIManager.Ins != null) UIManager.Ins.ShowWarning("Vùng đất đang bị Kẻ Địch chiếm đóng! Hãy tiêu diệt Căn cứ Địch trước!");
            return;
        }

        // Đảm bảo Nhà Chính được khởi tạo nếu đã đánh dấu isTownHallEstablished
        currentZone.EnsureTownHallInstantiated();

        UpgradeableBuilding townHall = currentZone.TownHallBuilding;
        if (townHall == null)
        {
            townHall = currentZone.GetComponentInChildren<UpgradeableBuilding>();
        }
        if (townHall == null)
        {
            townHall = Object.FindFirstObjectByType<UpgradeableBuilding>();
        }

        if (!currentZone.isTownHallEstablished || townHall == null)
        {
            // MỞ BẢNG XÂY DỰNG NHÀ CHÍNH TRÊN UPGRADE SIDE PANEL CHO VÙNG ĐẤT NÀY
            if (UIManager.Ins != null)
            {
                UIManager.Ins.OpenEstablishTownHallPanel(currentZone);
            }
            else if (BuildingUpgradeSidePanelUI.Ins != null)
            {
                BuildingUpgradeSidePanelUI.Ins.ShowEstablishTownHallPanel(currentZone);
            }
            return;
        }

        Debug.Log($"[SettlementSidePanelUI] Nhấn nút Nâng cấp Thủ Đô {currentZone.settlementName} -> Mở Bảng Nâng Cấp cho Nhà Chính.");

        if (UIManager.Ins != null)
        {
            UIManager.Ins.ShowUpgradePanel(townHall);
        }
        else if (BuildingUpgradeSidePanelUI.Ins != null)
        {
            BuildingUpgradeSidePanelUI.Ins.ShowUpgradePanel(townHall);
        }
    }
}
