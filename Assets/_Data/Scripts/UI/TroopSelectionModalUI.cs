using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*
 * TroopSelectionModalUI.cs
 * Folder: Scripts/UI/
 * Popup Modal cho phép người chơi chọn loại lính (Kiếm Sĩ, Cung Thủ, Thương Thủ) để bắt đầu huấn luyện.
 */

public class TroopSelectionModalUI : MonoBehaviour
{
    public static TroopSelectionModalUI Ins { get; private set; }

    [Header("=== CÁC NÚT CHỌN LOẠI LÍNH ===")]
    [SerializeField] private Button trainMeleeBtn;
    [SerializeField] private Button trainArcherBtn;
    [SerializeField] private Button trainSpearBtn;
    [SerializeField] private Button trainCrossbowBtn;
    [SerializeField] private Button trainCannonBtn;
    [SerializeField] private Button closeBtn;

    private SettlementZone targetZone;
    private int targetSlotIndex;
    private bool isInitialized = false;

    private void Awake()
    {
        if (Ins == null) Ins = this;
        else if (Ins != this)
        {
            Destroy(gameObject);
            return;
        }

        InitListeners();
        HideUnsupportedTrainingOptions();
    }

    private void OnDestroy()
    {
        if (Ins == this) Ins = null;
    }

    private void InitListeners()
    {
        if (isInitialized) return;
        isInitialized = true;

        if (trainMeleeBtn != null)
        {
            trainMeleeBtn.onClick.RemoveListener(OnClickMelee);
            trainMeleeBtn.onClick.AddListener(OnClickMelee);
        }

        if (trainArcherBtn != null)
        {
            trainArcherBtn.onClick.RemoveListener(OnClickArcher);
            trainArcherBtn.onClick.AddListener(OnClickArcher);
        }

        if (trainSpearBtn != null)
        {
            trainSpearBtn.onClick.RemoveListener(OnClickSpear);
            trainSpearBtn.onClick.AddListener(OnClickSpear);
        }

        if (closeBtn != null)
        {
            closeBtn.onClick.RemoveListener(CloseModal);
            closeBtn.onClick.AddListener(CloseModal);
        }
    }

    public void OpenModal(SettlementZone zone, int slotIndex)
    {
        if (zone == null || slotIndex < 0 || slotIndex >= TroopTrainingManager.MAX_TRAINING_SLOTS)
        {
            Debug.LogWarning("[TroopSelectionModalUI] Không thể mở bảng huấn luyện vì vùng hoặc ô huấn luyện không hợp lệ.", this);
            return;
        }

        if (Ins == null) Ins = this;
        InitListeners();

        targetZone = zone;
        targetSlotIndex = slotIndex;

        RefreshResearchAvailability();

        BuildTrainingUIManager.Ins?.ShowTrainingWindow();
        gameObject.SetActive(true);
        transform.SetAsLastSibling(); // Đưa Popup lên trên cùng của Canvas để không bị che bởi các UI khác
    }

    public void CloseModal()
    {
        gameObject.SetActive(false);
        targetZone = null;
        targetSlotIndex = -1;
        BuildTrainingUIManager.Ins?.NotifyWindowClosed(BuildTrainingUIManager.ManagedWindow.Training);
    }

    private void OnClickMelee() => OnSelectTroop(BuildingType.BarracksMelee);
    private void OnClickArcher() => OnSelectTroop(BuildingType.BarracksArcher);
    private void OnClickSpear() => OnSelectTroop(BuildingType.BarracksSpear);

    private void RefreshResearchAvailability()
    {
        SetItemAvailability(trainMeleeBtn, true);
        SetItemAvailability(trainArcherBtn, SpawnSoldier.IsTroopTrainingUnlocked(BuildingType.BarracksArcher));
        bool isTutorialShieldBypass = CampaignTutorialManager.Ins != null &&
                                      !CampaignTutorialManager.Ins.IsTutorialCompleted();
        SetItemAvailability(trainSpearBtn,
            isTutorialShieldBypass ||
            (SpawnSoldier.IsTroopTrainingUnlocked(BuildingType.BarracksSpear) &&
             CampaignTutorialManager.IsShieldTroopTrainingUnlocked()));

        // Nỏ/Pháo là công nghệ mở khóa tháp. Chúng không có BuildingType,
        // UnitController prefab hay luồng lưu trận tương ứng nên không được
        // quảng bá nhầm là lính có thể huấn luyện.
        HideUnsupportedTrainingOptions();
    }

    private void HideUnsupportedTrainingOptions()
    {
        if (trainCrossbowBtn != null) trainCrossbowBtn.gameObject.SetActive(false);
        if (trainCannonBtn != null) trainCannonBtn.gameObject.SetActive(false);
    }

    private static void SetItemAvailability(Button button, bool isUnlocked, bool canSelect = true)
    {
        if (button == null) return;

        button.interactable = isUnlocked && canSelect;
        CanvasGroup group = button.GetComponent<CanvasGroup>();
        if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();

        // A locked item is visibly dimmed to 30% and cannot receive clicks.
        group.alpha = isUnlocked ? 1f : 0.3f;
        group.interactable = isUnlocked && canSelect;
        group.blocksRaycasts = isUnlocked && canSelect;
    }

    private void OnSelectTroop(BuildingType troopType)
    {
        if (targetZone == null || TroopTrainingManager.Ins == null)
        {
            Debug.LogWarning("[TroopSelectionModalUI] Thiếu SettlementZone hoặc TroopTrainingManager; yêu cầu huấn luyện chưa được thực hiện.", this);
            return;
        }

        if (TroopTrainingManager.Ins.StartTraining(targetZone, targetSlotIndex, troopType))
        {
            SettlementSidePanelUI.Ins?.RefreshTroopTrainingSlots();
            CloseModal();
        }
    }
}
