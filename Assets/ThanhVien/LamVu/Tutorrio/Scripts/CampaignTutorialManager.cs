using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum TutorialStage
{
    None,
    Stage1_TownHall,         
    Stage2_CivilBuildings,   
    Stage3_UpgradeWood,      
    Stage4_ExpandLand,          // 🔥 MỚI: Stage Mở rộng khu vực xây dựng
    Stage5_BuildDefenseTowers,  
    Stage6_EnemyWave,        
    Stage7_Complete          
}

public class CampaignTutorialManager : MonoBehaviour
{
    public static CampaignTutorialManager Ins { get; private set; }

    [Header("=== TRẠNG THÁI HIỆN TẠI ===")]
    public TutorialStage currentStage = TutorialStage.None;

    public bool IsTutorialActive => currentStage != TutorialStage.None && currentStage != TutorialStage.Stage7_Complete;
    public bool IsTutorialComplete => currentStage == TutorialStage.Stage7_Complete;

    public bool CanOpenBuildMenu()
    {
        if (!IsTutorialActive) return true;
        return currentStage == TutorialStage.Stage2_CivilBuildings || currentStage == TutorialStage.Stage5_BuildDefenseTowers;
    }

    [Header("=== UI HIGHLIGHT & WARNING ===")]
    [SerializeField] private GameObject overlayDim;         
    [SerializeField] private GameObject handPointer;        
    [SerializeField] private RectTransform highlightRing;   
    [SerializeField] private Vector2 pointerOffset = new Vector2(30f, -30f);
    [SerializeField] private Canvas tutorialCanvas;         
    [SerializeField] private Canvas buildShopCanvas;        
    [SerializeField] private TMP_Text hintText;             
    [SerializeField] private TMP_Text warningText;          

    [Header("=== HIỆU ỨNG CẢNH BÁO ĐỊCH ===")]
    [SerializeField] private Image redFlashOverlay;            
    [SerializeField] private GameObject warningBannerPanel;    
    [SerializeField] private TMP_Text runningWarningText;      

    [Header("=== STAGE 1: CÁC CÔNG TRÌNH CẦN SỬA CHỮA ===")]
    [SerializeField] private UpgradeableBuilding townHallBuilding;
    [SerializeField] private UpgradeableBuilding riceStorageBuilding;

    private bool isTownHallRepaired = false;
    private bool isRiceStorageRepaired = false;

    [Header("=== TÙY CHỈNH ANIMATION BÀN TAY ===")]
    [SerializeField] private float pointerMoveSpeed = 12f;  
    [SerializeField] private float bobbingSpeed = 8f;       
    [SerializeField] private float bobbingAmount = 12f;     

    [Header("=== THỜI DIỂM THOẠI MARCUS / GIÀ LÀNG ===")]
    [SerializeField] private DialogueData[] stage1Dialogues;
    [SerializeField] private DialogueData[] stage2Dialogues;
    [SerializeField] private DialogueData[] stage3Dialogues;
    [SerializeField] private DialogueData[] stageExpandLandDialogues; // 🔥 MỚI: Thoại mở rộng đất
    [SerializeField] private DialogueData[] stage4Dialogues;
    [SerializeField] private DialogueData[] stage5WarningDialogues;
    [SerializeField] private DialogueData[] stage6CompleteDialogues;

    [Header("=== NÚT BẤM CẦN KHỐNG CHẾ ===")]
    [SerializeField] private Button buildMenuButton;
    [SerializeField] private Button civilianTabButton;
    [SerializeField] private Button villaTabButton;         
    [SerializeField] private Button militaryTabButton;      
    [SerializeField] private Button buildWoodCutterButton;
    [SerializeField] private Button buildStoneStorageButton;
    [SerializeField] private Button buildWatchTowerButton;
    [SerializeField] private Button buildArcherTowerButton; 
    [SerializeField] private Button upgradeBuildingButton;

    [Header("=== SCENE REFERENCES ===")]
    [SerializeField] private Transform townHallTransform;   
    [SerializeField] private EnemySpawn enemySpawner;       

    [Header("=== CẤU HÌNH WAVE TUTORIAL ===")]
    [SerializeField] private int tutorialEnemyCount = 2;    
    private int enemiesRemaining = 0;

