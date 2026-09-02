using UnityEngine;
using System.Collections.Generic;
using TMPro;

/*
 * ConstructionManager.cs
 * Folder: Scripts/Building/
 * Dự án: KHẨN HOANG (PENTA DEV)
 */

public class ConstructionManager : Singleton<ConstructionManager>
{
    [System.Serializable]
    public struct BuildingCost
    {
        public BuildingType buildingType;
        public int goldCost;
        public int woodCost;
        public int stoneCost;
        public int foodCost;

        [Header("UI Text Hiển Thị Giá Riêng (Kéo thả TextMeshPro vào đây)")]
        public TextMeshProUGUI uiWoodText;
        public TextMeshProUGUI uiStoneText;
        public TextMeshProUGUI uiFoodText;
    }

    [Header("Cấu hình chi phí xây dựng nhà")]
    public List<BuildingCost> constructionCosts = new List<BuildingCost>();

    [Header("Cấu hình tăng trưởng giá")]
    [Range(0f, 100f)] public float costIncreasePercentage = 10f;

    [Header("Prefab thật - Dân sự")]
    public GameObject housePrefab;
    public GameObject woodCutterPrefab;
    public GameObject stoneMinePrefab;
    public GameObject kitchenPrefab;
    public GameObject foodStoragePrefab;
    public GameObject stoneStoragePrefab;
    public GameObject warehousePrefab;

    [Header("Prefab thật - Phòng thủ")]
    public GameObject watchTowerPrefab;
    public GameObject archerTowerPrefab;
    public GameObject cannonPrefab;

    [Header("Prefab thật - Quân sự (Nhà lính)")]
    public GameObject barracksMeleePrefab;
    public GameObject barracksArcherPrefab;
    public GameObject barracksSpearPrefab;

    [Header("Prefab thật - Tài nguyên")]
    public GameObject woodPrefab;
    public GameObject ricePrefab;
    public GameObject stonePrefab;

    private Dictionary<BuildingType, int> buildingCounts = new Dictionary<BuildingType, int>();
    private DemaciaConstructionPricing demaciaPricing;

    /// <summary>Giá tập trung gắn cùng GameObject với ConstructionManager.</summary>
    public DemaciaConstructionPricing DemaciaPricing
    {
        get
        {
            if (demaciaPricing == null)
            {
                demaciaPricing = GetComponent<DemaciaConstructionPricing>();
            }

            return demaciaPricing;
        }
    }

    private void Start()
    {
        BuildingCtrl[] existingBuildings = FindObjectsOfType<BuildingCtrl>();
        foreach (BuildingCtrl building in existingBuildings)
        {
            BuildingType type = building.buildingType;
            if (!buildingCounts.ContainsKey(type))
            {
                buildingCounts[type] = 0;
            }
            buildingCounts[type]++;
        }

        UpdateAllCostUI();
    }

    public void ResetBuildingCounts()
    {
        buildingCounts.Clear();
    }

    public BuildingCost GetBuildingCost(BuildingType type)
    {
        if (DemaciaPricing != null && DemaciaPricing.TryGetConstructionCost(type, out DemaciaConstructionPricing.ResourceCost demaciaCost))
        {
            // Bảng Demacia là giá cố định: không tăng dần theo số lượng công trình.
            return new BuildingCost
            {
                buildingType = type,
                goldCost = demaciaCost.goldCost,
                woodCost = demaciaCost.woodCost,
                stoneCost = demaciaCost.stoneCost,
                foodCost = 0
            };
        }

        BuildingCost baseCost = new BuildingCost { buildingType = type, goldCost = 0, woodCost = 0, stoneCost = 0, foodCost = 0 };
        foreach (var cost in constructionCosts)
        {
            if (cost.buildingType == type)
            {
                baseCost = cost;
                break;
            }
        }
        int count = 0;
        if (buildingCounts.ContainsKey(type))
        {
            count = buildingCounts[type];
        }
        if (count > 0)
        {
            float rate = costIncreasePercentage / 100f;
            float multiplier = 1f + (rate * count);
            baseCost.woodCost = Mathf.RoundToInt(baseCost.woodCost * multiplier);
            baseCost.stoneCost = Mathf.RoundToInt(baseCost.stoneCost * multiplier);
        }

        // Lương thực chỉ dùng để giới hạn huấn luyện lính, không phải chi phí xây dựng.
        baseCost.foodCost = 0;
        return baseCost;
    }

    /// <summary>Lấy giá lập Nhà Chính theo tên SettlementZone từ bảng giá tập trung.</summary>
    public BuildingCost GetSettlementEstablishCost(SettlementZone zone)
    {
        BuildingCost legacyCost = new BuildingCost
        {
            buildingType = BuildingType.House,
            goldCost = 0,
            woodCost = zone != null ? zone.establishWoodCost : 0,
            stoneCost = zone != null ? zone.establishStoneCost : 0,
            foodCost = 0
        };

        if (zone != null && DemaciaPricing != null &&
            DemaciaPricing.TryGetSettlementBuildCost(zone.settlementName, out DemaciaConstructionPricing.ResourceCost demaciaCost))
        {
            legacyCost.goldCost = demaciaCost.goldCost;
            legacyCost.woodCost = demaciaCost.woodCost;
            legacyCost.stoneCost = demaciaCost.stoneCost;
        }

        return legacyCost;
    }

    public void UpdateCostUI(BuildingType type)
    {
        BuildingCost realCost = GetBuildingCost(type);

        for (int i = 0; i < constructionCosts.Count; i++)
        {
            if (constructionCosts[i].buildingType == type)
            {
                if (constructionCosts[i].uiWoodText != null) 
                    constructionCosts[i].uiWoodText.text = realCost.woodCost.ToString();

                if (constructionCosts[i].uiStoneText != null) 
                    constructionCosts[i].uiStoneText.text = realCost.stoneCost.ToString();

                if (constructionCosts[i].uiFoodText != null)
                    constructionCosts[i].uiFoodText.gameObject.SetActive(false);
                
                break;
            }
        }
    }

