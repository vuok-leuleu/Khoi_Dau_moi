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
        if (RTSCameraController.IsMouseDragging || RTSCameraController.WasMouseDragThisPress) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // Khi đang điều quân, dùng Zone đã được handler này gán sẵn thay vì
        // raycast qua model. Nhờ đó VASKASIA <-> EVENMOOR đều chọn được làm đích.
        if (MoveModeController.IsMoveModeActive)
        {
            MoveModeController.Ins?.TrySelectDestination(targetZone);
            return;
        }

        if (targetZone != null)
        {
            if (!targetZone.isUnlocked)
            {
                // Vùng còn bị khóa không được mở settlement UI.
                SettlementSidePanelUI.Ins?.SetMoveButtonVisible(false);
                UIManager.Ins?.CloseSettlementPanel();
                return;
            }

            if (targetZone.hasEnemyOutpost)
            {
                // Vùng còn địch chỉ hiện luồng tấn công (nếu đủ điều kiện),
                // tuyệt đối không hiện bảng settlement.
                SettlementSidePanelUI.Ins?.SetMoveButtonVisible(false);
                UIManager.Ins?.CloseSettlementPanel();
                targetZone.TryShowConquestAttackButton();
            }
            else if (SettlementManager.Ins != null)
            {
                SettlementManager.Ins.SelectSettlement(targetZone);
            }
        }
    }
}
