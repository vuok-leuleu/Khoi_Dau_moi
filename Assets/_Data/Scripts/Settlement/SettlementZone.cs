using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*
 * SettlementZone.cs
 * Folder: Scripts/Settlement/
 * Dự án: KHẨN HOANG (PENTA DEV)
 * Phong cách: Demacia Rising Multi-Settlement Stage Territory Data
 */

public class SettlementZone : MonoBehaviour
{
    [Header("=== HIỂN THỊ TEXT UI VÙNG ĐẤT / ẢI ===")]
    [Tooltip("Text hiển thị Cấp độ công trình / Vùng đất (VD: Lv. 1)")]
    public TMP_Text levelTextTMP;
    public Text levelTextLegacy;

    [Tooltip("Text hiển thị Tên vùng đất (VD: ZEFFIRA, AI 1)")]
    public TMP_Text nameTextTMP;
    public Text nameTextLegacy;

    [Tooltip("Text hiển thị Số lượng lính hiện có tại vùng đất (VD: Lính: 0)")]
    public TMP_Text soldierCountTextTMP;
    public Text soldierCountTextLegacy;


    [Header("=== THÔNG TIN VÙNG ĐẤT / ẢI ===")]
    public string settlementName = "ZEFFIRA";
    public int settlementLevel = 1;
    public bool isUnlocked = true;                     // Vùng đất đã được mở khóa trên bản đồ chưa
    public bool isTownHallEstablished = true;           // Đã xây Nhà Chính chưa (Vùng đất khởi đầu = true)
    [Tooltip("Chỉ một vùng được tích: thành đầu tiên có Trại Lính trung tâm và được phép huấn luyện lính.")]
    public bool isStartingSettlement = false;

    [Header("=== PHÂN BẬC VÙNG ĐẤT (ZONE TIER / ẢI) ===")]
    [Tooltip("Bậc 0 = Vùng đất khởi đầu (ZEFFIRA). Bậc 1, 2, 3... = Các ải mở khóa tiếp theo.")]
    public int zoneTier = 0;
    [Tooltip("Vùng đất bậc trước đó cần giải phóng để mở khóa vùng đất này (để trống sẽ tự tìm Zone bậc zoneTier - 1).")]
    public SettlementZone previousTierZone;

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
    public bool unlockAllBuildings = false;
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
    /// Vùng đất đã được người chơi chinh phục và có thể mở Settlement UI.
    /// Vùng chưa mở khóa hoặc còn căn cứ địch chỉ được xử lý như mục tiêu bản đồ,
    /// không được mở bảng settlement.
    /// </summary>
    public bool IsConquered => isUnlocked && !hasEnemyOutpost;

