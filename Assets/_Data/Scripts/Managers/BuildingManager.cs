using UnityEngine;
using System.Collections.Generic;

/*
 * BuildingManager.cs
 * Folder: Scripts/Managers/
 * Người làm: DŨNG (Logic) + ĐĂNG (Kiến trúc & Singleton Master)
 * Dự án: KHẨN HOANG (PENTA DEV)
 *
 * NHIỆM VỤ: Quản lý tập trung toàn bộ thực thể công trình (BuildingCtrl) trong scene.
 * KIẾN TRÚC: Kế thừa Generic Singleton<T> – truy cập toàn cục qua BuildingManager.Ins
 *
 * API CHUẨN (class khác phải dùng đúng tên):
 *   AddBuilding / RemoveBuilding   – đăng ký / huỷ đăng ký
 *   FindAvailable(type)            – tìm công trình sẵn sàng cho worker
 *   CanBuild(pos, type, ignore)    – kiểm tra vị trí có bị chồng không
 *   GetAllStates()                 – gom trạng thái để save
 *   LoadStates(states)             – restore từ save
 */

public class BuildingManager : Singleton<BuildingManager>
{
    // ================= DATA =================

    private readonly List<BuildingCtrl> buildings = new List<BuildingCtrl>();

    /// <summary>
    /// ReadOnly để các hệ thống khác (AI, UI) duyệt mà không sửa trực tiếp danh sách.
    /// </summary>
    public IReadOnlyList<BuildingCtrl> Buildings => buildings;


    // ================= ĐĂNG KÝ / HUỶ ĐĂNG KÝ =================

    /// <summary>
    /// Đăng ký công trình vào hệ thống. Gọi tự động từ BuildingCtrl.Start().
    /// </summary>
    public void AddBuilding(BuildingCtrl building)
    {
        if (building == null) return;
        if (buildings.Contains(building)) return;

        if (building.buildingType == BuildingType.None)
        {
            Debug.LogError($"[BuildingManager] ❌ BuildingType chưa thiết lập (None) trên: {building.gameObject.name}");
            return;
        }

        buildings.Add(building);
        Debug.Log($"[BuildingManager] ➕ Đã đăng ký: {building.buildingType} ({building.gameObject.name})");
    }

    /// <summary>
    /// Gỡ công trình khỏi danh sách. Gọi tự động từ BuildingCtrl.OnDestroy().
    /// </summary>
    public void RemoveBuilding(BuildingCtrl building)
    {
        if (building == null) return;
        if (!buildings.Contains(building)) return;

        buildings.Remove(building);
        Debug.Log($"[BuildingManager] ➖ Đã xoá: {building.buildingType} ({building.gameObject.name})");
    }


    // ================= TÌM KIẾM =================

    /// <summary>
    /// Tìm công trình đầu tiên thuộc loại chỉ định đang sẵn sàng (đã xây xong và không có worker).
    /// Dùng cho AI Nông dân: tìm Kitchen để giao lương thực, tìm Warehouse để cất tài nguyên...
    /// </summary>
    public BuildingCtrl FindAvailable(BuildingType type)
    {
        foreach (var b in buildings)
        {
            if (b != null && b.buildingType == type && b.IsAvailable)
                return b;
        }
        return null;
    }

    /// <summary>
    /// Lấy tất cả công trình thuộc loại chỉ định (bất kể trạng thái).
    /// </summary>
    public List<BuildingCtrl> FindAll(BuildingType type)
    {
        var result = new List<BuildingCtrl>();
        foreach (var b in buildings)
        {
            if (b != null && b.buildingType == type)
                result.Add(b);
        }
        return result;
    }


    // ================= SAVE / LOAD =================

