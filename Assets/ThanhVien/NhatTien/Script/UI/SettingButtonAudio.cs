using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*
 * SettingButtonAudio.cs
 * Thư mục: Assets/ThanhVien/NhatTien/Script/UI/
 * Tác giả: Nhật Tiến
 *
 * CHỨC NĂNG:
 * Tự động thêm âm thanh click vào tất cả các nút Button, Toggle, Dropdown trong bảng Setting.
 * - An toàn 100%: Dùng AddListener, KHÔNG xóa hay can thiệp vào logic của các script khác.
 * - Tiện lợi: Chỉ cần gắn 1 script này vào Panel cha (Setting_UI hoặc SettingMenu_UI hoặc SettingsPanel), 
 *   toàn bộ nút con (Tiếp tục, Menu, Thoát, Lưu, Tải, Nút X, 2 Tab, Checkbox...) đều sẽ tự động có âm thanh click!
 * - Hỗ trợ tùy chỉnh: Có thể kéo thả file AudioClip riêng trong Inspector (nếu muốn). Nếu không kéo, script tự dùng âm thanh chuẩn của AudioManager.
 */

public class SettingButtonAudio : MonoBehaviour
{
    [Header("Âm thanh tùy chọn (Để trống sẽ dùng âm thanh mặc định)")]
    [Tooltip("File âm thanh khi nhấn nút. Nếu để trống, AudioManager sẽ tự động phát tiếng click chuẩn.")]
    public AudioClip customClickSound;

    [Header("Tùy chọn tự động")]
    [Tooltip("Tự động quét và gắn âm thanh cho tất cả Button, Toggle, Dropdown con bên dưới GameObject này")]
    public bool autoBindChildren = true;

    [Tooltip("Gắn âm thanh cho cả các Toggle/Checkbox (Đồng bộ dọc, Bóng...)")]
    public bool includeToggles = true;

    [Tooltip("Gắn âm thanh cho cả Dropdown khi mở/chọn")]
    public bool includeDropdowns = true;

    // Lưu các controls đã được đăng ký để tránh trùng lặp âm thanh
    private readonly HashSet<Button> _boundButtons = new HashSet<Button>();
    private readonly HashSet<Toggle> _boundToggles = new HashSet<Toggle>();
    private readonly HashSet<TMP_Dropdown> _boundDropdowns = new HashSet<TMP_Dropdown>();

    void Awake()
    {
        BindAllAudio();
    }

    void OnEnable()
    {
        BindAllAudio();
    }

    /// <summary>
    /// Quét và đăng ký âm thanh click an toàn cho tất cả các UI controls
    /// </summary>
    public void BindAllAudio()
    {
        // 1. Nếu component này gắn trực tiếp trên 1 Button
        Button selfButton = GetComponent<Button>();
        if (selfButton != null)
        {
            RegisterButton(selfButton);
        }

        // 2. Nếu component này gắn trực tiếp trên 1 Toggle
        Toggle selfToggle = GetComponent<Toggle>();
        if (selfToggle != null && includeToggles)
        {
            RegisterToggle(selfToggle);
        }

        // 3. Tự động quét tất cả các con bên dưới
        if (autoBindChildren)
        {
            Button[] childButtons = GetComponentsInChildren<Button>(true);
            foreach (Button btn in childButtons)
            {
                RegisterButton(btn);
            }

            if (includeToggles)
            {
                Toggle[] childToggles = GetComponentsInChildren<Toggle>(true);
                foreach (Toggle tog in childToggles)
                {
                    RegisterToggle(tog);
                }
            }

            if (includeDropdowns)
            {
                TMP_Dropdown[] childDropdowns = GetComponentsInChildren<TMP_Dropdown>(true);
                foreach (TMP_Dropdown dd in childDropdowns)
                {
                    RegisterDropdown(dd);
                }
            }
        }
    }

    private void RegisterButton(Button btn)
    {
        if (btn == null || _boundButtons.Contains(btn)) return;

        // Nếu nút đã có UIButtonEffects tự phát âm thanh thì bỏ qua để không bị phát 2 lần
        UIButtonEffects fx = btn.GetComponent<UIButtonEffects>();
        if (fx != null)
        {
            _boundButtons.Add(btn);
            return;
        }

        _boundButtons.Add(btn);
        btn.onClick.AddListener(PlaySound);
    }

    private void RegisterToggle(Toggle tog)
    {
        if (tog == null || _boundToggles.Contains(tog)) return;

        _boundToggles.Add(tog);
        tog.onValueChanged.AddListener((val) => PlaySound());
    }

    private void RegisterDropdown(TMP_Dropdown dd)
    {
        if (dd == null || _boundDropdowns.Contains(dd)) return;

        _boundDropdowns.Add(dd);
        dd.onValueChanged.AddListener((val) => PlaySound());
    }

    /// <summary>
    /// Phát âm thanh click
    /// </summary>
    public void PlaySound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick(customClickSound);
        }
    }
}
