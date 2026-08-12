using System.Collections.Generic;
using UnityEngine;

/*
 * SettlementManager.cs
 * Folder: Scripts/Settlement/
 * Dự án: KHẨN HOANG (PENTA DEV)
 * Phong cách: Demacia Rising Multi-Settlement Manager
 */

public class SettlementManager : Singleton<SettlementManager>
{
    [Header("=== DANH SÁCH CÁC VÙNG ĐẤT / ẢI TRÊN BẢN ĐỒ ===")]
    [SerializeField] private List<SettlementZone> allSettlements = new List<SettlementZone>();
    [SerializeField] private SettlementZone currentSettlement;

    public SettlementZone CurrentSettlement => currentSettlement;
    public List<SettlementZone> AllSettlements => allSettlements;

    private void Start()
    {
        InitializeSettlements();
    }

    public void InitializeSettlements()
    {
        if (allSettlements.Count == 0)
        {
            allSettlements.AddRange(FindObjectsOfType<SettlementZone>());
        }

        if (currentSettlement == null && allSettlements.Count > 0)
        {
            currentSettlement = allSettlements[0];
        }

        Debug.Log($"[SettlementManager] Khởi tạo thành công {allSettlements.Count} vùng đất. Vùng đất hiện tại: {(currentSettlement != null ? currentSettlement.settlementName : "None")}");
    }

    /// <summary>
    /// Tìm Vùng đất theo Cấp bậc (Tier 0 = ZEFFIRA, Tier 1 = Ải 1, ...)
    /// </summary>
    public SettlementZone GetZoneByTier(int tier)
    {
        foreach (var z in allSettlements)
        {
            if (z != null && z.GetEffectiveTier() == tier) return z;
        }
        return null;
    }

    /// <summary>
    /// Tìm Vùng đất theo Tên (settlementName)
    /// </summary>
    public SettlementZone GetZoneByName(string name)
    {
        if (allSettlements != null)
        {
            foreach (var z in allSettlements)
            {
                if (z != null && z.settlementName == name) return z;
            }
        }
        return null;
    }

    /// <summary>
    /// Lưu trạng thái PlayerPrefs cho TẤT CẢ các Vùng đất hiện có
    /// </summary>
    public void SaveAllSettlementsState()
    {
        if (allSettlements == null) return;
        foreach (var zone in allSettlements)
        {
            if (zone != null)
            {
                zone.SaveSettlementState();
            }
        }
        PlayerPrefs.Save();
        Debug.Log("[SettlementManager] 💾 Đã lưu trạng thái toàn bộ Vùng đất vào PlayerPrefs.");
    }

    /// <summary>
    /// Cập nhật ẩn/hiện toàn bộ các Vùng đất theo Cấp bậc Tier (Bậc N chỉ hiện khi Bậc N-1 được giải phóng)
    /// </summary>
    public void UpdateAllZoneTiers()
    {
        if (allSettlements == null || allSettlements.Count == 0) return;

        allSettlements.Sort((a, b) => a.GetEffectiveTier().CompareTo(b.GetEffectiveTier()));

        foreach (var zone in allSettlements)
        {
            if (zone != null)
            {
                zone.UpdateZoneTierVisibility();
            }
        }
    }

    /// <summary>
    /// Chọn vùng đất active hiện tại khi người chơi click trên bản đồ
    /// </summary>
    public void SelectSettlement(SettlementZone zone)
    {
        if (zone == null) return;

        currentSettlement = zone;
        Debug.Log($"[SettlementManager] Đã chọn vùng đất: {currentSettlement.settlementName} (Đã có nhà chính: {currentSettlement.isTownHallEstablished})");

        if (UIManager.Ins != null)
        {
            UIManager.Ins.OpenSettlementPanel();
        }
        else if (SettlementSidePanelUI.Ins != null)
        {
            SettlementSidePanelUI.Ins.gameObject.SetActive(true);
            SettlementSidePanelUI.Ins.RefreshPanel();
        }
    }
}
