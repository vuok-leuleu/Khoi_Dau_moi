using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*
 * GraphicsSettingsUI.cs
 * Thư mục: Assets/ThanhVien/NhatTien/Script/UI/
 * Tác giả: Nhật Tiến
 *
 * CHỨC NĂNG:
 * Điều khiển các tùy chỉnh Đồ họa trong Canvas (1):
 *   - Chất lượng đồ họa (Low / Medium / High / Ultra)
 *   - Bật/Tắt bóng đổ (Shadows)
 *   - VSync (Đồng bộ màn hình)
 *   - Anti-Aliasing (Khử răng cưa)
 *
 * CÁCH DÙNG:
 * 1. Gắn script này vào Canvas (1) hoặc bất kỳ GameObject nào trong Canvas (1).
 * 2. Kéo các UI element vào đúng slot trong Inspector.
 */

public class GraphicsSettingsUI : MonoBehaviour
{
    [Header("Chất lượng đồ họa tổng thể")]
    [Tooltip("Dropdown với 4 lựa chọn: Low / Medium / High / Ultra")]
    public TMP_Dropdown qualityDropdown;

    [Header("Bóng đổ (Shadows)")]
    [Tooltip("Toggle bật / tắt bóng đổ")]
    public Toggle shadowsToggle;

    [Header("VSync - Đồng bộ màn hình")]
    [Tooltip("Toggle bật / tắt VSync")]
    public Toggle vSyncToggle;

    [Header("Anti-Aliasing (Khử răng cưa)")]
    [Tooltip("Dropdown: Off / 2x MSAA / 4x MSAA / 8x MSAA")]
    public TMP_Dropdown antiAliasingDropdown;

    private bool _initialized = false;

    void Awake()
    {
        Init();
    }

    void OnEnable()
    {
        Init();
        SyncUIFromSettings();
    }

    void Init()
    {
        if (_initialized) return;
        _initialized = true;

        SetupDropdowns();
        RegisterListeners();
    }

    // ─────────────────────────────────────────────────────────
    // KHỞI TẠO DROPDOWN
    // ─────────────────────────────────────────────────────────