    private bool isPlacingBuilding = false;          
    private bool isWaitingForConstruction = false;   
    private bool hasBuiltWoodCutter = false;
    private bool hasBuiltStoneStorage = false;
    private bool hasBuiltWatchTower = false;
    private bool hasBuiltArcherTower = false; 
    private bool hasExpandedLand = false; // 🔥 MỚI: Cờ kiểm tra đã mở rộng đất chưa
    private bool hasOpenedBuildMenu = false;
    private bool hasOpenedTab = false;

    private RectTransform pointerRect;
    private Vector2 targetScreenPosition;
    private Coroutine cameraFocusCoroutine;
    private RectTransform currentTargetUI;

    private Vector3 currentTargetWorldPos;
    private bool isPointingAtWorld = false;

    private void Awake()
    {
        if (Ins == null) Ins = this;
        else Destroy(gameObject);

        if (handPointer != null)
        {
            pointerRect = handPointer.GetComponent<RectTransform>();
            handPointer.SetActive(false); 
        }

        if (highlightRing != null)
        {
            highlightRing.gameObject.SetActive(false); 
        }

        if (tutorialCanvas == null && handPointer != null)
            tutorialCanvas = handPointer.GetComponentInParent<Canvas>();
    }

    private void Start()
    {
        Time.timeScale = 1f;

        HidePointer();

        if (buildMenuButton != null) buildMenuButton.onClick.AddListener(OnBuildMenuButtonClicked);
        if (civilianTabButton != null) civilianTabButton.onClick.AddListener(OnTabClicked);
        if (villaTabButton != null) villaTabButton.onClick.AddListener(OnTabClicked);
        if (militaryTabButton != null) militaryTabButton.onClick.AddListener(OnTabClicked);

        if (buildWoodCutterButton != null) buildWoodCutterButton.onClick.AddListener(OnStartPlacement);
        if (buildStoneStorageButton != null) buildStoneStorageButton.onClick.AddListener(OnStartPlacement);
        if (buildWatchTowerButton != null) buildWatchTowerButton.onClick.AddListener(OnStartPlacement);
        if (buildArcherTowerButton != null) buildArcherTowerButton.onClick.AddListener(OnStartPlacement);
        if (upgradeBuildingButton != null) upgradeBuildingButton.onClick.AddListener(OnActionButtonClicked);

        StartStage1();
    }

    private void Update()
    {
        UpdateHandPointerAnimation();
        UpdateHighlightRingAnimation();
        UpdateStage3PointerDynamic();
        CheckUIFallbackState(); 
    }

    private void OnActionButtonClicked()
    {
        HidePointer();
    }

    private void CheckUIFallbackState()
    {
        if (currentStage == TutorialStage.None || currentStage == TutorialStage.Stage7_Complete) return;
        if (NPCDialogueUI.Ins != null && NPCDialogueUI.Ins.IsDialogueActive) return; 
        if (isPlacingBuilding || isWaitingForConstruction) return;

        // 🛑 CHẶN FALLBACK ở các Stage đang chỉ vào World 3D / Công trình
        if (currentStage == TutorialStage.Stage1_TownHall || 
            currentStage == TutorialStage.Stage3_UpgradeWood || 
            currentStage == TutorialStage.Stage4_ExpandLand) return;

        if (hasOpenedBuildMenu && buildShopCanvas != null && !buildShopCanvas.gameObject.activeInHierarchy)
        {
            if (currentStage == TutorialStage.Stage2_CivilBuildings)
            {
                ResetStage2Menu();
            }
            else if (currentStage == TutorialStage.Stage5_BuildDefenseTowers)
            {
                ResetStage5Menu();
            }
        }

        if (currentTargetUI != null && !currentTargetUI.gameObject.activeInHierarchy && handPointer.activeSelf)
        {
            if (buildMenuButton != null)
            {
                PointHandAtUI(buildMenuButton.transform as RectTransform);
            }
        }
    }

    // ====================================================================
    // GIAI ĐOẠN 1: SỬA CHỮA 2 CÔNG TRÌNH (NHÀ CHÍNH & KHO LÚA)
    // ====================================================================
    public void StartStage1()
    {
        currentStage = TutorialStage.Stage1_TownHall;
        LockAllInputs();
        HidePointer();

        RunDialogueSequence(stage1Dialogues, () =>
        {
            GuideNextRepairTarget();
        });
    }