    /// <summary>
    /// Gom trạng thái tất cả công trình để lưu JSON.
    /// Gọi từ BuildingSystem.SaveBuildings() hoặc JsonDataManager.
    /// </summary>
    public List<BuildingState> GetAllStates()
    {
        var states = new List<BuildingState>();
        HashSet<GameObject> processedObj = new HashSet<GameObject>();

        // 1. Quét tất cả UpgradeableBuilding trong Scene
        UpgradeableBuilding[] ubs = Object.FindObjectsByType<UpgradeableBuilding>(FindObjectsSortMode.None);
        foreach (var ub in ubs)
        {
            if (ub == null || !ub.gameObject.activeInHierarchy) continue;

            // Bỏ qua các object ghost / preview
            if (ub.GetComponentInParent<GhostBuilding>() != null || ub.GetComponent<GhostBuilding>() != null) continue;
            string gName = ub.gameObject.name.ToLower();
            if (gName.Contains("ghost") || gName.Contains("ghot")) continue;

            if (processedObj.Contains(ub.gameObject)) continue;
            processedObj.Add(ub.gameObject);

            BuildingCtrl bCtrl = ub.GetComponent<BuildingCtrl>();
            BuildingState state = bCtrl != null ? bCtrl.ToState() : new BuildingState
            {
                buildingType = ub.buildingType,
                prefabName = ub.gameObject.name,
                position = new SerializableVector3(ub.transform.position),
                rotation = new SerializableVector3(ub.transform.eulerAngles),
                buildProgress = 1f,
                isBuilt = true
            };

            state.slotIndex = ub.slotIndex;

            // Lấy chính xác level hiện tại của từng công trình
            SettlementZone parentZone = ub.GetComponentInParent<SettlementZone>();
            if (parentZone == null) parentZone = FindClosestZone(ub.transform.position);

            if (parentZone != null && SettlementZone.IsTownHallBuilding(ub, parentZone))
            {
                state.level = Mathf.Max(ub.CurrentLevel, parentZone.settlementLevel - 1);
            }
            else
            {
                state.level = ub.CurrentLevel;
            }

            state.isRuined = ub.IsRuined;
            state.startAsRuined = ub.StartAsRuined;
            state.isInitialBuildNeeded = ub.IsInitialBuildNeeded;

            states.Add(state);
        }

        // 2. Quét bổ sung các BuildingCtrl chưa có UpgradeableBuilding (nếu có)
        BuildingCtrl[] sceneBuildings = Object.FindObjectsByType<BuildingCtrl>(FindObjectsSortMode.None);
        foreach (var b in sceneBuildings)
        {
            if (b != null && b.gameObject.activeInHierarchy && !processedObj.Contains(b.gameObject))
            {
                processedObj.Add(b.gameObject);
                BuildingState state = b.ToState();
                states.Add(state);
            }
        }

        return states;
    }

    private SettlementZone FindClosestZone(Vector3 pos)
    {
        SettlementZone[] allZones = FindObjectsByType<SettlementZone>(FindObjectsSortMode.None);
        SettlementZone bestZone = null;
        float minDst = float.MaxValue;
        foreach (var z in allZones)
        {
            if (z != null)
            {
                Vector3 zPos = (z.townHallPoint != null) ? z.townHallPoint.position : z.transform.position;
                float dst = Vector3.Distance(zPos, pos);
                if (dst < minDst)
                {
                    minDst = dst;
                    bestZone = z;
                }
            }
        }
        return bestZone;
    }

