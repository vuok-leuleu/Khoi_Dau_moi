using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/*
 * CampaignTutorialManager.cs
 * Hệ thống Hướng Dẫn Tutorial 6 Giai Đoạn CHUẨN XÁC NGUYÊN BẢN THEO 0. PROLOGUE cho Demacia Rising
 * 
 * QUY TẮC BẮT BUỘC:
 * 1. Đồng bộ 100% với 6 nhiệm vụ trong 0. PROLOGUE của ChapterQuestTestController:
 *    - Quest 0: Bấm vào Zeffira để mở giao diện Thành Phố
 *    - Quest 1: Xây dựng Xưởng Gỗ tại Zeffira (Cần Qua Ngày)
 *    - Quest 2: Huấn luyện 1 Hộ Vệ tại Zeffira (Cần Qua Ngày)
 *    - Quest 3: Di chuyển quân đến lãnh thổ địch phía Đông Zeffira (VASKASIA)
 *    - Quest 4: Chuẩn bị và giành chiến thắng trong trận đánh đầu
 *    - Quest 5: Xây dựng Vaskasia trên vùng đất trống
 * 2. Vòng tròn Highlight Ring chỉ đúng mục tiêu / nút bấm.
 * 3. Khống chế và ngăn chặn mọi đợt quái tấn công thành khi đang trong Tutorial.
 * 4. Khóa camera và tương tác khi đang mở Dialogue để tránh bị click lệch trỏ.
 * 5. Tự động ẩn hoàn toàn Hint Text và Canvas khi hoàn thành xong Tutorial.
 */

public enum DemaciaTutorialStage
{
    None,
    Stage0_OpenSettlementView,    // 0. Bấm vào Zeffira để mở giao diện Thành Phố
    Stage1_BuildWood,             // 1. Xây Xưởng Gỗ tại Zeffira
    Stage1_SkipDayWood,           // 1. Bấm qua ngày hoàn thành Xưởng Gỗ
    Stage2_TrainGuard,            // 2. Chọn huấn luyện 1 Hộ Vệ (Guard) tại Zeffira
    Stage2_SkipDayTroop,          // 2. Bấm qua ngày hoàn tất huấn luyện Hộ Vệ
    Stage3_MarchToEnemyEast,      // 3. Di chuyển quân đến lãnh thổ địch phía Đông Zeffira (Vaskasia)
    Stage4_AttackEnemyBattle,     // 4. Chuẩn bị và giành chiến thắng trong trận đánh đầu
    Stage4_VictoryComplete,       // 4. Hoàn thành trận đánh đầu
    Stage5_EstablishVaskasia,     // 5. Xây dựng Vaskasia trên vùng đất trống
    Completed
}

public class CampaignTutorialManager : MonoBehaviour
{
    public static CampaignTutorialManager Ins { get; private set; }

    [Header("=== TRẠNG THÁI TUTORIAL DEMACIA ===")]
    public DemaciaTutorialStage currentStage = DemaciaTutorialStage.None;

    [Header("=== THÀNH PHẦN GIAO DIỆN HƯỚNG DẪN ===")]
    [SerializeField] private GameObject overlayDim;         
    [SerializeField] private GameObject handPointer;        // 🔒 Ẩn hoàn toàn
    [SerializeField] private RectTransform highlightRing;   // 🔒 Vòng tròn Highlight duy nhất
    [SerializeField] private Canvas tutorialCanvas;         
    [SerializeField] private TMP_Text hintText;             


    [Header("=== THÀNH PHẦN SCENE & VÙNG ĐẤT ===")]
    [SerializeField] private SettlementZone baseZone;        // Vùng đất khởi đầu (ZEFFIRA)
    [SerializeField] private SettlementZone enemyZone;       // Vùng đất cần chinh phục phía Đông (VASKASIA)
    [SerializeField] private EnemySpawn enemySpawner;       
    [SerializeField] private int tutorialEnemyCount = 3;    

    [Header("=== NÚT TẤN CÔNG CĂN CỨ ĐỊCH ===")]
    [SerializeField] private Button outpostAttackButton;

