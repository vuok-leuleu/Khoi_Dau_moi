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

    private bool _initialized = false;

    void Awake()
    {
        Init();
    }

    void OnEnable()
    {
        Init();
        SyncUI();
    }

    void Init()
    {
        if (_initialized) return;
        _initialized = true;

        if (screenModeDropdown != null)
        {
            screenModeDropdown.onValueChanged.RemoveAllListeners();
            screenModeDropdown.onValueChanged.AddListener(OnDropdownChanged);
        }

        if (mouseSpeedSlider != null)
        {
            mouseSpeedSlider.minValue = 0.1f;
            mouseSpeedSlider.maxValue = 10f;
            mouseSpeedSlider.onValueChanged.RemoveAllListeners();
            mouseSpeedSlider.onValueChanged.AddListener(OnMouseSpeedChanged);
        }
    }

    void SyncUI()
    {
        if (SettingManager.Ins == null) return;

        if (screenModeDropdown != null)
        {
            int currentSettingMode = SettingManager.Ins.screenModeIndex;
            int dropdownIndex = SettingModeToDropdown[Mathf.Clamp(currentSettingMode, 0, 2)];
            screenModeDropdown.SetValueWithoutNotify(dropdownIndex);
        }

        if (mouseSpeedSlider != null)
        {
            float currentSpeed = SettingManager.Ins.mouseSpeed;
            mouseSpeedSlider.SetValueWithoutNotify(currentSpeed);
            UpdateMouseSpeedText(currentSpeed);
        }
    }

    void OnDropdownChanged(int dropdownIndex)
    {
        if (SettingManager.Ins == null) return;

        int settingMode = DropdownToSettingMode[dropdownIndex];

        // Nếu chế độ chọn giống hệt chế độ hiện tại thì không làm gì để tránh làm giật/tắt UI
        if (SettingManager.Ins.screenModeIndex == settingMode) return;

        SettingManager.Ins.SetScreenMode(settingMode);
        Debug.Log($"[ScreenSettingsUI] ✅ Đã đổi chế độ màn hình sang Mode [{settingMode}]");
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

    void OnDestroy()
    {
        if (screenModeDropdown != null)
            screenModeDropdown.onValueChanged.RemoveAllListeners();

        if (mouseSpeedSlider != null)
            mouseSpeedSlider.onValueChanged.RemoveAllListeners();
    }
}
