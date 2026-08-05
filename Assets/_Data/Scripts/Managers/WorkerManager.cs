using UnityEngine;
using System.Collections.Generic;

/*
 * WorkerManager.cs
 * Folder: Scripts/Managers/
 * Dự án: KHẨN HOANG (PENTA DEV)
 *
 * CHỨC NĂNG:
 * Quản lý danh sách các AI Worker hiện đang có trong game.
 * Phục vụ cho tính năng Save/Load trạng thái Worker (vị trí, tài nguyên đang vác).
 */
public class WorkerManager : Singleton<WorkerManager>
{
    public class WorkerRef
    {
        public GameObject workerObj;
        public string type;
    }

    private List<WorkerRef> activeWorkers = new List<WorkerRef>();

    public void RegisterWorker(GameObject worker, string type)
    {
        if (worker == null) return;
        activeWorkers.Add(new WorkerRef { workerObj = worker, type = type });
    }

    public void UnregisterWorker(GameObject worker)
    {
        if (worker == null) return;
        activeWorkers.RemoveAll(w => w.workerObj == worker);
    }

    public List<WorkerState> GetAllStates()
    {
        List<WorkerState> states = new List<WorkerState>();
        foreach (var w in activeWorkers)
        {
            if (w.workerObj == null) continue;

            WorkerState state = new WorkerState();
            state.workerType = w.type;
            state.position = new SerializableVector3(w.workerObj.transform.position);
            state.rotation = new SerializableVector3(w.workerObj.transform.eulerAngles);

            // Kiểm tra trạng thái đang cầm đồ tùy theo loại worker
            if (w.type == "Tree")
            {
                var carry = w.workerObj.GetComponent<WorkerCarryItem>();
                state.isCarryingItem = (carry != null && carry.IsCarrying());
            }
            else if (w.type == "Rice")
            {
                var carry = w.workerObj.GetComponent<WorkerCarryRice>();
                state.isCarryingItem = (carry != null && carry.IsCarrying());
            }
            else if (w.type == "Stone")
            {
                var carry = w.workerObj.GetComponent<WorkerCarryStone>();
                state.isCarryingItem = (carry != null && carry.IsCarrying());
            }

            states.Add(state);
        }
        return states;
    }

    public void LoadStates(List<WorkerState> states)
    {
        if (states == null || states.Count == 0) return;

        // Dọn dẹp công nhân cũ trước khi load
        foreach (var w in activeWorkers)
        {
            if (w.workerObj != null) Destroy(w.workerObj);
        }
        activeWorkers.Clear();

        if (WorkerSpawner.Instance == null)
        {
            Debug.LogError("[WorkerManager] Không tìm thấy WorkerSpawner!");
            return;
        }

        foreach (var state in states)
        {
            WorkerSpawner.WorkerType type;
            if (state.workerType == "Tree") type = WorkerSpawner.WorkerType.Tree;
            else if (state.workerType == "Rice") type = WorkerSpawner.WorkerType.Rice;
            else if (state.workerType == "Stone") type = WorkerSpawner.WorkerType.Stone;
            else continue;

            // Spawn worker tại vị trí đã lưu, không cần scatter radius
            Vector3 pos = state.position.ToVector3();
            GameObject newWorker = WorkerSpawner.Instance.SpawnWorker(type, pos, 0f);
            
            if (newWorker != null)
            {
                newWorker.transform.eulerAngles = state.rotation.ToVector3();
                
                // Nếu worker đang cầm đồ, ta ép nó nhặt lại 1 vật phẩm ảo để cầm
                if (state.isCarryingItem)
                {
                    if (type == WorkerSpawner.WorkerType.Tree)
                    {
                        var carry = newWorker.GetComponent<WorkerCarryItem>();
                        if (carry != null) carry.PickUpFakeItemForLoad();
                    }
                    else if (type == WorkerSpawner.WorkerType.Rice)
                    {
                        var carry = newWorker.GetComponent<WorkerCarryRice>();
                        if (carry != null) carry.PickUpFakeItemForLoad();
                    }
                    else if (type == WorkerSpawner.WorkerType.Stone)
                    {
                        var carry = newWorker.GetComponent<WorkerCarryStone>();
                        if (carry != null) carry.PickUpFakeItemForLoad();
                    }
                }
            }
        }
        Debug.Log($"[WorkerManager] Đã load {states.Count} worker.");
    }
}