    [Header("=== CÁC MẢNG LỜI THOẠI TRƯỞNG LÀNG MARCUS (INSPECTOR) ===")]
    [SerializeField] private DialogueData[] stage0Dialogues;
    [SerializeField] private DialogueData[] stage1Dialogues;
    [SerializeField] private DialogueData[] stage2Dialogues;
    [SerializeField] private DialogueData[] stage3Dialogues;
    [SerializeField] private DialogueData[] stage4Dialogues;
    [SerializeField] private DialogueData[] stage5Dialogues;
    [SerializeField] private DialogueData[] stageCompleteDialogues;

    private RectTransform currentTargetUI;
    private Vector3 currentTargetWorldPos;
    private bool isPointingAtWorld = false;
    private float nextUITargetRefreshTime;
    private const string TutorialStagePrefKey = "PrologueTutorialStage";
    private const int PrologueQuestCount = 6;

    public bool IsTutorialCompleted()
    {
        return currentStage == DemaciaTutorialStage.Completed || PlayerPrefs.GetInt("TutorialCompleted", 0) == 1;
    }

    private void OnDestroy()
    {
        if (Ins == this) Ins = null;
    }

    private void Awake()
    {
        if (Ins == null) Ins = this;
        else Destroy(gameObject);

        if (handPointer != null) handPointer.SetActive(false);
        if (highlightRing != null) highlightRing.gameObject.SetActive(false);

        if (tutorialCanvas == null && highlightRing != null)
            tutorialCanvas = highlightRing.GetComponentInParent<Canvas>();
    }

    private void Start()
    {
        // 1. Kiểm tra nếu đã hoàn thành Tutorial từ trước
        if (PlayerPrefs.GetInt("TutorialCompleted", 0) == 1)
        {
            SetStage(DemaciaTutorialStage.Completed);
            HidePointer();
            UpdateHint("");
            if (tutorialCanvas != null) tutorialCanvas.gameObject.SetActive(false);
            return;
        }

        // 2. Tìm vùng đất tự động: Bắt buộc tìm chính xác ZEFFIRA và VASKASIA
        AutoDetectZones();

        // 3. Xử lý khi trở về từ SceneBattle
        if (BattleData.HasResult || BattleData.LastBattleWasVictory)
        {
            int lastStage = PlayerPrefs.GetInt("SavedTutorialStage", 4);
            BattleData.HasResult = false;
            BattleData.LastBattleWasVictory = false;

            EnsureAllBuiltBuildingsCompleted();

            if (lastStage >= 4)
            {
                StartCoroutine(HandleStage4ReturnRoutine());
                return;
            }
        }

        Time.timeScale = 1f;
        HidePointer();

        RestoreTutorialCheckpoint();
    }

    private void RestoreTutorialCheckpoint()
    {
        DemaciaTutorialStage savedStage = (DemaciaTutorialStage)PlayerPrefs.GetInt(
            TutorialStagePrefKey,
            (int)DemaciaTutorialStage.Stage0_OpenSettlementView);

        switch (savedStage)
        {
            case DemaciaTutorialStage.Stage1_BuildWood:
            case DemaciaTutorialStage.Stage1_SkipDayWood:
                StartStage1_BuildWood();
                break;
            case DemaciaTutorialStage.Stage2_TrainGuard:
            case DemaciaTutorialStage.Stage2_SkipDayTroop:
                StartStage2_TrainGuard();
                break;
            case DemaciaTutorialStage.Stage3_MarchToEnemyEast:
                StartStage3_MarchToEnemyEast();
                break;
            case DemaciaTutorialStage.Stage4_AttackEnemyBattle:
                StartStage4_AttackEnemyBattle();
                break;
            case DemaciaTutorialStage.Stage5_EstablishVaskasia:
                StartStage5_EstablishVaskasia();
                break;
            default:
                StartStage0_OpenSettlementView();
                break;
        }
    }

