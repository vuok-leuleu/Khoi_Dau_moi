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