    void SetupDropdowns()
    {
        // Quality Dropdown
        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "Thấp",
                "Trung Bình",
                "Cao",
                "Rất Cao"
            });
        }

        // Anti-Aliasing Dropdown
        if (antiAliasingDropdown != null)
        {
            antiAliasingDropdown.ClearOptions();
            antiAliasingDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "Tắt",
                "2x MSAA",
                "4x MSAA",
                "8x MSAA"
            });
        }
    }

    // ─────────────────────────────────────────────────────────
    // ĐỒNG BỘ GIÁ TRỊ HIỆN TẠI LÊN UI
    // ─────────────────────────────────────────────────────────

    void SyncUIFromSettings()
    {
        // Tải từ PlayerPrefs (mặc định Ultra = 3)
        int savedQuality = PlayerPrefs.GetInt("Settings_GraphicsQuality", 3);
        if (qualityDropdown != null)
        {
            qualityDropdown.SetValueWithoutNotify(savedQuality);
        }

        // Shadows (mặc định Bật = 1)
        bool savedShadows = PlayerPrefs.GetInt("Settings_Shadows", 1) == 1;
        if (shadowsToggle != null)
            shadowsToggle.SetIsOnWithoutNotify(savedShadows);

        // VSync (mặc định Tắt = 0)
        bool savedVSync = PlayerPrefs.GetInt("Settings_VSync", 0) == 1;
        if (vSyncToggle != null)
            vSyncToggle.SetIsOnWithoutNotify(savedVSync);

        // Anti-Aliasing (mặc định 2x = 1)
        int savedAA = PlayerPrefs.GetInt("Settings_AA", 1);
        if (antiAliasingDropdown != null)
            antiAliasingDropdown.SetValueWithoutNotify(savedAA);
    }

    // ─────────────────────────────────────────────────────────
    // ĐĂNG KÝ SỰ KIỆN THAY ĐỔI
    // ─────────────────────────────────────────────────────────

    void RegisterListeners()
    {
        if (qualityDropdown != null)
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);

        if (shadowsToggle != null)
            shadowsToggle.onValueChanged.AddListener(OnShadowsChanged);

        if (vSyncToggle != null)
            vSyncToggle.onValueChanged.AddListener(OnVSyncChanged);

        if (antiAliasingDropdown != null)
            antiAliasingDropdown.onValueChanged.AddListener(OnAntiAliasingChanged);
    }

    // ─────────────────────────────────────────────────────────
    // XỬ LÝ THAY ĐỔI
    // ─────────────────────────────────────────────────────────

    void OnQualityChanged(int dropdownIndex)
    {
        int totalLevels = QualitySettings.names.Length;
        int unityIndex = Mathf.Clamp(Mathf.RoundToInt(dropdownIndex / 3f * (totalLevels - 1)), 0, totalLevels - 1);
        QualitySettings.SetQualityLevel(unityIndex, applyExpensiveChanges: true);

        // Hỗ trợ URP (Universal Render Pipeline) nếu project đang dùng URP
        #if UNITY_PIPELINE_URP || true
        // Điều chỉnh Shadow Distance theo chất lượng
        float[] shadowDistances = { 15f, 40f, 80f, 150f };
        QualitySettings.shadowDistance = shadowDistances[dropdownIndex];
        #endif

        // Lưu lựa chọn
        PlayerPrefs.SetInt("Settings_GraphicsQuality", dropdownIndex);
        PlayerPrefs.Save();

        Debug.Log($"[GraphicsSettings] ✅ Đã đổi & lưu Chất lượng Đồ họa: {QualitySettings.names[unityIndex]}");
    }

    void OnShadowsChanged(bool isOn)
    {
        QualitySettings.shadows = isOn ? ShadowQuality.All : ShadowQuality.Disable;
        
        // Tắt/bật toàn bộ bóng của các đèn chính trong Scene để thấy rõ ngay lập tức
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light l in lights)
        {
            if (l.type == LightType.Directional)
            {
                l.shadows = isOn ? LightShadows.Soft : LightShadows.None;
            }
        }

        // Lưu lựa chọn (BUG FIX: trước đây thiếu đoạn lưu này nên tắt/bật bóng đổ
        // không được nhớ lại khi mở UI lần sau)
        PlayerPrefs.SetInt("Settings_Shadows", isOn ? 1 : 0);
        PlayerPrefs.Save();

        Debug.Log($"[GraphicsSettings] ✅ Bóng đổ (Shadows): {(isOn ? "BẬT" : "TẮT")}");
    }

    void OnVSyncChanged(bool isOn)
    {
        QualitySettings.vSyncCount = isOn ? 1 : 0;
        Application.targetFrameRate = isOn ? -1 : 60;

        // Lưu lựa chọn (BUG FIX: đây là nguyên nhân VSync bật rồi mà mở UI lại bị tắt —
        // trước đây hàm này không hề gọi PlayerPrefs.SetInt/Save cho "Settings_VSync")
        PlayerPrefs.SetInt("Settings_VSync", isOn ? 1 : 0);
        PlayerPrefs.Save();

        Debug.Log($"[GraphicsSettings] ✅ VSync: {(isOn ? "BẬT (FPS khóa theo Hz màn hình)" : "TẮT (FPS 60)")}");
    }

    void OnAntiAliasingChanged(int dropdownIndex)
    {
        int[] aaValues = { 0, 2, 4, 8 };
        QualitySettings.antiAliasing = aaValues[dropdownIndex];

        // Áp dụng thêm cho Main Camera nếu có
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.allowMSAA = (aaValues[dropdownIndex] > 0);
        }

        // Lưu lựa chọn (BUG FIX: đây là nguyên nhân AA luôn quay về 2x MSAA khi mở lại UI —
        // trước đây hàm này không hề gọi PlayerPrefs.SetInt/Save cho "Settings_AA")
        PlayerPrefs.SetInt("Settings_AA", dropdownIndex);
        PlayerPrefs.Save();

        Debug.Log($"[GraphicsSettings] ✅ Khử răng cưa (Anti-Aliasing): {aaValues[dropdownIndex]}x MSAA");
    }

    void OnDestroy()
    {
        if (qualityDropdown      != null) qualityDropdown.onValueChanged.RemoveListener(OnQualityChanged);
        if (shadowsToggle        != null) shadowsToggle.onValueChanged.RemoveListener(OnShadowsChanged);
        if (vSyncToggle          != null) vSyncToggle.onValueChanged.RemoveListener(OnVSyncChanged);
        if (antiAliasingDropdown != null) antiAliasingDropdown.onValueChanged.RemoveListener(OnAntiAliasingChanged);
    }
}