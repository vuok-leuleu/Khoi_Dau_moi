using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*
 * TroopTrainingSlotUI.cs
 * Folder: Scripts/UI/
 * Quản lý hiển thị cho 1 Ô Huấn Luyện Lính (Troop Training Slot UI)
 */

public class TroopTrainingSlotUI : MonoBehaviour
{
    [Header("=== CÁC TRẠNG THÁI HIỂN THỊ (STATE OBJECTS) ===")]
    [SerializeField] private GameObject lockedStateObj;    // Giao diện khi bị Khóa
    [SerializeField] private GameObject emptyStateObj;     // Giao diện khi Ô Trống
    [SerializeField] private GameObject trainingStateObj;  // Giao diện khi Đang Huấn Luyện
    [SerializeField] private GameObject completedStateObj; // Giao diện khi Hoàn Thành

    [Header("=== THÔNG TIN TEXT & ICON ===")]
    [SerializeField] private TextMeshProUGUI lockedTextTMP;
    [SerializeField] private TextMeshProUGUI emptyTextTMP;
    [SerializeField] private Image troopIconImg;
    [SerializeField] private TextMeshProUGUI troopNameTMP;
    [SerializeField] private TextMeshProUGUI remainingTimeTMP;
    [SerializeField] private Button slotButton;

    private TroopTrainingSlotData currentSlotData;
    private SettlementZone currentZone;

    private void Awake()
    {
        if (slotButton == null) slotButton = GetComponent<Button>();
    }

    private void Start()
    {
        if (slotButton != null)
        {
            slotButton.onClick.RemoveListener(OnClickSlot);
            slotButton.onClick.AddListener(OnClickSlot);
        }
    }

    /// <summary>
    /// Cập nhật dữ liệu và hiển thị trực quan cho Ô Huấn Luyện
    /// </summary>
    public void SetData(TroopTrainingSlotData data, SettlementZone zone)
    {
        currentSlotData = data;
        currentZone = zone;

        if (data == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        // Tắt tất cả các trạng thái trước khi bật đúng trạng thái tương ứng
        if (lockedStateObj != null) lockedStateObj.SetActive(false);
        if (emptyStateObj != null) emptyStateObj.SetActive(false);
        if (trainingStateObj != null) trainingStateObj.SetActive(false);
        if (completedStateObj != null) completedStateObj.SetActive(false);

        // 1. CHẾ ĐỘ BỊ KHÓA 🔒
        if (!data.isUnlocked)
        {
            if (lockedStateObj != null) lockedStateObj.SetActive(true);
            if (lockedTextTMP != null)
            {
                int reqLevel = GetRequiredBarracksLevelForSlot(data.slotIndex);
                lockedTextTMP.text = $"Trại Lính Lv.{reqLevel}";
            }
            return;
        }

        // 2. CHẾ ĐỘ ĐANG HUẤN LUYỆN ⏳
        if (data.isTraining)
        {
            if (trainingStateObj != null) trainingStateObj.SetActive(true);
            if (troopNameTMP != null) troopNameTMP.text = GetTroopDisplayName(data.troopType);
            if (remainingTimeTMP != null) remainingTimeTMP.text = $"Còn {data.remainingWaves} Ngày";
            return;
        }

        // 3. CHẾ ĐỘ HOÀN THÀNH ✅ (Đã chứa lính)
        if (data.isCompleted)
        {
            if (completedStateObj != null)
            {
                completedStateObj.SetActive(true);
                TextMeshProUGUI compTMP = completedStateObj.GetComponentInChildren<TextMeshProUGUI>();
                if (compTMP != null)
                {
                    compTMP.text = GetTroopDisplayName(data.troopType);
                }
            }
            if (troopNameTMP != null) troopNameTMP.text = GetTroopDisplayName(data.troopType);
            return;
        }

        // 4. CHẾ ĐỘ Ô TRỐNG ➕
        if (emptyStateObj != null) emptyStateObj.SetActive(true);
        if (emptyTextTMP != null) emptyTextTMP.text = "+ Huấn Luyện";
    }

    private void OnClickSlot()
    {
        if (currentSlotData == null || currentZone == null) return;

        if (!currentSlotData.isUnlocked)
        {
            int reqLevel = GetRequiredBarracksLevelForSlot(currentSlotData.slotIndex);
            string warnMsg = $"Trại Lính Cấp 1 chỉ mở 3 ô huấn luyện. Hãy nâng cấp Trại Lính lên Lv.{reqLevel} để mở thêm ô!";
            if (UIManager.Ins != null) UIManager.Ins.ShowWarning(warnMsg);
            Debug.Log($"[TroopTrainingSlotUI] Ô {currentSlotData.slotIndex + 1} đang bị khóa. Yêu cầu Trại Lính Lv.{reqLevel}.");
            return;
        }

        if (currentSlotData.isTraining)
        {
            string msg = $"Ô {currentSlotData.slotIndex + 1} đang huấn luyện {GetTroopDisplayName(currentSlotData.troopType)}. Còn {currentSlotData.remainingWaves} Ngày.";
            if (UIManager.Ins != null) UIManager.Ins.ShowWarning(msg);
            Debug.Log($"[TroopTrainingSlotUI] {msg}");
            return;
        }

        if (currentSlotData.isCompleted)
        {
            string msg = $"Ô này đang chứa 1 Lính {GetTroopDisplayName(currentSlotData.troopType)}. Hãy nâng cấp Trại Lính để huấn luyện thêm lính!";
            if (UIManager.Ins != null) UIManager.Ins.ShowWarning(msg);
            Debug.Log($"[TroopTrainingSlotUI] Ô {currentSlotData.slotIndex + 1}: {msg}");
            return;
        }

        // Nếu là Ô trống, mở Bảng chọn loại lính để bắt đầu huấn luyện
        TroopSelectionModalUI modal = TroopSelectionModalUI.Ins;
        if (modal == null)
        {
            modal = Object.FindFirstObjectByType<TroopSelectionModalUI>(FindObjectsInactive.Include);
        }

        if (modal != null)
        {
            modal.OpenModal(currentZone, currentSlotData.slotIndex);
        }
        else
        {
            Debug.LogWarning("[TroopTrainingSlotUI] ⚠️ Không tìm thấy TroopSelectionModalUI trong Scene! Đang huấn luyện mặc định Kiếm Sĩ.");
            if (TroopTrainingManager.Ins != null)
            {
                TroopTrainingManager.Ins.StartTraining(currentZone, currentSlotData.slotIndex, BuildingType.BarracksMelee);
            }
        }
    }

    private int GetRequiredBarracksLevelForSlot(int slotIndex)
    {
        if (slotIndex < 3) return 1; // Ô 0, 1, 2 -> Cần Lv.1
        if (slotIndex < 5) return 2; // Ô 3, 4 -> Cần Lv.2
        return 3;                    // Ô 5, 6, 7 -> Cần Lv.3
    }

    private string GetTroopDisplayName(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.BarracksMelee: return "Kiếm Sĩ";
            case BuildingType.BarracksArcher: return "Cung Thủ";
            case BuildingType.BarracksSpear: return "Thương Thủ";
            default: return "Binh Lính";
        }
    }
}