    private void GuideNextRepairTarget()
    {
        UpgradeableBuilding currentTarget = null;
        string targetName = "";

        if (!isTownHallRepaired && townHallBuilding != null)
        {
            currentTarget = townHallBuilding;
            targetName = "**Nhà Chính**";
        }
        else if (!isRiceStorageRepaired && riceStorageBuilding != null)
        {
            currentTarget = riceStorageBuilding;
            targetName = "**Kho Lúa**";
        }

        if (currentTarget != null)
        {
            FocusCameraOn(currentTarget.transform.position, 1.0f);

            // 🔥 TẬN DỤNG SCANNER: Kiểm tra nếu UI Sửa Chữa đang mở thì chỉ tay thẳng vào NÚT SỬA CHỮA (Repair Button)
            RectTransform repairBtnRect = (TutorialSceneScanner.Ins != null) 
                ? TutorialSceneScanner.Ins.GetRepairButtonTransform(currentTarget) 
                : null;

            if (repairBtnRect != null && repairBtnRect.gameObject.activeInHierarchy)
            {
                PointHandAtUI(repairBtnRect); // Chỉ trực tiếp vào Nút Sửa Chữa trên Canvas UI
            }
            else
            {
                PointHandAt(currentTarget.transform.position); // Nếu chưa mở UI thì chỉ vào công trình
            }

            UpdateHint($" Bước 1: {targetName} đang bị hư hại! Bấm vào và chọn **Sửa Chữa**.");
        }
        else
        {
            HidePointer();
            StartStage2();
        }
    }

    public void OnBuildingRepaired(UpgradeableBuilding building)
    {
        if (currentStage != TutorialStage.Stage1_TownHall) return;

        if (building == townHallBuilding) isTownHallRepaired = true;
        else if (building == riceStorageBuilding) isRiceStorageRepaired = true;

        GuideNextRepairTarget();
    }

    public void OnClickTownHall()
    {
        if (currentStage != TutorialStage.Stage1_TownHall) return;
        HidePointer();
    }

    // ====================================================================
    // GIAI ĐOẠN 2: XÂY CÔNG TRÌNH DÂN SỰ
    // ====================================================================
    private void StartStage2()
    {
        currentStage = TutorialStage.Stage2_CivilBuildings;
        HidePointer();

        RunDialogueSequence(stage2Dialogues, () =>
        {
            ResetStage2Menu();
        });
    }

    private void ResetStage2Menu()
    {
        hasOpenedBuildMenu = false;
        hasOpenedTab = false;
        SetButtonInteractable(buildMenuButton, true);
        SetButtonInteractable(civilianTabButton, false);
        SetButtonInteractable(villaTabButton, false);
        SetButtonInteractable(buildWoodCutterButton, false);
        SetButtonInteractable(buildStoneStorageButton, false);

        PointHandAtUI(buildMenuButton.transform as RectTransform);

        if (!hasBuiltWoodCutter)
            UpdateHint(" Bước 2: Nhấn **Cửa Hàng Xây Dựng** để chọn xây Khai Thác Gỗ.");
        else
            UpdateHint(" Bước 2: Nhấn **Cửa Hàng Xây Dựng** để tiếp tục chọn xây Kho Đá.");
    }

    private void OnBuildMenuButtonClicked()
    {
        if (currentStage != TutorialStage.Stage2_CivilBuildings && currentStage != TutorialStage.Stage5_BuildDefenseTowers) return;

        hasOpenedBuildMenu = true;
        hasOpenedTab = false;

        if (currentStage == TutorialStage.Stage2_CivilBuildings)
        {
            SetBuildTabButtonsInteractable(true);
            Button buildTabButton = GetSharedBuildTabButton();
            if (buildTabButton != null)
            {
                PointHandAtUI(buildTabButton.transform as RectTransform);
            }
            UpdateHint(" Bước 2: Chọn tab **Dân Sự** để mở các công trình.");
        }
        else if (currentStage == TutorialStage.Stage5_BuildDefenseTowers)
        {
            SetButtonInteractable(militaryTabButton, true);
            PointHandAtUI(militaryTabButton.transform as RectTransform);
            UpdateHint(" Bước 5: Chọn tab **Quân Sự** để xem tháp phòng thủ.");
        }
    }

