using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*
 * BuildingShopUI.cs
 * Folder: Scripts/UI/
 * Dự án: KHẨN HOANG (PENTA DEV)
 * Phong cách: Demacia Rising Two-Column Building Pop-up Shop
 */

public class BuildingShopUI : MonoBehaviour
{
    public static BuildingShopUI Ins { get; private set; }

    [Header("=== CẤU HÌNH HEADER & ĐÓNG cửa SỔ ===")]
    [SerializeField] private Button closeBtn;

    [Header("=== CỘT BÊN TRÁI (DANH SÁCH CÔNG TRÌNH) ===")]
    [SerializeField] private Transform itemListContainer;
    [Tooltip("Kéo thả các Nút công trình thủ công vào đây nếu muốn gán cố định trên Unity Inspector")]
    [SerializeField] private BuildingShopItemUI[] shopItemButtons;
    [SerializeField] private BuildingShopItemUI currentSelectedItem;

    [Header("=== CỘT BÊN PHẢI (XEM TRƯỚC CHI TIẾT & NÚT XÂY) ===")]
    [SerializeField] private Image previewArtImage;
    [SerializeField] private TextMeshProUGUI selectedNameTMP;
    [SerializeField] private TextMeshProUGUI benefitTextTMP;
    [SerializeField] private TextMeshProUGUI descriptionTMP;

    [Header("=== CHI PHÍ TÀI NGUYÊN (TMP) ===")]
    [SerializeField] private TextMeshProUGUI woodCostTMP;
    [SerializeField] private TextMeshProUGUI stoneCostTMP;
    [SerializeField] private TextMeshProUGUI foodCostTMP;

    [Header("=== NÚT XÂY DỰNG & THỜI GIAN ===")]
    [SerializeField] private Button constructBtn;
    [SerializeField] private TextMeshProUGUI buildDurationTMP;

    [Header("=== TÙY CHỈNH MÀU GIÁ ===")]
    [SerializeField] private Color affordableColor = new Color(0.2f, 0.9f, 0.3f, 1f);   // Xanh lá
    [SerializeField] private Color unaffordableColor = new Color(0.9f, 0.2f, 0.2f, 1f); // Đỏ

