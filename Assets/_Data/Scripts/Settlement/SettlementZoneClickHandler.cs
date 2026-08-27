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

    private void OnMouseUp()
    {
        if (MoveModeController.IsMoveModeActive) return;
        if (RTSCameraController.IsMouseDragging || RTSCameraController.WasMouseDragThisPress) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (targetZone != null)
        {
            if (targetZone.hasEnemyOutpost)
            {
                EnemySpawn spawner = targetZone.GetComponentInChildren<EnemySpawn>();
                if (spawner != null)
                {
                    spawner.TryShowAttackButton();
                }
            }
            else if (SettlementManager.Ins != null)
            {
                SettlementManager.Ins.SelectSettlement(targetZone);
            }
        }
    }
}
