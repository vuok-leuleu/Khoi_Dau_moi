using UnityEngine;

public class UIPhaoThu : MonoBehaviour
{
    public static UIPhaoThu Instance;

    [Header("Tag")]
    public string cannonTag = "Cannon";

    public int cannonCount;


    void Awake()
    {
        Instance = this;
    }


    void Update()
    {
        cannonCount = CountCannons();
    }


    public int GetCannonCount()
    {
        return cannonCount;
    }


    public int CountCannons()
    {
        int count = 0;

        // 1. Đếm công trình Pháo Thu qua UpgradeableBuilding theo BuildingType.Cannon
        UpgradeableBuilding[] buildings = Object.FindObjectsByType<UpgradeableBuilding>(FindObjectsSortMode.None);
        foreach (var b in buildings)
        {
            if (b != null && b.gameObject.activeInHierarchy && b.buildingType == BuildingType.Cannon && !b.IsRuined)
            {
                count++;
            }
        }

        // 2. Dự phòng: Thử tìm theo Tag "Cannon" (nếu tag đã được định nghĩa trong Unity Tag Manager)
        try
        {
            GameObject[] cannons = GameObject.FindGameObjectsWithTag(cannonTag);
            if (cannons != null && cannons.Length > count)
            {
                count = cannons.Length;
            }
        }
        catch (UnityException)
        {
            // Bỏ qua lỗi nếu Tag "Cannon" chưa được tạo trong Project Settings -> Tags & Layers
        }

        return count;
    }
}