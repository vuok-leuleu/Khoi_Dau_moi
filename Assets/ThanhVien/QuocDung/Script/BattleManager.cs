using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class BattleManager : MonoBehaviour
{
    [Header("Spawn Locations")]
    [Tooltip("Vị trí sinh phe Người chơi (BÊN TRÁI)")]
    [SerializeField] private Transform leftSpawnPoint;
    [Tooltip("Vị trí sinh phe Enemy (BÊN PHẢI)")]
    [SerializeField] private Transform rightSpawnPoint;
    [Tooltip("Điểm spawn trên cao dành riêng cho Rồng. Nếu bỏ trống, BattleManager sẽ tự tạo phía trên Right Spawn Point.")]
    [SerializeField] private Transform dragonHighSpawnPoint;
    [SerializeField, Min(0f)] private float dragonSpawnHeight = 10f;
    [Tooltip("Độ lệch độ cao khi rồng đáp. Giá trị âm hạ rồng xuống để bàn chân chạm cùng mặt đất với Enemy.")]
    [SerializeField, Range(-5f, 5f)] private float dragonLandingHeightOffset = -0.5f;

    [Header("Distance & Grid Spacing Settings")]
    [SerializeField] private float buildingSpacing = 4.0f;
    [SerializeField] private float unitSpacing = 2.0f;
    [SerializeField] private int unitsPerRow = 4;

    [Header("Enemy Prefab Settings")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject enemyRangedPrefab;

    [Header("Dragon Spawn Settings")]
    [SerializeField] private bool spawnDragonAtBattleStart = true;
    [Tooltip("Chấp nhận GameObject hoặc component Dragon để tránh lỗi cast từ prefab cũ.")]
    [SerializeField] private UnityEngine.Object dragonPrefab;

    [Header("Player Soldier Prefabs")]
    [SerializeField] private GameObject soldierPrefab;
    [SerializeField] private GameObject archerSoldierPrefab;
    [SerializeField] private GameObject tankSoldierPrefab;

    [Header("Player Building Prefabs")]
    [SerializeField] private GameObject barracksPrefab;
    [SerializeField] private GameObject archerTowerPrefab;
    [SerializeField] private GameObject watchTowerPrefab;
    [SerializeField] private GameObject cannonPrefab;

    [System.Serializable]
    public struct CustomBuildingPrefab
    {
        public BuildingType buildingType;
        public GameObject prefab;
    }

    [Header("Custom Building Mapping (Optional)")]
    [SerializeField] private List<CustomBuildingPrefab> customBuildingPrefabs = new List<CustomBuildingPrefab>();

    [Header("Standalone Test Mode (Kích hoạt khi mở trực tiếp BattleScene trong Editor)")]
    [SerializeField] private bool enableTestFallback = true;
    [SerializeField] private int testEnemyWaveCount = 1;
    [SerializeField] private int testBarracksCount = 1;
    [SerializeField] private int testBarracksLevel = 1;
    [SerializeField] private bool testSpawnArcherTower = true;

    [Header("Camera Settings")]
    [Tooltip("Camera chính dùng cho trận đấu (nếu chưa gán sẽ tự lấy Camera.main)")]
    [SerializeField] private Camera battleCamera;
    [Tooltip("Ô đế / Transform để gắn Camera tại vị trí giao tranh")]
    [SerializeField] private Transform battleCameraPoint;
    [Tooltip("Tự động di chuyển Camera đến vị trí giao tranh khi bắt đầu trận")]
    [SerializeField] private bool autoPositionCamera = true;
    [Tooltip("Độ lệch vị trí Camera so với trung tâm điểm giao tranh (nếu không dùng battleCameraPoint)")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 8f, -12f);
    [Tooltip("Góc xoay mặc định của Camera (nếu không dùng battleCameraPoint)")]
    [SerializeField] private Vector3 cameraRotation = new Vector3(30f, 0f, 0f);

    [Header("Battle Result & Transition Settings")]
    [Tooltip("Thời gian chờ trước khi tự động chuyển scene (Mặc định: 0s để bấm nút Return là quay scene ngay)")]
    [SerializeField] private float battleEndDelay = 0.0f;

    [Header("Demacia Battle UI Settings")]
    [Tooltip("Bật/Tắt thanh máu Demacia Rising trên đỉnh camera")]
    [SerializeField] private bool enableDemaciaUI = true;
    [Tooltip("Kích thước thanh UI Máu Demacia (Chiều rộng, Chiều cao)")]
    [SerializeField] private Vector2 uiBarSize = new Vector2(680f, 38f);
    [Tooltip("Khoảng cách mép trên màn hình xuống thanh UI")]
    [SerializeField] private float topPadding = 25f;
    [Tooltip("Màu thanh máu Phe Lính (Bên Trái)")]
    [SerializeField] private Color playerBarColor = new Color(0.9f, 0.15f, 0.15f, 1f);
    [Tooltip("Màu thanh máu Phe Enemy (Bên Phải)")]
    [SerializeField] private Color enemyBarColor = new Color(0.75f, 0.10f, 0.10f, 1f);
    [Tooltip("Màu viền khung kim loại Demacia")]
    [SerializeField] private Color frameColor = new Color(0.85f, 0.70f, 0.25f, 1f);
    [Tooltip("Màu vạch chia đôi & viên kim cương ở giữa")]
    [SerializeField] private Color centerDividerColor = new Color(1f, 0.88f, 0.35f, 1f);
    [SerializeField] private Sprite customFrameSprite;
    [SerializeField] private Sprite customFillSprite;

    private bool isBattleOver = false;
    private List<GameObject> spawnedPlayerObjects = new List<GameObject>();
    private List<GameObject> spawnedEnemyObjects = new List<GameObject>();

    // Demacia Runtime UI Components & Variables
    private Canvas demaciaCanvas;
    private Image playerFillImage;
    private Image enemyFillImage;
    private TextMeshProUGUI playerPercentText;
    private TextMeshProUGUI enemyPercentText;

    private float initialMaxPlayerHP = 0f;
    private float initialMaxEnemyHP = 0f;
    private float targetPlayerFill = 1f;
    private float targetEnemyFill = 1f;
    private float currentDisplayPlayerFill = 1f;
    private float currentDisplayEnemyFill = 1f;
    private Sprite defaultWhiteSprite;

    public static BattleManager Ins { get; private set; }

    private void OnDestroy()
    {
        if (Ins == this) Ins = null;
    }

    private void Awake()
    {
        Ins = this;
        Time.timeScale = 1f;
    }

    private void Start()
    {
        // SceneBattle có thể được mở trực tiếp trong Editor, vì vậy luôn xóa
        // dữ liệu đội hình của trận trước trước khi sinh lính cho trận mới.
        BattleData.BeginNewBattle();

        // 1. Kiểm tra vị trí Spawn mặc định nếu chưa gán trong Inspector
        EnsureSpawnPoints();

        // 2. Kiểm tra dữ liệu truyền từ Scene chính (qua BattleData)
        if (!BattleData.HasData && enableTestFallback)
        {
            SetupFallbackTestData();
        }

        // 3. Tiến hành Spawn theo yêu cầu:
        //    - Phe Người Chơi (Lính & Công trình) bên TRÁI
        //    - Phe Enemy (theo số lượng Wave) bên PHẢI
        SpawnPlayerSide();
        SpawnEnemySide();

        // 4. Thiết lập vị trí Camera tại giao tranh
        SetupBattleCamera();

        // 5. Khởi tạo thanh UI Máu Demacia Rising ở mép trên màn hình
        InitializeDemaciaUI();

        // 6. Cho lính và Enemy lập tức bay vào đánh nhau
        StartCoroutine(TriggerImmediateCombatRoutine());

        // 7. Theo dõi kết quả giao tranh và tự động chuyển về Scene chính
        StartCoroutine(MonitorBattleRoutine());

        Debug.Log($"[BattleManager] 🔥 Trận đấu khởi tạo thành công! " +
                  $"Sinh {spawnedPlayerObjects.Count} vật thể Người Chơi (BÊN TRÁI) và {spawnedEnemyObjects.Count} Enemy (BÊN PHẢI).");
    }

    /// <summary>
    /// Coroutine liên tục kiểm tra kết quả trận đấu giữa Phe Lính và Phe Enemy
    /// </summary>
    private System.Collections.IEnumerator MonitorBattleRoutine()
    {
        yield return new WaitForSeconds(0.2f);

        while (!isBattleOver)
        {
            yield return new WaitForSeconds(0.2f);

            // 1. Đếm số Enemy còn sống trong Battle Scene
            int livingEnemies = 0;
            foreach (var enemyObj in spawnedEnemyObjects)
            {
                if (enemyObj != null && enemyObj.activeInHierarchy)
                {
                    EnemyHealth hp = enemyObj.GetComponent<EnemyHealth>();
                    if (hp == null) hp = enemyObj.GetComponentInChildren<EnemyHealth>();

                    if (hp != null)
                    {
                        if (hp.CurrentHealth > 0f) livingEnemies++;
                    }
                    else
                    {
                        livingEnemies++;
                    }
                }
            }

            // 2. Đếm số Lính (UnitController) còn sống trong Battle Scene
            int livingSoldiers = 0;
            foreach (var playerObj in spawnedPlayerObjects)
            {
                if (playerObj != null && playerObj.activeInHierarchy)
                {
                    UnitController unit = playerObj.GetComponent<UnitController>();
                    if (unit == null) unit = playerObj.GetComponentInChildren<UnitController>();

                    if (unit != null)
                    {
                        HPSoldier hp = unit.GetComponent<HPSoldier>();
                        if (hp == null) hp = unit.GetComponentInChildren<HPSoldier>();

                        if (hp != null)
                        {
                            if (!hp.IsDead && hp.CurrentHealth > 0f) livingSoldiers++;
                        }
                        else
                        {
                            livingSoldiers++;
                        }
                    }
                }
            }

            // 3. Lấy trạng thái tổng máu của cả 2 phe
            GetBattleHealthState(out float curPlayerHP, out _, out float curEnemyHP, out _);

            // 4. Đánh giá kết quả giao tranh (khi số đơn vị = 0 HOẶC vạch máu phe nào rút hết về 0 trước):
            if (livingEnemies == 0 || curEnemyHP <= 0f)
            {
                // Phe Lính THẮNG!
                isBattleOver = true;
                Debug.Log($"[BattleManager] 🏆 PHE LÍNH THẮNG TRẬN! Số lính sống sót = {livingSoldiers}. Cập nhật dữ liệu kết quả trận đấu.");

                BattleData.HasResult = true;
                BattleData.IsPlayerVictory = true;
                BattleData.SurvivingSoldiersCount = livingSoldiers;

                // Hoàn tất đánh giá kết quả, không delay, không tự chuyển scene (UISceneBattle sẽ tự hiển thị UI và chuyển scene khi bấm Return)
                yield break;
            }
            else if (livingSoldiers == 0 || curPlayerHP <= 0f)
            {
                // Phe Lính THUA!
                isBattleOver = true;
                Debug.Log($"[BattleManager] 💀 PHE LÍNH THUA TRẬN! Toàn bộ lính đã ngã xuống. Cập nhật dữ liệu kết quả trận đấu.");

                BattleData.HasResult = true;
                BattleData.IsPlayerVictory = false;
                BattleData.SurvivingSoldiersCount = 0;

                // Hoàn tất đánh giá kết quả, không delay, không tự chuyển scene (UISceneBattle sẽ tự hiển thị UI và chuyển scene khi bấm Return)
                yield break;
            }
        }
    }

    /// <summary>
    /// Chuyển về Scene chính ngay lập tức không delay
    /// </summary>
    public void ReturnToMainScene()
    {
        Time.timeScale = 1f;
        string targetScene = (BattleData.HasData && !string.IsNullOrEmpty(BattleData.MainSceneName)) ? BattleData.MainSceneName : "MainScene";
        Debug.Log($"[BattleManager] 🚀 ReturnToMainScene -> LoadScene('{targetScene}') ngay lập tức!");
        CloudSceneTransition.LoadSceneWithCloud(targetScene);
    }

    private void Update()
    {
        UpdateDemaciaUI();
    }

    /// <summary>
    /// Tính toán tổng HP hiện tại và tổng HP tối đa của cả Phe Lính (Player) và Phe Enemy
    /// </summary>
    public void GetBattleHealthState(out float currentSoldierHP, out float maxSoldierHP, out float currentEnemyHP, out float maxEnemyHP)
    {
        currentSoldierHP = 0f;
        maxSoldierHP = 0f;
        foreach (var playerObj in spawnedPlayerObjects)
        {
            if (playerObj == null || !playerObj.activeInHierarchy) continue;

            UnitController unit = playerObj.GetComponent<UnitController>();
            if (unit == null) unit = playerObj.GetComponentInChildren<UnitController>();

            if (unit != null)
            {
                HPSoldier hp = unit.GetComponent<HPSoldier>();
                if (hp == null) hp = unit.GetComponentInChildren<HPSoldier>();

                if (hp != null)
                {
                    if (!hp.IsDead && hp.CurrentHealth > 0f)
                    {
                        currentSoldierHP += hp.CurrentHealth;
                    }
                    maxSoldierHP += hp.MaxHealth;
                }
                else
                {
                    currentSoldierHP += 100f;
                    maxSoldierHP += 100f;
                }
            }
        }

        currentEnemyHP = 0f;
        maxEnemyHP = 0f;
        foreach (var enemyObj in spawnedEnemyObjects)
        {
            if (enemyObj == null || !enemyObj.activeInHierarchy) continue;

            EnemyHealth hp = enemyObj.GetComponent<EnemyHealth>();
            if (hp == null) hp = enemyObj.GetComponentInChildren<EnemyHealth>();

            if (hp != null)
            {
                if (!hp.IsDead && hp.CurrentHealth > 0f)
                {
                    currentEnemyHP += hp.CurrentHealth;
                }
                maxEnemyHP += hp.MaxHealth;
            }
            else
            {
                currentEnemyHP += 100f;
                maxEnemyHP += 100f;
            }
        }
    }

    /// <summary>
    /// Khởi tạo toàn bộ giao diện Demacia Rising Top UI Bar bằng Code (Screen Space - Overlay)
    /// </summary>
    private void InitializeDemaciaUI()
    {
        if (!enableDemaciaUI) return;

        // 1. Lấy trạng thái máu ban đầu để tính % chuẩn xác
        GetBattleHealthState(out float curPlayerHP, out initialMaxPlayerHP, out float curEnemyHP, out initialMaxEnemyHP);

        if (initialMaxPlayerHP <= 0f) initialMaxPlayerHP = Mathf.Max(1f, curPlayerHP);
        if (initialMaxEnemyHP <= 0f) initialMaxEnemyHP = Mathf.Max(1f, curEnemyHP);

        // 2. Tạo Sprite trắng mặc định nếu chưa có
        Texture2D whiteTex = new Texture2D(1, 1);
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();
        defaultWhiteSprite = Sprite.Create(whiteTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));

        // 3. Tạo Canvas Overlay đỉnh màn hình
        GameObject canvasObj = new GameObject("DemaciaBattleUI_Canvas");
        demaciaCanvas = canvasObj.AddComponent<Canvas>();
        demaciaCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        demaciaCanvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // 4. Panel chính chứa thanh Demacia (Căn giữa mép trên)
        GameObject mainPanel = new GameObject("DemaciaUI_MainPanel");
        mainPanel.transform.SetParent(canvasObj.transform, false);

        RectTransform panelRect = mainPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -topPadding);
        panelRect.sizeDelta = uiBarSize;

        // Background nền tối kim loại
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(mainPanel.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = customFrameSprite != null ? customFrameSprite : defaultWhiteSprite;
        bgImg.color = new Color(0.06f, 0.06f, 0.08f, 0.92f);

        // Viền Vàng Hoàng Gia Demacia (Top & Bottom Frame)
        GameObject topFrame = new GameObject("Frame_Top");
        topFrame.transform.SetParent(mainPanel.transform, false);
        RectTransform topRect = topFrame.AddComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0f, 1f);
        topRect.anchorMax = new Vector2(1f, 1f);
        topRect.pivot = new Vector2(0.5f, 1f);
        topRect.anchoredPosition = Vector2.zero;
        topRect.sizeDelta = new Vector2(0f, 3.5f);
        Image topImg = topFrame.AddComponent<Image>();
        topImg.sprite = defaultWhiteSprite;
        topImg.color = frameColor;

        GameObject botFrame = new GameObject("Frame_Bottom");
        botFrame.transform.SetParent(mainPanel.transform, false);
        RectTransform botRect = botFrame.AddComponent<RectTransform>();
        botRect.anchorMin = new Vector2(0f, 0f);
        botRect.anchorMax = new Vector2(1f, 0f);
        botRect.pivot = new Vector2(0.5f, 0f);
        botRect.anchoredPosition = Vector2.zero;
        botRect.sizeDelta = new Vector2(0f, 3.5f);
        Image botImg = botFrame.AddComponent<Image>();
        botImg.sprite = defaultWhiteSprite;
        botImg.color = frameColor;

        // 5. Thanh máu Phe Lính (BÊN TRÁI - Fills right to left from center)
        GameObject leftHolder = new GameObject("PlayerHP_Holder");
        leftHolder.transform.SetParent(mainPanel.transform, false);
        RectTransform leftHolderRect = leftHolder.AddComponent<RectTransform>();
        leftHolderRect.anchorMin = new Vector2(0.005f, 0.12f);
        leftHolderRect.anchorMax = new Vector2(0.495f, 0.88f);
        leftHolderRect.sizeDelta = Vector2.zero;

        GameObject leftFillObj = new GameObject("PlayerHP_Fill");
        leftFillObj.transform.SetParent(leftHolder.transform, false);
        RectTransform leftFillRect = leftFillObj.AddComponent<RectTransform>();
        leftFillRect.anchorMin = Vector2.zero;
        leftFillRect.anchorMax = Vector2.one;
        leftFillRect.sizeDelta = Vector2.zero;

        playerFillImage = leftFillObj.AddComponent<Image>();
        playerFillImage.sprite = customFillSprite != null ? customFillSprite : defaultWhiteSprite;
        playerFillImage.color = playerBarColor;
        playerFillImage.type = Image.Type.Filled;
        playerFillImage.fillMethod = Image.FillMethod.Horizontal;
        playerFillImage.fillOrigin = (int)Image.OriginHorizontal.Right; // Touch center divider at 100%!

        // 6. Thanh máu Phe Enemy (BÊN PHẢI - Fills left to right from center)
        GameObject rightHolder = new GameObject("EnemyHP_Holder");
        rightHolder.transform.SetParent(mainPanel.transform, false);
        RectTransform rightHolderRect = rightHolder.AddComponent<RectTransform>();
        rightHolderRect.anchorMin = new Vector2(0.505f, 0.12f);
        rightHolderRect.anchorMax = new Vector2(0.995f, 0.88f);
        rightHolderRect.sizeDelta = Vector2.zero;

        GameObject rightFillObj = new GameObject("EnemyHP_Fill");
        rightFillObj.transform.SetParent(rightHolder.transform, false);
        RectTransform rightFillRect = rightFillObj.AddComponent<RectTransform>();
        rightFillRect.anchorMin = Vector2.zero;
        rightFillRect.anchorMax = Vector2.one;
        rightFillRect.sizeDelta = Vector2.zero;

        enemyFillImage = rightFillObj.AddComponent<Image>();
        enemyFillImage.sprite = customFillSprite != null ? customFillSprite : defaultWhiteSprite;
        enemyFillImage.color = enemyBarColor;
        enemyFillImage.type = Image.Type.Filled;
        enemyFillImage.fillMethod = Image.FillMethod.Horizontal;
        enemyFillImage.fillOrigin = (int)Image.OriginHorizontal.Left; // Touch center divider at 100%!

        // 7. Vạch chia đôi trung tâm (Center Divider & Diamond Ornament)
        GameObject centerLine = new GameObject("CenterDivider_Line");
        centerLine.transform.SetParent(mainPanel.transform, false);
        RectTransform lineRect = centerLine.AddComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0.5f, 0f);
        lineRect.anchorMax = new Vector2(0.5f, 1f);
        lineRect.sizeDelta = new Vector2(4f, 10f);
        Image lineImg = centerLine.AddComponent<Image>();
        lineImg.sprite = defaultWhiteSprite;
        lineImg.color = centerDividerColor;

        GameObject diamondObj = new GameObject("CenterDivider_Diamond");
        diamondObj.transform.SetParent(mainPanel.transform, false);
        RectTransform diamondRect = diamondObj.AddComponent<RectTransform>();
        diamondRect.anchorMin = new Vector2(0.5f, 0.5f);
        diamondRect.anchorMax = new Vector2(0.5f, 0.5f);
        diamondRect.sizeDelta = new Vector2(16f, 16f);
        diamondRect.localRotation = Quaternion.Euler(0, 0, 45f);
        Image diamondImg = diamondObj.AddComponent<Image>();
        diamondImg.sprite = defaultWhiteSprite;
        diamondImg.color = centerDividerColor;

        // 8. Nhãn Văn Bản % Máu (TMP)
        GameObject pTextObj = new GameObject("PlayerText");
        pTextObj.transform.SetParent(mainPanel.transform, false);
        RectTransform pTextRect = pTextObj.AddComponent<RectTransform>();
        pTextRect.anchorMin = new Vector2(0.01f, 1f);
        pTextRect.anchorMax = new Vector2(0.4f, 1f);
        pTextRect.pivot = new Vector2(0f, 0f);
        pTextRect.anchoredPosition = new Vector2(0f, 4f);
        pTextRect.sizeDelta = new Vector2(200f, 24f);

        playerPercentText = pTextObj.AddComponent<TextMeshProUGUI>();
        playerPercentText.text = "LÍNH: 100%";
        playerPercentText.fontSize = 15f;
        playerPercentText.fontStyle = FontStyles.Bold;
        playerPercentText.color = new Color(1f, 0.95f, 0.85f, 1f);
        playerPercentText.alignment = TextAlignmentOptions.Left;

        GameObject eTextObj = new GameObject("EnemyText");
        eTextObj.transform.SetParent(mainPanel.transform, false);
        RectTransform eTextRect = eTextObj.AddComponent<RectTransform>();
        eTextRect.anchorMin = new Vector2(0.6f, 1f);
        eTextRect.anchorMax = new Vector2(0.99f, 1f);
        eTextRect.pivot = new Vector2(1f, 0f);
        eTextRect.anchoredPosition = new Vector2(0f, 4f);
        eTextRect.sizeDelta = new Vector2(200f, 24f);

        enemyPercentText = eTextObj.AddComponent<TextMeshProUGUI>();
        enemyPercentText.text = "ENEMY: 100%";
        enemyPercentText.fontSize = 15f;
        enemyPercentText.fontStyle = FontStyles.Bold;
        enemyPercentText.color = new Color(1f, 0.95f, 0.85f, 1f);
        enemyPercentText.alignment = TextAlignmentOptions.Right;

        GameObject titleObj = new GameObject("CenterTitle");
        titleObj.transform.SetParent(mainPanel.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.35f, 1f);
        titleRect.anchorMax = new Vector2(0.65f, 1f);
        titleRect.pivot = new Vector2(0.5f, 0f);
        titleRect.anchoredPosition = new Vector2(0f, 4f);
        titleRect.sizeDelta = new Vector2(250f, 24f);

        TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "FOR DEMACIA!";
        titleTMP.fontSize = 16f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = centerDividerColor;
        titleTMP.alignment = TextAlignmentOptions.Center;
    }

    /// <summary>
    /// Cập nhật hiệu ứng rút máu mượt mà (Lerp) liên tục theo thời gian thực
    /// </summary>
    private void UpdateDemaciaUI()
    {
        if (!enableDemaciaUI || playerFillImage == null || enemyFillImage == null) return;

        GetBattleHealthState(out float curPlayerHP, out float curMaxPlayerHP, out float curEnemyHP, out float curMaxEnemyHP);

        float maxP = initialMaxPlayerHP > 0f ? initialMaxPlayerHP : Mathf.Max(1f, curMaxPlayerHP);
        float maxE = initialMaxEnemyHP > 0f ? initialMaxEnemyHP : Mathf.Max(1f, curMaxEnemyHP);

        targetPlayerFill = Mathf.Clamp01(curPlayerHP / maxP);
        targetEnemyFill = Mathf.Clamp01(curEnemyHP / maxE);

        currentDisplayPlayerFill = Mathf.Lerp(currentDisplayPlayerFill, targetPlayerFill, Time.deltaTime * 6f);
        currentDisplayEnemyFill = Mathf.Lerp(currentDisplayEnemyFill, targetEnemyFill, Time.deltaTime * 6f);

        playerFillImage.fillAmount = currentDisplayPlayerFill;
        enemyFillImage.fillAmount = currentDisplayEnemyFill;

        if (playerPercentText != null)
        {
            playerPercentText.text = $"LÍNH: {Mathf.CeilToInt(currentDisplayPlayerFill * 100)}%";
        }
        if (enemyPercentText != null)
        {
            enemyPercentText.text = $"ENEMY: {Mathf.CeilToInt(currentDisplayEnemyFill * 100)}%";
        }
    }

    /// <summary>
    /// Kích hoạt cho cả Lính và Enemy lập tức xông vào đánh nhau khi mở Battle Scene
    /// </summary>
    private System.Collections.IEnumerator TriggerImmediateCombatRoutine()
    {
        yield return new WaitForEndOfFrame();

        Vector3 enemyTargetPos = (rightSpawnPoint != null) ? rightSpawnPoint.position : (transform.position + Vector3.right * 15f);

        // 1. Kích hoạt Lính người chơi lao vào đánh Enemy ở bên Phải
        UnitController[] playerUnits = Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        foreach (var unit in playerUnits)
        {
            if (unit != null && unit.gameObject.activeInHierarchy)
            {
                unit.EnableCombat(enemyTargetPos);
            }
        }

        // 2. Kích hoạt Enemy lao vào đánh Lính người chơi ở bên Trái
        EnemyAI[] enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                enemy.EnableCombat();
            }
        }

        Debug.Log("[BattleManager] ⚔️ Cả lính và Enemy đã lập tức bay vào đánh nhau!");
    }

    /// <summary>
    /// Định vị Camera tại vị trí ô đế giao tranh (battleCameraPoint) hoặc điểm trung tâm trận đấu
    /// </summary>
    private void SetupBattleCamera()
    {
        if (battleCamera == null)
        {
            battleCamera = Camera.main;
        }

        if (battleCamera == null)
        {
            battleCamera = Object.FindFirstObjectByType<Camera>();
        }

        if (battleCamera == null || !autoPositionCamera) return;

        if (battleCameraPoint != null)
        {
            battleCamera.transform.position = battleCameraPoint.position;
            battleCamera.transform.rotation = battleCameraPoint.rotation;
            Debug.Log($"[BattleManager] Đã gắn Camera vào ô đế battleCameraPoint: {battleCameraPoint.position}");
        }
        else
        {
            // Tính vị trí trung tâm giữa phe Người chơi (Trái) và Enemy (Phải)
            Vector3 centerPos = (leftSpawnPoint.position + rightSpawnPoint.position) * 0.5f;
            battleCamera.transform.position = centerPos + cameraOffset;
            battleCamera.transform.rotation = Quaternion.Euler(cameraRotation);
            Debug.Log($"[BattleManager] Đã tự động di chuyển Camera đến tâm điểm giao tranh: {centerPos}");
        }
    }

    /// <summary>
    /// Đảm bảo tự tạo Spawn Point bên TRÁI và BÊN PHẢI nếu chưa gán trong Inspector
    /// </summary>
    private void EnsureSpawnPoints()
    {
        if (leftSpawnPoint == null)
        {
            GameObject leftObj = GameObject.Find("LeftSpawnPoint");
            if (leftObj != null)
            {
                leftSpawnPoint = leftObj.transform;
            }
            else
            {
                leftObj = new GameObject("LeftSpawnPoint_Player");
                leftObj.transform.position = transform.position + Vector3.left * 15f;
                leftSpawnPoint = leftObj.transform;
            }
        }

        if (rightSpawnPoint == null)
        {
            GameObject rightObj = GameObject.Find("RightSpawnPoint");
            if (rightObj != null)
            {
                rightSpawnPoint = rightObj.transform;
            }
            else
            {
                rightObj = new GameObject("RightSpawnPoint_Enemy");
                rightObj.transform.position = transform.position + Vector3.right * 15f;
                rightSpawnPoint = rightObj.transform;
            }
        }

        EnsureDragonHighSpawnPoint();
    }

    /// <summary>
    /// Dùng một điểm spawn riêng để rồng xuất hiện trên cao, không chồng lên đội Enemy dưới đất.
    /// Có thể gán DragonHighSpawnPoint trong Inspector để tự chọn vị trí này.
    /// </summary>
    private void EnsureDragonHighSpawnPoint()
    {
        if (dragonHighSpawnPoint != null || rightSpawnPoint == null) return;

        GameObject pointObject = GameObject.Find("DragonHighSpawnPoint");
        if (pointObject == null)
        {
            pointObject = new GameObject("DragonHighSpawnPoint");
            pointObject.transform.SetParent(transform);
            pointObject.transform.position = rightSpawnPoint.position + Vector3.up * dragonSpawnHeight;
        }

        dragonHighSpawnPoint = pointObject.transform;
    }

    /// <summary>
    /// Cài đặt dữ liệu giả lập cho chế độ Test độc lập
    /// </summary>
    private void SetupFallbackTestData()
    {
        BattleData.ClearConquestEnemyComposition();
        BattleData.ClearRaidEnemyComposition();
        BattleData.SpawnDragonInCurrentBattle = false;
        BattleData.EnemyWaveCount = Mathf.Max(1, testEnemyWaveCount);
        BattleData.PlayerBuildings.Clear();

        // Tạo Doanh trại Test
        for (int i = 0; i < testBarracksCount; i++)
        {
            int lvl = Mathf.Clamp(testBarracksLevel, 1, 3);
            // The map owns troop spawning. Formation research has already been
            // applied by TroopTrainingManager before this data is captured.
            // Standalone battle-test data has no map squad to serialize.
            int soldiers = 3 + ResearchUpgradeEffects.FormationBonus;

            BattleData.PlayerBuildings.Add(new BattleData.BuildingInfo
            {
                buildingType = BuildingType.BarracksMelee,
                level = lvl,
                soldierCount = soldiers,
                originalPosition = Vector3.zero
            });
        }

        // Tạo Tháp cung Test
        if (testSpawnArcherTower)
        {
            BattleData.PlayerBuildings.Add(new BattleData.BuildingInfo
            {
                buildingType = BuildingType.ArcherTower,
                level = 1,
                soldierCount = 0,
                originalPosition = Vector3.zero
            });
        }

        BattleData.HasData = true;
        Debug.Log("[BattleManager] Đã tự động tạo dữ liệu Test cho BattleScene.");
    }

    private bool IsCombatBuilding(BuildingType type)
    {
        return type == BuildingType.BarracksMelee ||
               type == BuildingType.BarracksArcher ||
               type == BuildingType.BarracksSpear ||
               type == BuildingType.ArcherTower ||
               type == BuildingType.WatchTower ||
               type == BuildingType.Cannon;
    }

    private void SetupSpawnedBuildingState(GameObject spawnedBuilding, int level)
    {
        if (spawnedBuilding == null) return;

        // Tắt SpawnSoldier trên công trình ở SceneBattle để tránh tự động spawn lính lần 2!
        SpawnSoldier spawner = spawnedBuilding.GetComponent<SpawnSoldier>();
        if (spawner == null) spawner = spawnedBuilding.GetComponentInChildren<SpawnSoldier>();
        if (spawner != null)
        {
            spawner.enabled = false;
        }

        // Cập nhật Cấp độ và ĐẶT TRẠNG THÁI ĐÃ XÂY XONG cho UpgradeableBuilding
        UpgradeableBuilding ub = spawnedBuilding.GetComponent<UpgradeableBuilding>();
        if (ub == null) ub = spawnedBuilding.GetComponentInChildren<UpgradeableBuilding>();
        if (ub != null)
        {
            int targetLevel = Mathf.Max(0, level - 1);
            ub.LoadBuildingData(targetLevel, isRuinedState: false, isInitialBuildNeededState: false);
        }

        // Cập nhật trạng thái cho BuildingCtrl nếu có
        BuildingCtrl buildingCtrl = spawnedBuilding.GetComponent<BuildingCtrl>();
        if (buildingCtrl == null) buildingCtrl = spawnedBuilding.GetComponentInChildren<BuildingCtrl>();
    }

    private GameObject GetSoldierPrefabForAttackMode(AttackMode mode, bool ignoreResearchRequirements = false)
    {
        switch (mode)
        {
            case AttackMode.Ranged:
                if (!ignoreResearchRequirements && !ResearchUpgradeEffects.ArcherUnlocked) return null;
                if (archerSoldierPrefab != null) return archerSoldierPrefab;
                break;
            case AttackMode.Tank:
                if (!ignoreResearchRequirements && !ResearchUpgradeEffects.ShieldUnlocked) return null;
                if (tankSoldierPrefab != null) return tankSoldierPrefab;
                break;
            case AttackMode.Melee:
            default:
                if (soldierPrefab != null) return soldierPrefab;
                break;
        }

        // Tự động tìm trong Resources / Assets nếu chưa gán Inspector
        var allSoldiers = Resources.FindObjectsOfTypeAll<UnitController>();
        foreach (var u in allSoldiers)
        {
            if (u != null && u.AttackMode == mode)
            {
                if (!u.gameObject.scene.IsValid() || u.gameObject.name.ToLower().Contains("prefab"))
                {
                    return u.gameObject;
                }
            }
        }

        // Fallback
        if (soldierPrefab != null) return soldierPrefab;
        if (tankSoldierPrefab != null) return tankSoldierPrefab;
        if (archerSoldierPrefab != null) return archerSoldierPrefab;
        return null;
    }

    private GameObject GetSoldierPrefabForBuildingType(BuildingType buildingType)
    {
        switch (buildingType)
        {
            case BuildingType.BarracksArcher:
                return GetSoldierPrefabForAttackMode(AttackMode.Ranged);
            case BuildingType.BarracksSpear:
                return GetSoldierPrefabForAttackMode(AttackMode.Tank);
            case BuildingType.BarracksMelee:
            default:
                return GetSoldierPrefabForAttackMode(AttackMode.Melee);
        }
    }

    /// <summary>
    /// BattleData contains the actual squad size currently on the strategic
    /// map. Formation research is applied there, so adding it again here would
    /// duplicate reinforcements in SceneBattle.
    /// </summary>
    private static int GetBattleSquadSize(int baseSoldierCount)
    {
        return Mathf.Max(0, baseSoldierCount);
    }

    private static void AddFormationReinforcements(List<AttackMode> soldierModes)
    {
        // The list is serialized from the actual units selected on the map.
        // It already includes formation reinforcements; retain this method so
        // older call sites remain explicit no-ops rather than double-spawning.
    }

    /// <summary>
    /// Spawn toàn bộ Công trình và Lính của Người Chơi ở BÊN TRÁI
    /// </summary>
    private void SpawnPlayerSide()
    {
        if (leftSpawnPoint == null) return;

        Vector3 originLeft = leftSpawnPoint.position;
        Vector3 soldierFrontOrigin = originLeft + Vector3.right * 6f;

        int soldierTotalSpawned = 0;

        // 1. Trường hợp Xuất chinh / Chinh phạt: Sinh đúng danh sách và loại lính đã cử đi
        if (BattleData.IsAttackingExpedition)
        {
            List<AttackMode> expeditionSoldierModes = new List<AttackMode>();
            foreach (var march in BattleData.SavedSoldierMarches)
            {
                if (march != null && march.hasReachedExpeditionDestination &&
                    march.marchDestinationZoneName == BattleData.TargetedSettlementZoneName)
                {
                    expeditionSoldierModes.Add(march.attackMode);
                }
            }

            // Nếu số lượng lính trong danh sách march chưa đủ TotalSoldiersInBase thì bổ sung
            while (expeditionSoldierModes.Count < BattleData.TotalSoldiersInBase)
            {
                expeditionSoldierModes.Add(AttackMode.Melee);
            }

            AddFormationReinforcements(expeditionSoldierModes);

            for (int i = 0; i < expeditionSoldierModes.Count; i++)
            {
                GameObject prefab = GetSoldierPrefabForAttackMode(expeditionSoldierModes[i], true);
                if (prefab != null)
                {
                    int row = soldierTotalSpawned / unitsPerRow;
                    int col = soldierTotalSpawned % unitsPerRow;

                    float sOffsetZ = (col - (unitsPerRow - 1) * 0.5f) * unitSpacing;
                    Vector3 soldierPos = soldierFrontOrigin + Vector3.left * (row * unitSpacing) + Vector3.forward * sOffsetZ;
                    Quaternion soldierRot = Quaternion.Euler(0, 90, 0);

                    GameObject spawnedSoldier = Instantiate(prefab, soldierPos, soldierRot);
                    spawnedSoldier.name = $"Player_Soldier_{soldierTotalSpawned + 1}_{expeditionSoldierModes[i]}";
                    spawnedPlayerObjects.Add(spawnedSoldier);
                    RegisterSpawnedPlayerUnit(spawnedSoldier);
                }
                soldierTotalSpawned++;
            }
        }
        else
        {
            // 2. Trường hợp Phòng thủ: ưu tiên danh sách UnitController thực tế
            // đang đóng tại vùng bị địch đánh. Cache SpawnSoldier có thể thiếu
            // lính được tạo qua tutorial/save, dẫn đến Cung thủ bị thay thành
            // Kiếm Sĩ trong SceneBattle.
            List<AttackMode> defendingSoldierModes = new List<AttackMode>();
            if (BattleData.DefendingSoldiers != null && BattleData.DefendingSoldiers.Count > 0)
            {
                foreach (BattleData.DefendingSoldierInfo soldier in BattleData.DefendingSoldiers)
                {
                    if (soldier != null) defendingSoldierModes.Add(soldier.attackMode);
                }
            }
            else
            {
                // Tương thích dữ liệu battle cũ chưa có DefendingSoldiers.
                foreach (var buildingInfo in BattleData.PlayerBuildings)
                {
                    int countToSpawn = GetBattleSquadSize(buildingInfo.soldierCount);
                    AttackMode mode = GetAttackModeForBuildingType(buildingInfo.buildingType);
                    for (int i = 0; i < countToSpawn; i++) defendingSoldierModes.Add(mode);
                }
            }

            // Fallback chỉ dành cho save cũ không xác định được unit thực tế.
            while (defendingSoldierModes.Count < BattleData.TotalSoldiersInBase)
                defendingSoldierModes.Add(AttackMode.Melee);

            for (int i = 0; i < defendingSoldierModes.Count; i++)
            {
                GameObject prefab = GetSoldierPrefabForAttackMode(defendingSoldierModes[i], true);
                if (prefab != null)
                {
                    int row = soldierTotalSpawned / unitsPerRow;
                    int col = soldierTotalSpawned % unitsPerRow;

                    float sOffsetZ = (col - (unitsPerRow - 1) * 0.5f) * unitSpacing;
                    Vector3 soldierPos = soldierFrontOrigin + Vector3.left * (row * unitSpacing) + Vector3.forward * sOffsetZ;
                    Quaternion soldierRot = Quaternion.Euler(0, 90, 0);

                    GameObject spawnedSoldier = Instantiate(prefab, soldierPos, soldierRot);
                    spawnedSoldier.name = $"Player_Soldier_{soldierTotalSpawned + 1}_{defendingSoldierModes[i]}";
                    spawnedPlayerObjects.Add(spawnedSoldier);
                    RegisterSpawnedPlayerUnit(spawnedSoldier);
                }
                soldierTotalSpawned++;
            }
        }
    }

    private static AttackMode GetAttackModeForBuildingType(BuildingType buildingType)
    {
        switch (buildingType)
        {
            case BuildingType.BarracksArcher: return AttackMode.Ranged;
            case BuildingType.BarracksSpear: return AttackMode.Tank;
            default: return AttackMode.Melee;
        }
    }

    private static void RegisterSpawnedPlayerUnit(GameObject spawnedSoldier)
    {
        if (spawnedSoldier == null) return;

        UnitController unit = spawnedSoldier.GetComponent<UnitController>();
        if (unit == null) unit = spawnedSoldier.GetComponentInChildren<UnitController>();

        if (unit != null)
        {
            BattleData.RegisterBattleParticipant(unit.AttackMode);
        }
        else
        {
            Debug.LogWarning($"[BattleManager] Không xác định được loại lính của '{spawnedSoldier.name}' để hiển thị kết quả.");
        }
    }

    /// <summary>
    /// Spawn toàn bộ Enemy thuộc Wave ở BÊN PHẢI
    /// </summary>
    private void SpawnEnemySide()
    {
        if (rightSpawnPoint == null)
        {
            Debug.LogWarning("[BattleManager] Chưa cài đặt rightSpawnPoint!");
            return;
        }

        bool useConquestComposition = BattleData.HasExplicitConquestEnemyComposition &&
                                       BattleData.ConquestEnemyPrefabs != null &&
                                       BattleData.ConquestEnemyPrefabs.Count > 0;
        bool useRaidComposition = !BattleData.IsAttackingExpedition &&
                                  BattleData.HasExplicitRaidEnemyComposition &&
                                  BattleData.RaidEnemyPrefabs != null &&
                                  BattleData.RaidEnemyPrefabs.Count > 0;
        bool useExplicitComposition = useConquestComposition || useRaidComposition;
        int count = useConquestComposition
            ? BattleData.ConquestEnemyPrefabs.Count
            : useRaidComposition
                ? BattleData.RaidEnemyPrefabs.Count
            : Mathf.Max(1, BattleData.EnemyWaveCount);

        if (useExplicitComposition || enemyPrefab != null)
        {
            Vector3 originRight = rightSpawnPoint.position;

            for (int i = 0; i < count; i++)
            {
                GameObject prefabToSpawn = useConquestComposition
                    ? BattleData.ConquestEnemyPrefabs[i]
                    : useRaidComposition
                        ? BattleData.RaidEnemyPrefabs[i]
                    : enemyPrefab;
                if (prefabToSpawn == null) continue;

                int row = i / unitsPerRow;
                int col = i % unitsPerRow;

                Vector3 enemyPos = originRight + Vector3.right * (row * unitSpacing) + Vector3.forward * (col * unitSpacing - 1.5f);
                Quaternion enemyRot = Quaternion.Euler(0, -90, 0); // Quay mặt về phía bên Trái (Player)

                GameObject spawnedEnemy = Instantiate(prefabToSpawn, enemyPos, enemyRot);
                spawnedEnemy.name = $"Enemy_WaveUnit_{i + 1}";
                spawnedEnemyObjects.Add(spawnedEnemy);

                // Kích hoạt AI giao tranh cho Enemy nếu có
                EnemyAI enemyAI = spawnedEnemy.GetComponent<EnemyAI>();
                if (enemyAI != null)
                {
                    enemyAI.EnableCombat();
                }
            }
        }

        // Rồng chỉ xuất hiện ở trận phòng thủ lớn được kịch bản đánh dấu.
        if (!useConquestComposition && BattleData.SpawnDragonInCurrentBattle) SpawnDragon();
    }

    /// <summary>
    /// Spawn đúng một rồng tại điểm riêng trên cao. Rồng không dùng vị trí của wave Enemy dưới đất.
    /// </summary>
    private void SpawnDragon()
    {
        if (!spawnDragonAtBattleStart)
        {
            Debug.LogWarning("[BattleManager] Raid Rồng đã được đánh dấu, nhưng Spawn Dragon At Battle Start đang tắt.");
            return;
        }

        GameObject dragonPrefabObject = ResolvePrefabGameObject(dragonPrefab);
        if (dragonPrefabObject == null)
        {
            Debug.LogError("[BattleManager] Raid Rồng đã được đánh dấu, nhưng Dragon Prefab không phải GameObject/Dragon hợp lệ.");
            return;
        }

        if (dragonHighSpawnPoint == null)
        {
            Debug.LogError("[BattleManager] Raid Rồng đã được đánh dấu, nhưng không tìm thấy DragonHighSpawnPoint.");
            return;
        }

        // Make the model face into the battlefield instead of relying on a
        // world-axis direction. In the current scene this resolves to Y = -90.
        Vector3 battleDirection = leftSpawnPoint != null
            ? leftSpawnPoint.position - dragonHighSpawnPoint.position
            : Vector3.left;
        battleDirection.y = 0f;
        Quaternion dragonRotation = battleDirection.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(battleDirection.normalized)
            : Quaternion.Euler(0f, -90f, 0f);
        GameObject spawnedDragon = Instantiate(dragonPrefabObject, dragonHighSpawnPoint.position, dragonRotation);

        // Bảo đảm vẫn nhìn thấy được nếu prefab được lưu ở trạng thái inactive.
        spawnedDragon.SetActive(true);
        spawnedDragon.name = "Enemy_Dragon";

        Dragon dragon = spawnedDragon.GetComponent<Dragon>();
        if (dragon != null)
        {
            // Pivot của model Rồng thấp hơn chân; bù Y để Idle Landing không xuyên mặt đất.
            Vector3 dragonLandingPosition = rightSpawnPoint.position + Vector3.up * dragonLandingHeightOffset;
            dragon.SetLandingGroundPosition(dragonLandingPosition);
        }

        spawnedEnemyObjects.Add(spawnedDragon);
    }

    private static GameObject ResolvePrefabGameObject(UnityEngine.Object prefabReference)
    {
        if (prefabReference is GameObject gameObject) return gameObject;
        if (prefabReference is Component component) return component.gameObject;
        return null;
    }

    private GameObject FindBuildingPrefabByType(BuildingType type)
    {
        var allBuildings = Resources.FindObjectsOfTypeAll<UpgradeableBuilding>();
        foreach (var b in allBuildings)
        {
            if (b != null && b.buildingType == type)
            {
                // Ưu tiên các Prefab Asset hoặc thực thể mẫu chưa nằm trực tiếp trong Scene giao tranh
                if (!b.gameObject.scene.IsValid() || b.gameObject.name.ToLower().Contains("prefab"))
                {
                    return b.gameObject;
                }
            }
        }

        // Fallback: Thử tìm bất kỳ mẫu công trình cùng loại nào đang có sẵn
        foreach (var b in allBuildings)
        {
            if (b != null && b.buildingType == type)
            {
                return b.gameObject;
            }
        }

        return null;
    }

    /// <summary>
    /// Tìm Prefab công trình chuẩn xác dựa theo BuildingType
    /// </summary>
    private GameObject GetBuildingPrefab(BuildingType type)
    {
        // 1. Kiểm tra mảng Custom mapping trong Inspector
        foreach (var custom in customBuildingPrefabs)
        {
            if (custom.buildingType == type && custom.prefab != null)
            {
                return custom.prefab;
            }
        }

        // 2. Kiểm tra các field được gán thủ công trong Inspector
        switch (type)
        {
            case BuildingType.ArcherTower:
                if (archerTowerPrefab != null) return archerTowerPrefab;
                break;

            case BuildingType.WatchTower:
                if (watchTowerPrefab != null) return watchTowerPrefab;
                break;

            case BuildingType.Cannon:
                if (cannonPrefab != null) return cannonPrefab;
                break;

            case BuildingType.BarracksMelee:
            case BuildingType.BarracksArcher:
            case BuildingType.BarracksSpear:
                if (barracksPrefab != null) return barracksPrefab;
                break;
        }

        // 3. Tự động truy tìm Prefab chuẩn xác của loại nhà đó trong Project/Memory
        GameObject foundDynamic = FindBuildingPrefabByType(type);
        if (foundDynamic != null)
        {
            return foundDynamic;
        }

        // 4. Dự phòng cuối cùng nếu hoàn toàn không tìm thấy
        if (archerTowerPrefab != null) return archerTowerPrefab;
        if (watchTowerPrefab != null) return watchTowerPrefab;
        if (cannonPrefab != null) return cannonPrefab;
        return barracksPrefab;
    }
}
