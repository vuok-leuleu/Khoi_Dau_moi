using System;

/*
 * BuildingState.cs
 * Folder: Scripts/Building/
 * Người làm: DŨNG
 *
 * Snapshot trạng thái 1 công trình tại thời điểm save.
 * Dùng trong GameSaveData.buildings (List<BuildingState>).
 *
 * Luồng:
 *   BuildingCtrl.ToState()          → tạo ra BuildingState
 *   BuildingManager.GetAllStates()  → gom List<BuildingState>
 *   JsonDataManager.SaveGame()      → lưu vào JSON
 *   JsonDataManager.LoadGame()      → đọc từ JSON
 *   BuildingCtrl.FromState()        → restore lại scene
 *
 * KHÔNG kế thừa MonoBehaviour – class thuần C#.
 */

[Serializable]
public class BuildingState
{
    // ── ĐỊNH DANH ────────────────────────────────
    public BuildingType buildingType;   // Loại công trình
    public string prefabName;           // Tên prefab để load lại

    // ── VỊ TRÍ & XOAY ────────────────────────────
    public SerializableVector3 position;    // Vị trí đặt trong scene
    public SerializableVector3 rotation;    // Góc xoay (bội số 90°)

    // ── TRẠNG THÁI ───────────────────────────────
    public float buildProgress;     // Tiến độ xây: 0.0 → 1.0
    public bool isBuilt;            // Đã xây xong chưa
    public bool isOccupied;         // Có worker đang làm việc không
    public int currentWorkers;      // Số worker hiện tại của công trình
    public int maxWorkers;          // Số worker tối đa của công trình
    public int level;               // Cấp độ công trình (dùng khi có nâng cấp)
    
    // ── THUỘC TÍNH NÂNG CAO (TUTORIAL / LƯU TRẠNG THÁI) ─────────
    public bool isRuined;
    public bool startAsRuined;
    public bool isInitialBuildNeeded;
    public int slotIndex = -1;           // Index của ô Slot 3D (0, 1, 2, 3...)
    public int soldierCount = 0;         // Số lượng lính riêng thuộc về Doanh Trại này
}