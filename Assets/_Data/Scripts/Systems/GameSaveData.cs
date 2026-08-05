using System;
using System.Collections.Generic;

/*
 * GameSaveData.cs
 * Folder: Scripts/Systems/Json/
 * Người làm: DŨNG
 *
 * Class tổng hợp TOÀN BỘ dữ liệu cần lưu vào file JSON
 * Được JsonDataManager.SaveGame() serialize → file
 * Được JsonDataManager.LoadGame() deserialize ← file
 *
 * Quan hệ:
 *   BuildingManager.GetAllStates() → buildings
 *   ResourceManager.GetAllData()   → resources
 *   JsonDataManager.SaveGame(GameSaveData)
 *   JsonDataManager.LoadGame() → GameSaveData
 *
 * KHÔNG kế thừa MonoBehaviour – class thuần C#
 */

[Serializable]
public class GameSaveData
{
    // ── META ────────────────────────────────────
    public string sceneName;        // Tên scene đang chơi
    public long savedAtUnix;      // Thời điểm lưu (Unix timestamp)

    // ── CÔNG TRÌNH ──────────────────────────────
    public List<BuildingState> buildings = new List<BuildingState>();

    // ── TÀI NGUYÊN ──────────────────────────────
    public List<ResourceData> resources = new List<ResourceData>();

    // ── AI WORKER ───────────────────────────────
    public List<WorkerState> workers = new List<WorkerState>();

    // ── NODE TÀI NGUYÊN TRÊN BẢN ĐỒ ─────────────
    public List<ResourceEntityState> resourceEntities = new List<ResourceEntityState>();
}

[Serializable]
public class WorkerState
{
    public string workerType; // "Tree", "Stone", "Rice"
    public SerializableVector3 position;
    public SerializableVector3 rotation;
    public bool isCarryingItem; 
}

[Serializable]
public class ResourceEntityState
{
    public SerializableVector3 position; // Dùng toạ độ làm ID định danh
    public string resourceType;          // "Tree", "Stone", "Rice"
    public int currentHealth;
    public bool isVisible;               // Trạng thái đã bị khai thác hết và đang chờ hồi sinh chưa
}