    /// <summary>
    /// Khôi phục trạng thái công trình từ save JSON.
    /// Ưu tiên cập nhật dữ liệu cho công trình đã có sẵn trong Scene; chỉ khởi tạo mới nếu công trình chưa tồn tại.
    /// </summary>
    public void LoadStates(List<BuildingState> states)
    {
        if (states == null || states.Count == 0) return;

        UpgradeableBuilding[] existingUbs = FindObjectsByType<UpgradeableBuilding>(FindObjectsSortMode.None);

        foreach (var state in states)
        {
            if (state == null || state.buildingType == BuildingType.None) continue;

            Vector3 targetPos = state.position.ToVector3();
            SettlementZone targetZone = FindClosestZone(targetPos);
            UpgradeableBuilding targetUb = null;
            bool isTownHallState = IsTownHallState(state, targetZone);

            // Tự nâng cấp save cũ: những công trình thường từng không có
            // slotIndex sẽ được xác định lại từ vị trí của slot. Lần Save kế
            // tiếp sẽ ghi index chuẩn, nên không còn bị nhầm với Nhà Chính.
            if (!isTownHallState && state.slotIndex < 0 && targetZone != null)
            {
                int inferredSlotIndex = targetZone.GetSlotIndexAtPosition(targetPos);
                if (inferredSlotIndex >= 0)
                {
                    state.slotIndex = inferredSlotIndex;
                }
            }

            // 1. Nếu là công trình trong slot (slotIndex >= 0): Ưu tiên khớp theo slotIndex trong Vùng đất tương ứng
            if (targetZone != null && state.slotIndex >= 0)
            {
                targetUb = targetZone.GetBuildingAtSlot(state.slotIndex);
            }

            // 2. Nếu là Nhà Chính: khớp thẳng với Nhà Chính của Vùng đất tương ứng.
            // Không được dùng riêng slotIndex < 0 ở đây: các save cũ và một
            // số công trình có sẵn trước đây chưa được gán slotIndex, khiến
            // chúng bị nạp nhầm đè lên Nhà Chính sau khi quay từ SceneBattle.
            if (targetUb == null && isTownHallState)
            {
                if (targetZone.townHallBuilding != null)
                {
                    targetUb = targetZone.townHallBuilding;
                }
                else
                {
                    UpgradeableBuilding[] zoneUbs = targetZone.GetComponentsInChildren<UpgradeableBuilding>(true);
                    foreach (var ub in zoneUbs)
                    {
                        if (ub != null && SettlementZone.IsTownHallBuilding(ub, targetZone))
                        {
                            targetUb = ub;
                            break;
                        }
                    }
                }
            }

            // 3. Fallback khớp theo khoảng cách trong Vùng đất tương ứng hoặc toàn Scene
            if (targetUb == null && targetZone != null)
            {
                UpgradeableBuilding[] zoneUbs = targetZone.GetComponentsInChildren<UpgradeableBuilding>(true);
                foreach (var ub in zoneUbs)
                {
                    if (ub != null && ub.gameObject.activeInHierarchy && ub.buildingType == state.buildingType)
                    {
                        if (Vector3.Distance(ub.transform.position, targetPos) < 4.5f)
                        {
                            targetUb = ub;
                            break;
                        }
                    }
                }
            }

            if (targetUb == null)
            {
                foreach (var ub in existingUbs)
                {
                    if (ub != null && ub.gameObject.activeInHierarchy && ub.buildingType == state.buildingType)
                    {
                        if (Vector3.Distance(ub.transform.position, targetPos) < 4.5f)
                        {
                            targetUb = ub;
                            break;
                        }
                    }
                }
            }

            // 4. Nếu đã có sẵn ➔ Cập nhật dữ liệu Level & State, KHÔNG SPAWN MỚI
            if (targetUb != null)
            {
                targetUb.slotIndex = state.slotIndex;
                BuildingCtrl bCtrl = targetUb.GetComponent<BuildingCtrl>();
                if (bCtrl != null) bCtrl.FromState(state);

                bool isReturningFromBattle = BattleData.HasData || BattleData.HasResult || BattleData.LastBattleWasVictory;
                bool initBuildNeeded = isReturningFromBattle ? false : state.isInitialBuildNeeded;

                if (targetZone != null)
                {
                    targetZone.LoadSettlementState();
                    if (SettlementZone.IsTownHallBuilding(targetUb, targetZone))
                    {
                        targetZone.townHallBuilding = targetUb;
                        int townHallLevel = Mathf.Max(targetZone.settlementLevel - 1, state.level);
                        targetZone.settlementLevel = townHallLevel + 1;
                        targetUb.LoadBuildingData(townHallLevel, state.isRuined, initBuildNeeded);
                        targetZone.SaveSettlementState();
                    }
                    else
                    {
                        targetUb.LoadBuildingData(state.level, state.isRuined, initBuildNeeded);
                        targetUb.transform.SetParent(targetZone.transform, true);
                        if (state.slotIndex >= 0)
                        {
                            targetUb.transform.position = targetZone.GetSlotWorldPosition(state.slotIndex);
                        }
                        targetZone.RegisterBuilding(targetUb);
                    }
                }
                else
                {
                    targetUb.LoadBuildingData(state.level, state.isRuined, initBuildNeeded);
                }

                Debug.Log($"[BuildingManager] 🔄 Đã khôi phục dữ liệu cho {state.buildingType} tại {targetZone?.settlementName} slot {state.slotIndex} (Level {state.level}).");
            }
            else
            {
                // 5. Nếu chưa có trong Scene ➔ Khởi tạo mới từ Prefab tại ô Slot của Vùng đất tương ứng
                Vector3 spawnPos = (targetZone != null && state.slotIndex >= 0) ? targetZone.GetSlotWorldPosition(state.slotIndex) : targetPos;

                Transform targetParent = targetZone != null ? targetZone.transform : (SettlementManager.Ins != null && SettlementManager.Ins.CurrentSettlement != null ? SettlementManager.Ins.CurrentSettlement.transform : null);
                GameObject prefab = ConstructionManager.Ins != null ? ConstructionManager.Ins.GetPrefab(state.buildingType) : null;
                BuildingCtrl spawned = null;

                if (prefab != null)
                {
                    GameObject obj = Instantiate(prefab, spawnPos, Quaternion.Euler(state.rotation.ToVector3()), targetParent);
                    obj.name = state.buildingType.ToString();
                    spawned = obj.GetComponent<BuildingCtrl>();
                }

                if (spawned != null)
                {
                    spawned.FromState(state);
                    UpgradeableBuilding upgradeable = spawned.GetComponent<UpgradeableBuilding>();
                    if (upgradeable == null) upgradeable = spawned.GetComponentInChildren<UpgradeableBuilding>();

                    if (upgradeable != null)
                    {
                        upgradeable.slotIndex = state.slotIndex;

                        bool isReturningFromBattle = BattleData.HasData || BattleData.HasResult || BattleData.LastBattleWasVictory;
                        bool initBuildNeeded = isReturningFromBattle ? false : state.isInitialBuildNeeded;

                        if (targetZone != null)
                        {
                            targetZone.LoadSettlementState();
                            upgradeable.transform.SetParent(targetZone.transform, true);
                            if (SettlementZone.IsTownHallBuilding(upgradeable, targetZone))
                            {
                                targetZone.townHallBuilding = upgradeable;
                                int townHallLevel = Mathf.Max(targetZone.settlementLevel - 1, state.level);
                                targetZone.settlementLevel = townHallLevel + 1;
                                upgradeable.LoadBuildingData(townHallLevel, state.isRuined, initBuildNeeded);
                                targetZone.SaveSettlementState();
                            }
                            else
                            {
                                upgradeable.LoadBuildingData(state.level, state.isRuined, initBuildNeeded);
                                targetZone.RegisterBuilding(upgradeable);
                            }
                        }
                        else
                        {
                            upgradeable.LoadBuildingData(state.level, state.isRuined, initBuildNeeded);
                        }
                    }
                    Debug.Log($"[BuildingManager] ➕ Đã tái tạo mới {state.buildingType} tại {targetZone?.settlementName} slot {state.slotIndex}.");
                }
            }
        }

        // 🔥 Nạp & Đăng ký đầy đủ công trình cho TẤT CẢ các Vùng đất ngay lập tức
        SettlementZone[] allSceneZones = FindObjectsByType<SettlementZone>(FindObjectsSortMode.None);
        foreach (var z in allSceneZones)
        {
            if (z != null)
            {
                z.EnsureAllBuildingsRegistered();
                z.AlignBuildingsToSlotPositions();
            }
        }

        if (SettlementSidePanelUI.Ins != null)
        {
            SettlementSidePanelUI.Ins.UpdateHeaderVisual();
            SettlementSidePanelUI.Ins.RefreshPanel();
        }
    }

