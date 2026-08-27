using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/*
 * TroopTrainingSlotUI.cs
 * Folder: Scripts/UI/
 * Quản lý hiển thị cho 1 Ô Huấn Luyện Lính (Troop Training Slot UI)
 */

public class TroopTrainingSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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
    private SlotHoverFrame hoverFrame;

    private void Awake()
    {
        AutoBindReferences();
        ConfigureButton();
        SetupHoverFrame();
    }

    private void OnEnable()
    {
        AutoBindReferences();
        ConfigureButton();
        SetupHoverFrame();
    }

    private void OnDisable()
    {
        hoverFrame?.Hide();

        if (slotButton != null && !HasPersistentClickListener(slotButton))
        {
            slotButton.onClick.RemoveListener(OnClickSlot);
        }
    }

    private void SetupHoverFrame()
    {
        if (hoverFrame != null) return;

        SlotHoverFrameSettings settings = GetComponentInParent<SlotHoverFrameSettings>();
        if (settings == null || settings.HoverFrameSprite == null) return;

        RectTransform slotTransform = transform as RectTransform;
        if (slotTransform == null) return;

        hoverFrame = new SlotHoverFrame(slotTransform, settings);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hoverFrame?.Show();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hoverFrame?.Hide();
    }

    private void ConfigureButton()
    {
        if (slotButton == null) return;

        // Prefab hiện tại đã có persistent listener. Chỉ thêm runtime listener khi thật sự thiếu.
        slotButton.onClick.RemoveListener(OnClickSlot);
        if (!HasPersistentClickListener(slotButton))
        {
            slotButton.onClick.AddListener(OnClickSlot);
        }
    }

    private bool HasPersistentClickListener(Button button)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) == this &&
                button.onClick.GetPersistentMethodName(i) == nameof(OnClickSlot))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Fallback cho trường hợp component được thêm lúc runtime hoặc prefab bị mất reference.
    /// </summary>
    private void AutoBindReferences()
    {
        if (slotButton == null)
        {
            slotButton = GetComponent<Button>();
            if (slotButton == null) slotButton = GetComponentInChildren<Button>(true);
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child == null) continue;

            string childName = child.name.Trim();
            if (lockedStateObj == null && childName.Equals("LockedStateObj", System.StringComparison.OrdinalIgnoreCase))
                lockedStateObj = child.gameObject;
            else if (emptyStateObj == null && childName.Equals("EmptyStateObj", System.StringComparison.OrdinalIgnoreCase))
                emptyStateObj = child.gameObject;
            else if (trainingStateObj == null && childName.Equals("TrainingStateObj", System.StringComparison.OrdinalIgnoreCase))
                trainingStateObj = child.gameObject;
            else if (completedStateObj == null && childName.Equals("CompletedStateObj", System.StringComparison.OrdinalIgnoreCase))
                completedStateObj = child.gameObject;

            if (lockedTextTMP == null && childName.StartsWith("LockedText_TMP", System.StringComparison.OrdinalIgnoreCase))
                lockedTextTMP = child.GetComponent<TextMeshProUGUI>();
            else if (emptyTextTMP == null && childName.StartsWith("EmptyTitle_TMP", System.StringComparison.OrdinalIgnoreCase))
                emptyTextTMP = child.GetComponent<TextMeshProUGUI>();
            else if (troopNameTMP == null && childName.StartsWith("BuildingName_TMP", System.StringComparison.OrdinalIgnoreCase))
                troopNameTMP = child.GetComponent<TextMeshProUGUI>();
            else if (remainingTimeTMP == null && childName.StartsWith("RemainingTime_TMP", System.StringComparison.OrdinalIgnoreCase))
                remainingTimeTMP = child.GetComponent<TextMeshProUGUI>();

            if (troopIconImg == null && childName.Equals("TroopIcon_Img", System.StringComparison.OrdinalIgnoreCase))
                troopIconImg = child.GetComponent<Image>();
        }
    }

    /// <summary>
    /// Sao chép phần trình bày của bốn trạng thái từ một ô mẫu.
    /// Dữ liệu lính và sự kiện click của từng ô không bị sao chép.
    /// </summary>
    public void CopyVisualStyleFrom(TroopTrainingSlotUI template)
    {
        if (template == null || template == this) return;

        template.AutoBindReferences();
        AutoBindReferences();

        CopyStateVisualStyle(template.lockedStateObj, lockedStateObj);
        CopyStateVisualStyle(template.emptyStateObj, emptyStateObj);
        CopyStateVisualStyle(template.trainingStateObj, trainingStateObj);
        CopyStateVisualStyle(template.completedStateObj, completedStateObj);
    }

    private static void CopyStateVisualStyle(GameObject source, GameObject destination)
    {
        if (source == null || destination == null) return;

        CopyVisualHierarchy(source.transform, destination.transform);
    }

    private static void CopyVisualHierarchy(Transform source, Transform destination)
    {
        CopyRectTransform(source as RectTransform, destination as RectTransform);

        Image sourceImage = source.GetComponent<Image>();
        Image destinationImage = destination.GetComponent<Image>();
        if (sourceImage != null && destinationImage != null)
        {
            destinationImage.sprite = sourceImage.sprite;
            destinationImage.color = sourceImage.color;
            destinationImage.material = sourceImage.material;
            destinationImage.type = sourceImage.type;
            destinationImage.preserveAspect = sourceImage.preserveAspect;
            destinationImage.raycastTarget = sourceImage.raycastTarget;
            destinationImage.maskable = sourceImage.maskable;
        }

        TextMeshProUGUI sourceText = source.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI destinationText = destination.GetComponent<TextMeshProUGUI>();
        if (sourceText != null && destinationText != null)
        {
            destinationText.font = sourceText.font;
            destinationText.fontSharedMaterial = sourceText.fontSharedMaterial;
            destinationText.fontSize = sourceText.fontSize;
            destinationText.enableAutoSizing = sourceText.enableAutoSizing;
            destinationText.fontSizeMin = sourceText.fontSizeMin;
            destinationText.fontSizeMax = sourceText.fontSizeMax;
            destinationText.fontStyle = sourceText.fontStyle;
            destinationText.alignment = sourceText.alignment;
            destinationText.color = sourceText.color;
            destinationText.enableWordWrapping = sourceText.enableWordWrapping;
            destinationText.overflowMode = sourceText.overflowMode;
            destinationText.margin = sourceText.margin;
            destinationText.characterSpacing = sourceText.characterSpacing;
            destinationText.wordSpacing = sourceText.wordSpacing;
            destinationText.lineSpacing = sourceText.lineSpacing;
            destinationText.paragraphSpacing = sourceText.paragraphSpacing;
            destinationText.raycastTarget = sourceText.raycastTarget;
            destinationText.maskable = sourceText.maskable;
        }

        for (int i = 0; i < source.childCount; i++)
        {
            Transform sourceChild = source.GetChild(i);
            Transform destinationChild = FindChildByTrimmedName(destination, sourceChild.name);
            if (destinationChild != null)
            {
                CopyVisualHierarchy(sourceChild, destinationChild);
            }
        }
    }

    private static Transform FindChildByTrimmedName(Transform parent, string childName)
    {
        string targetName = childName.Trim();
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name.Trim().Equals(targetName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private static void CopyRectTransform(RectTransform source, RectTransform destination)
    {
        if (source == null || destination == null) return;

        destination.anchorMin = source.anchorMin;
        destination.anchorMax = source.anchorMax;
        destination.pivot = source.pivot;

        if (source.anchorMin == source.anchorMax)
        {
            destination.anchoredPosition3D = source.anchoredPosition3D;
            destination.sizeDelta = source.sizeDelta;
        }
        else
        {
            destination.offsetMin = source.offsetMin;
            destination.offsetMax = source.offsetMax;
        }

        destination.localRotation = source.localRotation;
        destination.localScale = source.localScale;
    }

    /// <summary>
    /// Cập nhật dữ liệu và hiển thị trực quan cho Ô Huấn Luyện
    /// </summary>
    public void SetData(TroopTrainingSlotData data, SettlementZone zone)
    {
        AutoBindReferences();
        currentSlotData = data;
        currentZone = zone;

        bool isGarrisonView = TroopTrainingManager.Ins != null &&
                              !TroopTrainingManager.Ins.IsCentralTrainingSettlement(zone);

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
            string troopLabel = GetTroopDisplayName(data.troopType);

            if (completedStateObj != null)
            {
                completedStateObj.SetActive(true);
                TextMeshProUGUI compTMP = completedStateObj.GetComponentInChildren<TextMeshProUGUI>();
                if (compTMP != null)
                {
                    compTMP.text = troopLabel;
                }
            }
            if (troopNameTMP != null) troopNameTMP.text = troopLabel;
            return;
        }

        // 4. CHẾ ĐỘ Ô TRỐNG ➕
        if (emptyStateObj != null) emptyStateObj.SetActive(true);
        if (emptyTextTMP != null) emptyTextTMP.text = "+ Huấn Luyện";
    }

    private void OnClickSlot()
    {
        if (currentSlotData == null || currentZone == null) return;

        if (TroopTrainingManager.Ins != null && !TroopTrainingManager.Ins.IsCentralTrainingSettlement(currentZone))
        {
            string warning = currentSlotData.isCompleted
                ? $"{currentZone.settlementName} đang đồn trú {currentSlotData.stationedSoldierCount} {GetTroopDisplayName(currentSlotData.troopType)}."
                : $"{currentZone.settlementName} không có {GetTroopDisplayName(currentSlotData.troopType)} đồn trú.";
            if (UIManager.Ins != null) UIManager.Ins.ShowWarning(warning);
            Debug.Log($"[TroopTrainingSlotUI] {warning}");
            return;
        }

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
            case BuildingType.BarracksSpear: return "Hộ Vệ";
            default: return "Binh Lính";
        }
    }
}
