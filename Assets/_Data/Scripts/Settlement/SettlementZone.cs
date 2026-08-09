using System.Collections.Generic;
using UnityEngine;

/*
 * SettlementZone.cs
 * Folder: Scripts/Settlement/
 * Dự án: KHẨN HOANG (PENTA DEV)
 * Phong cách: Demacia Rising Multi-Settlement Stage Territory Data
 */

public class SettlementZone : MonoBehaviour
{
    [Header("=== THÔNG TIN VÙNG ĐẤT / ẢI ===")]
    public string settlementName = "ZEFFIRA";
    public int settlementLevel = 1;
    public bool isUnlocked = true;                     // Vùng đất đã được mở khóa trên bản đồ chưa
    public bool isTownHallEstablished = true;           // Đã xây Nhà Chính chưa (Vùng đất khởi đầu = true)

    [Header("=== VỊ TRÍ 3D CỦA NHÀ CHÍNH & CÁC Ô SLOT ===")]
    public Transform townHallPoint;                     // Vị trí đặt Nhà Chính ở trung tâm
    public GameObject townHallPrefab;                   // Prefab Nhà Chính khi khởi tạo
    public List<Transform> slotPoints = new List<Transform>(); // Danh sách các mốc vị trí 3D của ô slot

    [Header("=== CHI PHÍ XÂY NHÀ CHÍNH CHO VÙNG ĐẤT MỚI ===")]
    public int establishWoodCost = 100;
    public int establishStoneCost = 100;
    public int establishFoodCost = 50;

    [Header("=== CÔNG TRÌNH CÓ SẴN TRÊN CÁC Ô SLOT ===")]
    [Tooltip("Kéo các Prefab công trình từ Project vào đây để tự động sinh sẵn khi bắt đầu game (Element 0 tương ứng với Slot 0).")]
    public List<GameObject> prebuiltSlotPrefabs = new List<GameObject>();

    [Header("=== CÔNG TRÌNH MỞ KHÓA TẠI VÙNG ĐẤT NÀY ===")]
    [Tooltip("Tích chọn nếu vùng đất này cho phép xây tất cả các loại công trình.")]
    public bool unlockAllBuildings = true;
    [Tooltip("Danh sách các loại công trình mở khóa riêng tại vùng đất này (ví dụ: Trại Lính Cung, Tháp Phòng Thủ...)")]
    public List<BuildingType> unlockedBuildingTypes = new List<BuildingType>();

    [Header("=== CĂN CỨ / CÔNG TRÌNH ĐỊCH (CHINH PHỤC VÙNG ĐẤT) ===")]
    [Tooltip("Tích vào nếu vùng đất này ban đầu bị Kẻ Địch chiếm đóng.")]
    public bool hasEnemyOutpost = false;
    [Tooltip("Số lượng Enemy tại căn cứ này khi đem quân đến đánh (Tùy chỉnh riêng từng vùng).")]
    public int enemyCountInBase = 5;
    [Tooltip("Kéo Prefab Căn cứ / Công trình Địch từ Project vào đây.")]
    public GameObject enemyOutpostPrefab;
    [Tooltip("Kéo vị trí / GameObject Spawn / EnemySpawn vào đây (nếu để trống sẽ tự động tìm EnemySpawn trong Scene).")]
    public Transform enemySpawnPoint;
    [Tooltip("Gán EnemySpawn trực tiếp (tùy chọn).")]
    public EnemySpawn enemySpawn;