    /// <summary>
    /// Nhận diện bản ghi Nhà Chính theo vị trí thực, thay vì suy luận tất cả
    /// slotIndex âm đều là Nhà Chính. Cách này vẫn đọc đúng các file save cũ
    /// chưa lưu index cho công trình thường.
    /// </summary>
    private static bool IsTownHallState(BuildingState state, SettlementZone zone)
    {
        if (state == null || zone == null || state.slotIndex >= 0) return false;

        Vector3 savedPosition = state.position.ToVector3();

        if (zone.townHallBuilding != null &&
            Vector3.Distance(savedPosition, zone.townHallBuilding.transform.position) < 4.5f)
        {
            return true;
        }

        Transform townHallAnchor = zone.townHallPoint != null ? zone.townHallPoint : zone.transform;
        return state.buildingType == BuildingType.House && townHallAnchor != null &&
               Vector3.Distance(savedPosition, townHallAnchor.position) < 4.5f;
    }

    /// <summary>Phá hủy toàn bộ công trình hiện có – chỉ gọi trước LoadStates().</summary>
    private void ClearAll()
    {
        HashSet<GameObject> toDestroy = new HashSet<GameObject>();

        UpgradeableBuilding[] allUbs = FindObjectsByType<UpgradeableBuilding>(FindObjectsSortMode.None);
        foreach (var ub in allUbs)
        {
            if (ub != null && ub.gameObject != null)
            {
                if (ub.GetComponentInParent<GhostBuilding>() != null || ub.GetComponent<GhostBuilding>() != null) continue;
                string gName = ub.gameObject.name.ToLower();
                if (gName.Contains("ghost") || gName.Contains("ghot")) continue;

                toDestroy.Add(ub.gameObject);
            }
        }

        BuildingCtrl[] allBuildingsInScene = FindObjectsByType<BuildingCtrl>(FindObjectsSortMode.None);
        foreach (var b in allBuildingsInScene)
        {
            if (b != null && b.gameObject != null)
            {
                toDestroy.Add(b.gameObject);
            }
        }

        foreach (var obj in toDestroy)
        {
            if (obj != null)
            {
                obj.SetActive(false);
                if (Application.isPlaying) Destroy(obj);
                else DestroyImmediate(obj);
            }
        }

        buildings.Clear();

        if (SettlementManager.Ins != null && SettlementManager.Ins.CurrentSettlement != null)
        {
            if (SettlementManager.Ins.CurrentSettlement.builtStructures != null)
            {
                SettlementManager.Ins.CurrentSettlement.builtStructures.Clear();
            }
        }

        Debug.Log("[BuildingManager] 🗑️ Đã dọn sạch toàn bộ công trình trong scene.");
    }


    // ================= KIỂM TRA VỊ TRÍ =================

    /// <summary>
    /// Kiểm tra vị trí có thể xây được không (không chồng lên công trình khác).
    ///
    /// Lưu ý: kiểm tra với TẤT CẢ công trình đang tồn tại (kể cả đang xây dở),
    /// không chỉ những công trình IsAvailable – tránh chồng lấn khi đặt mới.
    ///
    /// ignoreBuilding: bỏ qua chính nó khi BuildingCtrl.Start() tự đăng ký.
    /// </summary>
    public bool CanBuild(Vector3 position, BuildingType buildingType, BuildingCtrl ignoreBuilding = null)
    {
        Bounds testBounds = new Bounds(position, Vector3.one); // Kích thước tạm – GhostBuilding dùng OverlapBox chính xác hơn

        foreach (var b in buildings)
        {
            if (b == null || b == ignoreBuilding) continue;

            Collider col = b.GetComponent<Collider>();
            if (col == null) continue;

            // Cập nhật kích thước test theo collider thực tế của công trình đã có
            Bounds testBoundsActual = new Bounds(position, col.bounds.size);
            if (col.bounds.Intersects(testBoundsActual))
                return false;
        }

        return true;
    }
}
