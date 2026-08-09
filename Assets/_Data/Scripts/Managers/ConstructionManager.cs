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
        BuildingCost baseCost = new BuildingCost { buildingType = type, woodCost = 0, stoneCost = 0, foodCost = 0 };
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
            baseCost.foodCost = Mathf.RoundToInt(baseCost.foodCost * multiplier);
        }
        return baseCost;
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
                    constructionCosts[i].uiFoodText.text = realCost.foodCost.ToString();
                
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
        BuildingCost cost = GetBuildingCost(type);

        if (JsonDataManager.Ins != null)
        {
            if (!JsonDataManager.Ins.HasEnoughResources(cost.woodCost, cost.stoneCost, cost.foodCost))
            {
                Debug.LogWarning($"[ConstructionManager] Thiếu tài nguyên xây {type}!");
                return;
            }

            JsonDataManager.Ins.AddWood(-cost.woodCost);
            JsonDataManager.Ins.AddStone(-cost.stoneCost);
            JsonDataManager.Ins.AddFood(-cost.foodCost);
            JsonDataManager.Ins.BroadcastAllResources();
        }

        var spawned = SpawnBuilding(type, position, rotation);
        if (spawned != null)
        {
            UpgradeableBuilding ub = spawned.GetComponent<UpgradeableBuilding>();
            if (ub == null) ub = spawned.GetComponentInChildren<UpgradeableBuilding>();
            if (ub != null)
            {
                ub.IsInitialBuildNeeded = false;
                if (SettlementManager.Ins != null && SettlementManager.Ins.CurrentSettlement != null)
                {
                    SettlementManager.Ins.CurrentSettlement.RegisterBuilding(ub);
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

    private GameObject GetPrefab(BuildingType type)
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