    /// <summary>
    /// Kiểm tra xem loại công nghệ công trình này đã được mở khóa toàn quốc (cho phép xây ở mọi vùng đất) chưa.
    /// - Công trình Khởi Đầu (House, WoodCutter, Kitchen, FoodStorage, BarracksMelee): Mặc định mở khóa từ Đầu Game ở mọi nơi.
    /// - Công trình Nâng Cao (StoneMine/StoneStorage, BarracksArcher, ArcherTower...): Khóa toàn quốc cho tới khi XÂM CHIẾM / GIẢI PHÓNG được Vùng Đất chứa công nghệ đó!
    /// </summary>
    public static bool IsBuildingTypeUnlockedGlobally(BuildingType type)
    {
        if (type == BuildingType.None) return true;

        // 1. CÔNG TRÌNH CƠ BẢN KHỞI ĐẦU: Mặc định được mở khóa xây dựng ở tất cả các vùng đất
        if (type == BuildingType.House || 
            type == BuildingType.WoodCutter || 
            type == BuildingType.Kitchen || 
            type == BuildingType.FoodStorage || 
            type == BuildingType.BarracksMelee)
        {
            return true;
        }

        // 2. CÔNG NGHỆ NÂNG CAO (Kho Đá/Mỏ Đá, Lính Cung, Tháp Canh...): Chỉ mở khóa xây ở mọi nơi sau khi XÂM CHIẾM được Vùng Đất chứa công nghệ đó!
        SettlementZone[] allZones = Object.FindObjectsByType<SettlementZone>(FindObjectsSortMode.None);
        if (allZones == null || allZones.Length == 0) return true;

        foreach (var zone in allZones)
        {
            if (zone == null) continue;

            // ĐIỀU KIỆN XÂM CHIẾM THÀNH CÔNG: Vùng đất được mở khóa VÀ ĐÃ TIÊU DIỆT CĂN CỨ ĐỊCH (hasEnemyOutpost == false)
            bool isConqueredTerritory = zone.isUnlocked && !zone.hasEnemyOutpost;

            if (isConqueredTerritory)
            {
                // Nếu vùng đất đã xâm chiếm này cho phép mở khóa tất cả công nghệ
                if (zone.unlockAllBuildings) return true;

                // Kiểm tra loại công nghệ trong danh sách unlockedBuildingTypes do người dùng kéo thả trên Inspector
                if (zone.unlockedBuildingTypes != null)
                {
                    foreach (var unlockedType in zone.unlockedBuildingTypes)
                    {
                        if (unlockedType == type) return true;

                        // Đồng bộ cặp Mỏ Đá (StoneMine) & Kho Đá (StoneStorage) nếu có
                        if ((type == BuildingType.StoneMine || type == BuildingType.StoneStorage) &&
                            (unlockedType == BuildingType.StoneMine || unlockedType == BuildingType.StoneStorage))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        // Chưa xâm chiếm được Vùng Đất nào mở khóa công nghệ này -> KHÓA XÂY DỰNG TOÀN QUỐC!
        return false;
    }

    /// <summary>
    /// Kiểm tra xem loại công trình này có được phép xây không
    /// </summary>
    public bool IsBuildingUnlocked(BuildingType type)
    {
        return IsBuildingTypeUnlockedGlobally(type);
    }

    [HideInInspector]
    public GameObject spawnedEnemyOutpostInstance;

    [HideInInspector]
    public UpgradeableBuilding townHallBuilding;        // Building Nhà chính hiện tại
    [HideInInspector]
    public List<UpgradeableBuilding> builtStructures = new List<UpgradeableBuilding>(); // Danh sách công trình đã xây

    public void RegisterBuilding(UpgradeableBuilding building)
    {
        if (building == null) return;
        if (!builtStructures.Contains(building))
        {
            builtStructures.Add(building);
        }
    }

    private void Awake()
    {
        if (townHallPoint == null) townHallPoint = transform;
    }

    private void Start()
    {
        LoadSettlementState();
        InstantiateEnemyOutpost();
        EnsureTownHallInstantiated();
        InstantiatePrebuiltBuildings();
        Update3DSlotVisibility();
    }

    public void SaveSettlementState()
    {
        PlayerPrefs.SetInt($"Settlement_{settlementName}_Level", settlementLevel);
        PlayerPrefs.SetInt($"Settlement_{settlementName}_Unlocked", isUnlocked ? 1 : 0);
        PlayerPrefs.SetInt($"Settlement_{settlementName}_TownHallEstablished", isTownHallEstablished ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void LoadSettlementState()
    {
        if (PlayerPrefs.HasKey($"Settlement_{settlementName}_Level"))
        {
            settlementLevel = PlayerPrefs.GetInt($"Settlement_{settlementName}_Level", settlementLevel);
            isUnlocked = PlayerPrefs.GetInt($"Settlement_{settlementName}_Unlocked", isUnlocked ? 1 : 0) == 1;
            isTownHallEstablished = PlayerPrefs.GetInt($"Settlement_{settlementName}_TownHallEstablished", isTownHallEstablished ? 1 : 0) == 1;
        }
    }

    public void EnsureTownHallInstantiated()
    {
        // 1. Quét tìm Nhà Chính thực tế thuộc vùng đất này để tránh lấy nhầm Nhà Chính của vùng đất khác
        if (townHallBuilding == null || !townHallBuilding.gameObject.scene.IsValid())
        {
            UpgradeableBuilding[] localUbs = GetComponentsInChildren<UpgradeableBuilding>(true);
            foreach (var ub in localUbs)
            {
                if (ub != null && ub.gameObject.scene.IsValid() && 
                   (ub.buildingType == BuildingType.House || ub.buildingName.Contains("Nhà chính") || ub.buildingName.Contains("Town Hall") || ub.name.Contains("TownHall") || ub.name.Contains("House")))
                {
                    townHallBuilding = ub;
                    break;
                }
            }
        }

        if (isTownHallEstablished && townHallBuilding == null)
        {
            InstantiateTownHallObject();
        }

        if (isTownHallEstablished && townHallBuilding != null && !townHallBuilding.IsUpgrading && townHallBuilding.gameObject.scene.IsValid())
        {
            townHallBuilding.IsInitialBuildNeeded = false;
        }
    }

    public void InstantiateTownHallObject()
    {
        GameObject prefabToUse = (townHallPrefab != null) 
            ? townHallPrefab 
            : ((ConstructionManager.Ins != null) ? ConstructionManager.Ins.housePrefab : null);

        Transform spawnPoint = (townHallPoint != null) ? townHallPoint : transform;

        if (prefabToUse != null)
        {
            GameObject obj = Instantiate(prefabToUse, spawnPoint.position, spawnPoint.rotation, transform);
            townHallBuilding = obj.GetComponent<UpgradeableBuilding>();
            Debug.Log($"[SettlementZone] 🏰 Đã khởi tạo Nhà Chính 3D trong Scene cho vùng đất {settlementName}!");
        }
        else
        {
            Debug.LogWarning($"[SettlementZone] ⚠️ Chưa gán Town Hall Prefab cho vùng đất {settlementName}!");
        }
    }

    public UpgradeableBuilding TownHallBuilding
    {
        get
        {
            EnsureTownHallInstantiated();
            return townHallBuilding;
        }
    }

    public int SettlementLevel
    {
        get
        {
            var th = TownHallBuilding;
            if (th != null && isTownHallEstablished)
                return th.CurrentLevel + 1;
            return settlementLevel;
        }
    }

    private void Update()
    {
        // 🔒 Nếu bỏ tích hasEnemyOutpost trong Play Mode hoặc đã tiêu diệt nhưng đối tượng 3D vẫn còn -> Tự động Destroy đối tượng 3D
        if (!hasEnemyOutpost && spawnedEnemyOutpostInstance != null)
        {
            GameObject outpostObj = spawnedEnemyOutpostInstance;
            spawnedEnemyOutpostInstance = null;
            if (outpostObj != null)
            {
                outpostObj.SetActive(false);
                if (Application.isPlaying) Destroy(outpostObj);
                else DestroyImmediate(outpostObj);
            }

            if (SettlementSidePanelUI.Ins != null)
            {
                SettlementSidePanelUI.Ins.UpdateHeaderVisual();
                SettlementSidePanelUI.Ins.RefreshPanel();
            }
        }
        else if (hasEnemyOutpost && spawnedEnemyOutpostInstance == null && !isTownHallEstablished)
        {
            OnEnemyOutpostDestroyed();
        }

        var th = TownHallBuilding;
        if (th != null && isTownHallEstablished)
        {
            int expectedLevel = th.CurrentLevel + 1;
            if (settlementLevel != expectedLevel)
            {
                settlementLevel = expectedLevel;
                Update3DSlotVisibility();
                if (SettlementSidePanelUI.Ins != null)
                {
                    SettlementSidePanelUI.Ins.UpdateHeaderVisual();
                    SettlementSidePanelUI.Ins.RefreshPanel();
                }
            }
        }
    }

    /// <summary>
    /// Số lượng ô Slot được mở khóa theo Cấp độ Vùng Đất (settlementLevel):
    /// - Cấp 1: 2 ô slot
    /// - Cấp 2: 3 ô slot (mở thêm 1 ô)
    /// - Cấp 3+: Mở toàn bộ số slot còn lại!
    /// </summary>
    public int GetUnlockedSlotCount()
    {
        if (!isTownHallEstablished) return 0;

        // Chỉ ẩn slot khi Nhà Chính đang trong tiến trình XÂY DỰNG BAN ĐẦU
        if (townHallBuilding != null && townHallBuilding.IsInitialBuildNeeded)
            return 0;

        int totalSlotCount = (slotPoints.Count > 0) ? slotPoints.Count : 12;
        int level = SettlementLevel;

        if (level <= 1) return Mathf.Min(2, totalSlotCount);
        if (level == 2) return Mathf.Min(3, totalSlotCount);
        
        // Cấp 3 trở lên: Mở toàn bộ số ô slot!
        return totalSlotCount;
    }

    private void OnEnable()
    {
        Update3DSlotVisibility();
    }

    /// <summary>
    /// Bật/Tắt hiển thị 3D cho các mốc đất slot trên thế giới 3D tương ứng theo cấp độ đã mở khóa
    /// </summary>
    public void Update3DSlotVisibility()
    {
        if (this == null || gameObject == null) return;

        int unlockedCount = GetUnlockedSlotCount();

        for (int i = 0; i < slotPoints.Count; i++)
        {
            if (slotPoints[i] != null && slotPoints[i].gameObject != null)
            {
                bool activeState = isTownHallEstablished && !hasEnemyOutpost && (i < unlockedCount);
                if (slotPoints[i].gameObject.activeSelf != activeState)
                {
                    slotPoints[i].gameObject.SetActive(activeState);
                }
            }
        }
    }

    /// <summary>
    /// Sinh các công trình có sẵn (như nhà lính, kho gỗ...) theo cấu hình prebuiltSlotPrefabs
    /// </summary>
    public void InstantiatePrebuiltBuildings()
    {
        if (!isTownHallEstablished || hasEnemyOutpost) return;

        for (int i = 0; i < prebuiltSlotPrefabs.Count; i++)
        {
            if (prebuiltSlotPrefabs[i] == null) continue;
            if (i >= slotPoints.Count) break;

            Vector3 targetPos = GetSlotWorldPosition(i);
            bool alreadyExists = false;
            foreach (var b in builtStructures)
            {
                if (b != null && Vector3.Distance(b.transform.position, targetPos) < 2f)
                {
                    alreadyExists = true;
                    break;
                }
            }

            if (!alreadyExists)
            {
                GameObject obj = Instantiate(prebuiltSlotPrefabs[i], targetPos, Quaternion.identity, transform);
                UpgradeableBuilding ub = obj.GetComponent<UpgradeableBuilding>();
                if (ub != null)
                {
                    ub.IsInitialBuildNeeded = false;
                    RegisterBuilding(ub);
                    if (BuildingManager.Ins != null) BuildingManager.Ins.AddBuilding(ub.GetComponent<BuildingCtrl>());
                }
                Debug.Log($"[SettlementZone] 🛠️ Đã sinh công trình có sẵn '{prebuiltSlotPrefabs[i].name}' tại slot {i} vùng đất {settlementName}.");
            }
        }
    }

    /// <summary>
    /// Sinh Căn cứ / Công trình Địch ban đầu chiếm đóng vùng đất này
    /// </summary>
    public void InstantiateEnemyOutpost()
    {
        if (!hasEnemyOutpost) return;
        if (spawnedEnemyOutpostInstance != null) return;

        // 1. Tự động tìm EnemySpawn từ enemySpawn, enemySpawnPoint hoặc trong Scene / Children nếu chưa được gán
        if (enemySpawn == null && enemySpawnPoint != null)
        {
            enemySpawn = enemySpawnPoint.GetComponent<EnemySpawn>();
            if (enemySpawn == null) enemySpawn = enemySpawnPoint.GetComponentInParent<EnemySpawn>();
            if (enemySpawn == null) enemySpawn = enemySpawnPoint.GetComponentInChildren<EnemySpawn>();
        }

        if (enemySpawn == null)
        {
            enemySpawn = GetComponentInChildren<EnemySpawn>();
            if (enemySpawn == null)
            {
                enemySpawn = Object.FindFirstObjectByType<EnemySpawn>();
            }
        }

        // 2. Xác định vị trí spawn từ EnemySpawn (hoặc enemySpawnPoint / townHallPoint / transform)
        Vector3 spawnPosition = transform.position;
        Quaternion spawnRotation = transform.rotation;

        if (enemySpawn != null)
        {
            spawnPosition = enemySpawn.GetSpawnPosition();
            spawnRotation = enemySpawn.transform.rotation;
        }
        else if (enemySpawnPoint != null)
        {
            spawnPosition = enemySpawnPoint.position;
            spawnRotation = enemySpawnPoint.rotation;
        }
        else if (townHallPoint != null)
        {
            spawnPosition = townHallPoint.position;
            spawnRotation = townHallPoint.rotation;
        }

        // 3. Khởi tạo hoặc liên kết Căn cứ Địch
        if (enemySpawn != null && enemySpawn.gameObject.scene.IsValid() && enemySpawn.GetComponentInChildren<HPTower>() != null)
        {
            spawnedEnemyOutpostInstance = enemySpawn.gameObject;
        }
        else if (enemySpawnPoint != null && enemySpawnPoint.gameObject.scene.IsValid() && enemySpawnPoint.GetComponentInChildren<HPTower>() != null)
        {
            spawnedEnemyOutpostInstance = enemySpawnPoint.gameObject;
        }
        else if (enemyOutpostPrefab != null)
        {
            spawnedEnemyOutpostInstance = Instantiate(enemyOutpostPrefab, spawnPosition, spawnRotation, transform);
        }

        // 4. Đăng ký sự kiện tiêu diệt Căn cứ Địch
        if (spawnedEnemyOutpostInstance != null)
        {
            if (enemySpawn == null)
            {
                enemySpawn = spawnedEnemyOutpostInstance.GetComponentInChildren<EnemySpawn>();
            }

            HPTower enemyHP = spawnedEnemyOutpostInstance.GetComponent<HPTower>();
            if (enemyHP == null) enemyHP = spawnedEnemyOutpostInstance.GetComponentInChildren<HPTower>();

            if (enemyHP != null)
            {
                enemyHP.OnDeathEvent -= OnEnemyOutpostDestroyed;
                enemyHP.OnDeathEvent += OnEnemyOutpostDestroyed;
            }

            Debug.Log($"[SettlementZone] ⚔️ Căn cứ Địch đã khởi tạo/liên kết tại vị trí spawn {spawnPosition} cho vùng đất {settlementName}!");
        }
        else
        {
            Debug.LogWarning($"[SettlementZone] ⚠️ Chưa gán enemyOutpostPrefab hoặc không tìm thấy vị trí EnemySpawn cho vùng đất {settlementName}!");
        }
    }

    public void OnEnemyOutpostDestroyed()
    {
        hasEnemyOutpost = false;
        if (spawnedEnemyOutpostInstance != null)
        {
            Destroy(spawnedEnemyOutpostInstance);
            spawnedEnemyOutpostInstance = null;
        }

        Debug.Log($"[SettlementZone] 🎉 CHINH PHỤC THÀNH CÔNG! Đã tiêu diệt Căn cứ Địch tại vùng đất {settlementName}!");

        if (SettlementSidePanelUI.Ins != null)
        {
            SettlementSidePanelUI.Ins.UpdateHeaderVisual();
            SettlementSidePanelUI.Ins.RefreshPanel();
        }
    }

    /// <summary>
    /// Xây dựng Nhà Chính cho vùng đất mới khi người chơi bấm "XÂY NHÀ CHÍNH"
    /// </summary>
    public bool EstablishTownHall()
    {
        if (hasEnemyOutpost)
        {
            if (UIManager.Ins != null) UIManager.Ins.ShowWarning("Hãy tiêu diệt Căn cứ Địch trước khi xây Nhà Chính!");
            return false;
        }

        if (isTownHallEstablished && townHallBuilding != null) return true;

        // Trừ tài nguyên
        if (JsonDataManager.Ins != null)
        {
            if (!JsonDataManager.Ins.HasEnoughResources(establishWoodCost, establishStoneCost, establishFoodCost))
            {
                if (UIManager.Ins != null) UIManager.Ins.ShowWarning("Không đủ tài nguyên xây Nhà Chính!");
                return false;
            }

            JsonDataManager.Ins.AddWood(-establishWoodCost);
            JsonDataManager.Ins.AddStone(-establishStoneCost);
            JsonDataManager.Ins.AddFood(-establishFoodCost);
        }

        isTownHallEstablished = true;
        settlementLevel = 1;

        InstantiateTownHallObject();

        if (townHallBuilding != null)
        {
            townHallBuilding.StartInitialBuildProcess();
        }

        InstantiatePrebuiltBuildings();

        Debug.Log($"[SettlementZone] 🎉 Đã bắt đầu xây dựng Nhà Chính cho vùng đất: {settlementName}!");

        if (CampaignTutorialManager.Ins != null)
        {
            CampaignTutorialManager.Ins.OnTownHallEstablished(this);
        }

        if (SettlementSidePanelUI.Ins != null) SettlementSidePanelUI.Ins.RefreshPanel();
        return true;
    }

    /// <summary>
    /// Nâng cấp Cấp độ vùng đất / Nhà chính. Thay đổi model 3D tương ứng theo cấp độ mới!
    /// </summary>
    public void UpgradeSettlementLevel()
    {
        settlementLevel++;
        if (townHallBuilding != null)
        {
            townHallBuilding.Upgrade();
        }
        SaveSettlementState();
        Debug.Log($"[SettlementZone] 🚀 Đã nâng cấp vùng đất {settlementName} lên Cấp {settlementLevel}!");
        if (SettlementSidePanelUI.Ins != null) SettlementSidePanelUI.Ins.RefreshPanel();
    }

    /// <summary>
    /// Lấy vị trí 3D cho ô Slot theo Index.
    /// Nếu index vượt quá số slotPoint kéo trong Inspector, tự động tính toán vị trí lưới xung quanh TownHall.
    /// </summary>
    public Vector3 GetSlotWorldPosition(int index)
    {
        if (index >= 0 && index < slotPoints.Count && slotPoints[index] != null)
        {
            return slotPoints[index].position;
        }

        // Tự động tính toán vị trí lưới xung quanh TownHall nếu thiếu slotPoint trong Inspector
        Vector3 origin = (townHallPoint != null) ? townHallPoint.position : transform.position;

        int cols = 3;
        int row = index / cols;
        int col = index % cols;

        float spacing = 12f; // Khoảng cách 12m giữa các mốc công trình
        float offsetX = (col - 1) * spacing;
        float offsetZ = (row + 1) * spacing;

        return origin + new Vector3(offsetX, 0f, offsetZ);
    }

    /// <summary>
    /// Định vị tự động các công trình đã xây lên vị trí 3D slot chuẩn nếu bị trùng lặp tại (0,0,0)
    /// </summary>
    public void AlignBuildingsToSlotPositions()
    {
        for (int i = 0; i < builtStructures.Count; i++)
        {
            if (builtStructures[i] == null) continue;

            // Nếu nhà chưa có vị trí chuẩn (đang ở Vector3.zero)
            if (builtStructures[i].transform.position == Vector3.zero)
            {
                builtStructures[i].transform.position = GetSlotWorldPosition(i);
            }
        }
    }
}
