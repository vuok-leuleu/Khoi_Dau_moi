using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/*
 * FoodSyncHook.cs
 * Folder: Scripts/Systems/
 * Người làm: NhậtTiến
 * Dự án: KHẨN HOANG (PENTA DEV)
 *
 * NHIỆM VỤ:
 *   Hiển thị chỉ số Lúa Mì {Đang Dùng}/{Tổng} lên HUD một cách chính xác và mượt mà.
 *   Hoàn toàn ĐỘC LẬP — KHÔNG sửa đổi hay ghi đè bất kỳ file nào của thành viên khác.
 *
 * QUY TẮC LÚA MÌ:
 *   - Max  = 1 (cơ bản) + mỗi Kho Lúa xây xong (Lv1 +1, Lv2 +2, ...)
 *   - Used = Số đạo lính còn sống (3 lính = 1 đạo = 1 lúa), kể cả điều quân/đồn trú thành khác
 *   - Lúa CHỈ hoàn lại khi lính CHẾT THẬT SỰ — không phải khi điều quân
 *
 * CƠ CHẾ:
 *   1. Tự khởi động qua [RuntimeInitializeOnLoadMethod] — không cần kéo vào Scene hay Prefab.
 *   2. Cập nhật 10 lần/giây (mỗi 0.1s) — hiệu năng nhẹ, hiển thị mượt mà.
 *   3. Sau mỗi lần Scene load: chờ hệ thống nạp xong rồi cập nhật lập tức.
 */

public class FoodSyncHook : MonoBehaviour
{
    private static FoodSyncHook _instance;
    private float _refreshTimer;
    private const float REFRESH_INTERVAL = 0.1f;

    // Mỗi đạo lính gồm 3 lính → 1 đơn vị lúa (khớp với SOLDIERS_PER_TRAINING_UNIT trong TroopTrainingManager)
    private const int SOLDIERS_PER_FOOD_UNIT = 3;

