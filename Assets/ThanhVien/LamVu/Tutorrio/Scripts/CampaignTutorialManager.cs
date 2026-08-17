using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/*
 * CampaignTutorialManager.cs
 * Hệ thống Hướng Dẫn Tutorial 6 Giai Đoạn CHUẨN XÁC NGUYÊN BẢN THEO YÊU CẦU cho Demacia Rising
 * 
 * QUY TẮC BẮT BUỘC:
 * 1. Giữ lại Vòng Tròn Highlight Ring, ẩn hoàn toàn Bàn Tay Pointer.
 * 2. Hướng dẫn chữ tuyệt đối KHÔNG DÙNG Emoji, Icon, Ký hiệu, tuyệt đối KHÔNG DÙNG từ "hoặc", phải có Dấu Câu Tiếng Việt chuẩn xác.
 * 3. Vòng tròn Ring luôn luôn chỉ đúng vị trí đối tượng/nút bấm mục tiêu.
 */

public enum DemaciaTutorialStage
{
    None,
    Stage1_BuildWood,           // 1. Xây Xưởng Gỗ
    Stage1_SkipDayWood,         // 1. Bấm qua ngày hoàn thành Xưởng Gỗ
    Stage1_ResourceExplain,     // 1. Giải thích tài nguyên khi qua ngày
    
    Stage2_ViewEnemyTerritory,  // 2. Trỏ vùng đất cần chinh phục
    Stage2_ReturnToBase,        // 2. Trở về vùng đất hiện tại
    Stage2_BuildBarracks,       // 2. Xây Trại Lính
    Stage2_SkipDayBarracks,     // 2. Bấm qua ngày hoàn thành Trại Lính

    Stage3_EnemyDiscovered,     // 3. Spawn 1 quái, kẻ địch phát hiện ra ta và tấn công trước

    Stage4_AttackEnemyMonster,  // 4. Tấn công quái (SceneBattle)
    Stage4_VictoryComplete,     // 4. Phòng thủ thành công

    Stage5_AttackEnemyOutpost,  // 5. Tấn công căn cứ địch (SceneBattle)
    Stage5_VictoryConquer,      // 5. Chiếm được vùng đất mới

    Stage6_BuildOnNewLand,      // 6. Hướng dẫn xây công trình trên vùng đất vừa chiếm
    Completed
}

public class CampaignTutorialManager : MonoBehaviour
{
    public static CampaignTutorialManager Ins { get; private set; }

    [Header("=== TRẠNG THÁI TUTORIAL DEMACIA ===")]
    public DemaciaTutorialStage currentStage = DemaciaTutorialStage.None;

    [Header("=== THÀNH PHẦN GIAO DIỆN HƯỚNG DẪN ===")]
    [SerializeField] private GameObject overlayDim;         
    [SerializeField] private GameObject handPointer;        // 🔒 Sẽ bị ẩn hoàn toàn theo yêu cầu
    [SerializeField] private RectTransform highlightRing;   // 🔒 Vòng tròn Highlight duy nhất
    [SerializeField] private Canvas tutorialCanvas;         
    [SerializeField] private TMP_Text hintText;             

    [Header("=== THÀNH PHẦN SCENE & VÙNG ĐẤT ===")]
    [SerializeField] private SettlementZone baseZone;        // Vùng đất khởi đầu (ZEFFIRA)
    [SerializeField] private SettlementZone enemyZone;       // Vùng đất cần chinh phục (Zone B)
    [SerializeField] private EnemySpawn enemySpawner;       
    [SerializeField] private int tutorialEnemyCount = 3;    

    [Header("=== NÚT TẤN CÔNG CĂN CỨ ĐỊCH (STAGE 5) ===")]
    [SerializeField] private Button outpostAttackButton;

    [Header("=== CÁC MẢNG LỜI THOẠI TRƯỞNG LÀNG MARCUS (CHỈNH SỬA TRÊN INSPECTOR) ===")]
    [Tooltip("Danh sách các câu thoại Trưởng Làng Marcus ở Stage 1 (Nếu trống sẽ dùng thoại mặc định trong code)")]
    [SerializeField] private DialogueData[] stage1Dialogues;
    [Tooltip("Danh sách các câu thoại Trưởng Làng Marcus ở Stage 2")]
    [SerializeField] private DialogueData[] stage2Dialogues;
    [Tooltip("Danh sách các câu thoại Trưởng Làng Marcus ở Stage 3")]
    [SerializeField] private DialogueData[] stage3Dialogues;
    [Tooltip("Danh sách các câu thoại Trưởng Làng Marcus ở Stage 4")]
    [SerializeField] private DialogueData[] stage4Dialogues;
    [Tooltip("Danh sách các câu thoại Trưởng Làng Marcus ở Stage 5")]
    [SerializeField] private DialogueData[] stage5Dialogues;
    [Tooltip("Danh sách các câu thoại Trưởng Làng Marcus khi hoàn thành Tutorial")]
    [SerializeField] private DialogueData[] stageCompleteDialogues;

