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

    private void Awake()
    {
        if (Ins == null) Ins = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (trainMeleeBtn != null) trainMeleeBtn.onClick.AddListener(() => OnSelectTroop(BuildingType.BarracksMelee));
        if (trainArcherBtn != null) trainArcherBtn.onClick.AddListener(() => OnSelectTroop(BuildingType.BarracksArcher));
        if (trainSpearBtn != null) trainSpearBtn.onClick.AddListener(() => OnSelectTroop(BuildingType.BarracksSpear));
        if (closeBtn != null) closeBtn.onClick.AddListener(CloseModal);

        gameObject.SetActive(false);
    }

    public void OpenModal(SettlementZone zone, int slotIndex)
    {
        targetZone = zone;
        targetSlotIndex = slotIndex;
        gameObject.SetActive(true);
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
