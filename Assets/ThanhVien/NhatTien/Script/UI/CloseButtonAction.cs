using UnityEngine;
using UnityEngine.UI;

/*
 * CloseButtonAction.cs
 * Thư mục: Assets/ThanhVien/NhatTien/Script/UI/
 * Tác giả: Nhật Tiến
 * 
 * CHỨC NĂNG:
 * Gắn trực tiếp vào nút CloseButton (nút X). 
 * Tự động tắt bảng Panel Settings (hoặc GameObject chỉ định) khi bấm mà không cần kéo OnClick thủ công.
 */

[RequireComponent(typeof(Button))]
public class CloseButtonAction : MonoBehaviour
{
    [Tooltip("GameObject cần ẩn/tắt khi bấm nút X. Nếu để trống, sẽ tự tắt GameObject cha cấp cao nhất (Setting_UI / SettingsPanel).")]
    public GameObject panelToClose;

    void Awake()
    {
        Button btn = GetComponent<Button>();

        // Tìm GameObject panel root nếu người dùng không gán
        if (panelToClose == null)
        {
            // Tìm GameObject có tên Setting_UI hoặc SettingsPanel ở cấp cha
            Transform current = transform.parent;
            while (current != null)
            {
                if (current.name.Contains("Setting_UI") || current.name.Contains("SettingsPanel"))
                {
                    panelToClose = current.gameObject;
                    break;
                }
                current = current.parent;
            }

            // Nếu vẫn chưa tìm thấy thì ẩn chính GameObject gốc của canvas
            if (panelToClose == null && transform.root != null)
            {
                panelToClose = transform.root.gameObject;
            }
        }

        // Đăng ký sự kiện Click tự động
        if (btn != null)
        {
            btn.onClick.AddListener(ClosePanel);
        }
    }

    public void ClosePanel()
    {
        if (panelToClose != null)
        {
            panelToClose.SetActive(false);

            // Phát âm thanh đóng panel
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayPanelClose();
            }
        }
    }
}
