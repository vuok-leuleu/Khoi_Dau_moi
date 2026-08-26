using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SpawnSoldier : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Danh sách các loại lính có thể sinh. Mỗi lính sẽ chọn ngẫu nhiên một prefab trong danh sách này.")]
    [SerializeField] private List<GameObject> soldierPrefabs = new List<GameObject>();
    [Tooltip("Prefab dự phòng cho dữ liệu cũ. Chỉ dùng khi danh sách Soldier Prefabs đang trống.")]
    [SerializeField] private GameObject soldierPrefab;
    [Tooltip("Bán kính phân bố vị trí lính quanh khu vực sinh lính")]
    [Range(0.5f, 10f)]
    [SerializeField] private float spawnRadius = 3.5f;
    [Tooltip("Khoảng cách đẩy vị trí sinh lính ra phía trước cổng/mặt tiền công trình (tránh bị chìm vào trong nhà)")]
    [Range(0f, 10f)]
    [SerializeField] private float spawnForwardOffset = 2.5f;

    [Header("Upgrade Settings")]
    [SerializeField] private int currentLevel = 1;

    [Header("Test Settings")]
    [SerializeField] private float testDuration = 5f;

    // Danh sách lưu các lính đã spawn để có thể xóa khi nâng cấp
    private List<GameObject> spawnedSoldiers = new List<GameObject>();
    private UpgradeableBuilding upgradeableBuilding;
    private bool isOnMainBuildingObject = false;

    [Header("Hologram Settings")]
    [SerializeField] private Material hologramMaterial;
    [SerializeField] private Color hologramColor = new Color(0f, 0.7f, 1f, 0.35f);
    private bool spawnedHolograms = false;
    private List<GameObject> spawnedHologramsList = new List<GameObject>();
    private Material dynamicHologramMaterial;
    private bool isTesting = false;
    private Coroutine hologramAnimationCoroutine;

    public float TestDuration => testDuration;

    private List<GameObject> GetConfiguredSoldierPrefabs()
    {
        List<GameObject> configuredPrefabs = new List<GameObject>();

        if (soldierPrefabs != null)
        {
            foreach (GameObject prefab in soldierPrefabs)
            {
                if (prefab != null)
                {
                    configuredPrefabs.Add(prefab);
                }
            }
        }

        if (configuredPrefabs.Count == 0 && soldierPrefab != null)
        {
            configuredPrefabs.Add(soldierPrefab);
        }

        return configuredPrefabs;
    }

    private GameObject GetRandomSoldierPrefab()
    {
        List<GameObject> configuredPrefabs = GetConfiguredSoldierPrefabs();
        if (configuredPrefabs.Count == 0)
        {
            return null;
        }

        return configuredPrefabs[Random.Range(0, configuredPrefabs.Count)];
    }

    private GameObject GetSoldierPrefabByIndex(int soldierTypeIndex)
    {
        List<GameObject> configuredPrefabs = GetConfiguredSoldierPrefabs();
        if (soldierTypeIndex < 0 || soldierTypeIndex >= configuredPrefabs.Count)
        {
            Debug.LogWarning($"[SpawnSoldier] Loại lính tại chỉ số {soldierTypeIndex} không tồn tại trên {gameObject.name}.");
            return null;
        }

        return configuredPrefabs[soldierTypeIndex];
    }

    private GameObject GetTrainedSoldierPrefab(BuildingType troopType)
    {
        AttackMode expectedAttackMode;
        switch (troopType)
        {
            case BuildingType.BarracksMelee:
                expectedAttackMode = AttackMode.Melee;
                break;
            case BuildingType.BarracksArcher:
                expectedAttackMode = AttackMode.Ranged;
                break;
            case BuildingType.BarracksSpear:
                expectedAttackMode = AttackMode.Tank;
                break;
            default:
                return null;
        }

        foreach (GameObject prefab in GetConfiguredSoldierPrefabs())
        {
            UnitController unit = prefab.GetComponent<UnitController>();
            if (unit == null) unit = prefab.GetComponentInChildren<UnitController>();

            if (unit != null && unit.AttackMode == expectedAttackMode)
            {
                return prefab;
            }
        }

        return null;
    }

    public bool CanSpawnTrainedSoldier(BuildingType troopType)
    {
        return GetTrainedSoldierPrefab(troopType) != null;
    }

    public List<UnitController> GetActiveSoldierControllers()
    {
        List<UnitController> list = new List<UnitController>();
        if (spawnedSoldiers != null)
        {
            foreach (var go in spawnedSoldiers)
            {
                if (go != null && go.activeInHierarchy)
                {
                    UnitController uc = go.GetComponent<UnitController>();
                    if (uc == null) uc = go.GetComponentInChildren<UnitController>();
                    if (uc != null && !list.Contains(uc)) list.Add(uc);
                }
            }
        }

        if (list.Count == 0)
        {
            UnitController[] children = GetComponentsInChildren<UnitController>();
            foreach (var uc in children)
            {
                if (uc != null && uc.gameObject.activeInHierarchy && !list.Contains(uc))
                {
                    list.Add(uc);
                }
            }
        }
        return list;
    }

    /// <summary>
    /// Gives newly trained units an explicit settlement owner.  Units are not
    /// necessarily children of their barracks, so transform hierarchy must not be
    /// used as the source of truth once they can march between settlements.
    /// </summary>
    private void AssignHomeSettlement(UnitController unit)
    {
        if (unit == null) return;

        SettlementZone ownerZone = GetComponentInParent<SettlementZone>();
        if (ownerZone != null)
        {
            unit.stationedSettlementZoneName = ownerZone.settlementName;
        }
    }

    private bool IsUnitAvailableAtThisBarracks(UnitController unit)
    {
        if (unit == null || unit.isDead || unit.isExpeditionMarching) return false;

        SettlementZone ownerZone = GetComponentInParent<SettlementZone>();
        return ownerZone == null || unit.IsStationedInZone(ownerZone.settlementName);
    }

    private bool IsBarracksBuildingType(BuildingType type)
    {
        return type == BuildingType.BarracksMelee ||
               type == BuildingType.BarracksArcher ||
               type == BuildingType.BarracksSpear ||
               type.ToString().StartsWith("Barracks");
    }

    void Awake()
    {
        UpgradeableBuilding ub = GetComponent<UpgradeableBuilding>();
        if (ub != null && IsBarracksBuildingType(ub.buildingType))
        {
            upgradeableBuilding = ub;
            isOnMainBuildingObject = true;
        }
        else
        {
            UpgradeableBuilding[] parents = GetComponentsInParent<UpgradeableBuilding>();
            foreach (var p in parents)
            {
                if (p != null && IsBarracksBuildingType(p.buildingType))
                {
                    upgradeableBuilding = p;
                    isOnMainBuildingObject = false;
                    break;
                }
            }
        }
    }

    void OnEnable()
    {
        if (upgradeableBuilding != null)
        {
            upgradeableBuilding.OnUpgradeStart += HandleUpgradeStart;
            upgradeableBuilding.OnUpgradeComplete += HandleUpgradeComplete;
            upgradeableBuilding.OnLevelChanged += HandleLevelChanged;

            if (upgradeableBuilding.IsUpgrading && !upgradeableBuilding.IsInitialBuildNeeded)
            {
                HandleUpgradeStart();
            }
            else
            {
                SyncLevel();
            }
        }
        else
        {
            // Đồng bộ Level hiện tại (lính chỉ được sinh thông qua hệ thống Ô Huấn Luyện)
            SyncLevel();
        }
    }

    void OnDisable()
    {
        if (upgradeableBuilding != null)
        {
            upgradeableBuilding.OnUpgradeStart -= HandleUpgradeStart;
            upgradeableBuilding.OnUpgradeComplete -= HandleUpgradeComplete;
            upgradeableBuilding.OnLevelChanged -= HandleLevelChanged;
        }

        // Không xóa lính thực khi OnDisable để bảo toàn lính khi nâng cấp công trình
        ClearHolograms();
    }

    [Header("Training Control")]
    public bool autoSpawnOnBuild = false; // Mặc định false: Lính sẽ được huấn luyện thông qua Ô Huấn Luyện

    private void SyncLevel()
    {
        if (upgradeableBuilding != null)
        {
            if (upgradeableBuilding.IsUpgrading) return;

            int activeLevel = upgradeableBuilding.CurrentLevel + 1;

            if (isOnMainBuildingObject)
            {
                currentLevel = activeLevel;
            }
            else
            {
                if (currentLevel != activeLevel)
                {
                    return;
                }
            }
        }

        if (autoSpawnOnBuild)
        {
            int count = GetMaxSoldiersForLevel(currentLevel);
            SpawnSoldiers(count);
        }
    }

    /// <summary>
    /// Sinh 1 lính đã hoàn tất huấn luyện tại Doanh Trại này
    /// </summary>
    public GameObject SpawnOneTrainedSoldier(GameObject customPrefab = null)
    {
        GameObject prefabToUse = customPrefab != null ? customPrefab : GetRandomSoldierPrefab();
        if (prefabToUse == null)
        {
            Debug.LogWarning($"[SpawnSoldier] ⚠️ Chưa gán Soldier Prefabs cho {gameObject.name}!");
            return null;
        }

        Vector3 baseSpawnCenter = transform.position + transform.forward * spawnForwardOffset;
        Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
        Vector3 rawPosition = new Vector3(
            baseSpawnCenter.x + randomCircle.x,
            baseSpawnCenter.y,
            baseSpawnCenter.z + randomCircle.y
        );

        Vector3 spawnPosition = rawPosition;
        if (UnityEngine.AI.NavMesh.SamplePosition(rawPosition, out UnityEngine.AI.NavMeshHit hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
        {
            spawnPosition = hit.position;
        }

        GameObject newSoldier = Instantiate(prefabToUse, spawnPosition, transform.rotation);
        newSoldier.transform.SetParent(transform, true);

        // Gán thông số Level và Damage
        float damage = GetDamageForLevel(currentLevel);
        UnitController unit = newSoldier.GetComponent<UnitController>();
        if (unit == null) unit = newSoldier.GetComponentInChildren<UnitController>();
        if (unit != null)
        {
            unit.SetHomePosition(spawnPosition);
            unit.SetAttackDamage(damage);
            AssignHomeSettlement(unit);
        }

        spawnedSoldiers.Add(newSoldier);
        Debug.Log($"[SpawnSoldier] 🎉 Đã sinh 1 lính mới từ Ô Huấn Luyện tại {gameObject.name} (Lv {currentLevel})!");
        return newSoldier;
    }

    /// <summary>
    /// Sinh một đơn vị huấn luyện với các lính cùng loại tương ứng với doanh trại.
    /// </summary>
    public int SpawnTrainedSoldiers(BuildingType troopType, int count)
    {
        GameObject prefabToUse = GetTrainedSoldierPrefab(troopType);
        if (prefabToUse == null)
        {
            Debug.LogWarning($"[SpawnSoldier] Không tìm thấy prefab phù hợp cho {troopType} trên {gameObject.name}.");
            return 0;
        }

        int spawnedCount = 0;
        for (int i = 0; i < count; i++)
        {
            if (SpawnOneTrainedSoldier(prefabToUse) != null)
            {
                spawnedCount++;
            }
        }

        return spawnedCount;
    }

    /// <summary>
    /// Sinh một lính đã hoàn tất huấn luyện theo vị trí trong danh sách Soldier Prefabs.
    /// </summary>
    public GameObject SpawnOneTrainedSoldierByType(int soldierTypeIndex)
    {
        GameObject prefabToUse = GetSoldierPrefabByIndex(soldierTypeIndex);
        return prefabToUse == null ? null : SpawnOneTrainedSoldier(prefabToUse);
    }

    private void HandleUpgradeStart()
    {
        if (isTesting) return;
        if (upgradeableBuilding == null) return;
        if (upgradeableBuilding.IsInitialBuildNeeded) return;

        int activeLevel = upgradeableBuilding.CurrentLevel + 1;
        if (currentLevel == activeLevel)
        {
            int count = GetMaxSoldiersForLevel(currentLevel);
            SpawnHologramSoldiers(count);
            StartHologramAnimationCoroutine();
        }
    }

    private void HandleUpgradeComplete()
    {
        if (isTesting) return;
        ClearHolograms();
        SyncLevel(); // Tự động spawn lính thật ngay khi nâng cấp/xây dựng hoàn tất!
    }

    private void HandleLevelChanged()
    {
        if (isTesting) return;
        ClearHolograms();
        SyncLevel(); // Tự động spawn lính thật khi level công trình thay đổi!
    }

    private void StartHologramAnimationCoroutine()
    {
        StopHologramAnimationCoroutine();
        hologramAnimationCoroutine = StartCoroutine(HologramAnimationRoutine());
    }

    private void StopHologramAnimationCoroutine()
    {
        if (hologramAnimationCoroutine != null)
        {
            StopCoroutine(hologramAnimationCoroutine);
            hologramAnimationCoroutine = null;
        }
    }

    private System.Collections.IEnumerator HologramAnimationRoutine()
    {
        while (spawnedHolograms)
        {
            UpdateHologramAnimations();
            yield return null;
        }
    }

    // Hàm ép lính quay về hoạt ảnh Idle (Dùng cho lính thật khi đứng yên)
    private void PlayIdleAnimationOnAnimator(Animator anim)
    {
        if (anim == null) return;

        foreach (AnimatorControllerParameter param in anim.parameters)
        {
            if (param.name == "IsTrain" && param.type == AnimatorControllerParameterType.Bool)
            {
                anim.SetBool("IsTrain", false);
            }
            if (param.name == "IsAttack" && param.type == AnimatorControllerParameterType.Bool)
            {
                anim.SetBool("IsAttack", false);
            }
            if (param.name == "IsShoot" && param.type == AnimatorControllerParameterType.Bool)
            {
                anim.SetBool("IsShoot", false);
            }
        }

        string[] idleStateNames = new string[] { "Idle", "IdleArcher", "IdleWalker", "Idle_Attack", "IdleCanonLv1", "IdleCanonLv2", "IdleCanonLv3" };
        bool stateFound = false;

        foreach (string stateName in idleStateNames)
        {
            int hash = Animator.StringToHash(stateName);
            if (anim.HasState(0, hash))
            {
                anim.Play(hash, 0, 0f);
                stateFound = true;
                break;
            }
        }

        if (!stateFound)
        {
            anim.Play(0, 0, 0f);
        }
    }

    // Hàm ép lính chuyển sang hoạt ảnh Train / Attack (Dùng cho lính Hologram khi đếm ngược)
    private void PlayTrainAnimationOnAnimator(Animator anim, bool isInitial = false)
    {
        if (anim == null) return;

        foreach (AnimatorControllerParameter param in anim.parameters)
        {
            if (param.name == "IsTrain" && param.type == AnimatorControllerParameterType.Bool)
            {
                anim.SetBool("IsTrain", true);
            }
        }

        string[] trainStateNames = new string[] { "Train", "ArcherAttackLv1", "Attack", "Chem", "Shoot", "AttackCanonLv1" };
        bool stateFound = false;

        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);

        foreach (string stateName in trainStateNames)
        {
            int hash = Animator.StringToHash(stateName);
            if (anim.HasState(0, hash))
            {
                if (isInitial)
                {
                    anim.Play(hash, 0, Random.Range(0f, 1f));
                }
                else
                {
                    if (stateInfo.shortNameHash == hash)
                    {
                        if (stateInfo.normalizedTime >= 0.95f && !anim.IsInTransition(0))
                        {
                            anim.Play(hash, 0, 0f);
                        }
                    }
                    else if (!anim.IsInTransition(0))
                    {
                        anim.Play(hash, 0, 0f);
                    }
                }
                stateFound = true;
                break;
            }
        }

        if (!stateFound)
        {
            if (isInitial)
            {
                anim.Play(0, 0, Random.Range(0f, 1f));
            }
            else if (stateInfo.normalizedTime >= 0.95f && !anim.IsInTransition(0))
            {
                anim.Play(0, 0, 0f);
            }
        }
    }

    // Hàm phụ trợ cập nhật hoạt ảnh Train cho danh sách Hologram (đảm bảo lặp liên tục trong suốt thời gian test/nâng cấp)
    private void UpdateHologramAnimations()
    {
        if (spawnedHologramsList == null) return;

        for (int i = 0; i < spawnedHologramsList.Count; i++)
        {
            GameObject hologram = spawnedHologramsList[i];
            if (hologram != null)
            {
                Animator anim = hologram.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    PlayTrainAnimationOnAnimator(anim, false);
                }
            }
        }
    }

    // Hàm lấy số lượng lính tối đa dựa theo Level (Lv1: 4, Lv2: 6, Lv3: 8)
    public int GetMaxSoldiersForLevel(int level)
    {
        switch (level)
        {
            case 1: return 4;
            case 2: return 6;
            case 3: return 8;
            default: return 4; // Fallback
        }
    }

    public int GetCurrentActiveSoldierCount()
    {
        if (spawnedSoldiers == null) return 0;
        int count = 0;
        foreach (var s in spawnedSoldiers)
        {
            if (s != null && s.activeInHierarchy)
            {
                UnitController uc = s.GetComponent<UnitController>();
                if (uc == null) uc = s.GetComponentInChildren<UnitController>();
                if (IsUnitAvailableAtThisBarracks(uc)) count++;
            }
        }
        return count;
    }

    public bool IsCapacityFull()
    {
        int maxAllowed = GetMaxSoldiersForLevel(currentLevel);
        return GetCurrentActiveSoldierCount() >= maxAllowed;
    }

    public void DestroyAllSoldiers()
    {
        SettlementZone ownerZone = GetComponentInParent<SettlementZone>();
        if (ownerZone == null)
        {
            ClearSpawnedSoldiers();
            return;
        }

        // spawnedSoldiers retains references after an expedition.  Never use that
        // historical list to kill a squad that has since moved to another region.
        for (int i = spawnedSoldiers.Count - 1; i >= 0; i--)
        {
            GameObject soldier = spawnedSoldiers[i];
            UnitController unit = soldier != null ? soldier.GetComponent<UnitController>() : null;
            if (unit == null && soldier != null) unit = soldier.GetComponentInChildren<UnitController>();

            if (unit != null && unit.IsStationedInZone(ownerZone.settlementName))
            {
                Destroy(soldier);
                spawnedSoldiers.RemoveAt(i);
            }
            else if (soldier == null)
            {
                spawnedSoldiers.RemoveAt(i);
            }
        }
    }

    // Hàm lấy sát thương của lính dựa theo Level
    public float GetDamageForLevel(int level)
    {
        switch (level)
        {
            case 1: return 10f;
            case 2: return 20f;
            case 3: return 50f;
            default: return 10f; // Fallback
        }
    }

    // Hàm dùng để spawn một số lượng lính nhất định
    public void SpawnSoldiers(int count)
    {
        SpawnSoldiersInternal(count, null);
    }

    /// <summary>
    /// Sinh nhiều lính cùng một loại theo vị trí trong danh sách Soldier Prefabs.
    /// </summary>
    public void SpawnSoldiersByType(int count, int soldierTypeIndex)
    {
        GameObject prefabToUse = GetSoldierPrefabByIndex(soldierTypeIndex);
        if (prefabToUse != null)
        {
            SpawnSoldiersInternal(count, prefabToUse);
        }
    }

    private void SpawnSoldiersInternal(int count, GameObject fixedPrefab)
    {
        if (fixedPrefab == null && GetRandomSoldierPrefab() == null)
        {
            Debug.LogWarning("Soldier Prefabs chưa được gán trong Inspector!");
            return;
        }

        // Tự động dọn dẹp hologram và lính cũ trước khi spawn lính mới để tránh trùng lặp
        ClearHolograms();
        ClearSpawnedSoldiers();

        int maxAllowed = GetMaxSoldiersForLevel(currentLevel);
        if (count <= 0 || count > maxAllowed)
        {
            count = maxAllowed;
        }

        float damage = GetDamageForLevel(currentLevel);
        Debug.Log($"[SpawnSoldier] {gameObject.name} (Lv {currentLevel}) đang spawn {count} lính mới với sát thương {damage}.");

        Vector3 baseSpawnCenter = transform.position + transform.forward * spawnForwardOffset;

        for (int i = 0; i < count; i++)
        {
            GameObject prefabToUse = fixedPrefab != null ? fixedPrefab : GetRandomSoldierPrefab();
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 rawPosition = new Vector3(
                baseSpawnCenter.x + randomCircle.x,
                baseSpawnCenter.y,
                baseSpawnCenter.z + randomCircle.y
            );

            Vector3 spawnPosition = rawPosition;
            if (UnityEngine.AI.NavMesh.SamplePosition(rawPosition, out UnityEngine.AI.NavMeshHit hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
            {
                spawnPosition = hit.position;
            }

            GameObject soldier = Instantiate(prefabToUse, spawnPosition, Quaternion.identity);

            UnitController unit = soldier.GetComponent<UnitController>();
            if (unit != null)
            {
                unit.SetHomePosition(spawnPosition);
                unit.SetAttackDamage(damage);
                AssignHomeSettlement(unit);
            }

            // Đảm bảo lính thực TẮT IsTrain và quay về hoạt ảnh Idle ban đầu
            Animator anim = soldier.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                PlayIdleAnimationOnAnimator(anim);
            }

            spawnedSoldiers.Add(soldier);
        }
    }

    // Hàm dọn dẹp các lính cũ đang hoạt động
    private void ClearSpawnedSoldiers()
    {
        if (spawnedSoldiers == null) return;

        // Nếu Scene đang đóng / unload thì chỉ dọn dẹp list C#, KHÔNG gọi Destroy() để tránh lỗi Scene Cleanup của Unity Engine
        if (!gameObject.scene.isLoaded)
        {
            spawnedSoldiers.Clear();
            return;
        }

        Debug.Log($"[SpawnSoldier] ClearSpawnedSoldiers được gọi trên {gameObject.name}. Danh sách đang có {spawnedSoldiers.Count} lính.");

        for (int i = spawnedSoldiers.Count - 1; i >= 0; i--)
        {
            GameObject soldier = spawnedSoldiers[i];
            if (soldier != null)
            {
                Debug.Log($"[SpawnSoldier] Hủy lính trong danh sách: {soldier.name}");
                Destroy(soldier);
            }
        }
        spawnedSoldiers.Clear();
    }

    // Hàm tạo material hologram mặc định bằng code
    private Material CreateDefaultHologramMaterial()
    {
        if (dynamicHologramMaterial != null)
        {
            return dynamicHologramMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material mat = new Material(shader);
        mat.name = "HologramMaterial_Runtime";

        if (shader.name.Contains("Universal Render Pipeline"))
        {
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f); // Alpha Blend
            mat.SetColor("_BaseColor", hologramColor);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else if (shader.name.Contains("Standard"))
        {
            mat.SetFloat("_Mode", 3f); // Transparent
            mat.SetColor("_Color", hologramColor);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            mat.color = hologramColor;
        }

        if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", hologramColor);
        }
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", hologramColor);
        }

        dynamicHologramMaterial = mat;
        return dynamicHologramMaterial;
    }

    // Hàm sinh lính hologram để chạy hiệu ứng huấn luyện
    public void SpawnHologramSoldiers(int count)
    {
        SpawnHologramSoldiersInternal(count, null);
    }

    /// <summary>
    /// Sinh lính hologram cùng một loại theo vị trí trong danh sách Soldier Prefabs.
    /// </summary>
    public void SpawnHologramSoldiersByType(int count, int soldierTypeIndex)
    {
        GameObject prefabToUse = GetSoldierPrefabByIndex(soldierTypeIndex);
        if (prefabToUse != null)
        {
            SpawnHologramSoldiersInternal(count, prefabToUse);
        }
    }

    private void SpawnHologramSoldiersInternal(int count, GameObject fixedPrefab)
    {
        if (fixedPrefab == null && GetRandomSoldierPrefab() == null)
        {
            Debug.LogWarning("Soldier Prefabs chưa được gán trong Inspector!");
            return;
        }

        // Đảm bảo xóa hologram cũ nếu có
        ClearHolograms();

        Debug.Log($"[SpawnSoldier] {gameObject.name} (Lv {currentLevel}) đang sinh {count} lính hologram để huấn luyện.");

        Material holoMat = hologramMaterial;
        if (holoMat == null)
        {
            holoMat = CreateDefaultHologramMaterial();
        }

        Vector3 baseSpawnCenter = transform.position + transform.forward * spawnForwardOffset;

        for (int i = 0; i < count; i++)
        {
            GameObject prefabToUse = fixedPrefab != null ? fixedPrefab : GetRandomSoldierPrefab();
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 rawPosition = new Vector3(
                baseSpawnCenter.x + randomCircle.x,
                baseSpawnCenter.y,
                baseSpawnCenter.z + randomCircle.y
            );

            Vector3 spawnPosition = rawPosition;
            if (UnityEngine.AI.NavMesh.SamplePosition(rawPosition, out UnityEngine.AI.NavMeshHit hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
            {
                spawnPosition = hit.position;
            }

            GameObject hologram = Instantiate(prefabToUse, spawnPosition, Quaternion.identity);
            hologram.name = $"{prefabToUse.name}_Hologram_{i}";

            UnitController unit = hologram.GetComponent<UnitController>();
            if (unit != null)
            {
                unit.enabled = false;
            }

            UnityEngine.AI.NavMeshAgent agent = hologram.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
            }

            Collider[] colliders = hologram.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                if (col != null)
                {
                    col.enabled = false;
                }
            }

            Rigidbody rb = hologram.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }

            Renderer[] renderers = hologram.GetComponentsInChildren<Renderer>(true);
            foreach (var ren in renderers)
            {
                if (ren != null && holoMat != null)
                {
                    Material[] sharedMats = ren.sharedMaterials;
                    Material[] newMats = new Material[sharedMats.Length];
                    for (int j = 0; j < newMats.Length; j++)
                    {
                        newMats[j] = holoMat;
                    }
                    ren.materials = newMats;
                }
            }

            Animator anim = hologram.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.enabled = true;
                PlayTrainAnimationOnAnimator(anim, true);
            }

            spawnedHologramsList.Add(hologram);
        }

        spawnedHolograms = true;
    }

    // Hàm xóa lính hologram khi hoàn tất nâng cấp
    private void ClearHolograms()
    {
        StopHologramAnimationCoroutine();
        if (spawnedHologramsList != null && spawnedHologramsList.Count > 0)
        {
            if (!gameObject.scene.isLoaded)
            {
                spawnedHologramsList.Clear();
                spawnedHolograms = false;
                return;
            }

            Debug.Log($"[SpawnSoldier] ClearHolograms được gọi trên {gameObject.name}. Đang dọn dẹp {spawnedHologramsList.Count} lính hologram.");
            foreach (GameObject hologram in spawnedHologramsList)
            {
                if (hologram != null)
                {
                    Destroy(hologram);
                }
            }
            spawnedHologramsList.Clear();
        }
        spawnedHolograms = false;
    }

    void OnDestroy()
    {
        if (dynamicHologramMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(dynamicHologramMaterial);
            }
            else
            {
                DestroyImmediate(dynamicHologramMaterial);
            }
        }
    }

    [ContextMenu("Upgrade")]
    public void Upgrade()
    {
        if (currentLevel >= 3)
        {
            Debug.Log("Đã đạt cấp độ tối đa (Level 3)!");
            return;
        }

        currentLevel++;
        Debug.Log($"Nâng cấp thành công Trại Lính lên Level {currentLevel}! Mở khóa thêm ô huấn luyện mới.");
    }

    public int CurrentLevel => currentLevel;

    public int GetActiveSoldiersCount()
    {
        if (spawnedSoldiers == null) return 0;
        spawnedSoldiers.RemoveAll(soldier => soldier == null);
        return spawnedSoldiers.Count;
    }

    public void LoadAndSpawnSoldiers(int count, int buildingLevel)
    {
        ClearSpawnedSoldiers();
        currentLevel = buildingLevel + 1;
        int maxAllowed = GetMaxSoldiersForLevel(currentLevel);
        int validCount = Mathf.Clamp(count, 0, maxAllowed);
        if (validCount > 0)
        {
            SpawnSoldiers(validCount);
        }
    }

    [ContextMenu("Test Training")]
    public void TestTraining5s()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SpawnSoldier] Chỉ có thể chạy test ở chế độ Play Mode!");
            return;
        }
        StartCoroutine(TestTrainingRoutine());
    }

    private System.Collections.IEnumerator TestTrainingRoutine()
    {
        isTesting = true;
        Debug.Log($"[SpawnSoldier] Bắt đầu chạy thử nghiệm hoạt ảnh Train trong {testDuration} giây...");
        
        // 1. Dọn dẹp lính thực và hologram cũ
        ClearSpawnedSoldiers();
        ClearHolograms();
        
        // 2. Sinh lính hologram tương ứng với level hiện tại của công trình
        int count = GetMaxSoldiersForLevel(currentLevel);
        SpawnHologramSoldiers(count);
        
        spawnedHolograms = true;

        // 3. Đợi trong thời gian testDuration và duy trì lặp lại hoạt ảnh Train cho hologram
        float timer = 0f;
        while (timer < testDuration)
        {
            timer += Time.deltaTime;
            UpdateHologramAnimations();
            yield return null;
        }
        
        // 4. Dọn dẹp hologram khi hoàn tất test
        ClearHolograms();
        
        // 5. Sinh lại lính thực theo level hiện tại và chuyển về Idle
        int countReal = GetMaxSoldiersForLevel(currentLevel);
        SpawnSoldiers(countReal);
        
        isTesting = false;
        Debug.Log($"[SpawnSoldier] Kết thúc chạy thử nghiệm hoạt ảnh Train ({testDuration}s).");
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SpawnSoldier))]
public class SpawnSoldierEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SpawnSoldier spawner = (SpawnSoldier)target;

        GUILayout.Space(15);
        
        GUI.backgroundColor = new Color(0.2f, 0.6f, 1f, 1f);
        
        GUI.enabled = Application.isPlaying;
        if (GUILayout.Button($"Test Training Animation ({spawner.TestDuration}s)", GUILayout.Height(35)))
        {
            spawner.TestTraining5s();
        }
        GUI.enabled = true;
        
        GUI.backgroundColor = Color.white;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Vui lòng vào Play Mode để sử dụng nút Test hoạt ảnh!", MessageType.Info);
        }
    }
}
#endif