    /// <summary>
    /// Kiểm tra xem loại công nghệ công trình này đã được mở khóa toàn quốc (cho phép xây ở mọi vùng đất) chưa.
    /// - Trại Lính là công trình trung tâm đặt sẵn, không được xây trong Shop.
    /// - Các công trình khác tuân theo tiến độ mở khóa toàn quốc.
    /// </summary>
    public static bool IsBuildingTypeUnlockedGlobally(BuildingType type)
    {
        if (type == BuildingType.None) return true;

        // Trại Lính chỉ tồn tại sẵn ở thành khởi đầu, tuyệt đối không mở khóa để xây thêm.
        if (TroopTrainingManager.IsCentralBarracksType(type)) return false;

        // 1. CÔNG TRÌNH CƠ BẢN KHỞI ĐẦU: Mặc định được mở khóa xây dựng ở tất cả các vùng đất
        if (type == BuildingType.House || 
            type == BuildingType.WoodCutter || 
            type == BuildingType.Kitchen || 
            type == BuildingType.FoodStorage)
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


    private float uiUpdateTimer = 0f;

    /// <summary>
    /// Lấy tổng số lượng lính đang có tại Vùng đất này
    /// </summary>
    public int GetTotalSoldiersCount()
    {
        int total = 0;

        // Đếm theo nơi lính đang đóng thực tế thay vì theo SpawnSoldier đã sinh ra nó.
        // Lính được giữ nguyên parent ở doanh trại gốc khi hành quân, vì vậy đếm theo
        // hierarchy của SpawnSoldier sẽ khiến thành đích luôn hiển thị 0 lính.
        UnitController[] allUnits = FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        foreach (UnitController soldier in allUnits)
        {
            if (soldier == null || !soldier.gameObject.activeInHierarchy || soldier.isDead) continue;

            // Đoàn đang trên đường không thuộc về thành nào cho đến khi hoàn tất.
            if (soldier.isExpeditionMarching) continue;

            if (!string.IsNullOrEmpty(soldier.stationedSettlementZoneName))
            {
                if (soldier.stationedSettlementZoneName == settlementName)
                {
                    total++;
                }
                continue;
            }

            // Lính chưa từng hành quân vẫn thuộc vùng chứa doanh trại đã sinh chúng.
            if (soldier.GetComponentInParent<SettlementZone>() == this)
            {
                total++;
            }
        }

        // Nếu chưa sinh lính thực tế hoặc cần đồng bộ với các ô huấn luyện đã hoàn thành
        if (total == 0 && TroopTrainingManager.Ins != null)
        {
            var slots = TroopTrainingManager.Ins.GetSlotsForZone(this);
            if (slots != null)
            {
                foreach (var slot in slots)
                {
                    if (slot != null && slot.isUnlocked && slot.isCompleted)
                    {
                        total++;
                    }
                }
            }
        }

        return total;
    }

    /// <summary>
    /// Cập nhật hiển thị toàn bộ 3 Text UI: Cấp độ, Tên vùng đất, Số lượng lính hiện có
    /// </summary>
    private void AutoBindVisualTexts()
    {
        if (levelTextTMP != null && nameTextTMP != null && soldierCountTextTMP != null) return;

        TMP_Text[] tmps = GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in tmps)
        {
            if (t == null) continue;
            string objName = t.gameObject.name;
            Transform p = t.transform.parent;
            string parentName = p != null ? p.name : "";

            if (levelTextTMP == null && (objName.Contains("Level") || objName.Contains("Lv") || parentName.Contains("Level") || parentName.Contains("Lv")))
            {
                levelTextTMP = t;
            }
            else if (soldierCountTextTMP == null && (objName.Contains("Soldier") || objName.Contains("Count") || objName.Contains("Linh") || parentName.Contains("Soldier") || parentName.Contains("Linh")))
            {
                soldierCountTextTMP = t;
            }
            else if (nameTextTMP == null && (objName.Contains("Name") || parentName.Contains("Name")))
            {
                nameTextTMP = t;
            }
        }

        // Nếu NameLand prefab có cấu trúc cụ thể
        if ((levelTextTMP == null || nameTextTMP == null || soldierCountTextTMP == null) && tmps.Length >= 2)
        {
            foreach (var t in tmps)
            {
                if (t == null) continue;
                if (nameTextTMP == null && t.fontSize >= 11f && (t.text == "New Text" || t.text == settlementName))
                {
                    nameTextTMP = t;
                }
                else if (soldierCountTextTMP == null && t.text.Contains("0") && t.transform.parent != null && t.transform.parent.GetComponentInChildren<UnityEngine.UI.Image>() != null)
                {
                    soldierCountTextTMP = t;
                }
                else if (levelTextTMP == null && t != nameTextTMP && t != soldierCountTextTMP)
                {
                    levelTextTMP = t;
                }
            }
        }
    }

    public void UpdateZoneVisualText()
    {
        AutoBindVisualTexts();
        UpdateLevelText();
        UpdateNameText();
        UpdateSoldierCountText();
    }

    public void UpdateLevelText()
    {
        string lvlStr;
        if (!isUnlocked || hasEnemyOutpost)
        {
            lvlStr = "Khóa";
        }
        else if (!isTownHallEstablished || (townHallBuilding != null && townHallBuilding.IsInitialBuildNeeded))
        {
            lvlStr = "Lv. 0";
        }
        else
        {
            lvlStr = $"Lv. {settlementLevel}";
        }

        if (levelTextTMP != null) levelTextTMP.text = lvlStr;
        if (levelTextLegacy != null) levelTextLegacy.text = lvlStr;
    }

    public void UpdateNameText()
    {
        string nameStr = string.IsNullOrEmpty(settlementName) ? gameObject.name : settlementName;
        if (nameTextTMP != null) nameTextTMP.text = nameStr;
        if (nameTextLegacy != null) nameTextLegacy.text = nameStr;
    }

