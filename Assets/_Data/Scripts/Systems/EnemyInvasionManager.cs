using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * EnemyInvasionManager.cs
 * Quản lý Đợt Tấn Công của Địch nhắm vào Vùng Đất ngẫu nhiên đã xây Nhà Chính.
 * Khoảng cách đỗ quân: ~3m trước căn cứ.
 * Thời gian đếm ngược: 3 Wave (3 Ngày). Nếu quá 3 Ngày không phòng thủ -> Phòng thủ thua (Mất lính vùng đó & 50% tài nguyên).
 */

public class EnemyInvasionManager : MonoBehaviour
{
    public static EnemyInvasionManager Ins { get; private set; }

    [Header("Current Invasion Info")]
    public SettlementZone currentTargetedZone;
    public bool isInvasionActive = false;
    public bool isEnemiesArrivedAtTarget = false;
    public int remainingDefenseWaves = 3;
    public Vector3 invasionTargetPosition;

    private void Awake()
    {
        if (Ins == null)
        {
            Ins = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        if (DayNightManager.Ins != null)
        {
            DayNightManager.Ins.OnWaveStart += OnWaveStartHandler;
        }
    }

    private void OnDisable()
    {
        if (DayNightManager.Ins != null)
        {
            DayNightManager.Ins.OnWaveStart -= OnWaveStartHandler;
        }
    }

    /// <summary>
    /// Khởi động một đợt tấn công ngẫu nhiên nhắm vào Vùng Đất đã có Nhà Chính
    /// </summary>
    public SettlementZone PickRandomEstablishedTargetZone()
    {
        List<SettlementZone> establishedZones = new List<SettlementZone>();

        if (SettlementManager.Ins != null && SettlementManager.Ins.AllSettlements != null)
        {
            foreach (var z in SettlementManager.Ins.AllSettlements)
            {
                if (z != null && z.isTownHallEstablished)
                {
                    establishedZones.Add(z);
                }
            }
        }

        if (establishedZones.Count == 0)
        {
            SettlementZone[] sceneZones = Object.FindObjectsByType<SettlementZone>(FindObjectsSortMode.None);
            foreach (var z in sceneZones)
            {
                if (z != null && z.isTownHallEstablished)
                {
                    establishedZones.Add(z);
                }
            }
        }

        if (establishedZones.Count > 0)
        {
            int index = Random.Range(0, establishedZones.Count);
            currentTargetedZone = establishedZones[index];
        }
        else
        {
            currentTargetedZone = SettlementManager.Ins != null ? SettlementManager.Ins.CurrentSettlement : Object.FindFirstObjectByType<SettlementZone>();
        }

        if (currentTargetedZone != null)
        {
            isInvasionActive = true;
            isEnemiesArrivedAtTarget = false;
            remainingDefenseWaves = 3;

            // Đặt mục tiêu đỗ quân cách khoảng 3m phía trước Vùng Đất
            Vector3 zonePos = currentTargetedZone.transform.position;
            Vector3 fwd = currentTargetedZone.transform.forward;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            invasionTargetPosition = zonePos + fwd * 3.0f;

            string msg = $"⚔️ CẢNH BÁO: Địch đang mở đợt tấn công nhắm vào Vùng Đất {currentTargetedZone.settlementName}!";
            if (UIManager.Ins != null) UIManager.Ins.ShowWarning(msg);
            Debug.Log($"[EnemyInvasionManager] {msg}");
        }

        return currentTargetedZone;
    }

    /// <summary>
    /// Gọi khi kẻ địch đã hành quân đến vị trí cách 3m trước Vùng đất mục tiêu
    /// </summary>
    public void NotifyEnemiesArrivedAtTarget()
    {
        if (!isInvasionActive || isEnemiesArrivedAtTarget) return;

        isEnemiesArrivedAtTarget = true;
        remainingDefenseWaves = 3;

        string zoneName = currentTargetedZone != null ? currentTargetedZone.settlementName : "Căn Cứ";
        string msg = $"🚨 Kẻ địch đã áp sát và bao vây Vùng Đất {zoneName}! Còn {remainingDefenseWaves} Ngày để cử lính xuất trận phòng thủ!";
        if (UIManager.Ins != null) UIManager.Ins.ShowWarning(msg);
        Debug.Log($"[EnemyInvasionManager] {msg}");
    }

    /// <summary>
    /// Đếm ngược số Wave / Ngày trôi qua khi kẻ địch đang bao vây
    /// </summary>
    private void OnWaveStartHandler(int waveIndex)
    {
        if (!isInvasionActive || !isEnemiesArrivedAtTarget || currentTargetedZone == null) return;

        remainingDefenseWaves--;
        string zoneName = currentTargetedZone.settlementName;

        if (remainingDefenseWaves > 0)
        {
            string msg = $"⚠️ Vùng Đất {zoneName} đang bị bao vây! Còn {remainingDefenseWaves} Ngày để phòng thủ!";
            if (UIManager.Ins != null) UIManager.Ins.ShowWarning(msg);
            Debug.Log($"[EnemyInvasionManager] {msg}");
        }
        else
        {
            // Quá 3 Wave không cử lính ra phòng thủ -> PHÒNG THỦ THUA!
            TriggerDefenseDefeat(currentTargetedZone);
        }
    }

    /// <summary>
    /// Xử lý sự kiện Phòng Thủ Thua do không ra phòng thủ hoặc đánh thua tại Vùng đất này
    /// </summary>
    public void TriggerDefenseDefeat(SettlementZone zone)
    {
        if (zone == null) zone = currentTargetedZone;
        string zoneName = zone != null ? zone.settlementName : "Căn Cứ";

        Debug.Log($"[EnemyInvasionManager] 💥 Phòng thủ THUA tại Vùng Đất {zoneName}! Mất toàn bộ lính của vùng và 50% tài nguyên.");

        // 1. Tiêu diệt toàn bộ lính thuộc về Doanh trại của Vùng đất này
        if (zone != null)
        {
            foreach (UnitController unit in Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None))
            {
                if (unit != null && unit.gameObject.activeInHierarchy && unit.IsStationedInZone(zone.settlementName))
                {
                    Destroy(unit.gameObject);
                }
            }

            SpawnSoldier[] zoneSpawners = zone.GetComponentsInChildren<SpawnSoldier>(true);
            foreach (var spawner in zoneSpawners)
            {
                if (spawner != null)
                {
                    spawner.DestroyAllSoldiers();
                }
            }
        }

        // 2. Trừ 50% tài nguyên tích lũy
        if (JsonDataManager.Ins != null)
        {
            JsonDataManager.Ins.HalveAllResources();
        }

        string warnMsg = $"💥 Vùng Đất {zoneName} PHÒNG THỦ THẤT BẠI! Đã mất lính của vùng và 50% tài nguyên!";
        if (UIManager.Ins != null) UIManager.Ins.ShowWarning(warnMsg);

        // Reset trạng thái tấn công
        isInvasionActive = false;
        isEnemiesArrivedAtTarget = false;
        currentTargetedZone = null;
    }

    /// <summary>
    /// Gọi khi người chơi Phòng Thủ Thành Công (Đánh bại đợt quái xâm lược)
    /// </summary>
    public void TriggerDefenseVictory()
    {
        string zoneName = currentTargetedZone != null ? currentTargetedZone.settlementName : "Căn Cứ";
        string msg = $"🏆 PHÒNG THỦ THÀNH CÔNG! Đã đánh đuổi kẻ địch khỏi Vùng Đất {zoneName}. Bảo toàn 100% lính và tài nguyên!";
        if (UIManager.Ins != null) UIManager.Ins.ShowWarning(msg);
        Debug.Log($"[EnemyInvasionManager] {msg}");

        isInvasionActive = false;
        isEnemiesArrivedAtTarget = false;
        currentTargetedZone = null;
    }
}