    // ──────────────────────────────────────────────────────────────
    // KHỞI ĐỘNG TỰ ĐỘNG
    // ──────────────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInit()
    {
        if (_instance != null) return;
        GameObject go = new GameObject("[FoodSyncHook]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<FoodSyncHook>();
    }

    private void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "SceneBattle") return;
        StartCoroutine(DelayedSync());
    }

    private IEnumerator DelayedSync()
    {
        // Chờ 3 frame + 0.3s để BuildingSystem, BattleData, UnitController nạp xong
        yield return null;
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.3f);
        ForceRefreshHUD();
    }

    // ──────────────────────────────────────────────────────────────
    // CẬP NHẬT ĐỊNH KỲ
    // ──────────────────────────────────────────────────────────────

    private void Update()
    {
        _refreshTimer += Time.unscaledDeltaTime;
        if (_refreshTimer < REFRESH_INTERVAL) return;

        _refreshTimer = 0f;
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        TextMeshProUGUI foodText = GetFoodText();
        if (foodText == null) return;

        int used = CalcUsedFood();
        int max  = CalcMaxFood();
        string display = $"{used}/{max}";

        if (foodText.text != display)
            foodText.text = display;

        // Đồng bộ số lúa khả dụng vào JsonDataManager
        int available = Mathf.Max(0, max - used);
        if (JsonDataManager.Ins != null && JsonDataManager.Ins.food != available)
            JsonDataManager.Ins.SetFood(available);
    }

    // ──────────────────────────────────────────────────────────────
    // TÍNH SỐ LÚA ĐANG DÙNG
    //
    // Công thức:
    //   Used = (Số slot đang huấn luyện) + Ceil(Tổng lính còn sống / 3)
    //
    // Lý do dùng tổng lính thay vì slot index:
    //   Slot index là cục bộ trong từng thành. Khi lính sang thành khác,
    //   slot 0 ở thành A và slot 0 ở thành B sẽ bị nhầm là cùng 1 đạo
    //   nếu dùng HashSet theo slot index → đếm thiếu.
    //   Đếm tổng lính thực tế / 3 là cách chính xác nhất.
    // ──────────────────────────────────────────────────────────────

    private static int CalcUsedFood()
    {
        int usedCount = 0;

        // Phần 1: Slot đang huấn luyện trong hàng đợi (chưa ra lính thực tế)
        if (TroopTrainingManager.Ins != null)
        {
            SettlementZone central = TroopTrainingManager.Ins.CentralSettlement;
            if (central != null)
            {
                TroopTrainingSlotData[] slots = TroopTrainingManager.Ins.GetSlotsForZone(central);
                if (slots != null)
                {
                    for (int i = 0; i < TroopTrainingManager.MAX_TRAINING_SLOTS; i++)
                    {
                        if (slots[i] != null && slots[i].isTraining)
                            usedCount++;
                    }
                }
            }
        }

        // Phần 2: Đếm tổng số lính còn sống trên toàn bản đồ → chia 3 → số đạo
        // Gộp lính ở thành trung tâm + đang hành quân + đồn trú thành khác
        int totalAlive = 0;
        UnitController[] allUnits = Object.FindObjectsByType<UnitController>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (UnitController unit in allUnits)
        {
            if (unit == null || unit.isDead || !unit.gameObject.activeInHierarchy) continue;
            totalAlive++;
        }

        // CeilToInt: 1-3 lính = 1 đạo = 1 lúa, 4-6 lính = 2 đạo = 2 lúa...
        usedCount += Mathf.CeilToInt(totalAlive / (float)SOLDIERS_PER_FOOD_UNIT);
        return usedCount;
    }

    // ──────────────────────────────────────────────────────────────
    // TÍNH SỐ LÚA TỐI ĐA
    // Base = 1, mỗi Kho Lúa xây xong cộng (CurrentLevel + 1)
    // ──────────────────────────────────────────────────────────────

    private static int CalcMaxFood()
    {
        // Ưu tiên dùng hàm của TroopTrainingManager
        if (TroopTrainingManager.Ins != null)
            return TroopTrainingManager.Ins.GetTotalFoodCapacity();

        // Fallback tự tính nếu TroopTrainingManager chưa sẵn sàng
        int total = 1;
        UpgradeableBuilding[] allBuildings = Object.FindObjectsByType<UpgradeableBuilding>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var b in allBuildings)
        {
            if (b == null || !b.gameObject.activeInHierarchy) continue;
            if (b.IsInitialBuildNeeded || b.IsRuined || b.IsUpgrading) continue;

            string nameLow  = b.gameObject.name.ToLower();
            string bNameLow = b.buildingName != null ? b.buildingName.ToLower() : "";

            bool isFood = b.buildingType == BuildingType.FoodStorage
                       || b.buildingType == BuildingType.Rice
                       || nameLow.Contains("food")  || nameLow.Contains("lúa") || nameLow.Contains("lương")
                       || bNameLow.Contains("lúa") || bNameLow.Contains("lương");

            if (isFood)
                total += (b.CurrentLevel + 1);
        }

        return total;
    }

    // ──────────────────────────────────────────────────────────────
    // HỖ TRỢ — Cache foodText để tránh Find mỗi frame
    // ──────────────────────────────────────────────────────────────

    private static TextMeshProUGUI _cachedFoodText;
    private static float _cacheTimer;

    private static TextMeshProUGUI GetFoodText()
    {
        if (_cachedFoodText != null && Time.unscaledTime - _cacheTimer < 2f)
            return _cachedFoodText;

        if (HUDController.Instance != null)
            _cachedFoodText = HUDController.Instance.foodText;

        _cacheTimer = Time.unscaledTime;
        return _cachedFoodText;
    }

    private static void ForceRefreshHUD()
    {
        _cachedFoodText = null; // reset cache để tìm lại sau scene load
        TextMeshProUGUI foodText = GetFoodText();
        if (foodText == null) return;

        int used = CalcUsedFood();
        int max  = CalcMaxFood();
        foodText.text = $"{used}/{max}";
    }
}