    private void SetStage(DemaciaTutorialStage stage)
    {
        currentStage = stage;
        PlayerPrefs.SetInt(TutorialStagePrefKey, (int)stage);
        PlayerPrefs.SetInt("SavedTutorialStage", (int)stage);
        PlayerPrefs.Save();
    }

    private void ShowStepHint(int stepNumber, string message)
    {
        UpdateHint($"<b>PROLOGUE {stepNumber}/{PrologueQuestCount}</b>\n{message}");
    }

    /// <summary>
    /// Tự động tìm chính xác vùng đất Zeffira và Vaskasia
    /// </summary>
    private void AutoDetectZones()
    {
        SettlementZone[] zones = Object.FindObjectsByType<SettlementZone>(FindObjectsSortMode.None);
        
        if (baseZone == null)
        {
            foreach (var z in zones)
            {
                if (z != null && (z.settlementName.ToUpper().Contains("ZEFFIRA") || z.zoneTier == 0))
                {
                    baseZone = z;
                    break;
                }
            }
        }

        if (enemyZone == null)
        {
            // Ưu tiên tìm đúng vùng đất mang tên VASKASIA
            foreach (var z in zones)
            {
                if (z != null && z.settlementName.ToUpper().Contains("VASKASIA"))
                {
                    enemyZone = z;
                    break;
                }
            }

            // Fallback: Tìm vùng đất bậc 1 hoặc có tiền đồn quái
            if (enemyZone == null)
            {
                foreach (var z in zones)
                {
                    if (z != null && z != baseZone && (z.zoneTier == 1 || z.hasEnemyOutpost))
                    {
                        enemyZone = z;
                        break;
                    }
                }
            }
        }

        Debug.Log($"[CampaignTutorialManager] 🗺️ Base Zone: {(baseZone != null ? baseZone.settlementName : "NULL")}, Enemy Zone: {(enemyZone != null ? enemyZone.settlementName : "NULL")}");
    }

    private void Update()
    {
        UpdateHighlightRingAnimation();
    }

    private void SetCameraControlEnabled(bool isEnabled)
    {
        RTSCameraController cam = Object.FindFirstObjectByType<RTSCameraController>();
        if (cam != null)
        {
            cam.enabled = isEnabled;
        }
    }

    private void PlayDialogueSequence(DialogueData[] customArray, DialogueData[] defaultArray, System.Action onComplete = null)
    {
        DialogueData[] toPlay = (customArray != null && customArray.Length > 0) ? customArray : defaultArray;
        if (NPCDialogueUI.Ins != null && toPlay != null && toPlay.Length > 0)
        {
            // 🔒 Khóa camera và bật màn chặn click tránh người chơi ấn lệch trỏ trong lúc thoại
            SetCameraControlEnabled(false);
            if (overlayDim != null)
            {
                var img = overlayDim.GetComponent<UnityEngine.UI.Image>();
                if (img != null) img.raycastTarget = true;
                overlayDim.SetActive(true);
            }

            NPCDialogueUI.Ins.ShowDialogueSequence(toPlay, () =>
            {
                if (overlayDim != null) overlayDim.SetActive(false);
                SetCameraControlEnabled(true);
                onComplete?.Invoke();
            });
        }
        else
        {
            if (overlayDim != null) overlayDim.SetActive(false);
            SetCameraControlEnabled(true);
            onComplete?.Invoke();
        }
    }

    public bool CompleteQuestObjective(int questIndex)
    {
        if (ChapterQuestController.Instance == null)
        {
            Debug.LogWarning("[CampaignTutorialManager] Không tìm thấy ChapterQuestController để đồng bộ Prologue.");
            return false;
        }

        return ChapterQuestController.Instance.CompletePrologueObjective(questIndex);
    }

