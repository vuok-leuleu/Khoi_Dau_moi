using UnityEngine;
using System.Collections.Generic;

/*
 * BuildingCtrl.cs
 * Folder: Scripts/Building/
 * Người làm: DŨNG
 *
 * Thực thể đại diện cho 1 công trình trong scene.
 * Tự đăng ký/huỷ đăng ký với BuildingManager.
 */

public class BuildingCtrl : MonoBehaviour
{
    [Header("Cấu hình công trình")]
    public BuildingType buildingType = BuildingType.None;

    [Tooltip("Sức chứa worker tối đa của công trình này")]
    public int maxWorkers = 2;

    [Tooltip("Tốc độ sản xuất tài nguyên (nếu có, đơn vị/giây)")]
    public float productionRate = 1f;

    [Tooltip("Góc xoay cố định khi đặt (độ). 0, 90, 180, 270")]
    public float fixedYRotation = 0f;

    // ================= STATE =================
    private readonly System.Collections.Generic.List<WorkerCtrl> assignedWorkers
        = new System.Collections.Generic.List<WorkerCtrl>();

    public IReadOnlyList<WorkerCtrl> AssignedWorkers => assignedWorkers;
    public int CurrentWorkerCount => assignedWorkers.Count;
    public bool IsFull => assignedWorkers.Count >= maxWorkers;
    public bool IsAvailable => !IsFull;
    public bool IsBuilt => true;

    public void AddProgress(float progress)
    {
    }

    public Vector3 Position => transform.position;
    public float CurrentYRotation => NormalizeAngle(transform.eulerAngles.y);

    private void Start()
    {
        if (BuildingManager.HasInstance)
        {
            BuildingManager.Ins?.AddBuilding(this);
        }
        else if (BuildingManager.Ins != null)
        {
            BuildingManager.Ins.AddBuilding(this);
        }
    }

    private void OnDestroy()
    {
        if (BuildingManager.HasInstance)
        {
            BuildingManager.Ins?.RemoveBuilding(this);
        }
    }

    public void AssignWorker(WorkerCtrl worker)
    {
        if (!IsAvailable) return;
        if (worker == null) return;
        if (assignedWorkers.Contains(worker)) return;

        assignedWorkers.Add(worker);
    }

    public void RemoveWorker(WorkerCtrl worker)
    {
        if (worker == null) return;
        assignedWorkers.Remove(worker);
    }

    public BuildingState ToState()
    {
        UpgradeableBuilding ub = GetComponent<UpgradeableBuilding>();
        if (ub == null) ub = GetComponentInChildren<UpgradeableBuilding>();

        SpawnSoldier spawner = GetComponent<SpawnSoldier>();
        if (spawner == null) spawner = GetComponentInChildren<SpawnSoldier>();

        int sCount = spawner != null ? spawner.GetCurrentActiveSoldierCount() : 0;

        return new BuildingState
        {
            buildingType = buildingType,
            prefabName = gameObject.name,
            position = new SerializableVector3(transform.position),
            rotation = new SerializableVector3(transform.eulerAngles),
            buildProgress = 1f,
            isBuilt = IsBuilt,
            isOccupied = IsFull,
            currentWorkers = CurrentWorkerCount,
            maxWorkers = maxWorkers,
            level = ub != null ? ub.CurrentLevel : 0,
            slotIndex = ub != null ? ub.slotIndex : -1,
            soldierCount = sCount
        };
    }

    public BuildingCtrlState GetState()
    {
        return new BuildingCtrlState
        {
            buildingType = buildingType,
            position = transform.position,
            yRotation = CurrentYRotation,
            assignedWorkerCount = assignedWorkers.Count,
        };
    }

    public void FromState(BuildingState state)
    {
        if (state == null) return;

        transform.position = state.position.ToVector3();
        transform.rotation = Quaternion.Euler(state.rotation.ToVector3());

        UpgradeableBuilding ub = GetComponent<UpgradeableBuilding>();
        if (ub == null) ub = GetComponentInChildren<UpgradeableBuilding>();
        if (ub != null)
        {
            ub.slotIndex = state.slotIndex;
        }

        SpawnSoldier spawner = GetComponent<SpawnSoldier>();
        if (spawner == null) spawner = GetComponentInChildren<SpawnSoldier>();
        if (spawner != null && state.soldierCount > 0)
        {
            int lvl = ub != null ? ub.CurrentLevel + 1 : 1;
            spawner.LoadAndSpawnSoldiers(state.soldierCount, lvl - 1);
        }
    }

    public void RestoreState(BuildingCtrlState state)
    {
        if (state == null) return;

        transform.position = state.position;
        transform.rotation = Quaternion.Euler(0f, state.yRotation, 0f);
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0f) angle += 360f;

        // Snap về góc chuẩn 0, 90, 180, 270 để tránh sai số float
        if (Mathf.Abs(angle - 0f) < 5f || Mathf.Abs(angle - 360f) < 5f) return 0f;
        if (Mathf.Abs(angle - 90f) < 5f) return 90f;
        if (Mathf.Abs(angle - 180f) < 5f) return 180f;
        if (Mathf.Abs(angle - 270f) < 5f) return 270f;

        return angle;
    }
}

[System.Serializable]
public class BuildingCtrlState
{
    public BuildingType buildingType;
    public Vector3 position;
    public float yRotation;
    public int assignedWorkerCount;
}