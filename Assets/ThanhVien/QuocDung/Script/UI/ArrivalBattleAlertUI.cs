using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ArrivalBattleAlertUI : MonoBehaviour
{
    [Header("Presentation Assets")]
    [SerializeField] private Sprite meleeIcon;
    [SerializeField] private Sprite rangedIcon;

    [Header("Canvas References")]
    [SerializeField] private GameObject enemyEntry;
    [SerializeField] private Image enemyIcon;
    [SerializeField] private TextMeshProUGUI enemyCountText;
    [SerializeField] private Button fightButton;

    private EnemyAI activeLeader;
    private bool isTransitioning = false;

    public static void ShowFor(EnemyAI leader, Transform focusTarget)
    {
        ArrivalBattleAlertUI alert =
            FindFirstObjectByType<ArrivalBattleAlertUI>(FindObjectsInactive.Include);

        if (alert == null)
        {
            Debug.LogWarning("[ArrivalBattleAlertUI] ArrivalBattleAlertUI canvas was not found in this scene.");
            return;
        }

        alert.Show(leader);
    }

    private void Awake()
    {
        if (fightButton != null)
        {
            fightButton.onClick.RemoveListener(StartBattle);
            fightButton.onClick.AddListener(StartBattle);
        }
    }

    private void OnEnable()
    {
        isTransitioning = false;
        if (fightButton != null) fightButton.interactable = true;
    }

    public void Show(EnemyAI leader)
    {
        activeLeader = leader;
        isTransitioning = false;
        if (fightButton != null) fightButton.interactable = true;
        gameObject.SetActive(true);
        PopulateEnemyInfo(leader);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void PopulateEnemyInfo(EnemyAI leader)
    {
        if (enemyEntry == null || enemyCountText == null) return;

        int meleeCount = 0;
        int rangedCount = 0;

        IEnumerable<EnemyAI> enemies =
            leader != null && leader.squadEnemies != null && leader.squadEnemies.Count > 0
                ? (IEnumerable<EnemyAI>)leader.squadEnemies
                : new[] { leader };

        foreach (EnemyAI enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            if (enemy.attackType == EnemyAI.EnemyAttackType.Ranged)
                rangedCount++;
            else
                meleeCount++;
        }

        int totalCount = meleeCount + rangedCount;
        enemyEntry.SetActive(totalCount > 0);
        enemyCountText.text = totalCount.ToString();

        if (enemyIcon != null)
        {
            enemyIcon.sprite = rangedCount > meleeCount ? rangedIcon : meleeIcon;
            enemyIcon.preserveAspect = true;
        }
    }

    private void StartBattle()
    {
        if (isTransitioning) return;
        isTransitioning = true;

        if (fightButton != null) fightButton.interactable = false;

        ExecuteStartBattle();
    }

    private void ExecuteStartBattle()
    {
        if (activeLeader != null)
        {
            activeLeader.OnAttackButtonClicked();
        }
        else
        {
            CloudSceneTransition.LoadSceneWithCloud("SceneBattle");
        }
    }
}