    // ====================================================================
    // 0. PROLOGUE - STEP 0: BẤM VÀO ZEFFIRA ĐỂ MỞ SETTLEMENT VIEW
    // ====================================================================
    public void StartStage0_OpenSettlementView()
    {
        SetStage(DemaciaTutorialStage.Stage0_OpenSettlementView);

        PlayDialogueSequence(stage0Dialogues, new DialogueData[]
        {
            new DialogueData { speakerName = "Trưởng Làng Marcus", message = "Thưa lãnh chúa, khu định cư Zeffira của chúng ta cần được tái thiết sau những biến động. Hãy kiểm tra tình hình bên trong lãnh địa." }
        }, () =>
        {
            ShowStepHint(1, "Hãy bấm vào Zeffira để mở giao diện Thành Phố.");
            if (baseZone != null)
            {
                FocusCameraOn(baseZone.transform.position, 1.2f);
                Transform targetPoint = baseZone.townHallPoint != null ? baseZone.townHallPoint : baseZone.transform;
                PointHandAt(targetPoint.position + Vector3.up * 1.5f);
            }
        });
    }

    /// <summary>
    /// Gọi khi người chơi nhấp chọn Zeffira hoặc SettlementSidePanelUI mở lên
    /// </summary>
    public void OnSettlementOpened(SettlementZone zone)
    {
        if (currentStage == DemaciaTutorialStage.Stage0_OpenSettlementView && zone == baseZone)
        {
            CompleteQuestObjective(0); // Tick xong Quest 0
            StartStage1_BuildWood();
        }
    }

    // ====================================================================
    // 0. PROLOGUE - STEP 1: XÂY DỰNG XƯỞNG GỖ TẠI ZEFFIRA
    // ====================================================================
    public void StartStage1_BuildWood()
    {
        SetStage(DemaciaTutorialStage.Stage1_BuildWood);

        PlayDialogueSequence(stage1Dialogues, new DialogueData[]
        {
            new DialogueData { speakerName = "Trưởng Làng Marcus", message = "Gỗ là tài nguyên tiên quyết để xây dựng lãnh địa. Hãy chọn một ô đất trống để lập Xưởng Gỗ." }
        }, () =>
        {
            ShowStepHint(2, "Hãy chọn ô đất trống và xây dựng Xưởng Gỗ.");
            PointAtFirstEmptySlot();
        });
    }

    public void OnBuildingPlaced(BuildingType type)
    {
        if (currentStage == DemaciaTutorialStage.Stage1_BuildWood)
        {
            SetStage(DemaciaTutorialStage.Stage1_SkipDayWood);
            ShowStepHint(2, "Xưởng Gỗ cần hoàn tất trong lượt kế tiếp. Hãy bấm Qua Ngày.");
            PointAtSkipDayButton();
        }
        else if (currentStage == DemaciaTutorialStage.Stage5_EstablishVaskasia && type == BuildingType.House)
        {
            ShowStepHint(6, "Nhà Chính Vaskasia đang được khởi công. Hãy chờ xác nhận vùng đất mới.");
        }
    }

    public void OnDayOrWaveIncremented()
    {
        if (currentStage == DemaciaTutorialStage.Stage1_SkipDayWood)
        {
            CompleteQuestObjective(1); // Tick xong Quest 1: Xây xong Xưởng Gỗ
            StartStage2_TrainGuard();
        }
        else if (currentStage == DemaciaTutorialStage.Stage2_SkipDayTroop)
        {
            CompleteQuestObjective(2); // Tick xong Quest 2: Huấn luyện xong Hộ Vệ
            StartStage3_MarchToEnemyEast();
        }
    }

    // ====================================================================
    // 0. PROLOGUE - STEP 2: HUẤN LUYỆN 1 HỘ VỆ (GUARD) TẠI ZEFFIRA
    // ====================================================================
    public void StartStage2_TrainGuard()
    {
        SetStage(DemaciaTutorialStage.Stage2_TrainGuard);

        PlayDialogueSequence(stage2Dialogues, new DialogueData[]
        {
            new DialogueData { speakerName = "Trưởng Làng Marcus", message = "Khu vực biên cương lân cận đang bị kẻ địch dòm ngó. Hãy chiêu mộ một Hộ Vệ (Guard) tại Doanh Trại để bảo vệ người dân." }
        }, () =>
        {
            ShowStepHint(3, "Hãy chọn ô Huấn Luyện Lính tại Zeffira để chiêu mộ Hộ Vệ.");
            PointAtFirstTrainingSlot();
        });
    }

