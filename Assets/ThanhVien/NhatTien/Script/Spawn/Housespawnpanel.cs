using UnityEngine;
using UnityEngine.UI;

/*
 * HouseSpawnPanel.cs
 * Folder: Scripts/Spawning/
 * Dự án: KHẨN HOANG (PENTA DEV)
 *
 * CHỨC NĂNG CẬP NHẬT:
 * - Không tự sinh UI floating lơ lửng trên đầu House nữa.
 * - Tự tạo Panel chứa 3 nút Chiêu Mộ cố định ở GÓC DƯỚI BÊN PHẢI MÀN HÌNH (Screen Space).
 * - Đổi tên danh xưng: Thợ chặt gỗ, Nông dân, Thợ mỏ.
 * - Tiêu tốn 10 Lúa (Food) khi chiêu mộ.
 */

public class HouseSpawnPanel : MonoBehaviour
{
    [Header("References")]
    public House house;

    [Header("Cấu hình Chi phí")]
    [Tooltip("Số Lúa tiêu tốn để mua 1 dân làng.")]
    public int foodCostPerWorker = 10;

    private GameObject screenCanvasObj;
    private GameObject spawnPanelObj;

    private void Awake()
    {
        if (house == null) house = GetComponent<House>();
    }

    private void Start()
    {
    }

    /// <summary>
    /// Tạo Panel Chiêu Mộ ở Góc Dưới Bên Phải Màn Hình bằng Code
    /// </summary>
    private void CreateScreenSpaceUI()
    {
        // 1. Tìm hoặc Tạo Screen Space Canvas
        screenCanvasObj = GameObject.Find("WorkerSpawn_ScreenCanvas");
        if (screenCanvasObj == null)
        {
            screenCanvasObj = new GameObject("WorkerSpawn_ScreenCanvas");
            Canvas canvas = screenCanvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            CanvasScaler scaler = screenCanvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            screenCanvasObj.AddComponent<GraphicRaycaster>();
        }

        // 2. Tạo Panel chứa các nút bấm cố định Góc Dưới Bên Phải (Bottom-Right)
        spawnPanelObj = new GameObject("BottomRight_SpawnPanel");
        spawnPanelObj.transform.SetParent(screenCanvasObj.transform, false);

        RectTransform panelRect = spawnPanelObj.AddComponent<RectTransform>();
        // Anchor to Bottom-Right
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-30f, 30f); // Thụt vào lề 30px
        panelRect.sizeDelta = new Vector2(170f, 230f);

        Image panelBg = spawnPanelObj.AddComponent<Image>();
        panelBg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        VerticalLayoutGroup layout = spawnPanelObj.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        // 3. Tạo 3 Nút Chiêu Mộ với Tên Mới & Giá 10 Lúa
        CreateWorkerButton("BtnWood", "Thợ chặt gỗ\n(10 🌾)", new Color(0.2f, 0.6f, 0.2f), WorkerSpawner.WorkerType.Tree);
        CreateWorkerButton("BtnRice", "Nông dân\n(10 🌾)", new Color(0.8f, 0.65f, 0.1f), WorkerSpawner.WorkerType.Rice);
        CreateWorkerButton("BtnStone", "Thợ mỏ\n(10 🌾)", new Color(0.45f, 0.45f, 0.5f), WorkerSpawner.WorkerType.Stone);

        // Ban đầu ẩn Panel
        spawnPanelObj.SetActive(false);
    }

    private void CreateWorkerButton(string name, string textLabel, Color btnColor, WorkerSpawner.WorkerType type)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(spawnPanelObj.transform, false);

        Image img = btnObj.AddComponent<Image>();
        img.color = btnColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.text = textLabel;
        
        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont != null) text.font = defaultFont;

        text.fontSize = 16;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;

        btn.onClick.AddListener(() => TrySpawnWorker(type));
    }

    public void TogglePanel()
    {
        if (spawnPanelObj != null)
        {
            spawnPanelObj.SetActive(!spawnPanelObj.activeSelf);
        }
    }

    /// <summary>
    /// Xử lý trừ 10 Lúa trước khi sinh Worker
    /// </summary>
    private void TrySpawnWorker(WorkerSpawner.WorkerType type)
    {
        if (WorkerSpawner.Instance == null)
        {
            Debug.LogError("[HouseSpawnPanel] Không tìm thấy WorkerSpawner.Instance.");
            return;
        }

        // Kiểm tra và trừ Lúa
        if (JsonDataManager.Ins != null)
        {
            if (JsonDataManager.Ins.food < foodCostPerWorker)
            {
                Debug.LogWarning("[HouseSpawnPanel] Không đủ Lúa để chiêu mộ!");
                return;
            }
            JsonDataManager.Ins.AddFood(-foodCostPerWorker);
        }

        Vector3 spawnOrigin = (house != null) ? house.EntrancePosition : transform.position;
        WorkerSpawner.Instance.SpawnWorker(type, spawnOrigin);

        // Đóng panel sau khi mua thành công
        if (spawnPanelObj != null)
        {
            spawnPanelObj.SetActive(false);
        }
    }
}