    public void UpdateAllCostUI()
    {
        foreach (var cost in constructionCosts)
        {
            UpdateCostUI(cost.buildingType);
        }
    }

    public void PlaceBuilding(BuildingType type, Vector3 position, Quaternion rotation)
    {
        if (TroopTrainingManager.IsCentralBarracksType(type))
        {
            Debug.LogWarning("[ConstructionManager] Trại Lính là công trình trung tâm, không thể xây thêm.");
            UIManager.Ins?.ShowWarning("Trại Lính chỉ có sẵn tại thành đầu tiên và không thể xây thêm!");
            return;
        }

        if (!SettlementZone.IsBuildingTypeUnlockedGlobally(type))
        {
            Debug.LogWarning($"[ConstructionManager] Công trình {type} chưa được mở khóa.");
            UIManager.Ins?.ShowWarning("Công trình này chưa được mở khóa. Hãy chinh phục vùng đất tương ứng trước!");
            return;
        }

        BuildingCost cost = GetBuildingCost(type);

        // Không trừ tài nguyên nếu prefab chưa được gán cho loại công trình này.
        if (GetPrefab(type) == null)
        {
            Debug.LogWarning($"[ConstructionManager] Chưa có prefab cho {type}.");
            UIManager.Ins?.ShowWarning("Công trình này chưa được cấu hình prefab.");
            return;
        }

        if (JsonDataManager.Ins != null)
        {
            if (!JsonDataManager.Ins.TrySpendCombined(cost.woodCost, cost.stoneCost, 0, cost.goldCost))
            {
                Debug.LogWarning($"[ConstructionManager] Thiếu tài nguyên xây {type}!");
                UIManager.Ins?.ShowWarning("Không đủ Vàng, Gỗ hoặc Đá để xây công trình này!");
                return;
            }

            JsonDataManager.Ins.BroadcastAllResources();
        }

        var spawned = SpawnBuilding(type, position, rotation);
        if (spawned != null)
        {
            UpgradeableBuilding ub = spawned.GetComponent<UpgradeableBuilding>();
            if (ub == null) ub = spawned.GetComponentInChildren<UpgradeableBuilding>();
            if (ub != null)
            {
                // 🔥 Kích hoạt tiến trình THI CÔNG XÂY DỰNG BAN ĐẦU cho công trình vừa sinh từ slot
                ub.StartInitialBuildProcess();
                if (SettlementManager.Ins != null && SettlementManager.Ins.CurrentSettlement != null)
                {
                    var zone = SettlementManager.Ins.CurrentSettlement;
                    int slotIdx = zone.GetSlotIndexAtPosition(position);
                    if (slotIdx >= 0)
                    {
                        ub.slotIndex = slotIdx;
                        ub.transform.position = zone.GetSlotWorldPosition(slotIdx);
                    }
                    zone.RegisterBuilding(ub);
                }
            }

            if (!buildingCounts.ContainsKey(type)) buildingCounts[type] = 0;
            buildingCounts[type]++;

            UpdateCostUI(type);
            JsonDataManager.RegisterStat_BuildingConstructed();
            SettlementSidePanelUI.Ins?.RefreshPanel();

            CampaignTutorialManager.Ins?.OnBuildingPlaced(type);

            // 🔥 Lưu trạng thái công trình vừa xây vào file Save Slot 1
            BuildingSystem.Ins?.SaveBuildingsToSlot(1);

            Debug.Log($"[ConstructionManager] ✅ Đã xây {type} thành công!");
        }
    }

    public BuildingCtrl SpawnBuilding(BuildingType type, Vector3 position, Quaternion rotation)
    {
        GameObject prefab = GetPrefab(type);

        if (prefab == null) return null;

        Transform parentTransform = (SettlementManager.Ins != null && SettlementManager.Ins.CurrentSettlement != null)
            ? SettlementManager.Ins.CurrentSettlement.transform
            : null;

        GameObject obj = Instantiate(prefab, position, rotation);
        if (parentTransform != null)
        {
            obj.transform.SetParent(parentTransform, true);
        }
        obj.name = type.ToString();

        UpgradeableBuilding ub = obj.GetComponent<UpgradeableBuilding>();
        if (ub != null && SettlementManager.Ins != null && SettlementManager.Ins.CurrentSettlement != null)
        {
            SettlementManager.Ins.CurrentSettlement.RegisterBuilding(ub);
        }

        return obj.GetComponent<BuildingCtrl>();
    }

    public GameObject GetPrefab(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.House: return housePrefab;
            case BuildingType.WoodCutter: return woodCutterPrefab;
            case BuildingType.StoneMine: return stoneMinePrefab;
            case BuildingType.Kitchen: return kitchenPrefab;
            case BuildingType.FoodStorage: return foodStoragePrefab;
            case BuildingType.StoneStorage: return stoneStoragePrefab;
            case BuildingType.Warehouse: return warehousePrefab;
            case BuildingType.WatchTower: return watchTowerPrefab;
            case BuildingType.ArcherTower: return archerTowerPrefab;
            case BuildingType.Cannon: return cannonPrefab;
            case BuildingType.BarracksMelee: return barracksMeleePrefab;
            case BuildingType.BarracksArcher: return barracksArcherPrefab;
            case BuildingType.BarracksSpear: return barracksSpearPrefab;
            case BuildingType.Wood: return woodPrefab;
            case BuildingType.Rice: return ricePrefab;
            case BuildingType.Stone: return stonePrefab;
            default: return null;
        }
    }
}