    private void PointAtFirstTrainingSlot()
    {
        if (SettlementSidePanelUI.Ins != null)
        {
            var trainingSlots = SettlementSidePanelUI.Ins.GetComponentsInChildren<TroopTrainingSlotUI>();
            foreach (var slot in trainingSlots)
            {
                if (slot != null && slot.gameObject.activeInHierarchy)
                {
                    PointHandAtUI(slot.transform as RectTransform);
                    return;
                }
            }
        }
    }

    public void OnTroopTrainingStarted(BuildingType troopType)
    {
        if (currentStage == DemaciaTutorialStage.Stage2_TrainGuard)
        {
            // Chuyển sang chờ người chơi bấm Qua Ngày để lính tập luyện xong
            SetStage(DemaciaTutorialStage.Stage2_SkipDayTroop);
            ShowStepHint(3, "Hộ Vệ cần hoàn tất huấn luyện trong lượt kế tiếp. Hãy bấm Qua Ngày.");
            PointAtSkipDayButton();
        }
    }

    // ====================================================================
    // 0. PROLOGUE - STEP 3: DI CHUYỂN QUÂN ĐẾN LÃNH THỔ ĐỊCH PHÍA ĐÔNG ZEFFIRA (VASKASIA)
    // ====================================================================
    public void StartStage3_MarchToEnemyEast()
    {
        SetStage(DemaciaTutorialStage.Stage3_MarchToEnemyEast);

        PlayDialogueSequence(stage3Dialogues, new DialogueData[]
        {
            new DialogueData { speakerName = "Trưởng Làng Marcus", message = "Trinh sát báo về: Phía Đông Zeffira có một căn cứ địch đang chiếm đóng vùng đất màu mỡ Vaskasia. Hãy xuất quân tiến về phía Đông!" }
        }, () =>
        {
            ShowStepHint(4, "Hãy quan sát và điều chuyển lực lượng đến vùng đất Vaskasia ở phía Đông.");
            
            if (enemyZone != null)
            {
                FocusCameraOn(enemyZone.transform.position, 1.5f);
                Transform targetPoint = (enemyZone.spawnedEnemyOutpostInstance != null) ? 
                    enemyZone.spawnedEnemyOutpostInstance.transform : enemyZone.transform;
                PointHandAt(targetPoint.position + Vector3.up * 2f);
            }

            // Sau khi quan sát hoàn tất -> Hoàn thành Quest 3 và chuyển sang chuẩn bị tấn công
            Invoke(nameof(FinishMarchStep), 3.5f);
        });
    }

    private void FinishMarchStep()
    {
        if (currentStage == DemaciaTutorialStage.Stage3_MarchToEnemyEast)
        {
            CompleteQuestObjective(3); // Tick xong Quest 3: Di chuyển quân đến lãnh thổ địch phía Đông
            StartStage4_AttackEnemyBattle();
        }
    }

