#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/*
 * AttachButtonEffects.cs
 * Thư mục: Assets/ThanhVien/NhatTien/Script/UI/Editor/
 * 
 * Menu nhanh trong Unity Editor giúp tự động gắn UIButtonEffects cho tất cả nút trong BtnPanel.
 */

public class AttachButtonEffects : Editor
{
    [MenuItem("Tools/Penta Dev/Add Effects to Setting Buttons")]
    public static void AddEffectsToButtons()
    {
        // Tìm BtnPanel trong Scene
        GameObject btnPanel = GameObject.Find("BtnPanel");
        if (btnPanel == null)
        {
            Debug.LogError("❌ Không tìm thấy GameObject 'BtnPanel' trong Scene!");
            return;
        }

        Button[] buttons = btnPanel.GetComponentsInChildren<Button>(true);
        int addedCount = 0;

        foreach (Button btn in buttons)
        {
            if (btn.GetComponent<UIButtonEffects>() == null)
            {
                Undo.AddComponent<UIButtonEffects>(btn.gameObject);
                addedCount++;
            }
        }

        Debug.Log($"🎉 Đã thêm thành công hiệu ứng UIButtonEffects cho {addedCount} nút trong BtnPanel!");
    }
}
#endif