    private void OnTabClicked()
    {
        if (hasOpenedTab) return;

        hasOpenedTab = true;

        if (currentStage == TutorialStage.Stage2_CivilBuildings)
        {
            SetButtonInteractable(buildWoodCutterButton, !hasBuiltWoodCutter);
            SetButtonInteractable(buildStoneStorageButton, !hasBuiltStoneStorage);

            if (!hasBuiltWoodCutter)
            {
                if (buildWoodCutterButton != null) PointHandAtUI(buildWoodCutterButton.transform as RectTransform);
                UpdateHint(" Bước 2: Bấm chọn **Khai Thác Gỗ** để đặt xây.");
            }
            else if (!hasBuiltStoneStorage)
            {
                if (buildStoneStorageButton != null) PointHandAtUI(buildStoneStorageButton.transform as RectTransform);
                UpdateHint(" Bước 2: Bấm chọn **Kho Đá** để đặt xây.");
            }
        }
        else if (currentStage == TutorialStage.Stage5_BuildDefenseTowers)
        {
            if (!hasBuiltWatchTower)
            {
                SetButtonInteractable(buildWatchTowerButton, true);
                if (buildWatchTowerButton != null) PointHandAtUI(buildWatchTowerButton.transform as RectTransform);
                UpdateHint(" Bước 5: Chọn **Tháp Canh** để xây dựng phòng thủ.");
            }
            else if (!hasBuiltArcherTower)
            {
                SetButtonInteractable(buildArcherTowerButton, true);
                if (buildArcherTowerButton != null) PointHandAtUI(buildArcherTowerButton.transform as RectTransform);
                UpdateHint(" Bước 5: Chọn **Tháp Cung** để tăng cường hỏa lực.");
            }
        }
    }

    public void OnCivilBuildingPlaced(BuildingType buildingType, Transform placedBuildingTransform = null)
    {
        if (currentStage != TutorialStage.Stage2_CivilBuildings) return;

        isPlacingBuilding = false;

        if (buildingType == BuildingType.WoodCutter) hasBuiltWoodCutter = true;
        if (buildingType == BuildingType.StoneStorage) hasBuiltStoneStorage = true;

        if (placedBuildingTransform != null)
        {
            isWaitingForConstruction = true;
            HidePointer(); 
            UpdateHint("⏳ Công trình đang được xây dựng... Vui lòng đợi.");
        }
        else
        {
            CheckStage2Progress();
        }
    }

    public void OnBuildingConstructionFinished(BuildingType buildingType)
    {
        isWaitingForConstruction = false;

        if (currentStage == TutorialStage.Stage2_CivilBuildings)
        {
            CheckStage2Progress();
        }
        else if (currentStage == TutorialStage.Stage5_BuildDefenseTowers)
        {
            CheckStage5Progress();
        }
    }

    private void CheckStage2Progress()
    {
        if (!hasBuiltWoodCutter || !hasBuiltStoneStorage)
        {
            ResetStage2Menu();
        }
        else
        {
            HidePointer();
            UpdateHint("");
            StartStage3();
        }
    }

    // ====================================================================
    // GIAI ĐOẠN 3: NÂNG CẤP CÔNG TRÌNH
    // ====================================================================
    private void StartStage3()
    {
        currentStage = TutorialStage.Stage3_UpgradeWood;

        UpgradeableBuilding woodCutter = null;
        if (TutorialSceneScanner.Ins != null)
        {
            woodCutter = TutorialSceneScanner.Ins.FindPlacedBuilding(BuildingType.WoodCutter);
        }

        RunDialogueSequence(stage3Dialogues, () =>
        {
            if (woodCutter != null && TutorialSceneScanner.Ins != null)
            {
                FocusCameraOn(woodCutter.transform.position, 1.0f);

                bool isUIOpen = TutorialSceneScanner.Ins.IsBuildingUIOpen(woodCutter);
                RectTransform upgradeBtnRect = TutorialSceneScanner.Ins.GetUpgradeButtonTransform(woodCutter);

                if (isUIOpen && upgradeBtnRect != null)
                {
                    PointHandAtUI(upgradeBtnRect);
                }
                else
                {
                    PointHandAt(woodCutter.transform.position);
                }
            }
            
            SetButtonInteractable(upgradeBuildingButton, true);
            UpdateHint(" Bước 3: Nhấn vào **Nhà Khai Thác Gỗ** và chọn **Nâng Cấp**.");
        });
    }