    private List<BuildingShopItemUI> shopItemsList = new List<BuildingShopItemUI>();

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
        // Food không còn là chi phí xây dựng; ẩn cả text lẫn icon con của mục này.
        if (foodCostTMP != null) foodCostTMP.gameObject.SetActive(false);
    }

    private void Start()
    {
        if (closeBtn != null)
        {
            closeBtn.onClick.RemoveListener(CloseShop);
            closeBtn.onClick.AddListener(CloseShop);
        }

        if (constructBtn != null)
        {
            constructBtn.onClick.RemoveListener(OnClickConstructButton);
            constructBtn.onClick.AddListener(OnClickConstructButton);
        }

        RefreshAllItems();
    }

    private void OnEnable()
    {
        if (BuildTrainingUIManager.Ins == null && BuildingUpgradeSidePanelUI.Ins != null)
        {
            BuildingUpgradeSidePanelUI.Ins.ClosePanel();
        }
        RefreshAllItems();

        if (CampaignTutorialManager.Ins != null)
        {
            CampaignTutorialManager.Ins.OnShopOpened();
        }
    }

    /// <summary>
    /// Thu thập toàn bộ các item bên cột trái và làm mới giao diện
    /// </summary>
    public void RefreshAllItems()
    {
        shopItemsList.Clear();

        // 1. Ưu tiên lấy từ mảng shopItemButtons nếu người chơi gán trực tiếp trên Inspector
        if (shopItemButtons != null && shopItemButtons.Length > 0)
        {
            foreach (var item in shopItemButtons)
            {
                if (item != null && !shopItemsList.Contains(item))
                {
                    shopItemsList.Add(item);
                }
            }
        }

        // 2. Nếu chưa gán shopItemButtons, tự động thu thập từ itemListContainer hoặc toàn bộ thẻ con
        if (shopItemsList.Count == 0)
        {
            if (itemListContainer == null)
            {
                BuildingShopItemUI firstItem = GetComponentInChildren<BuildingShopItemUI>(true);
                if (firstItem != null) itemListContainer = firstItem.transform.parent;
                if (itemListContainer == null) itemListContainer = transform.Find("ItemListContainer") ?? transform.Find("Content");
            }

            if (itemListContainer != null)
            {
                itemListContainer.gameObject.SetActive(true);
            }

            BuildingShopItemUI[] allChildItems = GetComponentsInChildren<BuildingShopItemUI>(true);
            if (allChildItems == null || allChildItems.Length == 0)
            {
                allChildItems = Object.FindObjectsByType<BuildingShopItemUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }

            if (allChildItems != null)
            {
                foreach (var item in allChildItems)
                {
                    if (item != null && !shopItemsList.Contains(item))
                    {
                        shopItemsList.Add(item);
                    }
                }
            }
        }

        // Lấy thông tin Vùng đất hiện tại
        SettlementZone currentZone = (SettlementManager.Ins != null) ? SettlementManager.Ins.CurrentSettlement : null;
        if (currentZone == null) currentZone = Object.FindFirstObjectByType<SettlementZone>();

        // 3. Đảm bảo BẬT SetActive(true) cho toàn bộ các nút, cập nhật trạng thái Khóa/Mở Khóa theo Vùng Đất và gắn sự kiện Click
        foreach (var item in shopItemsList)
        {
            if (item != null)
            {
                // Trại Lính chỉ có một bản đặt sẵn ở thành khởi đầu, không được bán trong Shop.
                if (TroopTrainingManager.IsCentralBarracksType(item.buildingType))
                {
                    if (currentSelectedItem == item) currentSelectedItem = null;
                    item.gameObject.SetActive(false);
                    continue;
                }

                item.gameObject.SetActive(true);
                item.transform.localScale = Vector3.one;

                // Kiểm tra xem công trình có được mở khóa trên Vùng Đất Đã Giải Phóng chưa
                bool isUnlockedAtZone = SettlementZone.IsBuildingTypeUnlockedGlobally(item.buildingType);
                item.SetItemUnlockedState(isUnlockedAtZone);

                CanvasGroup cg = item.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = isUnlockedAtZone ? 1f : 0.4f;
                    cg.interactable = isUnlockedAtZone;
                    cg.blocksRaycasts = isUnlockedAtZone;
                }

                item.RefreshItemName();
                item.SetSelected(false);
                item.EnsureButtonListener();

                Button b = item.GetComponent<Button>();
                if (b == null) b = item.GetComponentInChildren<Button>(true);
                if (b != null)
                {
                    b.interactable = isUnlockedAtZone;
                    // Xóa listener cũ do BuildingShopUI từng tự thêm. Item tự quản lý đúng một listener.
                    b.onClick.RemoveListener(item.OnClickFromShop);
                }
            }
        }

        ClearSelectionDetails();
    }

    public void ClearSelectionDetails()
    {
        currentSelectedItem = null;
        if (selectedNameTMP != null) selectedNameTMP.text = "Chọn công trình";
        if (benefitTextTMP != null) benefitTextTMP.text = "";
        if (descriptionTMP != null) descriptionTMP.text = "Hãy chọn một công trình ở danh sách bên trái để xem chi tiết.";

        if (previewArtImage != null)
        {
            previewArtImage.gameObject.SetActive(false);
        }

        if (woodCostTMP != null) woodCostTMP.text = "0";
        if (stoneCostTMP != null) stoneCostTMP.text = "0";
        if (foodCostTMP != null) foodCostTMP.text = "0";

        if (constructBtn != null) constructBtn.interactable = false;
    }

    /// <summary>
    /// Tìm thẻ công trình trong Shop theo BuildingType
    /// </summary>
    public BuildingShopItemUI GetShopItem(BuildingType type)
    {
        if (shopItemsList != null)
        {
            foreach (var item in shopItemsList)
            {
                if (item != null && item.buildingType == type) return item;
            }
        }
        return null;
    }

    /// <summary>
    /// Chọn 1 công trình từ danh sách cột trái và hiển thị chi tiết sang cột phải
    /// </summary>
    public void SelectBuildingItem(BuildingShopItemUI item)
    {
        if (item == null) return;
        if (TroopTrainingManager.IsCentralBarracksType(item.buildingType))
        {
            ClearSelectionDetails();
            return;
        }

        currentSelectedItem = item;

        // 1. Đổi highlight nền kem ở danh sách cột trái
        foreach (var i in shopItemsList)
        {
            if (i != null)
            {
                i.SetSelected(i == currentSelectedItem);
            }
        }

        // 2. Cập nhật thông tin cột bên phải
        if (selectedNameTMP != null) selectedNameTMP.text = item.buildingName;
        if (benefitTextTMP != null) benefitTextTMP.text = item.benefitText;
        if (descriptionTMP != null) descriptionTMP.text = item.buildingDescription;

        if (previewArtImage != null)
        {
            if (item.artworkSprite != null)
            {
                previewArtImage.sprite = item.artworkSprite;
                previewArtImage.gameObject.SetActive(true);
            }
        }

        if (buildDurationTMP != null)
        {
            buildDurationTMP.text = item.buildDuration.ToString();
        }

        // 3. Lấy chi phí và kiểm tra đủ tiền
        RefreshCostAndAffordability(item.buildingType);

        if (CampaignTutorialManager.Ins != null)
        {
            CampaignTutorialManager.Ins.OnShopItemSelected(item.buildingType);
        }
    }

    /// <summary>
    /// Cập nhật chi phí tài nguyên và bật/tắt Nút XÂY DỰNG
    /// </summary>
    private void RefreshCostAndAffordability(BuildingType type)
    {
        if (type == BuildingType.None) return;

        int woodCost = 0, stoneCost = 0, foodCost = 0;
        if (ConstructionManager.Ins != null)
        {
            var costData = ConstructionManager.Ins.GetBuildingCost(type);
            woodCost = costData.woodCost;
            stoneCost = costData.stoneCost;
            foodCost = costData.foodCost;
        }

        bool hasEnoughWood = true, hasEnoughStone = true, hasEnoughFood = true;
        bool canAfford = true;

        if (JsonDataManager.Ins != null)
        {
            hasEnoughWood = JsonDataManager.Ins.wood >= woodCost;
            hasEnoughStone = JsonDataManager.Ins.stone >= stoneCost;
            hasEnoughFood = JsonDataManager.Ins.food >= foodCost;
            canAfford = JsonDataManager.Ins.HasEnoughResources(woodCost, stoneCost, foodCost);
        }

        // Đổi màu chữ giá
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

        // Kiểm tra xem công trình có được mở khóa toàn cục từ Vùng Đất Giải Phóng chưa
        bool isUnlockedAtZone = SettlementZone.IsBuildingTypeUnlockedGlobally(type);

        if (!isUnlockedAtZone && descriptionTMP != null)
        {
            descriptionTMP.text = "<color=red>🔒 Công trình này chưa mở khóa! Hãy đánh bại Kẻ Địch giải phóng vùng đất chứa công trình này.</color>";
        }

        // Cập nhật trạng thái Nút XÂY DỰNG
        if (constructBtn != null)
        {
            constructBtn.interactable = canAfford && isUnlockedAtZone;
        }
    }

    /// <summary>
    /// Khi bấm Nút XÂY DỰNG màu vàng nổi bật
    /// </summary>
    private void OnClickConstructButton()
    {
        if (currentSelectedItem == null || currentSelectedItem.buildingType == BuildingType.None) return;

        if (TroopTrainingManager.IsCentralBarracksType(currentSelectedItem.buildingType))
        {
            UIManager.Ins?.ShowWarning("Trại Lính chỉ có sẵn tại thành đầu tiên và không thể xây thêm!");
            ClearSelectionDetails();
            return;
        }

        if (BuildingSystem.Ins != null)
        {
            BuildingSystem.Ins.StartPlacing(currentSelectedItem.buildingType);
        }

        CloseShop();
    }

    public void CloseShop()
    {
        gameObject.SetActive(false);
        BuildTrainingUIManager.Ins?.NotifyWindowClosed(BuildTrainingUIManager.ManagedWindow.Build);
    }
}