    // ====================================================================
    // 0. PROLOGUE - STEP 4: CHUẨN BỊ VÀ GIÀNH CHIẾN THẮNG TRONG TRẬN ĐÁNH ĐẦU
    // ====================================================================
    public void StartStage4_AttackEnemyBattle()
    {
        SetStage(DemaciaTutorialStage.Stage4_AttackEnemyBattle);

        PlayDialogueSequence(stage4Dialogues, new DialogueData[]
        {
            new DialogueData { speakerName = "Trưởng Làng Marcus", message = "Quân địch đã dàn trận sẵn sàng! Hãy chỉ huy lực lượng tiến công và giành lấy chiến thắng đầu tiên!" }
        }, () =>
        {
            ShowStepHint(5, "Hãy chọn căn cứ địch và bấm nút Tấn Công để tham chiến.");

            Transform outpostTransform = (enemyZone != null && enemyZone.spawnedEnemyOutpostInstance != null) ? 
                enemyZone.spawnedEnemyOutpostInstance.transform : 
                (enemyZone != null ? enemyZone.transform : null);

            if (outpostTransform != null)
            {
                UIEnemyWaveButton attackBtnScript = UIEnemyWaveButton.CreateButton(outpostTransform, 2.5f);
                if (attackBtnScript != null)
                {
                    Button btn = attackBtnScript.GetComponentInChildren<Button>();
                    if (btn != null)
                    {
                        btn.onClick.AddListener(() =>
                        {
                            PlayerPrefs.SetInt("SavedTutorialStage", 4);
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
        });
    }

    public void OnOutpostAttackButtonClicked()
    {
        PlayerPrefs.SetInt("SavedTutorialStage", 4);
        PlayerPrefs.Save();

        BattleData.RecordCurrentSceneState(tutorialEnemyCount);
        Debug.Log("[CampaignTutorialManager] ⚔️ Bắt đầu trận đánh đầu tiên! Chuyển sang SceneBattle...");
        SceneManager.LoadScene("SceneBattle");
    }

    private IEnumerator HandleStage4ReturnRoutine()
    {
        SetStage(DemaciaTutorialStage.Stage4_VictoryComplete);

        // Xóa căn cứ địch trên Vaskasia sau khi thắng trận
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

        CompleteQuestObjective(4); // Tick xong Quest 4: Giành chiến thắng trong trận đánh đầu

        bool dialogueDone = false;
        PlayDialogueSequence(stage4Dialogues, new DialogueData[]
        {
            new DialogueData { speakerName = "Trưởng Làng Marcus", message = "Chiến thắng vang dội! Lực lượng địch đã bị quét sạch, vùng đất Vaskasia đã được giải phóng hoàn toàn." }
        }, () => { dialogueDone = true; });

        while (!dialogueDone) yield return null;

        StartStage5_EstablishVaskasia();
    }

    // ====================================================================
    // 0. PROLOGUE - STEP 5: XÂY DỰNG VASKASIA TRÊN VÙNG ĐẤT TRỐNG
    // ====================================================================
    public void StartStage5_EstablishVaskasia()
    {
        SetStage(DemaciaTutorialStage.Stage5_EstablishVaskasia);

        if (enemyZone != null && enemyZone.isTownHallEstablished)
        {
            CompleteTutorial();
            return;
        }

        PlayDialogueSequence(stage5Dialogues, new DialogueData[]
        {
            new DialogueData { speakerName = "Trưởng Làng Marcus", message = "Vùng đất này giờ đã an toàn. Hãy lập tức khởi công xây dựng Nhà Chính để biến nơi đây thành khu định cư mới mang tên Vaskasia!" }
        }, () =>
        {
            ShowStepHint(6, "Hãy chọn vùng đất mới và xây dựng Nhà Chính Vaskasia.");
            HidePointer();

            if (enemyZone != null)
            {
                FocusCameraOn(enemyZone.transform.position, 1.5f);
                Transform targetPoint = enemyZone.townHallPoint != null ? enemyZone.townHallPoint : enemyZone.transform;
                PointHandAt(targetPoint.position + Vector3.up * 1.5f);
            }
        });
    }

    public void OnTownHallEstablished(SettlementZone zone)
    {
        if (currentStage == DemaciaTutorialStage.Stage5_EstablishVaskasia && zone == enemyZone)
        {
            CompleteTutorial();
        }
    }

    private void CompleteTutorial()
    {
        CompleteQuestObjective(5); // Tick xong Quest 5: Xây dựng Vaskasia -> Hoàn thành toàn bộ Prologue!

        SetStage(DemaciaTutorialStage.Completed);
        PlayerPrefs.SetInt("TutorialCompleted", 1);
        PlayerPrefs.Save();

        HidePointer();

        PlayDialogueSequence(stageCompleteDialogues, new DialogueData[]
        {
            new DialogueData { speakerName = "Trưởng Làng Marcus", message = "Chúc mừng lãnh chúa! Người đã hoàn thành xuất sắc giai đoạn Mở Đầu (Prologue), mở rộng bờ cõi Demacia và nhận thưởng chiếc rương Demacian Orb quý giá!" }
        }, () =>
        {
            if (JsonDataManager.Ins != null)
            {
                JsonDataManager.Ins.AddGold(100);
                JsonDataManager.Ins.AddWood(100);
                JsonDataManager.Ins.AddStone(50);
                JsonDataManager.Ins.BroadcastAllResources();
            }
            
            // 🔥 ẨN HOÀN TOÀN HINT TEXT VÀ CANVAS KHI KẾT THÚC HƯỚNG DẪN
            UpdateHint("");
            if (hintText != null) hintText.gameObject.SetActive(false);
            if (tutorialCanvas != null) tutorialCanvas.gameObject.SetActive(false);

            // Mở bảng Chapter Quest chúc mừng và hiển thị Chương I
            if (ChapterQuestController.Instance != null)
            {
                ChapterQuestController.Instance.OpenWindow();
            }
        });
    }

    // ====================================================================
    // CÁC HÀM HOOK PHỤ TRỢ TỪ CÁC SCRIPT KHÁC
    // ====================================================================
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

    // ====================================================================
    // CÁC HÀM ĐIỀU KHIỂN CAMERA & HIGHLIGHT
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
        endPos.y = startPos.y;

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

    private void UpdateHighlightRingAnimation()
    {
        if (highlightRing == null) return;

        if (!isPointingAtWorld)
        {
            if (currentTargetUI == null || !currentTargetUI.gameObject.activeInHierarchy)
            {
                HideHighlightVisual();
                TryRefreshUITargetForCurrentStage();
                return;
            }

            Vector2 screenPoint = GetScreenPointForUI(currentTargetUI);
            ShowHighlightVisual(screenPoint);
        }
        else if (Camera.main != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(currentTargetWorldPos);
            if (screenPos.z <= 0f)
            {
                HideHighlightVisual();
                return;
            }

            ShowHighlightVisual(screenPos);
        }

        highlightRing.Rotate(Vector3.forward, -90f * Time.unscaledDeltaTime);
        float pulseScale = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.08f;
        highlightRing.localScale = Vector3.one * pulseScale;
    }

    private void TryRefreshUITargetForCurrentStage()
    {
        if (Time.unscaledTime < nextUITargetRefreshTime) return;
        nextUITargetRefreshTime = Time.unscaledTime + 0.25f;

        switch (currentStage)
        {
            case DemaciaTutorialStage.Stage1_BuildWood:
                PointAtFirstEmptySlot();
                break;
            case DemaciaTutorialStage.Stage2_TrainGuard:
                PointAtFirstTrainingSlot();
                break;
            case DemaciaTutorialStage.Stage1_SkipDayWood:
            case DemaciaTutorialStage.Stage2_SkipDayTroop:
                PointAtSkipDayButton();
                break;
        }
    }

    private void ShowHighlightVisual(Vector2 screenPoint)
    {
        if (highlightRing == null) return;
        highlightRing.gameObject.SetActive(true);
        highlightRing.position = screenPoint;
    }

    private void HideHighlightVisual()
    {
        if (highlightRing != null) highlightRing.gameObject.SetActive(false);
    }

    public void PointHandAt(Vector3 worldPos)
    {
        if (handPointer != null) handPointer.SetActive(false);

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
        if (handPointer != null) handPointer.SetActive(false);
        if (uiRect == null) return;

        isPointingAtWorld = false;
        currentTargetUI = uiRect;

        Vector2 screenPoint = GetScreenPointForUI(uiRect);

        if (highlightRing != null)
        {
            highlightRing.gameObject.SetActive(true);
            highlightRing.position = screenPoint;
        }
    }

    private Vector2 GetScreenPointForUI(RectTransform uiRect)
    {
        Canvas parentCanvas = uiRect.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = parentCanvas.worldCamera != null ? parentCanvas.worldCamera : Camera.main;
        }

        Vector3 worldCenter = uiRect.TransformPoint(uiRect.rect.center);
        return RectTransformUtility.WorldToScreenPoint(cam, worldCenter);
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
