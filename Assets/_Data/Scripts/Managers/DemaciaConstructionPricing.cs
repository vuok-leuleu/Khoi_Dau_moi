using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bảng giá xây dựng/nâng cấp tập trung theo Demacia Rising.
/// Gắn component này vào đúng GameObject đang có ConstructionManager.
/// Chỉ sử dụng Vàng (Khiên bạc), Gỗ và Đá; các giá chưa có dữ liệu Demacia
/// đầy đủ vẫn được đặt tại đây để chỉnh một chỗ trong Inspector.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ConstructionManager))]
public class DemaciaConstructionPricing : MonoBehaviour
{
    [Serializable]
    public struct ResourceCost
    {
        [Min(0)] public int goldCost;
        [Min(0)] public int woodCost;
        [Min(0)] public int stoneCost;

        public ResourceCost(int gold, int wood, int stone)
        {
            goldCost = Mathf.Max(0, gold);
            woodCost = Mathf.Max(0, wood);
            stoneCost = Mathf.Max(0, stone);
        }
    }

    [Serializable]
    public class UpgradePrice
    {
        [Tooltip("Cấp độ đạt được sau khi nâng. Ví dụ: 2 nghĩa là Lv1 -> Lv2.")]
        [Min(2)] public int targetLevel = 2;
        public ResourceCost cost;

        [TextArea(1, 3)]
        public string note;
    }

    [Serializable]
    public class BuildingPrice
    {
        public BuildingType buildingType;
        public ResourceCost constructionCost;
        public List<UpgradePrice> upgradePrices = new List<UpgradePrice>();

        [TextArea(1, 3)]
        public string note;
    }

    [Serializable]
    public class SettlementBuildPrice
    {
        [Tooltip("Tên SettlementZone, không phân biệt chữ hoa/thường. Ví dụ: EVENMOOR.")]
        public string settlementName;
        public ResourceCost constructionCost;

        [TextArea(1, 3)]
        public string note;
    }

    [Header("Chế độ giá")]
    [SerializeField] private bool useDemaciaPricing = true;

    [Header("Giá công trình - chỉnh trực tiếp tại ConstructionManager")]
    [SerializeField] private List<BuildingPrice> buildingPrices = new List<BuildingPrice>();

    [Header("Giá lập Nhà chính của 6 thành đầu")]
    [SerializeField] private List<SettlementBuildPrice> settlementBuildPrices = new List<SettlementBuildPrice>();

    public bool UseDemaciaPricing => useDemaciaPricing;

    private void Reset()
    {
        LoadDemaciaDefaults();
    }

    private void Awake()
    {
        EnsureDefaults();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureDefaults();
    }
#endif

    private void EnsureDefaults()
    {
        if (buildingPrices == null || buildingPrices.Count == 0)
        {
            LoadDemaciaDefaults();
        }
    }

    [ContextMenu("Nạp lại bảng giá mặc định Demacia")]
    public void LoadDemaciaDefaults()
    {
        buildingPrices = new List<BuildingPrice>
        {
            Building(BuildingType.House, Cost(0, 0, 0),
                "Nhà chính dùng giá nâng theo Demacia. Giá lập thành nằm ở bảng riêng bên dưới.",
                Upgrade(2, Cost(700, 300, 225), "Mốc Vaskasia Lv1 -> Lv2."),
                Upgrade(3, Cost(1000, 5000, 3000), "Mốc Zeffira Lv2 -> Lv3 đã xác minh.")),

            Building(BuildingType.WoodCutter, Cost(0, 0, 0),
                "Lumberyard đầu ở tutorial Zeffira miễn phí. Các mốc chưa có số công khai có thể chỉnh ngay tại đây.",
                Upgrade(2, Cost(0, 0, 100), "Lumberyard Lv1 -> Lv2 đã xác minh: 100 Đá; Kim loại được loại khỏi game này."),
                Upgrade(3, Cost(0, 0, 200), "Giá mặc định để cân bằng; chưa có số Demacia công khai xác minh.")),

            Building(BuildingType.StoneMine, Cost(250, 100, 0),
                "Quarry: 250 Khiên bạc + 100 Gỗ (đã bỏ Valor theo yêu cầu).",
                Upgrade(2, Cost(0, 100, 100), "Giá mặc định có thể chỉnh."),
                Upgrade(3, Cost(0, 200, 200), "Giá mặc định có thể chỉnh.")),

            Building(BuildingType.Kitchen, Cost(0, 20, 0),
                "Kitchen được dùng như Farm. Farm Lv1 trong Demacia: 20 Gỗ.",
                Upgrade(2, Cost(0, 40, 20), "Giá mặc định có thể chỉnh."),
                Upgrade(3, Cost(0, 80, 40), "Giá mặc định có thể chỉnh.")),

            Building(BuildingType.FoodStorage, Cost(0, 100, 100), "Không có công trình tương đương trực tiếp trong Demacia; giá được quản lý tập trung tại đây.",
                Upgrade(2, Cost(0, 10, 10), "Giá mặc định từ project cũ."),
                Upgrade(3, Cost(0, 20, 20), "Giá mặc định từ project cũ.")),

            Building(BuildingType.StoneStorage, Cost(250, 100, 0), "Prefab hiện tại đặt tên Xưởng Đá, nên dùng giá mở Quarry đã lọc Valor.",
                Upgrade(2, Cost(0, 100, 100), "Giá mặc định có thể chỉnh."),
                Upgrade(3, Cost(0, 200, 200), "Giá mặc định có thể chỉnh.")),

            Building(BuildingType.Warehouse, Cost(0, 100, 100), "Không có công trình tương đương trực tiếp trong Demacia.",
                Upgrade(2, Cost(0, 10, 10), "Giá mặc định từ project cũ."),
                Upgrade(3, Cost(0, 20, 20), "Giá mặc định từ project cũ.")),

            Building(BuildingType.WatchTower, Cost(0, 100, 100), "Không có bảng giá Demacia được xác minh cho tháp canh.",
                Upgrade(2, Cost(0, 10, 10), "Giá mặc định từ project cũ."),
                Upgrade(3, Cost(0, 20, 20), "Giá mặc định từ project cũ.")),

            Building(BuildingType.ArcherTower, Cost(0, 100, 100), "Không có công trình tương đương trực tiếp trong Demacia.",
                Upgrade(2, Cost(0, 10, 10), "Giá mặc định từ project cũ."),
                Upgrade(3, Cost(0, 20, 20), "Giá mặc định từ project cũ.")),

            Building(BuildingType.Cannon, Cost(0, 100, 100), "Không có công trình tương đương trực tiếp trong Demacia.",
                Upgrade(2, Cost(0, 10, 10), "Giá mặc định từ project cũ."),
                Upgrade(3, Cost(0, 20, 20), "Giá mặc định từ project cũ.")),

            Building(BuildingType.BarracksMelee, Cost(0, 0, 0), "Trại lính trung tâm ở Zeffira có sẵn và ConstructionManager chặn xây thêm.",
                Upgrade(2, Cost(0, 0, 0), "Chỉnh được nếu sau này cho phép nâng."),
                Upgrade(3, Cost(0, 0, 0), "Chỉnh được nếu sau này cho phép nâng.")),

            Building(BuildingType.BarracksArcher, Cost(0, 0, 0), "Chưa dùng prefab trong scene hiện tại."),
            Building(BuildingType.BarracksSpear, Cost(0, 0, 0), "Chưa dùng prefab trong scene hiện tại.")
        };

        settlementBuildPrices = new List<SettlementBuildPrice>
        {
            Settlement("VASKASIA", Cost(0, 0, 0), "Tutorial không công khai phí lập thành riêng; để 0 để bạn chỉnh nếu cần."),
            Settlement("EVENMOOR", Cost(0, 200, 0), "200 Gỗ."),
            Settlement("BROOKHOLLOW", Cost(700, 200, 0), "700 Khiên bạc + 200 Gỗ."),
            Settlement("TERBISIA", Cost(700, 400, 300), "700 Khiên bạc + 400 Gỗ + 300 Đá."),
            Settlement("TYLBURNE", Cost(700, 400, 300), "700 Khiên bạc + 400 Gỗ + 300 Đá.")
        };
    }