    private RectTransform currentTargetUI;
    private Vector3 currentTargetWorldPos;
    private bool isPointingAtWorld = false;
    private GameObject spawnedMonsterInstance;

    private void OnDestroy()
    {
        if (Ins == this) Ins = null;
    }

    private void Awake()
    {
        if (Ins == null) Ins = this;
        else Destroy(gameObject);

        // 🔒 Ẩn bàn tay theo yêu cầu (chỉ dùng Vòng Tròn Ring)
        if (handPointer != null) handPointer.SetActive(false);

        if (highlightRing != null) highlightRing.gameObject.SetActive(false);

        if (tutorialCanvas == null && highlightRing != null)
            tutorialCanvas = highlightRing.GetComponentInParent<Canvas>();
    }

    private void Start()
    {
        // Kiểm tra xem đã hoàn thành Tutorial hoàn toàn chưa
        if (PlayerPrefs.GetInt("TutorialCompleted", 0) == 1)
        {
            currentStage = DemaciaTutorialStage.Completed;
            HidePointer();
            if (tutorialCanvas != null) tutorialCanvas.gameObject.SetActive(false);
            return;
        }

        // Xử lý khi trở về từ SceneBattle
        if (BattleData.HasResult || BattleData.LastBattleWasVictory)
        {
            int lastStage = PlayerPrefs.GetInt("SavedTutorialStage", 4);
            BattleData.HasResult = false;
            BattleData.LastBattleWasVictory = false;

            EnsureAllBuiltBuildingsCompleted();

            if (lastStage == 4)
            {
                StartCoroutine(HandleStage4ReturnRoutine());
                return;
            }
            else if (lastStage == 5)
            {
                StartCoroutine(HandleStage5ReturnRoutine());
                return;
            }
        }

        Time.timeScale = 1f;
        HidePointer();

        // Bắt đầu Stage 1
        StartStage1_BuildWood();
    }

    private void Update()
    {
        UpdateHighlightRingAnimation();
    }