    public void OnBuildingUpgraded(UpgradeableBuilding building)
    {
        if (currentStage != TutorialStage.Stage3_UpgradeWood) return;

        if (building != null && building.buildingType == BuildingType.WoodCutter)
        {
            HidePointer();
            StartStage4ExpandLand(); // 🔥 Chuyển sang Stage mở rộng đất
        }
    }
    /// <summary>
    ///  Tự động chuyển đổi bàn tay giữa Công Trình 3D và Nút Nâng Cấp UI ở Stage 3
    /// </summary>
    private void UpdateStage3PointerDynamic()
    {
        if (currentStage != TutorialStage.Stage3_UpgradeWood) return;
        if (NPCDialogueUI.Ins != null && NPCDialogueUI.Ins.IsDialogueActive) return;

        UpgradeableBuilding woodCutter = (TutorialSceneScanner.Ins != null) 
            ? TutorialSceneScanner.Ins.FindPlacedBuilding(BuildingType.WoodCutter) 
            : null;

        if (woodCutter == null) return;

        bool isUIOpen = TutorialSceneScanner.Ins.IsBuildingUIOpen(woodCutter);
        RectTransform upgradeBtnRect = TutorialSceneScanner.Ins.GetUpgradeButtonTransform(woodCutter);

        if (isUIOpen && upgradeBtnRect != null && upgradeBtnRect.gameObject.activeInHierarchy)
        {
            // 1. Nếu UI Upgrade đang mở -> Chỉ vào Nút Nâng Cấp trên UI
            PointHandAtUI(upgradeBtnRect);
        }
        else
        {
            // 2. Nếu UI chưa mở -> Chỉ vào Nhà Khai Thác Gỗ 3D dưới đất
            PointHandAt(woodCutter.transform.position);
        }
    }

    // ====================================================================
    // GIAI ĐOẠN 4: HƯỚNG DẪN MỞ RỘNG KHU VỰC XÂY DỰNG
    // ====================================================================
    private void StartStage4ExpandLand()
    {
        currentStage = TutorialStage.Stage4_ExpandLand;

        RunDialogueSequence(stageExpandLandDialogues, () =>
        {
            StartCoroutine(Stage4ExpandLandRoutine());
        });
    }

    private IEnumerator Stage4ExpandLandRoutine()
    {
        // 1. Tắt ngón tay đi trong lúc Camera đang di chuyển
        HidePointer();

        Transform expandBtnTrans = (TutorialSceneScanner.Ins != null) 
            ? TutorialSceneScanner.Ins.GetExpandLandButtonTransform(ExpandDirection.North) 
            : null;

        if (expandBtnTrans != null)
        {
            float cameraDuration = 1.0f;

            // 2. Cho Camera trượt tới nút (+)
            FocusCameraOn(expandBtnTrans.position, cameraDuration);

            // 3. Đợi đúng 1.0 giây cho Camera dừng hẳn
            yield return new WaitForSecondsRealtime(cameraDuration);

            // 4. Camera đứng yên rồi mới tính vị trí chỉ tay
            RectTransform rectUI = expandBtnTrans.GetComponent<RectTransform>();
            if (rectUI != null)
            {
                PointHandAtUI(rectUI);
            }
            else
            {
                PointHandAt(expandBtnTrans.position);
            }
        }

        UpdateHint(" Bước 4: Hãy bấm vào nút **[+]** trên bản đồ để Mở Rộng Lãnh Thổ!");
    }

    /// <summary>
    /// 🔥 Gọi hàm này khi người chơi bấm nút (+) mở rộng đất thành công trong LandGridManager
    /// </summary>
    public void OnLandExpanded()
    {
        if (currentStage != TutorialStage.Stage4_ExpandLand) return;

        hasExpandedLand = true;
        HidePointer();
        UpdateHint("");

        // Chuyển sang Stage 5: Xây tháp phòng thủ
        StartStage5();
    }

    // ====================================================================
    // GIAI ĐOẠN 5: CẢNH BÁO & XÂY DỰNG THÁP PHÒNG THỦ
    // ====================================================================
    private void StartStage5()
    {
        currentStage = TutorialStage.Stage5_BuildDefenseTowers;

        Transform enemyCamp = null;
        if (TutorialSceneScanner.Ins != null)
        {
            enemyCamp = TutorialSceneScanner.Ins.GetEnemyCampTransform();
        }

        RunDialogueSequence(stage4Dialogues, () =>
        {
            StartCoroutine(Stage5CameraAndWarningSequence(enemyCamp));
        });
    }