    public bool TryGetConstructionCost(BuildingType buildingType, out ResourceCost cost)
    {
        if (!useDemaciaPricing)
        {
            cost = default;
            return false;
        }

        BuildingPrice price = FindBuildingPrice(buildingType);
        if (price == null)
        {
            cost = default;
            return false;
        }

        cost = price.constructionCost;
        return true;
    }

    public bool TryGetUpgradeCost(BuildingType buildingType, int targetLevel, out ResourceCost cost)
    {
        if (!useDemaciaPricing)
        {
            cost = default;
            return false;
        }

        BuildingPrice price = FindBuildingPrice(buildingType);
        if (price != null && price.upgradePrices != null)
        {
            foreach (UpgradePrice upgradePrice in price.upgradePrices)
            {
                if (upgradePrice != null && upgradePrice.targetLevel == targetLevel)
                {
                    cost = upgradePrice.cost;
                    return true;
                }
            }
        }

        cost = default;
        return false;
    }

    public bool TryGetSettlementBuildCost(string settlementName, out ResourceCost cost)
    {
        if (!useDemaciaPricing || string.IsNullOrWhiteSpace(settlementName) || settlementBuildPrices == null)
        {
            cost = default;
            return false;
        }

        foreach (SettlementBuildPrice price in settlementBuildPrices)
        {
            if (price != null && string.Equals(price.settlementName, settlementName, StringComparison.OrdinalIgnoreCase))
            {
                cost = price.constructionCost;
                return true;
            }
        }

        cost = default;
        return false;
    }

    public static bool CanAfford(ResourceCost cost)
    {
        return JsonDataManager.Ins == null ||
               JsonDataManager.Ins.HasEnoughResources(cost.woodCost, cost.stoneCost, 0, cost.goldCost);
    }

    public static bool TrySpend(ResourceCost cost)
    {
        return JsonDataManager.Ins == null ||
               JsonDataManager.Ins.TrySpendCombined(cost.woodCost, cost.stoneCost, 0, cost.goldCost);
    }

    private BuildingPrice FindBuildingPrice(BuildingType buildingType)
    {
        if (buildingPrices == null) return null;

        foreach (BuildingPrice price in buildingPrices)
        {
            if (price != null && price.buildingType == buildingType) return price;
        }

        return null;
    }

    private static ResourceCost Cost(int gold, int wood, int stone)
    {
        return new ResourceCost(gold, wood, stone);
    }

    private static UpgradePrice Upgrade(int targetLevel, ResourceCost cost, string note)
    {
        return new UpgradePrice { targetLevel = targetLevel, cost = cost, note = note };
    }

    private static BuildingPrice Building(BuildingType type, ResourceCost constructionCost, string note, params UpgradePrice[] upgrades)
    {
        return new BuildingPrice
        {
            buildingType = type,
            constructionCost = constructionCost,
            note = note,
            upgradePrices = new List<UpgradePrice>(upgrades)
        };
    }

    private static SettlementBuildPrice Settlement(string name, ResourceCost constructionCost, string note)
    {
        return new SettlementBuildPrice { settlementName = name, constructionCost = constructionCost, note = note };
    }
}
