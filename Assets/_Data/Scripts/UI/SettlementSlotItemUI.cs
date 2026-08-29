using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/*
 * SettlementSlotItemUI.cs
 * Folder: Scripts/UI/
 * Dự án: KHẨN HOANG (PENTA DEV)
 * Phong cách: Demacia Rising Settlement Building Slot
 */

public enum SettlementSlotState
{
    Empty,      // Ô TRỐNG (Có thể bấm vào để chọn nhà xây)
    Occupied,   // ĐÃ CÓ NHÀ (Hiển thị Tên, Icon & Cấp độ)
    Locked      // BỊ KHÓA 🔒
}

public class SettlementSlotItemUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("=== CẤU HÌNH TRẠNG THÁI ===")]
    public SettlementSlotState state = SettlementSlotState.Empty;

    [Header("=== THÀNH PHẦN UI (TMP & IMAGES) ===")]
    [SerializeField] private TextMeshProUGUI titleTMP;
    [SerializeField] private TextMeshProUGUI levelTMP;
    [SerializeField] private Image iconImage;

    [Header("=== HIỂN THỊ ĐƠN VỊ LÍNH ===")]
    [SerializeField] private GameObject troopInfoRoot;
    [SerializeField] private Image troopIconImage;
    [SerializeField] private TextMeshProUGUI troopCountTMP;

    [Header("=== CÁC OBJECT HIỂN THỊ THEO TRẠNG THÁI ===")]
    [SerializeField] private GameObject emptyStateObj;     // Thể hiện ô TRỐNG (Viền sáng + Chữ TRỐNG)
    [SerializeField] private GameObject occupiedStateObj;  // Thể hiện ô ĐÃ CÓ NHÀ
    [SerializeField] private GameObject lockedStateObj;    // Thể hiện ô KHÓA 🔒

    private Vector3 plotWorldPos;
    private UpgradeableBuilding buildingOnSlot;
    private Button slotButton;
    private SlotHoverFrame hoverFrame;


    public void SetTroopInfo(Sprite troopSprite, int unitCount)
    {
        bool show = troopSprite != null && unitCount > 0;
        if (troopInfoRoot != null) troopInfoRoot.SetActive(show);
        if (troopIconImage != null)
        {
            troopIconImage.sprite = troopSprite;
            troopIconImage.gameObject.SetActive(show);
        }
        if (troopCountTMP != null) troopCountTMP.text = show ? $"x{unitCount} đơn vị\\n({unitCount * 3} lính)" : "";
    }
    private void Awake()
    {
        SetupButtonListeners();
        SetupHoverFrame();
    }

    private void OnEnable()
    {
        SetupButtonListeners();
        SetupHoverFrame();
    }

    private void OnDisable()
    {
        hoverFrame?.SetSelected(false);
    }

    private void SetupHoverFrame()
    {
        if (hoverFrame != null) return;

        SlotHoverFrameSettings settings = GetComponentInParent<SlotHoverFrameSettings>();
        if (settings == null || settings.HoverFrameSprite == null) return;

        RectTransform slotTransform = transform as RectTransform;
        if (slotTransform == null) return;

        hoverFrame = new SlotHoverFrame(slotTransform, settings);
    }

    private void SetupButtonListeners()
    {
        if (slotButton == null)
        {
            slotButton = GetComponent<Button>();
            if (slotButton == null) slotButton = GetComponentInChildren<Button>(true);
        }

        if (slotButton == null) return;

        // Không thêm listener thứ hai nếu prefab đã gán OnClickSlot trong Inspector.
        slotButton.onClick.RemoveListener(OnClickSlot);
        if (!HasPersistentClickListener(slotButton))
        {
            slotButton.onClick.AddListener(OnClickSlot);
        }
    }

    private bool HasPersistentClickListener(Button button)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) == this &&
                button.onClick.GetPersistentMethodName(i) == nameof(OnClickSlot))
            {
                return true;
            }
        }

        return false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Button trên cùng GameObject đã phát onClick, không xử lý lại pointer event lần hai.
        if (slotButton != null && slotButton.gameObject == gameObject) return;

        Debug.Log($"[SettlementSlotItemUI] OnPointerClick trigger trên {gameObject.name}");
        OnClickSlot();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hoverFrame?.Show();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hoverFrame?.Hide();
    }

    /// <summary>
    /// Thiết lập ô TRỐNG cho vị trí đất tương ứng
    /// </summary>
    public void SetEmptySlot(Vector3 worldPos)
    {
        plotWorldPos = worldPos;
        buildingOnSlot = null;
        state = SettlementSlotState.Empty;

        if (emptyStateObj != null) emptyStateObj.SetActive(true);
        if (occupiedStateObj != null) occupiedStateObj.SetActive(false);
        if (lockedStateObj != null) lockedStateObj.SetActive(false);

        if (titleTMP != null) titleTMP.text = "TRỐNG";
        if (levelTMP != null) levelTMP.text = "";
    }

    /// <summary>
    /// Thiết lập ô ĐÃ CÓ CÔNG TRÌNH
    /// </summary>
    public void SetOccupiedSlot(UpgradeableBuilding building)
    {
        buildingOnSlot = building;
        state = SettlementSlotState.Occupied;

        if (building != null)
        {
            plotWorldPos = building.transform.position;
        }

        if (emptyStateObj != null) emptyStateObj.SetActive(false);
        if (occupiedStateObj != null) occupiedStateObj.SetActive(true);
        if (lockedStateObj != null) lockedStateObj.SetActive(false);

        if (building != null)
        {
            int displayLevel = building.CurrentLevel + 1;
            if (titleTMP != null) titleTMP.text = building.buildingName;
            if (levelTMP != null) levelTMP.text = $"Lv. {displayLevel}";

            if (iconImage != null)
            {
                Sprite sp = null;
                if (building.BuildingIcons != null && building.BuildingIcons.Length > 0)
                {
                    int idx = Mathf.Clamp(building.CurrentLevel, 0, building.BuildingIcons.Length - 1);
                    sp = building.BuildingIcons[idx];
                }

                if (sp == null && BuildingShopUI.Ins != null)
                {
                    var shopItem = BuildingShopUI.Ins.GetShopItem(building.buildingType);
                    if (shopItem != null) sp = shopItem.artworkSprite;
                }

                if (sp != null)
                {
                    iconImage.sprite = sp;
                    iconImage.color = Color.white;
                    iconImage.gameObject.SetActive(true);
                }
                else
                {
                    iconImage.gameObject.SetActive(false); // 🔒 Tắt khối vuông trắng khi không có Sprite
                }
            }
        }
    }

    /// <summary>
    /// Thiết lập ô BỊ KHÓA 🔒
    /// </summary>
    public void SetLockedSlot()
    {
        buildingOnSlot = null;
        state = SettlementSlotState.Locked;

        if (emptyStateObj != null) emptyStateObj.SetActive(false);
        if (occupiedStateObj != null) occupiedStateObj.SetActive(false);
        if (lockedStateObj != null) lockedStateObj.SetActive(true);

        if (titleTMP != null) titleTMP.text = "";
        if (levelTMP != null) levelTMP.text = "";
    }

    /// <summary>
    /// Xử lý sự kiện nhấp vào ô Slot trong Panel
    /// </summary>
    public void OnClickSlot()
    {
        Debug.Log($"[SettlementSlotItemUI] Clicked slot item '{gameObject.name}' with state: {state}");

        switch (state)
        {
            case SettlementSlotState.Empty:
                // Ô TRỐNG: Chọn ô đất trên map & Mở Shop xây nhà
                if (BuildingSystem.Ins != null)
                {
                    BuildingSystem.Ins.SelectSlot(plotWorldPos);
                }
                if (UIManager.Ins != null)
                {
                    UIManager.Ins.OpenBuildMenu();
                }
                break;

            case SettlementSlotState.Occupied:
                // Ô ĐÃ CÓ NHÀ: Mở bảng Nâng cấp / Thông tin của nhà đó
                if (buildingOnSlot != null && UIManager.Ins != null)
                {
                    UIManager.Ins.ShowUpgradePanel(buildingOnSlot);
                }
                else
                {
                    Debug.LogWarning($"[SettlementSlotItemUI] buildingOnSlot là NULL hoặc UIManager là NULL!");
                }
                break;

            case SettlementSlotState.Locked:
                // Ô KHÓA: Hiển thị thông báo
                if (UIManager.Ins != null)
                {
                    UIManager.Ins.ShowWarning("Ô đất này chưa được mở khóa! Hãy nâng cấp Thủ Đô.");
                }
                break;
        }
    }
}