    private IEnumerator Stage5CameraAndWarningSequence(Transform enemyCamp)
    {
        StartCoroutine(DoRedAlertRoutine());

        if (enemyCamp != null)
        {
            FocusCameraOn(enemyCamp.position, 1.2f);
            PointHandAt(enemyCamp.position);
            UpdateHint("⚠️ CẢNH BÁO: Phát hiện căn cứ kẻ thù lân cận!");
            yield return new WaitForSecondsRealtime(2.5f);
        }

        if (townHallTransform != null)
        {
            FocusCameraOn(townHallTransform.position, 1.2f);
            yield return new WaitForSecondsRealtime(1.2f);
        }

        ResetStage5Menu();
    }

    private IEnumerator DoRedAlertRoutine()
    {
        if (warningBannerPanel != null) warningBannerPanel.SetActive(true);
        if (runningWarningText != null) runningWarningText.text = "⚠️ CẢNH BÁO: KẺ THÙ ĐANG TIẾN CÔNG CĂN CỨ! HÃY XÂY THÁP PHÒNG THỦ! ⚠️";

        float timer = 0f;
        float alertDuration = 3f;
        while (timer < alertDuration)
        {
            timer += Time.unscaledDeltaTime;
            if (redFlashOverlay != null)
            {
                float alpha = (Mathf.Sin(Time.unscaledTime * 12f) + 1f) * 0.25f;
                redFlashOverlay.color = new Color(1f, 0f, 0f, alpha);
            }
            yield return null;
        }

        if (redFlashOverlay != null) redFlashOverlay.color = new Color(1f, 0f, 0f, 0f);
        if (warningBannerPanel != null) warningBannerPanel.SetActive(false);
    }

    private void ResetStage5Menu()
    {
        hasOpenedBuildMenu = false;
        hasOpenedTab = false;
        SetButtonInteractable(buildMenuButton, true);
        SetButtonInteractable(militaryTabButton, false);
        SetButtonInteractable(buildWatchTowerButton, false);
        SetButtonInteractable(buildArcherTowerButton, false);

        PointHandAtUI(buildMenuButton.transform as RectTransform);

        if (!hasBuiltWatchTower)
            UpdateHint(" Bước 5: Mở **Cửa Hàng Xây Dựng** để chọn xây Tháp Canh.");
        else if (!hasBuiltArcherTower)
            UpdateHint(" Bước 5: Mở **Cửa Hàng Xây Dựng** để chọn xây Tháp Cung.");
    }

    public void OnDefenseBuildingPlaced(BuildingType buildingType, Transform placedBuildingTransform = null)
    {
        if (currentStage != TutorialStage.Stage5_BuildDefenseTowers) return;

        if (buildingType == BuildingType.WatchTower)
        {
            hasBuiltWatchTower = true;
            isPlacingBuilding = false;
        }
        else if (buildingType == BuildingType.ArcherTower)
        {
            hasBuiltArcherTower = true;
            isPlacingBuilding = false;
        }

        if (placedBuildingTransform != null)
        {
            isWaitingForConstruction = true;
            HidePointer(); 
            string towerName = (buildingType == BuildingType.WatchTower) ? "Tháp Canh" : "Tháp Cung";
            UpdateHint($"⏳ {towerName} đang được xây dựng...");
        }
        else
        {
            CheckStage5Progress();
        }
    }

    private void CheckStage5Progress()
    {
        if (!hasBuiltWatchTower || !hasBuiltArcherTower)
        {
            ResetStage5Menu();
        }
        else
        {
            HidePointer();
            UpdateHint("");
            StartCoroutine(StartStage6Routine());
        }
    }

    // ====================================================================
    // GIAI ĐOẠN 6 & 7: KẺ THÙ TẤN CÔNG & HOÀN THÀNH TUTORIAL
    // ====================================================================
    private IEnumerator StartStage6Routine()
    {
        currentStage = TutorialStage.Stage6_EnemyWave;

        if (townHallTransform != null)
        {
            FocusCameraOn(townHallTransform.position, 1.0f);
        }

        bool dialogueDone = false;
        RunDialogueSequence(stage5WarningDialogues, () => { dialogueDone = true; });
        while (!dialogueDone) yield return null;

        enemiesRemaining = tutorialEnemyCount;
        
        if (enemySpawner != null)
        {
            for (int i = 0; i < tutorialEnemyCount; i++)
            {
                enemySpawner.SpawnEnemy(); 
            }

            EnemyAI[] activeEnemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            foreach (var ai in activeEnemies)
            {
                if (ai != null && townHallTransform != null)
                {
                    ai.villageCenter = townHallTransform;
                    ai.attackMainDirectly = true;
                }
            }
        }

        UnlockAllInputs();
    }

