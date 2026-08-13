using UnityEngine;

/*
 * SettingsCanvasFixer.cs
 * Thư mục: Assets/ThanhVien/NhatTien/Script/UI/
 * Tác giả: Nhật Tiến
 *
 * VẤN ĐỀ: Khi TMP_Dropdown mở ra, Unity tạo 1 tấm "Blocker" trong
 * root Canvas. Nếu Canvas của SettingMenu_UI có Sort Order = 0 (mặc định),
 * tấm Blocker này có Sort Order ngang bằng hoặc cao hơn → đè lên toàn bộ UI.
 *
 * GIẢI PHÁP: Gắn script này vào SettingMenu_UI. Script sẽ tự tìm và
 * nâng Sort Order của Canvas lên 100 để tấm Blocker luôn ở dưới UI.
 */

public class SettingsCanvasFixer : MonoBehaviour
{
    [Tooltip("Sort Order cho Canvas của panel Settings (phải cao hơn CanvasMenuMain)")]
    public int sortOrder = 100;

    void Awake()
    {
        // Nâng Sort Order của Canvas để Dropdown Blocker không đè lên UI
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortOrder;
        }
        else
        {
            // Nếu root object chưa có Canvas, tìm Canvas con đầu tiên
            canvas = GetComponentInChildren<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = sortOrder;
            }
            else
            {
                Debug.LogWarning("[SettingsCanvasFixer] Không tìm thấy Canvas nào! Hãy thêm Canvas component vào SettingMenu_UI.");
            }
        }
    }
}
