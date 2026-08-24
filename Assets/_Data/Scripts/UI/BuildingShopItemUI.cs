using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*
 * BuildingShopItemUI.cs
 * Folder: Scripts/UI/
 * Dự án: KHẨN HOANG (PENTA DEV)
 * Phong cách: Demacia Rising Building Item List Card (Cột Bên Trái)
 */

public class BuildingShopItemUI : MonoBehaviour
{
    [Header("=== CẤU HÌNH CÔNG TRÌNH ===")]
    public BuildingType buildingType;
    public string buildingName = "Xưởng Gỗ";
    public string benefitText = "15 Gỗ mỗi lượt";
    [TextArea(2, 4)]
    public string buildingDescription = "Công trình khai thác tài nguyên";
    public Sprite artworkSprite;
    public int buildDuration = 1;

    [Header("=== THÀNH PHẦN UI CỘT TRÁI ===")]
    [SerializeField] private TextMeshProUGUI nameTMP;
    [SerializeField] private GameObject selectionHighlightObj; // Nền kem phát sáng khi được chọn
    [SerializeField] private GameObject lockOverlayObj;        // Icon ổ khóa 🔒 hoặc mảng phủ mờ khi chưa mở khóa

    public bool IsUnlocked { get; private set; } = true;

    /// <summary>
    /// Đặt trạng thái Mở khóa / Khóa cho thẻ công trình trong Shop
    /// </summary>
    public void SetItemUnlockedState(bool isUnlocked)
    {
        IsUnlocked = isUnlocked;

        if (lockOverlayObj != null && lockOverlayObj != gameObject)
        {
            lockOverlayObj.SetActive(!isUnlocked);
        }

        Button b = btn != null ? btn : GetComponent<Button>();
        if (b == null) b = GetComponentInChildren<Button>();
        if (b != null)
        {
            b.interactable = isUnlocked;
        }
    }

    private Button btn;

    private void Awake()
    {
        EnsureButtonListener();
    }

    private void OnEnable()
    {
        EnsureButtonListener();
    }

    public void EnsureButtonListener()
    {
        if (btn == null) btn = GetComponent<Button>();
        if (btn == null) btn = GetComponentInChildren<Button>(true);

        if (btn != null)
        {
            btn.onClick.RemoveListener(OnClickItem);
            btn.onClick.AddListener(OnClickItem);
        }
    }

    private void Start()
    {
        RefreshItemName();
    }

    public void RefreshItemName()
    {
        if (nameTMP != null && !string.IsNullOrEmpty(buildingName))
        {
            nameTMP.text = buildingName;
        }
    }

    /// <summary>
    /// Đổi trạng thái hiển thị Nền Kem được chọn
    /// </summary>
    public void SetSelected(bool isSelected)
    {
        // 🔥 Bảo vệ: Tuyệt đối không bao giờ SetActive(false) nếu selectionHighlightObj bị gán nhầm vào chính GameObject nút!
        if (selectionHighlightObj != null && selectionHighlightObj != gameObject)
        {
            selectionHighlightObj.SetActive(isSelected);
        }
    }

    /// <summary>
    /// Khi nhấp vào mục công trình bên cột trái
    /// </summary>
    public void OnClickItem()
    {
        OnClickFromShop();
    }

    public void OnClickFromShop()
    {
        if (IsUnlocked && BuildingShopUI.Ins != null)
        {
            BuildingShopUI.Ins.SelectBuildingItem(this);
        }
    }
}