    public void OnEnemyKilled()
    {
        if (currentStage != TutorialStage.Stage6_EnemyWave) return;

        enemiesRemaining--;
        if (enemiesRemaining <= 0)
        {
            StartStage7();
        }
    }

    private void StartStage7()
    {
        currentStage = TutorialStage.Stage7_Complete;

        RunDialogueSequence(stage6CompleteDialogues, () =>
        {
            if (JsonDataManager.Ins != null)
            {
                JsonDataManager.Ins.AddWood(500);
                JsonDataManager.Ins.AddStone(500);
                JsonDataManager.Ins.BroadcastAllResources();
            }

            HidePointer();
            UpdateHint("🎉 Bạn đã hoàn thành Tutorial! Nhận **500 Gỗ & 500 Đá** tân thủ.");
            UnlockAllInputs();
        });
    }

    // ====================================================================
    // CÁC HÀM PHỤ TRỢ QUẢN LÝ BÀN TAY & UI
    // ====================================================================
    public void OnStartPlacement()
    {
        isPlacingBuilding = true;
        HidePointer();
        UpdateHint(" Bước 2: Hãy chọn vị trí thích hợp trên bản đồ để **Đặt Công Trình**.");
    }

    public bool CanPlaceBuilding(BuildingType type)
    {
        if (!IsTutorialActive) return true;

        switch (currentStage)
        {
            case TutorialStage.Stage2_CivilBuildings:
                return type == BuildingType.WoodCutter || type == BuildingType.StoneStorage;
            case TutorialStage.Stage5_BuildDefenseTowers:
                return type == BuildingType.WatchTower || type == BuildingType.ArcherTower;
            default:
                return false;
        }
    }

    public void OnCancelPlacement()
    {
        isPlacingBuilding = false;

        if (currentStage == TutorialStage.Stage2_CivilBuildings)
            ResetStage2Menu();
        else if (currentStage == TutorialStage.Stage5_BuildDefenseTowers)
            ResetStage5Menu();
    }

    private void UpdateHandPointerAnimation()
    {
        if (handPointer == null || !handPointer.activeSelf || pointerRect == null) return;

        if (isPointingAtWorld && Camera.main != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(currentTargetWorldPos);
            targetScreenPosition = (Vector2)screenPos + pointerOffset;
        }

        Vector2 currentPos = pointerRect.position;
        Vector2 smoothedPos = Vector2.Lerp(currentPos, targetScreenPosition, Time.unscaledDeltaTime * pointerMoveSpeed);

        float bobbingOffset = Mathf.Sin(Time.unscaledTime * bobbingSpeed) * bobbingAmount;
        Vector2 finalPos = smoothedPos + new Vector2(bobbingOffset * 0.5f, bobbingOffset);

        pointerRect.position = finalPos;
    }

    private void UpdateHighlightRingAnimation()
    {
        if (highlightRing == null || !highlightRing.gameObject.activeSelf) return;

        highlightRing.Rotate(Vector3.forward, -90f * Time.unscaledDeltaTime);
        float pulseScale = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.08f;
        highlightRing.localScale = Vector3.one * pulseScale;
    }

    private void PointHandAt(Vector3 worldPos)
    {
        if (handPointer == null) return;

        isPointingAtWorld = true;
        currentTargetWorldPos = worldPos;
        currentTargetUI = null;
        handPointer.SetActive(true);

        if (Camera.main != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            targetScreenPosition = (Vector2)screenPos + pointerOffset;
        }

        if (highlightRing != null) highlightRing.gameObject.SetActive(false);
    }

    private void PointHandAtUI(RectTransform uiRect)
    {
        if (handPointer == null || uiRect == null) return;

        isPointingAtWorld = false;

        // Cập nhật lại UI Layout ngay lập tức để lấy tọa độ chuẩn
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(uiRect);

        currentTargetUI = uiRect;
        handPointer.SetActive(true);

        Canvas parentCanvas = uiRect.GetComponentInParent<Canvas>();
        Camera cam = null;

        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = parentCanvas.worldCamera != null ? parentCanvas.worldCamera : Camera.main;
        }

