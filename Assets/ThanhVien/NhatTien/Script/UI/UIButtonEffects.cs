using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/*
 * UIButtonEffects.cs
 * Thư mục: Assets/ThanhVien/NhatTien/Script/UI/
 * Tác giả: Nhật Tiến
 *
 * CHỨC NĂNG:
 * Tạo hiệu ứng mượt mà khi Rê chuột vào (Hover/PointerEnter) và Nhấn (Click/PointerDown) cho Button UI:
 * - Khi di chuột vào (Hover): Phóng to nhẹ (Scale 1.08x) + Phát tiếng Sound SFX hover UI (nếu có).
 * - Khi nhấn giữ (Click/Press): Thu nhỏ nhẹ (Scale 0.95x).
 * - Khi bỏ chuột ra (PointerExit/Up): Trở về kích thước gốc 1.0x.
 * - Tự động kết nối với AudioManager.Instance phát tiếng Click / Hover.
 */

public class UIButtonEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Cấu hình Hiệu ứng Scale")]
    [Tooltip("Hệ số phóng to khi rê chuột vào (Mặc định: 1.08)")]
    public float hoverScale = 1.08f;

    [Tooltip("Hệ số thu nhỏ khi nhấn chuột vào (Mặc định: 0.95)")]
    public float clickScale = 0.95f;

    [Tooltip("Tốc độ chuyển đổi mượt mà (Mặc định: 15)")]
    public float animationSpeed = 15f;

    [Header("Âm thanh UI (Tùy chọn)")]
    public AudioClip customHoverSound;
    public AudioClip customClickSound;

    private Vector3 _originalScale;
    private Vector3 _targetScale;
    private Button _button;

    void Awake()
    {
        _originalScale = transform.localScale;
        _targetScale = _originalScale;
        _button = GetComponent<Button>();
    }

    void OnEnable()
    {
        // Khôi phục scale chuẩn mỗi khi active panel
        transform.localScale = _originalScale;
        _targetScale = _originalScale;
    }

    void Update()
    {
        // Lerp mượt mà scale hiện tại về target scale
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.unscaledDeltaTime * animationSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_button != null && !_button.interactable) return;

        _targetScale = _originalScale * hoverScale;

        // Phát tiếng Hover UI
        if (customHoverSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(customHoverSound);
        }
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

        // Phát tiếng Click UI
        if (customClickSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(customClickSound);
        }
        else if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_button != null && !_button.interactable) return;

        _targetScale = _originalScale * hoverScale;
    }
}
