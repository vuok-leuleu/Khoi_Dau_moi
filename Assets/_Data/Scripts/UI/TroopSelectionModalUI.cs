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
        InitListeners();
    }

    private void InitListeners()
    {
        if (isInitialized) return;
        isInitialized = true;

        if (trainMeleeBtn != null)
        {
            trainMeleeBtn.onClick.RemoveAllListeners();
            trainMeleeBtn.onClick.AddListener(() => OnSelectTroop(BuildingType.BarracksMelee));
        }

        if (trainArcherBtn != null)
        {
            trainArcherBtn.onClick.RemoveAllListeners();
            trainArcherBtn.onClick.AddListener(() => OnSelectTroop(BuildingType.BarracksArcher));
        }

        if (trainSpearBtn != null)
        {
            trainSpearBtn.onClick.RemoveAllListeners();
            trainSpearBtn.onClick.AddListener(() => OnSelectTroop(BuildingType.BarracksSpear));
        }

        if (closeBtn != null)
        {
            closeBtn.onClick.RemoveAllListeners();
            closeBtn.onClick.AddListener(CloseModal);
        }
    }

    public void OpenModal(SettlementZone zone, int slotIndex)
    {
        if (Ins == null) Ins = this;
        InitListeners();

        targetZone = zone;
        targetSlotIndex = slotIndex;

        gameObject.SetActive(true);
        transform.SetAsLastSibling(); // Đưa Popup lên trên cùng của Canvas để không bị che bởi các UI khác
    }

    public void CloseModal()
    {
        gameObject.SetActive(false);
    }

    private void OnSelectTroop(BuildingType troopType)
    {
        if (targetZone != null && TroopTrainingManager.Ins != null)
        {
            TroopTrainingManager.Ins.StartTraining(targetZone, targetSlotIndex, troopType);
        }
        CloseModal();
    }
}
