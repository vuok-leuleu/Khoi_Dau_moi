using UnityEngine;

/// <summary>
/// Converts the ResearchCanvas nodes into troop combat bonuses. It recalculates
/// from unlocked nodes, so ResetTree is safe and bonuses never stack twice.
/// </summary>
[RequireComponent(typeof(ResearchPanel))]
public class ResearchUpgradeEffects : MonoBehaviour
{
    private static float damageMultiplier = 1f;
    private static float defenseMultiplier = 1f;
    private static int formationBonus;
    private static float bowDamageMultiplier = 1f;
    private static float crossbowDamageMultiplier = 1f;
    private static float cannonDamageMultiplier = 1f;
    private static float swordDefenseMultiplier = 1f;
    private static float bowDefenseMultiplier = 1f;
    private static float shieldDefenseMultiplier = 1f;
    private static float crossbowDefenseMultiplier = 1f;
    private static float cannonDefenseMultiplier = 1f;

    public static int FormationBonus => formationBonus;
    public static bool ArcherUnlocked { get; private set; }
    public static bool ShieldUnlocked { get; private set; }
    public static bool CrossbowTowerUnlocked { get; private set; }
    public static bool CannonTowerUnlocked { get; private set; }
    public static bool IsResearchTreeAvailable { get; private set; }
    public static float GetDefenseMultiplier(SoldierResearchType troopType)
    {
        switch (troopType)
        {
            case SoldierResearchType.Sword: return defenseMultiplier * swordDefenseMultiplier;
            case SoldierResearchType.Bow: return defenseMultiplier * bowDefenseMultiplier;
            case SoldierResearchType.Shield: return defenseMultiplier * shieldDefenseMultiplier;
            case SoldierResearchType.Crossbow: return defenseMultiplier * crossbowDefenseMultiplier;
            case SoldierResearchType.Cannon: return defenseMultiplier * cannonDefenseMultiplier;
            default: return defenseMultiplier;
        }
    }

    public static float GetDamageMultiplier(SoldierResearchType troopType)
    {
        switch (troopType)
        {
            case SoldierResearchType.Sword: return damageMultiplier;
            case SoldierResearchType.Bow: return damageMultiplier * bowDamageMultiplier;
            case SoldierResearchType.Shield: return damageMultiplier;
            case SoldierResearchType.Crossbow: return damageMultiplier * crossbowDamageMultiplier;
            case SoldierResearchType.Cannon: return damageMultiplier * cannonDamageMultiplier;
            default: return damageMultiplier;
        }
    }

    private ResearchPanel researchPanel;

    private void OnEnable()
    {
        IsResearchTreeAvailable = true;
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
        if (researchPanel == null) return;

        formationBonus = CountUnlocked("formation_1", "formation_2", "formation_3");
        // The player-authored "Huấn luyện cung thủ" node unlocks ranged
        // soldiers instead of increasing sword damage.
        ArcherUnlocked = researchPanel.IsUnlocked("sword_damage_1");
        bowDamageMultiplier = MultiplierFor("bow_damage_1");
        // The player-authored shield node unlocks tank/shield soldiers instead
        // of increasing their damage.
        ShieldUnlocked = researchPanel.IsUnlocked("shield_damage_1");
        CrossbowTowerUnlocked = researchPanel.IsUnlocked("unlock_crossbow_tower_1");
        CannonTowerUnlocked = researchPanel.IsUnlocked("unlock_cannon_tower_1");
        crossbowDamageMultiplier = MultiplierFor("crossbow_damage_1");
        cannonDamageMultiplier = MultiplierFor("cannon_damage_1");
        swordDefenseMultiplier = MultiplierFor("sword_defense_1");
        bowDefenseMultiplier = MultiplierFor("bow_defense_1");
        shieldDefenseMultiplier = MultiplierFor("shield_defense_1");
        crossbowDefenseMultiplier = MultiplierFor("crossbow_defense_1");
        cannonDefenseMultiplier = MultiplierFor("cannon_defense_1");

        // The final doctrine adds a shared armour bonus on top of the
        // type-specific defence upgrades.
        defenseMultiplier = researchPanel.IsUnlocked("army_doctrine_1") ? 1.10f : 1f;
        damageMultiplier = researchPanel.IsUnlocked("army_doctrine_1") ? 1.10f : 1f;

        foreach (UnitController soldier in FindObjectsByType<UnitController>(FindObjectsSortMode.None))
            if (soldier != null) soldier.RefreshResearchDamage();

    }

    private float MultiplierFor(string nodeId) => researchPanel.IsUnlocked(nodeId) ? 1.15f : 1f;

    private int CountUnlocked(params string[] nodeIds)
    {
        int count = 0;
        foreach (string nodeId in nodeIds)
            if (researchPanel.IsUnlocked(nodeId)) count++;
        return count;
    }
}
