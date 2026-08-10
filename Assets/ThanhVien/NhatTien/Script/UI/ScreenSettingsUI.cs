using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*
 * ScreenSettingsUI.cs
 * Thư mục: Assets/ThanhVien/NhatTien/Script/UI/
 * Tác giả: Nhật Tiến
 *
 * CHỨC NĂNG:
 * - Kết nối Dropdown chế độ màn hình với SettingManager.
 * - Kết nối Slider độ nhạy chuột với SettingManager.
 *
 * CÁCH DÙNG:
 * 1. Gắn script này vào GameObject ScreenPanel (hoặc bất kỳ GO nào trong Setting_UI).
 * 2. Kéo Dropdown và Slider MouseSpeed vào đúng slot trong Inspector.
 * 3. Không cần làm gì thêm - script tự đồng bộ khi mở panel.
 *
 * MAP THỨ TỰ DROPDOWN HIỆN TẠI:
 *   Index 0 = "1920x1080"  → SettingManager.SetScreenMode(2)
 *   Index 1 = "FullScreen" → SettingManager.SetScreenMode(1)
 *   Index 2 = "Window"     → SettingManager.SetScreenMode(0)
 */
public class ScreenSettingsUI : MonoBehaviour
{
    [Header("Màn Hình")]
    [Tooltip("Kéo TMP_Dropdown từ ScreenPanel > Screen > Dropdown vào đây")]
    public TMP_Dropdown screenModeDropdown;

    [Header("Độ Nhạy Chuột")]
    [Tooltip("Kéo Slider MouseSpeed vào đây")]
    public Slider mouseSpeedSlider;

    [Tooltip("Text hiển thị giá trị độ nhạy (tùy chọn, để trống nếu không có)")]
    public TMP_Text mouseSpeedValueText;

    // Map: Dropdown index → SettingManager screen mode index
    // Dropdown: 0=1920x1080, 1=FullScreen, 2=Window
    // SettingManager: 0=Window, 1=FullScreen, 2=1920x1080
    private static readonly int[] DropdownToSettingMode = { 2, 1, 0 };
    private static readonly int[] SettingModeToDropdown = { 2, 1, 0 };

    void OnEnable()
    {
        SetupDropdown();
        SetupMouseSpeedSlider();
    }

    void Start()
    {
        SetupDropdown();
        SetupMouseSpeedSlider();
    }

    // ──────────────────────────────────────────────
    // DROPDOWN MÀN HÌNH
    // ──────────────────────────────────────────────

    void SetupDropdown()
    {
        if (screenModeDropdown == null)
        {
            Debug.LogWarning("[ScreenSettingsUI] Chưa gán Dropdown trong Inspector!");
            return;
        }

        if (SettingManager.Ins == null) return;

        // Xóa listener cũ để tránh đăng ký trùng lặp
        screenModeDropdown.onValueChanged.RemoveListener(OnDropdownChanged);

        // Đồng bộ giá trị hiện tại từ SettingManager lên Dropdown
        int currentSettingMode = SettingManager.Ins.screenModeIndex;
        int dropdownIndex = SettingModeToDropdown[currentSettingMode];
        screenModeDropdown.SetValueWithoutNotify(dropdownIndex);

        // Đăng ký sự kiện thay đổi
        screenModeDropdown.onValueChanged.AddListener(OnDropdownChanged);
    }

    void OnDropdownChanged(int dropdownIndex)
    {
        if (SettingManager.Ins == null) return;

        // Map dropdown index → SettingManager mode
        int settingMode = DropdownToSettingMode[dropdownIndex];
        SettingManager.Ins.SetScreenMode(settingMode);

        Debug.Log($"[ScreenSettingsUI] Đổi chế độ màn hình: Dropdown[{dropdownIndex}] → SettingMode[{settingMode}]");
    }

    // ──────────────────────────────────────────────
    // SLIDER ĐỘ NHẠY CHUỘT
    // ──────────────────────────────────────────────

    void SetupMouseSpeedSlider()
    {
        if (mouseSpeedSlider == null)
        {
            Debug.LogWarning("[ScreenSettingsUI] Chưa gán Slider MouseSpeed trong Inspector!");
            return;
        }

        if (SettingManager.Ins == null) return;

        // Đặt range hợp lý cho slider chuột
        mouseSpeedSlider.minValue = 0.1f;
        mouseSpeedSlider.maxValue = 10f;

        // Xóa listener cũ
        mouseSpeedSlider.onValueChanged.RemoveListener(OnMouseSpeedChanged);

        // Đồng bộ giá trị hiện tại
        float currentSpeed = SettingManager.Ins.mouseSpeed;
        mouseSpeedSlider.SetValueWithoutNotify(currentSpeed);
        UpdateMouseSpeedText(currentSpeed);

        // Đăng ký sự kiện
        mouseSpeedSlider.onValueChanged.AddListener(OnMouseSpeedChanged);
    }

    void OnMouseSpeedChanged(float value)
    {
        if (SettingManager.Ins == null) return;

        SettingManager.Ins.SetMouseSpeed(value);
        UpdateMouseSpeedText(value);

        Debug.Log($"[ScreenSettingsUI] Độ nhạy chuột: {value:F1}");
    }

    void UpdateMouseSpeedText(float value)
    {
        if (mouseSpeedValueText != null)
            mouseSpeedValueText.text = value.ToString("F1");
    }

    void OnDisable()
    {
        // Dọn listener khi panel bị ẩn để tránh memory leak
        if (screenModeDropdown != null)
            screenModeDropdown.onValueChanged.RemoveListener(OnDropdownChanged);

        if (mouseSpeedSlider != null)
            mouseSpeedSlider.onValueChanged.RemoveListener(OnMouseSpeedChanged);
    }
}