    public void UpdateSoldierCountText()
    {
        int count;
        if (!isUnlocked || hasEnemyOutpost)
        {
            count = enemyCountInBase;
            if (enemySpawn != null && enemySpawn.enemyCountInBase > 0)
            {
                count = enemySpawn.enemyCountInBase;
            }
            else
            {
                EnemySpawn localSpawn = GetComponentInChildren<EnemySpawn>(true);
                if (localSpawn != null && localSpawn.enemyCountInBase > 0)
                {
                    count = localSpawn.enemyCountInBase;
                }
            }
        }
        else
        {
            count = GetTotalSoldiersCount();
        }

        string countStr = $"{count}";
        if (soldierCountTextTMP != null) soldierCountTextTMP.text = countStr;
        if (soldierCountTextLegacy != null) soldierCountTextLegacy.text = countStr;
    }

    private void Awake()
    {
        if (string.IsNullOrEmpty(settlementName) || (settlementName == "ZEFFIRA" && gameObject.name != "ZEFFIRA"))
        {
            settlementName = gameObject.name;
        }
        if (townHallPoint == null) townHallPoint = transform;
        LoadSettlementState();
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(settlementName) || (settlementName == "ZEFFIRA" && gameObject.name != "ZEFFIRA"))
        {
            settlementName = gameObject.name;
        }
    }

    private void Start()
    {
        LoadSettlementState();
        UpdateZoneTierVisibility();
        InstantiateEnemyOutpost();
        EnsureTownHallInstantiated();
        InstantiatePrebuiltBuildings();
        Update3DSlotVisibility();
    }

    public int GetEffectiveTier()
    {
        if (zoneTier > 0) return zoneTier;
        if (settlementName.Equals("ZEFFIRA", System.StringComparison.OrdinalIgnoreCase)) return 0;
        if (transform.parent != null)
        {
            return transform.GetSiblingIndex();
        }
        return 0;
    }

    public SettlementZone GetPreviousZone()
    {
        if (previousTierZone != null && previousTierZone != this) return previousTierZone;

        int myTier = GetEffectiveTier();
        if (myTier <= 0) return null;

        // 1. Tìm từ SettlementManager
        if (SettlementManager.Ins != null)
        {
            previousTierZone = SettlementManager.Ins.GetZoneByTier(myTier - 1);
            if (previousTierZone != null && previousTierZone != this) return previousTierZone;
        }

        // 2. Tìm từ cùng Parent ("Land") theo thứ tự Sibling Index
        if (transform.parent != null)
        {
            int prevIndex = transform.GetSiblingIndex() - 1;
            if (prevIndex >= 0 && prevIndex < transform.parent.childCount)
            {
                previousTierZone = transform.parent.GetChild(prevIndex).GetComponent<SettlementZone>();
                if (previousTierZone != null && previousTierZone != this) return previousTierZone;
            }
        }

        // 3. Tìm trong toàn bộ Scene (kể cả inactive)
        SettlementZone[] allZones = Object.FindObjectsByType<SettlementZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var z in allZones)
        {
            if (z != null && z != this && z.GetEffectiveTier() == myTier - 1)
            {
                previousTierZone = z;
                return previousTierZone;
            }
        }
        return null;
    }

    /// <summary>
    /// Kiểm tra và ẩn/hiện Vùng đất theo phân bậc Tier (Bậc 0 mở sẵn, Bậc N ẩn cho tới khi Bậc N-1 giải phóng)
    /// </summary>
    public void UpdateZoneTierVisibility()
    {
        int myTier = GetEffectiveTier();

        // Bậc 0 (ZEFFIRA) luôn luôn mở khóa và hiển thị từ đầu
        if (myTier <= 0)
        {
            isUnlocked = true;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            return;
        }

        SettlementZone prevZone = GetPreviousZone();

        // Zone bậc N chỉ mở khóa & xuất hiện khi Zone bậc N-1 ĐÃ GIẢI PHÓNG (hasEnemyOutpost == false)
        bool isPrevConquered = false;
        if (prevZone != null)
        {
            isPrevConquered = !prevZone.hasEnemyOutpost;
        }
        else if (myTier == 1)
        {
            // Mặc định mở khóa Bậc 1 (VASKASIA) nếu Bậc 0 không có địch
            SettlementZone zeffira = GameObject.Find("ZEFFIRA")?.GetComponent<SettlementZone>();
            if (zeffira != null)
            {
                isPrevConquered = !zeffira.hasEnemyOutpost;
            }
            else
            {
                isPrevConquered = true;
            }
        }

        if (isPrevConquered)
        {
            isUnlocked = true;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }
        else
        {
            // 🔒 ẨN HOÀN TOÀN VÙNG ĐẤT BẬC CAO CHO ĐẾN KHI BẬC TRƯỚC ĐÓ ĐƯỢC GIẢI PHÓNG
            isUnlocked = false;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }

    public void SaveSettlementState()
    {
        PlayerPrefs.SetInt($"Settlement_{settlementName}_Level", settlementLevel);
        PlayerPrefs.SetInt($"Settlement_{settlementName}_Unlocked", isUnlocked ? 1 : 0);
        PlayerPrefs.SetInt($"Settlement_{settlementName}_TownHallEstablished", isTownHallEstablished ? 1 : 0);
        PlayerPrefs.SetInt($"Settlement_{settlementName}_HasEnemyOutpost", hasEnemyOutpost ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void LoadSettlementState()
    {
        if (PlayerPrefs.HasKey($"Settlement_{settlementName}_Level"))
        {
            settlementLevel = PlayerPrefs.GetInt($"Settlement_{settlementName}_Level", settlementLevel);
            if (GetEffectiveTier() > 0 && PlayerPrefs.HasKey($"Settlement_{settlementName}_Unlocked"))
            {
                isUnlocked = PlayerPrefs.GetInt($"Settlement_{settlementName}_Unlocked", isUnlocked ? 1 : 0) == 1;
            }
            isTownHallEstablished = PlayerPrefs.GetInt($"Settlement_{settlementName}_TownHallEstablished", isTownHallEstablished ? 1 : 0) == 1;
            if (PlayerPrefs.HasKey($"Settlement_{settlementName}_HasEnemyOutpost"))
            {
                hasEnemyOutpost = PlayerPrefs.GetInt($"Settlement_{settlementName}_HasEnemyOutpost", hasEnemyOutpost ? 1 : 0) == 1;
            }
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
            if (!isTownHallEstablished) return 0;

            if (PlayerPrefs.HasKey($"Settlement_{settlementName}_Level"))
            {
                int saved = PlayerPrefs.GetInt($"Settlement_{settlementName}_Level", settlementLevel);
                if (saved > settlementLevel) settlementLevel = saved;
            }
            return Mathf.Max(1, settlementLevel);
        }
    }

    private void Update()
    {
        uiUpdateTimer += Time.deltaTime;
        if (uiUpdateTimer >= 0.25f)
        {
            uiUpdateTimer = 0f;
            UpdateZoneVisualText();
        }

        // 🔒 Nếu bỏ tích hasEnemyOutpost trong Play Mode hoặc đã tiêu diệt nhưng đối tượng 3D vẫn còn -> Tự động Destroy đối tượng 3D
        if (!hasEnemyOutpost && (spawnedEnemyOutpostInstance != null || enemySpawn != null || GetComponentInChildren<EnemySpawn>(true) != null))
        {
            CleanUpEnemyOutposts();
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
            int curLevel = SettlementLevel;
            if (th.CurrentLevel + 1 < curLevel)
            {
                th.LoadBuildingData(curLevel - 1, th.IsRuined, false);
            }
            else if (th.CurrentLevel + 1 > curLevel)
            {
                settlementLevel = th.CurrentLevel + 1;
                SaveSettlementState();
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
    /// Sinh Căn cứ / Công trình Địch ban đầu chiếm đóng vùng đất này từ Prefab dưới Transform của Vùng đất
    /// </summary>
    public void InstantiateEnemyOutpost()
    {
        if (!hasEnemyOutpost)
        {
            CleanUpEnemyOutposts();
            return;
        }

        // 1. Xác định vị trí spawn chuẩn của Vùng đất này
        Vector3 spawnPosition = (enemySpawnPoint != null) ? enemySpawnPoint.position : ((townHallPoint != null) ? townHallPoint.position : transform.position);
        Quaternion spawnRotation = (enemySpawnPoint != null) ? enemySpawnPoint.rotation : transform.rotation;

        // 2. Khởi tạo Căn cứ Địch từ Prefab trực tiếp dưới Transform Vùng đất (SetParent = transform)
        if (spawnedEnemyOutpostInstance == null && enemyOutpostPrefab != null)
        {
            spawnedEnemyOutpostInstance = Instantiate(enemyOutpostPrefab, spawnPosition, spawnRotation, transform);
            spawnedEnemyOutpostInstance.name = $"{enemyOutpostPrefab.name}_{settlementName}";
        }
        else if (spawnedEnemyOutpostInstance == null && enemySpawnPoint != null && enemySpawnPoint.gameObject.scene.IsValid() && enemySpawnPoint.GetComponentInChildren<HPTower>() != null)
        {
            spawnedEnemyOutpostInstance = enemySpawnPoint.gameObject;
        }

        // 3. Lấy EnemySpawn CHỈ THUỘC VỀ Căn cứ Địch vừa sinh dưới Vùng đất này
        if (spawnedEnemyOutpostInstance != null)
        {
            enemySpawn = spawnedEnemyOutpostInstance.GetComponent<EnemySpawn>();
            if (enemySpawn == null) enemySpawn = spawnedEnemyOutpostInstance.GetComponentInChildren<EnemySpawn>();
        }

        if (enemySpawn == null)
        {
            enemySpawn = GetComponentInChildren<EnemySpawn>();
        }

        // The town hall is spawned at runtime, so give the local outpost a scene-valid target.
        if (enemySpawn != null && townHallBuilding != null && townHallBuilding.gameObject.activeInHierarchy)
        {
            enemySpawn.SetAttackTarget(townHallBuilding.transform);
        }

        // 4. Đăng ký sự kiện tiêu diệt Căn cứ Địch
        if (spawnedEnemyOutpostInstance != null)
        {
            HPTower enemyHP = spawnedEnemyOutpostInstance.GetComponent<HPTower>();
            if (enemyHP == null) enemyHP = spawnedEnemyOutpostInstance.GetComponentInChildren<HPTower>();

            if (enemyHP != null)
            {
                enemyHP.OnDeathEvent -= OnEnemyOutpostDestroyed;
                enemyHP.OnDeathEvent += OnEnemyOutpostDestroyed;
            }
            Debug.Log($"[SettlementZone] 🏰 Đã khởi tạo Căn cứ Địch thành công tại {settlementName}.");
        }
        else
        {
            Debug.LogWarning($"[SettlementZone] ⚠️ Chưa gán enemyOutpostPrefab cho vùng đất {settlementName}!");
        }
    }

    public bool HasValidEnemyOutpostInstance()
    {
        if (!hasEnemyOutpost || spawnedEnemyOutpostInstance == null) return false;
        if (townHallBuilding != null && spawnedEnemyOutpostInstance == townHallBuilding.gameObject) return false;

        UpgradeableBuilding building = spawnedEnemyOutpostInstance.GetComponent<UpgradeableBuilding>();
        if (building == null) building = spawnedEnemyOutpostInstance.GetComponentInChildren<UpgradeableBuilding>(true);
        return building == null || !IsTownHallBuilding(building, this);
    }

    /// <summary>
    /// Dọn dẹp / Hủy bỏ toàn bộ GameObject Căn cứ Địch & EnemySpawn thuộc về Vùng đất này khi đã giải phóng / chiếm lĩnh
    /// </summary>
    public void CleanUpEnemyOutposts()
    {
        if (spawnedEnemyOutpostInstance != null)
        {
            if (HasValidEnemyOutpostInstance() || (townHallBuilding != null && spawnedEnemyOutpostInstance != townHallBuilding.gameObject))
            {
                spawnedEnemyOutpostInstance.SetActive(false);
                if (Application.isPlaying) Destroy(spawnedEnemyOutpostInstance);
                else DestroyImmediate(spawnedEnemyOutpostInstance);
            }
            spawnedEnemyOutpostInstance = null;
        }

        // Dọn dẹp mọi EnemySpawn còn sót lại dưới transform vùng đất
        EnemySpawn[] childSpawns = GetComponentsInChildren<EnemySpawn>(true);
        foreach (var spawner in childSpawns)
        {
            if (spawner != null && spawner.gameObject != this.gameObject && (townHallBuilding == null || spawner.gameObject != townHallBuilding.gameObject))
            {
                spawner.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(spawner.gameObject);
                else DestroyImmediate(spawner.gameObject);
            }
        }
        enemySpawn = null;
    }

    public void OnEnemyOutpostDestroyed()
    {
        hasEnemyOutpost = false;
        isUnlocked = true;
        SaveSettlementState();
        CleanUpEnemyOutposts();

        Debug.Log($"[SettlementZone] 🎉 CHINH PHỤC THÀNH CÔNG! Đã tiêu diệt Căn cứ Địch tại vùng đất {settlementName}!");

        // 🔓 Tự động mở khóa & hiển thị Vùng đất Bậc tiếp theo
        if (SettlementManager.Ins != null)
        {
            SettlementManager.Ins.UpdateAllZoneTiers();
        }

        if (SettlementSidePanelUI.Ins != null)
        {
            SettlementSidePanelUI.Ins.UpdateHeaderVisual();
            SettlementSidePanelUI.Ins.RefreshPanel();
        }
        UpdateZoneVisualText();
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
            const int foodCost = 0;
            if (!JsonDataManager.Ins.HasEnoughResources(establishWoodCost, establishStoneCost, foodCost))
            {
                if (UIManager.Ins != null) UIManager.Ins.ShowWarning("Không đủ tài nguyên xây Nhà Chính!");
                return false;
            }

            JsonDataManager.Ins.AddWood(-establishWoodCost);
            JsonDataManager.Ins.AddStone(-establishStoneCost);
        }

        isTownHallEstablished = true;
        settlementLevel = 1;
        SaveSettlementState();
        UpdateZoneVisualText();

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
        SaveSettlementState();

        if (townHallBuilding != null)
        {
            townHallBuilding.LoadBuildingData(settlementLevel - 1, townHallBuilding.IsRuined, false);
            townHallBuilding.Upgrade();
        }

        SaveSettlementState();
        Update3DSlotVisibility();
        Debug.Log($"[SettlementZone] 🚀 Đã nâng cấp vùng đất {settlementName} lên Cấp {settlementLevel}!");
        if (SettlementSidePanelUI.Ins != null)
        {
            SettlementSidePanelUI.Ins.UpdateHeaderVisual();
            SettlementSidePanelUI.Ins.RefreshPanel();
        }
        UpdateZoneVisualText();
    }

    /// <summary>
    /// Kiểm tra xem vị trí 3D slot này đã có công trình thuộc Vùng đất chiếm đóng chưa (bán kính 3.0m)
    /// </summary>
    public bool IsPositionOccupiedByBuilding(Vector3 position)
    {
        if (townHallBuilding != null && townHallBuilding.gameObject.activeSelf)
        {
            if (Vector3.Distance(townHallBuilding.transform.position, position) < 3.5f) return true;
        }

        foreach (var b in builtStructures)
        {
            if (b != null && b.gameObject.activeSelf)
            {
                if (Vector3.Distance(b.transform.position, position) < 3.0f) return true;
            }
        }

        return false;
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

    /// <summary>
    /// Kiểm tra xem một UpgradeableBuilding có phải là Nhà Chính của Vùng đất hay không.
    /// </summary>
    public static bool IsTownHallBuilding(UpgradeableBuilding ub, SettlementZone zone)
    {
        if (ub == null) return false;
        if (zone != null && zone.townHallBuilding == ub) return true;
        if (ub.slotIndex >= 0) return false;
        if (zone != null && zone.townHallPoint != null && Vector3.Distance(ub.transform.position, zone.townHallPoint.position) < 3.5f)
        {
            return true;
        }
        if (ub.buildingName.ToLower().Contains("nhà chính") || ub.buildingName.ToLower().Contains("town hall") || ub.buildingName.ToLower().Contains("townhall") || ub.name.ToLower().Contains("townhall")) return true;
        return false;
    }

    /// <summary>
    /// Lấy chỉ số Slot (0, 1, 2...) tại vị trí 3D cho trước.
    /// </summary>
    public int GetSlotIndexAtPosition(Vector3 pos)
    {
        float minDistance = float.MaxValue;
        int closestSlotIndex = -1;

        for (int i = 0; i < slotPoints.Count; i++)
        {
            if (slotPoints[i] == null) continue;
            float dist = Vector3.Distance(pos, slotPoints[i].position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestSlotIndex = i;
            }
        }

        if (minDistance < 8.0f) return closestSlotIndex;

        return -1;
    }

    /// <summary>
    /// Lấy công trình đang ở slot tương ứng.
    /// </summary>
    public UpgradeableBuilding GetBuildingAtSlot(int slotIndex)
    {
        if (slotIndex < 0) return townHallBuilding;

        foreach (var b in builtStructures)
        {
            if (b != null && b.slotIndex == slotIndex)
            {
                return b;
            }
        }

        return null;
    }

    /// <summary>
    /// Đảm bảo tất cả các công trình thuộc Transform này đều được đăng ký đầy đủ.
    /// </summary>
    public void EnsureAllBuildingsRegistered()
    {
        UpgradeableBuilding[] childUbs = GetComponentsInChildren<UpgradeableBuilding>(true);
        foreach (var ub in childUbs)
        {
            if (ub != null)
            {
                RegisterBuilding(ub);
            }
        }
    }
}
