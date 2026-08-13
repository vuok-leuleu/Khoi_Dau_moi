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

    private List<SettlementSlotItemUI> activeSlotUIItems = new List<SettlementSlotItemUI>();
    private List<TroopTrainingSlotUI> activeTrainingSlotUIItems = new List<TroopTrainingSlotUI>();

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

        UpdateHeaderVisual();
        RefreshPanel();
    }

    private void OnEnable()
    {
        if (upgradeSettlementBtn != null)
        {
            upgradeSettlementBtn.interactable = true;
        }
        UpdateHeaderVisual();
        RefreshPanel();
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

        if (settlementNameTMP != null) settlementNameTMP.text = currentName;

        if (settlementLevelTMP != null)
        {
            if (hasEnemy)
            {
                settlementLevelTMP.text = "<color=red>ĐÁNH ĐỊCH CHINH PHỤC</color>";
            }
            else if (isTownHallBuilt)
            {
                settlementLevelTMP.text = $"Lv. {currentLevel}";
            }
            else
            {
                settlementLevelTMP.text = "<color=orange>CHƯA CÓ NHÀ CHÍNH</color>";
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

        // Giữ cho Container chứa 8 Ô Huấn Luyện luôn luôn hiển thị (ACTIVE) trên UI
        troopTrainingContainer.gameObject.SetActive(true);

        SettlementZone currentZone = (SettlementManager.Ins != null) ? SettlementManager.Ins.CurrentSettlement : null;
        if (currentZone == null) currentZone = Object.FindFirstObjectByType<SettlementZone>();

        // Tự động bảo đảm singleton TroopTrainingManager tồn tại
        if (TroopTrainingManager.Ins == null)
        {
            GameObject managerObj = new GameObject("TroopTrainingManager");
            managerObj.AddComponent<TroopTrainingManager>();
        }

        if (currentZone == null || TroopTrainingManager.Ins == null) return;

        TroopTrainingSlotData[] slots = TroopTrainingManager.Ins.GetSlotsForZone(currentZone);

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
