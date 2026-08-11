using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*
 * SettingsTabController.cs
 * Thư mục: Assets/ThanhVien/NhatTien/Script/UI/
 * Tác giả: Nhật Tiến
 *
 * CHỨC NĂNG:
 * Quản lý 2 Tab nút để chuyển đổi giữa:
 *   - Tab 1: Canvas Âm thanh & Màn hình (Canvas hiện tại)
 *   - Tab 2: Canvas Đồ họa (Canvas (1))
 *
 * CÁCH DÙNG:
 * 1. Gắn script này vào Setting_UI (hoặc bất kỳ GameObject cha nào chứa cả 2 Canvas).
 * 2. Kéo Canvas gốc (âm thanh) vào slot "audioCanvas".
 * 3. Kéo Canvas (1) (đồ họa) vào slot "graphicsCanvas".
 * 4. Kéo 2 nút Tab vào slot "tabAudioBtn" và "tabGraphicsBtn".
 */

public class SettingsTabController : MonoBehaviour
{
    [Header("NỘI DUNG 2 TAB (KHÔNG phải toàn bộ Canvas)")]
    [Tooltip("Container chứa AudioPanel + ScreenPanel bên trong Canvas chính")]
    public GameObject audioContent;   // Ví dụ: một GameObject gộp AudioPanel + ScreenPanel

    [Tooltip("Canvas (1) hoặc container nội dung Đồ họa")]
    public GameObject graphicsContent; // Canvas (1) hoặc panel bên trong nó

    [Header("2 Nút Tab")]
    public Button tabAudioBtn;
    public Button tabGraphicsBtn;

    [Header("Màu Tab Active / Inactive")]
    public Color activeTabColor   = new Color(0.85f, 0.65f, 0.20f);   // Vàng đồng — đang chọn
    public Color inactiveTabColor = new Color(0.30f, 0.20f, 0.10f);   // Nâu tối — không chọn
    public Color activeTextColor   = new Color(1f,    0.95f, 0.80f);   // Kem sáng
    public Color inactiveTextColor = new Color(0.55f, 0.40f, 0.25f);  // Nâu mờ

    private int _currentTab = 0; // 0 = Âm Thanh, 1 = Đồ Họa

    void Awake()
    {
        // Đăng ký 1 lần duy nhất trong Awake — không bị mất dù panel đóng/mở lại
        if (tabAudioBtn    != null) tabAudioBtn.onClick.AddListener(() => ShowTab(0));
        if (tabGraphicsBtn != null) tabGraphicsBtn.onClick.AddListener(() => ShowTab(1));
    }

    void OnEnable()
    {
        // Mỗi lần mở panel → reset về Tab Âm Thanh
        ShowTab(0);
    }

    /// <summary>
    /// Chuyển sang Tab theo index: 0 = Âm Thanh, 1 = Đồ Họa
    /// </summary>
    public void ShowTab(int tabIndex)
    {
        _currentTab = tabIndex;

        bool showAudio    = (tabIndex == 0);
        bool showGraphics = (tabIndex == 1);

        // Bật / Tắt NỘI DUNG (không tắt toàn bộ Canvas để tránh mất nút Tab)
        if (audioContent    != null) audioContent.SetActive(showAudio);
        if (graphicsContent != null) graphicsContent.SetActive(showGraphics);

        // Cập nhật màu nút Tab để phản hồi Tab nào đang chọn
        SetTabStyle(tabAudioBtn,    showAudio);
        SetTabStyle(tabGraphicsBtn, showGraphics);

        // Phát tiếng UI chuyển tab
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayButtonClick();
    }

    void SetTabStyle(Button btn, bool isActive)
    {
        if (btn == null) return;

        // Đổi màu Image nền của nút
        Image img = btn.GetComponent<Image>();
        if (img != null)
            img.color = isActive ? activeTabColor : inactiveTabColor;

        // Đổi màu chữ
        TMP_Text label = btn.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.color = isActive ? activeTextColor : inactiveTextColor;

        // Scale nhẹ nút đang active
        btn.transform.localScale = isActive
            ? new Vector3(1.06f, 1.06f, 1f)
            : Vector3.one;
    }

    void OnDestroy()
    {
        // Chỉ dọn khi GameObject bị xóa hẳn, không dọn khi chỉ ẩn panel
        if (tabAudioBtn    != null) tabAudioBtn.onClick.RemoveAllListeners();
        if (tabGraphicsBtn != null) tabGraphicsBtn.onClick.RemoveAllListeners();
    }
}