        // 🔥 TÍNH CHÍNH XÁC TÂM CỦA RECTTRANSFORM (Khắc phục lỗi lệch do Pivot/Layout Group)
        Vector3 worldCenter = uiRect.TransformPoint(uiRect.rect.center);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldCenter);

        targetScreenPosition = screenPoint + pointerOffset;

        if (highlightRing != null)
        {
            highlightRing.gameObject.SetActive(true);
            highlightRing.position = screenPoint;
        }
    }

    private void HidePointer()
    {
        isPointingAtWorld = false;
        currentTargetUI = null;
        if (handPointer != null) handPointer.SetActive(false);
        if (highlightRing != null) highlightRing.gameObject.SetActive(false);
    }

    private void RunDialogueSequence(DialogueData[] dialogues, System.Action onComplete = null)
    {
        // 🔥 1. Chỉ bật Dim đen khi bắt đầu chạy thoại cốt truyện
        if (overlayDim != null) overlayDim.SetActive(true);

        if (NPCDialogueUI.Ins != null)
        {
            NPCDialogueUI.Ins.ShowDialogueSequence(dialogues, () =>
            {
                // 🔥 2. Tắt Dim đen ngay khi kết thúc thoại cốt truyện
                if (overlayDim != null) overlayDim.SetActive(false);
                onComplete?.Invoke();
            });
        }
        else
        {
            if (overlayDim != null) overlayDim.SetActive(false);
            onComplete?.Invoke();
        }
    }

    private void FocusCameraOn(Vector3 targetWorldPos, float duration)
    {
        if (Camera.main == null) return;

        if (cameraFocusCoroutine != null) StopCoroutine(cameraFocusCoroutine);
        cameraFocusCoroutine = StartCoroutine(AnimateCameraFocus(targetWorldPos, duration));
    }

    private IEnumerator AnimateCameraFocus(Vector3 targetPos, float duration)
    {
        Transform camTrans = Camera.main.transform;
        Vector3 startPos = camTrans.position;
        Vector3 endPos = new Vector3(targetPos.x, camTrans.position.y, targetPos.z - 8f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            camTrans.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
    }

    private IEnumerator AnimateTextPop(RectTransform textRect)
    {
        if (textRect == null) yield break;

        textRect.localScale = Vector3.zero;
        float elapsed = 0f;
        float duration = 0.3f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float scale = Mathf.Sin(t * Mathf.PI * 0.5f) * 1.15f;
            textRect.localScale = Vector3.one * scale;
            yield return null;
        }
        textRect.localScale = Vector3.one;
    }

    private void LockAllInputs()
    {
        // 🛑 BỎ `overlayDim.SetActive(true)` ở đây để tránh bị tối màn hình khi không thoại
        SetButtonInteractable(buildMenuButton, false);
        SetButtonInteractable(civilianTabButton, false);
        SetButtonInteractable(villaTabButton, false);
        SetButtonInteractable(militaryTabButton, false);
        SetButtonInteractable(buildWoodCutterButton, false);
        SetButtonInteractable(buildStoneStorageButton, false);
        SetButtonInteractable(buildWatchTowerButton, false);
        SetButtonInteractable(buildArcherTowerButton, false);
        SetButtonInteractable(upgradeBuildingButton, false);
    }

    private void UnlockAllInputs()
    {
        // 🛑 BỎ `overlayDim.SetActive(false)` ở đây
        SetButtonInteractable(buildMenuButton, true);
        SetButtonInteractable(civilianTabButton, true);
        SetButtonInteractable(villaTabButton, true);
        SetButtonInteractable(militaryTabButton, true);
        SetButtonInteractable(buildWoodCutterButton, true);
        SetButtonInteractable(buildStoneStorageButton, true);
        SetButtonInteractable(buildWatchTowerButton, true);
        SetButtonInteractable(buildArcherTowerButton, true);
        SetButtonInteractable(upgradeBuildingButton, true);
    }

    private void SetButtonInteractable(Button btn, bool interactable)
    {
        if (btn != null) btn.interactable = interactable;
    }

    private void UpdateHint(string hintMessage)
    {
        if (hintText != null)
        {
            hintText.text = hintMessage;
            hintText.gameObject.SetActive(!string.IsNullOrEmpty(hintMessage));
            if (hintText.gameObject.activeSelf)
            {
                StartCoroutine(AnimateTextPop(hintText.rectTransform));
            }
        }
    }

    private Button GetSharedBuildTabButton()
    {
        if (civilianTabButton != null) return civilianTabButton;
        return villaTabButton;
    }

    private void SetBuildTabButtonsInteractable(bool interactable)
    {
        SetButtonInteractable(civilianTabButton, interactable);
        SetButtonInteractable(villaTabButton, interactable);
    }
}