using UnityEngine;

/// <summary>
/// Converts the ResearchCanvas nodes into troop combat bonuses. It recalculates
/// from unlocked nodes, so ResetTree is safe and bonuses never stack twice.
/// </summary>
[RequireComponent(typeof(ResearchPanel))]
public class ResearchUpgradeEffects : MonoBehaviour
{
    // ResearchCanvas là UI có thể đóng, còn PlayerPrefs là nguồn dữ liệu
    // gameplay. Đọc trực tiếp từ save giúp load game/chơi mới không phụ thuộc
    // vào việc người chơi đã từng mở panel nghiên cứu hay chưa.
    public static int FormationBonus => CountSavedUnlocked("formation_1", "formation_2", "formation_3");
    public static bool ArcherUnlocked => IsNodeUnlocked("sword_damage_1");
    public static bool ShieldUnlocked => IsNodeUnlocked("shield_damage_1");
    public static bool CrossbowTowerUnlocked => IsNodeUnlocked("unlock_crossbow_tower_1");
    public static bool CannonTowerUnlocked => IsNodeUnlocked("unlock_cannon_tower_1");
    public static float GetDefenseMultiplier(SoldierResearchType troopType)
    {
        float doctrineMultiplier = IsNodeUnlocked("army_doctrine_1") ? 1.10f : 1f;
        switch (troopType)
        {
            case SoldierResearchType.Sword: return doctrineMultiplier * NodeMultiplier("sword_defense_1");
            case SoldierResearchType.Bow: return doctrineMultiplier * NodeMultiplier("bow_defense_1");
            case SoldierResearchType.Shield: return doctrineMultiplier * NodeMultiplier("shield_defense_1");
            case SoldierResearchType.Crossbow: return doctrineMultiplier * NodeMultiplier("crossbow_defense_1");
            case SoldierResearchType.Cannon: return doctrineMultiplier * NodeMultiplier("cannon_defense_1");
            default: return doctrineMultiplier;
        }
    }

    public static float GetDamageMultiplier(SoldierResearchType troopType)
    {
        float doctrineMultiplier = IsNodeUnlocked("army_doctrine_1") ? 1.10f : 1f;
        switch (troopType)
        {
            case SoldierResearchType.Bow: return doctrineMultiplier * NodeMultiplier("bow_damage_1");
            case SoldierResearchType.Crossbow: return doctrineMultiplier * NodeMultiplier("crossbow_damage_1");
            case SoldierResearchType.Cannon: return doctrineMultiplier * NodeMultiplier("cannon_damage_1");
            default: return doctrineMultiplier;
        }
    }

    private ResearchPanel researchPanel;

    private void OnEnable()
    {
        researchPanel = GetComponent<ResearchPanel>();
        if (researchPanel != null) researchPanel.ResearchStateChanged += RefreshEffects;
        RefreshEffects();
    }

    private void OnDisable()
    {
        if (researchPanel != null) researchPanel.ResearchStateChanged -= RefreshEffects;
    }

    private void RefreshEffects()
    {
        ApplyResearchState(researchPanel);
    }

    /// <summary>
    /// Áp dụng trạng thái của ResearchPanel ngay khi mua nâng cấp. Hàm static
    /// giúp gameplay vẫn nhận hiệu ứng khi component UI đang bị tắt/không kịp
    /// đăng ký event OnEnable.
    /// </summary>
    public static void ApplyResearchState(ResearchPanel panel)
    {
        if (panel == null) return;

        foreach (UnitController soldier in FindObjectsByType<UnitController>(FindObjectsSortMode.None))
            if (soldier != null) soldier.RefreshResearchDamage();

        // Formation research must affect the strategic-map squads too.  They
        // were previously always created as 3 soldiers by TroopTrainingManager
        // and only received this bonus after entering SceneBattle.
        TroopTrainingManager.Ins?.ApplyFormationBonusToTrainedUnits();

        // Các research mở công trình phải cập nhật ngay UI đang mở; không bắt
        // người chơi đóng/mở lại Settlement hoặc Shop mới thấy Tháp Nỏ/Pháo.
        TroopSelectionModalUI.Ins?.RefreshResearchAvailability();
        BuildingShopUI.Ins?.RefreshAllItems();

    }

    private static bool IsNodeUnlocked(string nodeId) => ResearchPanel.IsNodeSavedAsUnlocked(nodeId);

    private static float NodeMultiplier(string nodeId) => IsNodeUnlocked(nodeId) ? 1.15f : 1f;

    private static int CountSavedUnlocked(params string[] nodeIds)
    {
        int count = 0;
        foreach (string nodeId in nodeIds)
            if (ResearchPanel.IsNodeSavedAsUnlocked(nodeId)) count++;
        return count;
    }
}