    private void PlayDialogueSequence(DialogueData[] customArray, DialogueData[] defaultArray, System.Action onComplete = null)
    {
        DialogueData[] toPlay = (customArray != null && customArray.Length > 0) ? customArray : defaultArray;
        if (NPCDialogueUI.Ins != null && toPlay != null && toPlay.Length > 0)
        {
            // 💡 Bật Overlay Dim khi bắt đầu đoạn hội thoại của Marcus
            if (overlayDim != null)
            {
                var img = overlayDim.GetComponent<UnityEngine.UI.Image>();
                if (img != null) img.raycastTarget = false;
                overlayDim.SetActive(true);
            }

            NPCDialogueUI.Ins.ShowDialogueSequence(toPlay, () =>
            {
                // 💡 Tắt Overlay Dim ngay khi hội thoại kết thúc để người chơi tương tác với thế giới 3D
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

    // ====================================================================
    // STAGE 1: XƯỞNG GỖ -> QUA NGÀY -> TÀI NGUYÊN -> XƯỞNG ĐÁ -> QUA NGÀY
    // ====================================================================
    public void StartStage1_BuildWood()
    {
        currentStage = DemaciaTutorialStage.Stage1_BuildWood;
        PlayerPrefs.SetInt("SavedTutorialStage", 1);
        PlayerPrefs.Save();

        PlayDialogueSequence(stage1Dialogues, new DialogueData[]
        {
            new DialogueData { speakerName = "Trưởng Làng", message = "Chào mừng bạn đến với vùng đất mới. Hãy bắt đầu phát triển vương quốc của chúng ta." }
        }, () =>
        {
            UpdateHint("Hãy chọn ô đất trống và xây dựng Xưởng Gỗ.");
            HidePointer();
        });
    }

    public void OnBuildingPlaced(BuildingType type)
    {
        if (currentStage == DemaciaTutorialStage.Stage1_BuildWood)
        {
            currentStage = DemaciaTutorialStage.Stage1_SkipDayWood;
            UpdateHint("Thời gian thi công cần qua ngày mới hoàn thành. Hãy bấm nút Qua Ngày để hoàn tất xây dựng.");
            PointAtSkipDayButton();
        }
        else if (currentStage == DemaciaTutorialStage.Stage2_BuildBarracks)
        {
            currentStage = DemaciaTutorialStage.Stage2_SkipDayBarracks;
            UpdateHint("Hãy bấm nút Qua Ngày để hoàn tất xây dựng Trại Lính.");
            PointAtSkipDayButton();
        }
        else if (currentStage == DemaciaTutorialStage.Stage6_BuildOnNewLand)
        {
            CompleteTutorial();
        }
    }

    public void OnDayOrWaveIncremented()
    {
        if (currentStage == DemaciaTutorialStage.Stage1_SkipDayWood)
        {
            currentStage = DemaciaTutorialStage.Stage1_ResourceExplain;
            UpdateHint("Khi qua ngày mới, các công trình sẽ cung cấp tài nguyên cho vương quốc của bạn.");
            HidePointer();
            Invoke(nameof(StartStage2_ViewEnemyTerritory), 3.5f);
        }
        else if (currentStage == DemaciaTutorialStage.Stage2_SkipDayBarracks)
        {
            StartStage3_EnemyDiscovered();
        }
    }

    // ====================================================================
    // STAGE 2: QUAN SÁT VÙNG ĐẤT ĐỊCH -> VỀ CĂN CỨ -> XÂY TRẠI LÍNH -> QUA NGÀY
    // ====================================================================
    private void StartStage2_ViewEnemyTerritory()
    {
        currentStage = DemaciaTutorialStage.Stage2_ViewEnemyTerritory;
        PlayerPrefs.SetInt("SavedTutorialStage", 2);
        PlayerPrefs.Save();

        UpdateHint("Hãy quan sát vùng đất cần chinh phục phía trước.");
        HidePointer();

        if (enemyZone != null)
        {
            FocusCameraOn(enemyZone.transform.position, 1.5f);
        }

        Invoke(nameof(StartStage2_ReturnToBase), 3.5f);
    }

    private void StartStage2_ReturnToBase()
    {
        currentStage = DemaciaTutorialStage.Stage2_ReturnToBase;
        UpdateHint("Trở về vùng đất hiện tại để chuẩn bị lực lượng.");
        HidePointer();

        if (baseZone != null)
        {
            FocusCameraOn(baseZone.transform.position, 1.5f);
        }

        Invoke(nameof(StartStage2_BuildBarracks), 3f);
    }

    private void StartStage2_BuildBarracks()
    {
        currentStage = DemaciaTutorialStage.Stage2_BuildBarracks;
        UpdateHint("Hãy chọn ô đất trống và xây dựng Trại Lính.");
        HidePointer();

        if (baseZone != null)
        {
            FocusCameraOn(baseZone.transform.position, 1f);
        }
    }

    private EnemySpawn GetEnemySpawner()
    {
        if (enemySpawner != null) return enemySpawner;
        if (enemyZone != null && enemyZone.spawnedEnemyOutpostInstance != null)
        {
            var spawner = enemyZone.spawnedEnemyOutpostInstance.GetComponentInChildren<EnemySpawn>();
            if (spawner != null) return spawner;
        }
        return Object.FindFirstObjectByType<EnemySpawn>();
    }

    // ====================================================================
    // STAGE 3: KẺ ĐỊCH PHÁT HIỆN VÀ TẤN CÔNG TRƯỚC
    // ====================================================================
    private void StartStage3_EnemyDiscovered()
    {
        currentStage = DemaciaTutorialStage.Stage3_EnemyDiscovered;
        PlayerPrefs.SetInt("SavedTutorialStage", 3);
        PlayerPrefs.Save();

        EnemySpawn spawner = GetEnemySpawner();
        if (spawner != null)
        {
            spawner.SpawnEnemy();
        }

        EnemyAI monster = Object.FindFirstObjectByType<EnemyAI>();
        if (monster != null)
        {
            spawnedMonsterInstance = monster.gameObject;
            PointHandAt(monster.transform.position + Vector3.up * 1.5f);
        }

        PlayDialogueSequence(stage3Dialogues, new DialogueData[]
        {
            new DialogueData { speakerName = "Trưởng Làng", message = "Kẻ địch đã phát hiện ra ta và tấn công trước." }
        }, () =>
        {
            StartStage4_AttackEnemyMonster();
        });
    }

    // ====================================================================
    // STAGE 4: TẤN CÔNG QUÁI (SCENE BATTLE) -> PHÒNG THỦ THÀNH CÔNG
    // ====================================================================
    private void StartStage4_AttackEnemyMonster()
    {
        currentStage = DemaciaTutorialStage.Stage4_AttackEnemyMonster;
        PlayerPrefs.SetInt("SavedTutorialStage", 4);
        PlayerPrefs.Save();

        UpdateHint("Hãy chọn kẻ địch để tiến hành tấn công phòng thủ.");

        if (spawnedMonsterInstance != null)
        {
            PointHandAt(spawnedMonsterInstance.transform.position + Vector3.up * 1.5f);
        }
        else
        {
            EnemyAI monster = Object.FindFirstObjectByType<EnemyAI>();
            if (monster != null)
            {
                spawnedMonsterInstance = monster.gameObject;
                PointHandAt(monster.transform.position + Vector3.up * 1.5f);
            }
        }
    }

    private IEnumerator HandleStage4ReturnRoutine()
    {
        currentStage = DemaciaTutorialStage.Stage4_VictoryComplete;

        bool dialogueDone = false;
        PlayDialogueSequence(stage4Dialogues, new DialogueData[]
        {
            new DialogueData { speakerName = "Trưởng Làng", message = "Phòng thủ thành công." }
        }, () => { dialogueDone = true; });

        while (!dialogueDone) yield return null;

        StartStage5_AttackEnemyOutpost();
    }

    // ====================================================================
    // STAGE 5: TẤN CÔNG CĂN CỨ ĐỊCH -> CHIẾM ĐÓNG VÙNG ĐẤT MỚI
    // ====================================================================
    private void StartStage5_AttackEnemyOutpost()
    {
        currentStage = DemaciaTutorialStage.Stage5_AttackEnemyOutpost;
        PlayerPrefs.SetInt("SavedTutorialStage", 5);
        PlayerPrefs.Save();

        UpdateHint("Hãy chọn căn cứ địch để tiến hành tấn công và chiếm đóng vùng đất này.");

        Transform outpostTransform = (enemyZone != null && enemyZone.spawnedEnemyOutpostInstance != null) ? 
            enemyZone.spawnedEnemyOutpostInstance.transform : 
            (enemyZone != null ? enemyZone.transform : null);

        if (outpostTransform != null)
        {
            // Tự động sinh Nút Kiếm Tấn Công nổi trên đầu Căn Cứ Địch
            UIEnemyWaveButton attackBtnScript = UIEnemyWaveButton.CreateButton(outpostTransform, 2.5f);
            if (attackBtnScript != null)
            {
                Button btn = attackBtnScript.GetComponentInChildren<Button>();
                if (btn != null)
                {
                    btn.onClick.AddListener(() =>
                    {
                        PlayerPrefs.SetInt("SavedTutorialStage", 5);
                        PlayerPrefs.Save();
                    });
                }
                PointHandAtUI(attackBtnScript.transform as RectTransform);
            }
            else
            {
                PointHandAt(outpostTransform.position + Vector3.up * 2f);
            }
        }
    }

    public void OnOutpostAttackButtonClicked()
    {
        PlayerPrefs.SetInt("SavedTutorialStage", 5);
        PlayerPrefs.Save();

        BattleData.RecordCurrentSceneState(tutorialEnemyCount);
        Debug.Log("[CampaignTutorialManager] Bấm Tấn Công Căn Cứ Địch Stage 5! Chuyển sang SceneBattle...");
        SceneManager.LoadScene("SceneBattle");
    }

    private IEnumerator HandleStage5ReturnRoutine()
    {
        currentStage = DemaciaTutorialStage.Stage5_VictoryConquer;

        // Xóa căn cứ địch trên Zone B và thiết lập vùng đất đã được chiếm đóng thành công
        if (enemyZone != null)
        {
            enemyZone.hasEnemyOutpost = false;
            enemyZone.isUnlocked = true;
            if (enemyZone.spawnedEnemyOutpostInstance != null)
            {
                Destroy(enemyZone.spawnedEnemyOutpostInstance);
            }
        }

        if (outpostAttackButton != null)
        {
            outpostAttackButton.gameObject.SetActive(false);
        }

        bool dialogueDone = false;
        PlayDialogueSequence(stage5Dialogues, new DialogueData[]
        {
            new DialogueData { speakerName = "Trưởng Làng", message = "Đã tiêu diệt toàn bộ lực lượng địch và chiếm đóng thành công vùng đất mới." }
        }, () => { dialogueDone = true; });

        while (!dialogueDone) yield return null;

        StartStage6_BuildOnNewLand();
    }

    // ====================================================================
    // STAGE 6: HƯỚNG DẪN XÂY NHÀ CHÍNH MỚI TRÊN VÙNG ĐẤT VỪA CHIẾM ĐÓNG
    // ====================================================================
    private void StartStage6_BuildOnNewLand()
    {
        currentStage = DemaciaTutorialStage.Stage6_BuildOnNewLand;
        PlayerPrefs.SetInt("SavedTutorialStage", 6);
        PlayerPrefs.Save();

        // CHỈ hoàn thành nếu vùng đất MỚI (enemyZone) đã thực sự được xây dựng Nhà Chính (isTownHallEstablished == true)
        if (enemyZone != null && enemyZone.isTownHallEstablished)
        {
            CompleteTutorial();
            return;
        }

        UpdateHint("Hãy xây dựng Nhà Chính mới trên vùng đất vừa chiếm đóng.");
        HidePointer();

        if (enemyZone != null)
        {
            FocusCameraOn(enemyZone.transform.position, 1.5f);
        }
    }

    private void CompleteTutorial()
    {
        currentStage = DemaciaTutorialStage.Completed;
        PlayerPrefs.SetInt("TutorialCompleted", 1);
        PlayerPrefs.Save();

        HidePointer();

        PlayDialogueSequence(stageCompleteDialogues, new DialogueData[]
        {
            new DialogueData { speakerName = "Trưởng Làng", message = "Chúc mừng bạn đã hoàn thành hướng dẫn và chiếm đóng thành công lãnh thổ mới." }
        }, () =>
        {
            if (JsonDataManager.Ins != null)
            {
                JsonDataManager.Ins.AddWood(500);
                JsonDataManager.Ins.AddStone(500);
                JsonDataManager.Ins.BroadcastAllResources();
            }
            UpdateHint("Đã hoàn thành Hướng Dẫn Khẩn Hoang! Nhận 500 Gỗ và 500 Đá.");
        });
    }

    // ====================================================================
    // TÍNH NĂNG MỞ RỘNG VÀ HOOK SỰ KIỆN TỪ CÁC SCRIPT KHÁC
    // ====================================================================
    public void OnTownHallEstablished(SettlementZone zone)
    {
        if (currentStage == DemaciaTutorialStage.Stage6_BuildOnNewLand)
        {
            if (zone == null || enemyZone == null || zone == enemyZone)
            {
                CompleteTutorial();
            }
        }
    }
    public void OnCivilBuildingPlaced(BuildingType buildingType, Transform placedBuildingTransform = null)
    {
        OnBuildingPlaced(buildingType);
    }

    public void OnDefenseBuildingPlaced(BuildingType buildingType, Transform transform = null)
    {
        OnBuildingPlaced(buildingType);
    }

    public void OnBuildingConstructionFinished(BuildingType buildingType)
    {
        OnDayOrWaveIncremented();
    }

    public void EnsureAllBuiltBuildingsCompleted()
    {
        UpgradeableBuilding[] allBuildings = Object.FindObjectsByType<UpgradeableBuilding>(FindObjectsSortMode.None);
        foreach (var b in allBuildings)
        {
            if (b != null && !b.IsRuined)
            {
                b.IsInitialBuildNeeded = false;
                b.LoadBuildingData(b.CurrentLevel, b.IsRuined, false);
            }
        }
    }

    public void OnBuildingUpgradeStarted(UpgradeableBuilding building) { }
    public void OnBuildingRepaired(UpgradeableBuilding building) { }
    public void OnBuildingUpgraded(UpgradeableBuilding building) { }
    public void OnStartPlacement() { HidePointer(); }
    public void OnCancelPlacement() { }
    public void OnEnemyKilled() { }

    public void OnShopOpened() { }
    public void OnShopItemSelected(BuildingType type) { }

    private void PointAtConstructButton()
    {
        if (BuildingShopUI.Ins != null)
        {
            Button btn = null;
            var buttons = BuildingShopUI.Ins.GetComponentsInChildren<Button>();
            foreach (var b in buttons)
            {
                if (b != null && (b.gameObject.name.Contains("Construct") || b.gameObject.name.Contains("Build") || b.gameObject.name.Contains("Xây")))
                {
                    btn = b;
                    break;
                }
            }
            if (btn != null)
            {
                PointHandAtUI(btn.transform as RectTransform);
            }
        }
    }

    // ====================================================================
    // CÁC HÀM ĐIỀU KHIỂN CAMERA & NẮM BẮT VỊ TRÍ
    // ====================================================================
    public void FocusCameraOn(Vector3 targetWorldPos, float duration = 1.2f)
    {
        if (Camera.main == null) return;
        StartCoroutine(SmoothMoveCameraRoutine(targetWorldPos, duration));
    }

    private IEnumerator SmoothMoveCameraRoutine(Vector3 targetWorldPos, float duration)
    {
        Transform camTrans = Camera.main.transform;
        Vector3 startPos = camTrans.position;
        
        Vector3 forwardDir = camTrans.forward;
        float heightDiff = Mathf.Abs(startPos.y - targetWorldPos.y);
        if (heightDiff < 5f) heightDiff = 20f;
        float distance = heightDiff / Mathf.Max(0.1f, Mathf.Abs(forwardDir.y));

        Vector3 endPos = targetWorldPos - forwardDir * distance;
        endPos.y = startPos.y; // Giữ nguyên độ cao Y hiện tại của Camera

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            camTrans.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
        camTrans.position = endPos;
    }

    // ====================================================================
    // CÁC HÀM PHỤ TRỢ (HIGHLIGHT RING, HINT, POSITIONING)
    // ====================================================================
    private void UpdateHighlightRingAnimation()
    {
        if (highlightRing == null || !highlightRing.gameObject.activeSelf) return;

        if (isPointingAtWorld && Camera.main != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(currentTargetWorldPos);
            highlightRing.position = screenPos;
        }

        highlightRing.Rotate(Vector3.forward, -90f * Time.unscaledDeltaTime);
        float pulseScale = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.08f;
        highlightRing.localScale = Vector3.one * pulseScale;
    }

    public void PointHandAt(Vector3 worldPos)
    {
        if (handPointer != null) handPointer.SetActive(false); // 🔒 Ẩn bàn tay theo yêu cầu

        isPointingAtWorld = true;
        currentTargetWorldPos = worldPos;
        currentTargetUI = null;

        if (Camera.main != null && highlightRing != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            highlightRing.gameObject.SetActive(true);
            highlightRing.position = screenPos;
        }
    }

    public void PointHandAtUI(RectTransform uiRect)
    {
        if (handPointer != null) handPointer.SetActive(false); // 🔒 Ẩn bàn tay theo yêu cầu
        if (uiRect == null) return;

        isPointingAtWorld = false;
        currentTargetUI = uiRect;

        Canvas parentCanvas = uiRect.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = parentCanvas.worldCamera != null ? parentCanvas.worldCamera : Camera.main;
        }

        Vector3 worldCenter = uiRect.TransformPoint(uiRect.rect.center);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldCenter);

        if (highlightRing != null)
        {
            highlightRing.gameObject.SetActive(true);
            highlightRing.position = screenPoint;
        }
    }

    private void PointAtFirstEmptySlot()
    {
        if (SettlementSidePanelUI.Ins != null)
        {
            var slots = SettlementSidePanelUI.Ins.GetComponentsInChildren<SettlementSlotItemUI>();
            foreach (var slot in slots)
            {
                if (slot != null && slot.state == SettlementSlotState.Empty)
                {
                    PointHandAtUI(slot.transform as RectTransform);
                    return;
                }
            }
        }
    }

    private void PointAtSkipDayButton()
    {
        Button skipBtn = (DayNightManager.Ins != null) ? DayNightManager.Ins.skipWaveButton : null;
        if (skipBtn == null)
        {
            skipBtn = GameObject.Find("SkipWaveButton")?.GetComponent<Button>() ?? GameObject.Find("StartWaveButton")?.GetComponent<Button>();
        }

        if (skipBtn != null)
        {
            PointHandAtUI(skipBtn.transform as RectTransform);
        }
    }

    public void HidePointer()
    {
        isPointingAtWorld = false;
        currentTargetUI = null;
        if (handPointer != null) handPointer.SetActive(false);
        if (highlightRing != null) highlightRing.gameObject.SetActive(false);
    }

    public void UpdateHint(string hintMessage)
    {
        if (hintText != null)
        {
            hintText.text = hintMessage;
            hintText.gameObject.SetActive(!string.IsNullOrEmpty(hintMessage));
        }
    }
}