using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/*
 * MuteIconButton.cs
 * Thư mục: Assets/ThanhVien/NhatTien/Script/UI/
 * Tác giả: Nhật Tiến
 *
 * CHỨC NĂNG:
 * Gắn vào Icon Âm Thanh / Nhạc Nền.
 * 1. Khi Click: Bật / Tắt tiếng (Mute / Unmute) qua AudioManager.
 * 2. Hiệu ứng Click/Hover: Nhún phóng to 1.08x khi rê chuột, thu nhỏ 0.95x khi bấm.
 * 3. Đổi màu hoặc làm mờ Icon (Alpha) khi bị Mute để người chơi nhận biết trực quan.
 */

public enum AudioIconType
{
    MasterVolume,
    BGMVolume,
    SFXVolume
}

[RequireComponent(typeof(Button))]
public class MuteIconButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Loại Âm Thanh Mute")]
    public AudioIconType iconType = AudioIconType.MasterVolume;

    [Header("Hiệu ứng Phóng to / Thu nhỏ")]
    public float hoverScale = 1.08f;
    public float clickScale = 0.95f;
    public float animationSpeed = 15f;

    [Header("Hiển thị trạng thái Mute (Tùy chọn)")]
    [Tooltip("Làm mờ icon khi Mute (Mặc định: Alpha = 0.4 khi Mute, 1.0 khi bình thường)")]
    public bool changeAlphaOnMute = true;
    public float mutedAlpha = 0.4f;

    private Vector3 _originalScale;
    private Vector3 _targetScale;
    private Button _button;
    private Image _iconImage;

    void Awake()
    {
        _originalScale = transform.localScale;
        _targetScale = _originalScale;
        _button = GetComponent<Button>();
        _iconImage = GetComponent<Image>();
    }

    void OnEnable()
    {
        transform.localScale = _originalScale;
        _targetScale = _originalScale;
        UpdateMuteVisualState();
    }

    void Start()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnIconClicked);
            _button.onClick.AddListener(OnIconClicked);
        }
        UpdateMuteVisualState();
    }

    void Update()
    {
        // Hiệu ứng scale mượt mà
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.unscaledDeltaTime * animationSpeed);
    }

    void OnIconClicked()
    {
        if (AudioManager.Instance == null) return;

        switch (iconType)
        {
            case AudioIconType.MasterVolume:
                AudioManager.Instance.ToggleMasterMute();
                break;

            case AudioIconType.BGMVolume:
                AudioManager.Instance.ToggleBGMMute();
                break;

            case AudioIconType.SFXVolume:
                AudioManager.Instance.ToggleSFXMute();
                break;
        }

        UpdateMuteVisualState();
    }

    public void UpdateMuteVisualState()
    {
        if (AudioManager.Instance == null || _iconImage == null || !changeAlphaOnMute) return;

        bool isMuted = false;
        switch (iconType)
        {
            case AudioIconType.MasterVolume:
                isMuted = AudioManager.Instance.IsMasterMuted;
                break;
            case AudioIconType.BGMVolume:
                isMuted = AudioManager.Instance.IsBGMMuted;
                break;
            case AudioIconType.SFXVolume:
                isMuted = AudioManager.Instance.IsSFXMuted;
                break;
        }

        // Đổi độ mờ (Alpha) của Icon khi bị Mute
        Color color = _iconImage.color;
        color.a = isMuted ? mutedAlpha : 1f;
        _iconImage.color = color;
    }

    // ── HIỆU ỨNG NHẤN / HOVER ──

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_button != null && !_button.interactable) return;
        _targetScale = _originalScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_button != null && !_button.interactable) return;
        _targetScale = _originalScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_button != null && !_button.interactable) return;
        _targetScale = _originalScale * clickScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_button != null && !_button.interactable) return;
        _targetScale = _originalScale * hoverScale;
    }
}
