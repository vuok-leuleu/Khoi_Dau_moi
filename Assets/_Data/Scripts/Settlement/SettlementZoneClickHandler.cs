using UnityEngine;
using UnityEngine.EventSystems;

/*
 * SettlementZoneClickHandler.cs
 * Folder: Scripts/Settlement/
 * Dự án: KHẨN HOANG (PENTA DEV)
 * Phong cách: Demacia Rising 3D Territory Click Handler
 */

public class SettlementZoneClickHandler : MonoBehaviour
{
    [SerializeField] private SettlementZone targetZone;

    private void Awake()
    {
        if (targetZone == null) targetZone = GetComponentInParent<SettlementZone>();
    }

    private void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (targetZone != null)
        {
            if (targetZone.hasEnemyOutpost)
            {
                EnemySpawn spawner = targetZone.GetComponentInChildren<EnemySpawn>();
                Transform btnTarget = (spawner != null) ? spawner.GetSpawnPoint() : (targetZone.spawnedEnemyOutpostInstance != null ? targetZone.spawnedEnemyOutpostInstance.transform : targetZone.transform);
                UIEnemyWaveButton.CreateButton(btnTarget);
            }
            else if (SettlementManager.Ins != null)
            {
                SettlementManager.Ins.SelectSettlement(targetZone);
            }
        }
    }
}
