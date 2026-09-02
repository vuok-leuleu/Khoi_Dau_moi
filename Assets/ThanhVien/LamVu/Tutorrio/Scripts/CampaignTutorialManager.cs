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
    Stage5_SkipDayTownHall,       // 5. Chờ Nhà Chính Vaskasia xây dựng xong
    Completed
}

/// <summary>
/// Chuỗi ba trận dùng riêng cho phần thuyết trình. Không thay thế 6 bước
/// Prologue; chuỗi này bắt đầu sau khi Prologue hoàn tất.
/// </summary>
public enum PresentationBattlePhase
{
    None,
    FirstDefenseActive,
    DragonCountdown,
    DragonDefenseActive,
    Completed,
    Failed
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

    [Header("=== KỊCH BẢN 3 TRẬN THUYẾT TRÌNH ===")]
    [Tooltip("Sau Prologue: phòng thủ nhỏ tại Vaskasia, cảnh báo 5 Wave phải chiếm EVENMOOR, rồi phòng thủ lớn có Rồng đúng một lần.")]
    [SerializeField] private bool enablePresentationBattleSequence = true;
    [SerializeField] private SettlementZone evenmoorZone;
    [SerializeField, Min(1)] private int wavesBeforeDragonDefense = 5;
    [SerializeField, Min(0)] private int warningWavesBeforeFirstDefense = 0;
    [SerializeField] private PresentationBattlePhase presentationBattlePhase = PresentationBattlePhase.None;
    [Tooltip("Thoại ngay sau khi thắng phòng thủ nhỏ: báo trước số Wave còn lại trước trận Rồng. Nếu để trống, game dùng câu thoại mặc định.")]
    [SerializeField] private DialogueData[] dragonCountdownDialogues;
    [Tooltip("Thoại cảnh báo ngay trước khi đợt Rồng được spawn từ Dragon Raid Spawn Point. Nếu để trống, game dùng câu thoại mặc định.")]
    [SerializeField] private DialogueData[] dragonDefenseWarningDialogues;
    [Tooltip("Thoại phát ngay sau khi người chơi phòng thủ thành công trước đợt Rồng. Nếu để trống, game dùng đoạn kết mặc định.")]
    [SerializeField] private DialogueData[] dragonDefenseVictoryDialogues;

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
    private const string PresentationBattlePhasePrefKey = "PresentationBattlePhase";
    private const string DragonDefenseWavePrefKey = "PresentationDragonDefenseWave";
    private const string ShieldTroopUnlockedPrefKey = "PresentationShieldTroopUnlocked";
    private const string StoneBuildingsUnlockedPrefKey = "PresentationEvenmoorStoneBuildingsUnlocked";
    private int dragonDefenseWave = -1;
    private DayNightManager subscribedDayNightManager;

    public bool IsTutorialCompleted()
    {
        return currentStage == DemaciaTutorialStage.Completed || PlayerPrefs.GetInt("TutorialCompleted", 0) == 1;
    }

    /// <summary>
    /// Khiên Binh vẫn dùng BarracksSpear có sẵn, nhưng sau Prologue chỉ được
    /// huấn luyện mới sau khi giải phóng EVENMOOR. Trong Prologue luôn cho phép
    /// để không làm hỏng bước huấn luyện Hộ Vệ ban đầu.
    /// </summary>
    public static bool IsShieldTroopTrainingUnlocked()
    {
        if (Ins == null || !Ins.enablePresentationBattleSequence || !Ins.IsTutorialCompleted())
        {
            return true;
        }

        if (PlayerPrefs.GetInt(ShieldTroopUnlockedPrefKey, 0) == 1)
        {
            return true;
        }

        return Ins.evenmoorZone != null && Ins.evenmoorZone.IsConquered;
    }

    /// <summary>
    /// Mỏ/Kho Đá được mở vĩnh viễn ngay khi EVENMOOR đã chinh phục. Cờ riêng
    /// này giữ nguyên qua lần chuyển SceneBattle rồi quay về bản đồ chính.
    /// </summary>
    public static bool AreStoneBuildingsUnlockedByEvenmoor()
    {
        if (PlayerPrefs.GetInt(StoneBuildingsUnlockedPrefKey, 0) == 1)
        {
            return true;
        }

        // Tương thích tiến trình đã chinh phục EVENMOOR trước khi cờ này được
        // thêm: tự ghi cờ ngay lần đầu shop kiểm tra công trình.
        if (Ins != null)
        {
            Ins.AutoDetectZones();
            if (Ins.evenmoorZone != null && Ins.evenmoorZone.IsConquered)
            {
                PlayerPrefs.SetInt(StoneBuildingsUnlockedPrefKey, 1);
                PlayerPrefs.Save();
                return true;
            }
        }

        return false;
    }

    private void OnDestroy()
    {
        if (subscribedDayNightManager != null)
        {
            subscribedDayNightManager.OnWaveStart -= OnWaveStarted;
        }
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
        // Tìm đủ cả 3 vùng trước khi kiểm tra trạng thái tutorial/save.
        AutoDetectZones();

        // 1. Kiểm tra nếu đã hoàn thành Tutorial từ trước
        if (PlayerPrefs.GetInt("TutorialCompleted", 0) == 1)
        {
            SetStage(DemaciaTutorialStage.Completed);
            HidePointer();
            UpdateHint("");
            EnsureTutorialCanvasCanShowDialogue();
            StartPresentationBattleSequenceIfNeeded();
            return;
        }

        // 2. Xử lý khi trở về từ SceneBattle
        // Chỉ tutorial mới được xử lý ở đây. Trước đây mọi trận thắng (ví dụ
        // Brookhollow) đều bị nhánh này bắt vào, sau đó xóa BattleData.HasResult
        // trước khi BattleReturnRestoreRunner kịp áp dụng kết quả chinh phục.
        if (IsTutorialBattleReturn())
        {
            int lastStage = PlayerPrefs.GetInt("SavedTutorialStage", 4);

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

    private bool IsTutorialBattleReturn()
    {
        if (!(BattleData.HasResult || BattleData.LastBattleWasVictory)) return false;
        if (enemyZone == null) return false;
        if (BattleData.HasResult && !BattleData.IsPlayerVictory) return false;

        // TargetedSettlementZoneName vẫn còn giữ tên mục tiêu ở thời điểm
        // Start() chạy. BattleReturnRestoreRunner sẽ xóa tên này sau khi đã
        // gọi ApplyBattleResultToScene(). Vì vậy chỉ nhận đúng trận đánh vào
        // vùng tutorial, không nuốt kết quả của các vùng khác.
        return !string.IsNullOrEmpty(BattleData.TargetedSettlementZoneName) &&
               BattleData.TargetedSettlementZoneName == enemyZone.settlementName;
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
            case DemaciaTutorialStage.Stage5_SkipDayTownHall:
                ResumeStage5TownHallConstruction();
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
        SettlementZone[] zones = Object.FindObjectsByType<SettlementZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
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

        if (evenmoorZone == null)
        {
            foreach (var z in zones)
            {
                if (z != null && z.settlementName.ToUpper().Contains("EVENMOOR"))
                {
                    evenmoorZone = z;
                    break;
                }
            }

            if (evenmoorZone == null)
            {
                foreach (var z in zones)
                {
                    if (z != null && z.GetEffectiveTier() == 2)
                    {
                        evenmoorZone = z;
                        break;
                    }
                }
            }
        }

        Debug.Log($"[CampaignTutorialManager] 🗺️ Base: {(baseZone != null ? baseZone.settlementName : "NULL")}, Vaskasia: {(enemyZone != null ? enemyZone.settlementName : "NULL")}, Evenmoor: {(evenmoorZone != null ? evenmoorZone.settlementName : "NULL")}");
    }

    private void Update()
    {
        EnsureWaveSubscription();
        UpdateHighlightRingAnimation();
    }

    /// <summary>
    /// Countdown trận Rồng phải chạy theo mọi Wave thực tế. Trước đây nó chỉ
    /// được gọi khi xây xong công trình, vì vậy bấm Qua Ngày/Wave không làm
    /// tiến trình hành quân của Rồng thay đổi.
    /// </summary>
    private void EnsureWaveSubscription()
    {
        DayNightManager activeManager = DayNightManager.HasInstance ? DayNightManager.Ins : null;
        if (subscribedDayNightManager == activeManager) return;

        if (subscribedDayNightManager != null)
        {
            subscribedDayNightManager.OnWaveStart -= OnWaveStarted;
            subscribedDayNightManager = null;
        }

        if (activeManager == null) return;

        activeManager.OnWaveStart -= OnWaveStarted;
        activeManager.OnWaveStart += OnWaveStarted;
        subscribedDayNightManager = activeManager;
    }

    private void OnWaveStarted(int waveIndex)
    {
        UpdateDragonDefenseCountdown();
    }

    private void SetCameraControlEnabled(bool isEnabled)
    {
        RTSCameraController cam = Object.FindFirstObjectByType<RTSCameraController>();
        if (cam != null)
        {
            cam.enabled = isEnabled;
        }
    }

    /// <summary>
    /// TutorialCanvas cũng chứa Dialogue Panel. Không được tắt cả Canvas sau
    /// Prologue, nếu không thoại không hiện và callback không trả camera về
    /// trạng thái bật. Hint, tay chỉ và vòng highlight được ẩn riêng.
    /// </summary>
    private void EnsureTutorialCanvasCanShowDialogue()
    {
        if (tutorialCanvas != null && !tutorialCanvas.gameObject.activeSelf)
        {
            tutorialCanvas.gameObject.SetActive(true);
        }
    }

    private void PlayDialogueSequence(DialogueData[] customArray, DialogueData[] defaultArray, System.Action onComplete = null)
    {
        EnsureTutorialCanvasCanShowDialogue();
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

    /// <summary>
    /// Stage 4 có hai câu thoại với hai thời điểm khác nhau:
    /// phần tử 0 trước khi vào trận, phần tử 1 sau khi chiến thắng trở về.
    /// Chỉ truyền một phần tử cho UI để không phát cả hai câu trong cùng một lần.
    /// </summary>
    private void PlayStage4Dialogue(int dialogueIndex, DialogueData fallbackDialogue, System.Action onComplete = null)
    {
        DialogueData[] selectedDialogue = null;

        if (stage4Dialogues != null &&
            dialogueIndex >= 0 &&
            dialogueIndex < stage4Dialogues.Length &&
            stage4Dialogues[dialogueIndex] != null)
        {
            selectedDialogue = new[] { stage4Dialogues[dialogueIndex] };
        }

        PlayDialogueSequence(selectedDialogue, new[] { fallbackDialogue }, onComplete);
    }

    public bool CompleteQuestObjective(int questIndex)
    {
        if (ChapterQuestController.Instance == null)
        {
            Debug.LogWarning("[CampaignTutorialManager] Không tìm thấy ChapterQuestController để đồng bộ Prologue.");
            return false;
        }

        if (ChapterQuestController.Instance.CompletePrologueObjective(questIndex))
        {
            return true;
        }

        // Khi controller được tạo lại sau SceneBattle, trạng thái quest cũ có thể chưa kịp tải.
        // Tutorial là nguồn trạng thái chuẩn ở luồng này, nên khôi phục các tick đến đúng bước hiện tại.
        Debug.LogWarning($"[CampaignTutorialManager] Đồng bộ lại Prologue đến mục tiêu {questIndex + 1}.");
        return ChapterQuestController.Instance.SynchronizePrologueObjectivesThrough(questIndex);
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
            StartStage5TownHallConstruction();
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

        UpdateDragonDefenseCountdown();
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

        // Chỉ phát Message 1 (phần tử 0) trước khi chuyển sang SceneBattle.
        PlayStage4Dialogue(0,
            new DialogueData { speakerName = "Trưởng Làng Marcus", message = "Quân địch đã dàn trận sẵn sàng! Hãy chỉ huy lực lượng tiến công và giành lấy chiến thắng đầu tiên!" }, () =>
        {
            ShowStepHint(5, "Hãy chọn căn cứ địch và bấm nút Tấn Công để tham chiến.");

            Transform outpostTransform = (enemyZone != null && enemyZone.spawnedEnemyOutpostInstance != null) ? 
                enemyZone.spawnedEnemyOutpostInstance.transform : 
                (enemyZone != null ? enemyZone.transform : null);

            if (outpostTransform != null)
            {
                // Bước 3 của tutorial chỉ là một đoạn chỉ dẫn có camera tự di
                // chuyển, không có thao tác chọn/điều quân thật. Chuẩn bị đội
                // Hộ Vệ như đã tới đích trước khi mở trận; nếu chỉ tạo nút ở
                // trạng thái sẵn sàng thì BattleData vẫn không có lính để mang
                // sang SceneBattle.
                PrepareTutorialExpedition();
                BattleData.AllowTutorialShieldTroopsInCurrentBattle = true;

                // Nút đánh phải được tạo ở trạng thái sẵn sàng; nếu để mặc định
                // isArrived = false thì UIEnemyWaveButton sẽ luôn từ chối click
                // và người chơi bị kẹt vĩnh viễn trước trận đầu tiên.
                UIEnemyWaveButton attackBtnScript = UIEnemyWaveButton.CreateButton(outpostTransform, 2.5f, true);
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

    /// <summary>
    /// Prologue tự hoàn tất bước hành quân để người chơi có thể vào trận ngay
    /// sau đoạn camera. BattleData chỉ chuyển những lính đã tới vùng mục tiêu,
    /// nên phải gắn metadata hành quân cho đội quân vừa huấn luyện.
    /// </summary>
    private void PrepareTutorialExpedition()
    {
        if (baseZone == null || enemyZone == null) return;

        Vector3 destination = enemyZone.townHallPoint != null
            ? enemyZone.townHallPoint.position
            : enemyZone.transform.position;
        int currentWave = DayNightManager.HasInstance && DayNightManager.Ins != null
            ? DayNightManager.Ins.CurrentWave
            : 0;
        int deployedCount = 0;

        foreach (UnitController unit in Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None))
        {
            if (unit == null || !unit.gameObject.activeInHierarchy || unit.isDead ||
                !unit.IsStationedInZone(baseZone.settlementName))
            {
                continue;
            }

            unit.marchStartPosition = unit.transform.position;
            unit.marchDestinationPosition = destination;
            unit.marchStartWave = currentWave;
            unit.marchWavesToReach = 0;
            unit.marchTargetWave = currentWave;
            unit.marchDestinationZoneName = enemyZone.settlementName;
            unit.marchDestinationTroopSlotIndex = -1;
            unit.isExpeditionMarching = false;
            unit.hasReachedExpeditionDestination = true;
            deployedCount++;
        }

        Debug.Log($"[CampaignTutorialManager] ⚔️ Đã chuẩn bị {deployedCount} Hộ Vệ cho trận đầu tại {enemyZone.settlementName}.");
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
        // Chỉ phát Message 2 (phần tử 1) sau khi chiến thắng và quay về từ SceneBattle.
        PlayStage4Dialogue(1,
            new DialogueData { speakerName = "Trưởng Làng Marcus", message = "Chiến thắng vang dội! Lực lượng địch đã bị quét sạch, vùng đất Vaskasia đã được giải phóng hoàn toàn." },
            () => { dialogueDone = true; });

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
            // Vùng đất được đánh dấu đã lập ngay khi bắt đầu xây Nhà Chính.
            // Chỉ hoàn tất tutorial nếu công trình thực tế đã xây xong.
            if (IsStage5TownHallConstructionComplete())
            {
                CompleteTutorial();
            }
            else
            {
                StartStage5TownHallConstruction();
            }
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

    private void StartStage5TownHallConstruction()
    {
        SetStage(DemaciaTutorialStage.Stage5_SkipDayTownHall);
        ShowStepHint(6, "Nhà Chính Vaskasia đang xây dựng. Hãy bấm Qua Ngày cho đến khi công trình hoàn tất.");
        PointAtSkipDayButton();
    }

    private void ResumeStage5TownHallConstruction()
    {
        if (IsStage5TownHallConstructionComplete())
        {
            CompleteTutorial();
            return;
        }

        StartStage5TownHallConstruction();
    }

    private bool IsStage5TownHallConstructionComplete()
    {
        if (enemyZone == null || !enemyZone.isTownHallEstablished) return false;

        UpgradeableBuilding townHall = enemyZone.TownHallBuilding;
        return townHall != null && !townHall.IsUpgrading && !townHall.IsInitialBuildNeeded;
    }

    public void OnTownHallEstablished(SettlementZone zone)
    {
        if (currentStage == DemaciaTutorialStage.Stage5_EstablishVaskasia && zone == enemyZone)
        {
            // EstablishTownHall được gọi khi vừa đặt công trình, không phải khi xây xong.
            StartStage5TownHallConstruction();
        }
    }

    private void CompleteTutorial()
    {
        CompleteQuestObjective(5); // Tick xong Quest 5: Xây dựng Vaskasia -> Hoàn thành toàn bộ Prologue!

        SetStage(DemaciaTutorialStage.Completed);
        PlayerPrefs.SetInt("TutorialCompleted", 1);
        PlayerPrefs.Save();

        // Ngăn raid ngẫu nhiên chen vào giữa lời thoại kết Prologue và trận
        // phòng thủ nhỏ đã định sẵn cho phần thuyết trình.
        if (enablePresentationBattleSequence)
        {
            EnemyInvasionManager.Ins?.SetAutomaticRaidsPaused(true);
        }

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
            
            // Ẩn phần chỉ dẫn của tutorial, nhưng giữ TutorialCanvas hoạt động
            // vì Dialogue Panel cho chiến dịch/trận Rồng là con của Canvas này.
            UpdateHint("");
            if (hintText != null) hintText.gameObject.SetActive(false);
            EnsureTutorialCanvasCanShowDialogue();

            // Mở bảng Chapter Quest chúc mừng và hiển thị Chương I
            if (ChapterQuestController.Instance != null)
            {
                ChapterQuestController.Instance.OpenWindow();
            }

            StartPresentationBattleSequenceIfNeeded();
        });
    }

    // ====================================================================
    // KỊCH BẢN THUYẾT TRÌNH: 2 TRẬN NHỎ + 1 TRẬN LỚN CÓ RỒNG
    // ====================================================================
    private void StartPresentationBattleSequenceIfNeeded()
    {
        if (!enablePresentationBattleSequence || !IsTutorialCompleted()) return;

        AutoDetectZones();
        LoadPresentationBattleState();
        if (enemyZone == null || !enemyZone.IsConquered) return;

        EnemyInvasionManager.Ins?.SetAutomaticRaidsPaused(presentationBattlePhase != PresentationBattlePhase.Completed &&
                                                           presentationBattlePhase != PresentationBattlePhase.Failed);

        if (presentationBattlePhase == PresentationBattlePhase.None)
        {
            presentationBattlePhase = PresentationBattlePhase.FirstDefenseActive;
            SavePresentationBattleState();
            StartCoroutine(StartFirstPresentationDefense());
        }
        else if (presentationBattlePhase == PresentationBattlePhase.DragonCountdown)
        {
            UpdateDragonDefenseCountdown();
        }
    }

    private IEnumerator StartFirstPresentationDefense()
    {
        // Chờ EnemyInvasionManager và bốn điểm Raid hoàn tất khởi tạo.
        yield return null;

        if (EnemyInvasionManager.Ins == null ||
            !EnemyInvasionManager.Ins.StartScriptedRaid(enemyZone, false, warningWavesBeforeFirstDefense))
        {
            Debug.LogWarning("[CampaignTutorialManager] Không thể bắt đầu trận phòng thủ nhỏ. Kiểm tra 4 Raid Spawn Point.");
            UIManager.Ins?.ShowWarning("Không thể gọi đợt địch: hãy kiểm tra 4 Raid Spawn Point.");
        }
    }

    /// <summary>
    /// Được EnemyInvasionManager gọi sau khi người chơi thắng một trận phòng thủ.
    /// Phase đã lưu quyết định đây là trận kịch bản nào; không dùng cờ tạm thời
    /// của EnemyInvasionManager vì cờ đó mất khi đi qua SceneBattle.
    /// </summary>
    public void OnScriptedDefenseVictory(bool wasDragonDefense)
    {
        if (presentationBattlePhase == PresentationBattlePhase.FirstDefenseActive && !wasDragonDefense)
        {
            // Trận Rồng phải hiện ngay sau khi thắng phòng thủ nhỏ. Mốc 5
            // Wave là thời gian đoàn Rồng hành quân trên map, không phải chờ
            // thêm thời gian chờ rồi mới cho công trình Rồng xuất hiện.
            presentationBattlePhase = PresentationBattlePhase.DragonDefenseActive;
            dragonDefenseWave = -1;
            SavePresentationBattleState();

            string msg = $"⚠️ MỤC TIÊU KHẨN: Còn {wavesBeforeDragonDefense} Wave nữa Rồng sẽ dẫn quân tấn công! Hãy chinh phục EVENMOOR để mở Mỏ Đá, chuẩn bị lực lượng Khiên Binh.";
            UIManager.Ins?.ShowWarning(msg);
            Debug.Log($"[CampaignTutorialManager] {msg}");
            // Khởi động ngay để công trình Rồng và nhãn "Còn 5 Wave" đã
            // tồn tại trên map trong lúc lời thoại được hiển thị.
            StartDragonDefenseFromAssignedSpawnPoint();
            DialogueData[] countdownDialogue = dragonCountdownDialogues != null && dragonCountdownDialogues.Length > 0
                ? dragonCountdownDialogues
                : dragonDefenseWarningDialogues;
            PlayDialogueSequence(countdownDialogue, new[]
            {
                new DialogueData
                {
                    speakerName = "Trưởng Làng Marcus",
                    message = $"Tin khẩn! Trinh sát báo Rồng sẽ dẫn quân tấn công sau {wavesBeforeDragonDefense} Wave. Hãy chinh phục EVENMOOR để mở Mỏ Đá và chuẩn bị Khiên Binh."
                }
            });
            return;
        }

        if (presentationBattlePhase == PresentationBattlePhase.DragonDefenseActive)
        {
            presentationBattlePhase = PresentationBattlePhase.Completed;
            SavePresentationBattleState();

            const string msg = "🏆 CHIẾN THẮNG LỚN! Rồng đã bị đánh bại, Demacia an toàn.";
            UIManager.Ins?.ShowWarning(msg);
            Debug.Log($"[CampaignTutorialManager] {msg}");
            PlayDialogueSequence(dragonDefenseVictoryDialogues, new[]
            {
                new DialogueData
                {
                    speakerName = "Trưởng Làng Marcus",
                    message = "Lãnh chúa, con Rồng đã bị đánh bại! Người dân Demacia cuối cùng cũng có thể sống trong bình yên."
                },
                new DialogueData
                {
                    speakerName = "Trưởng Làng Marcus",
                    message = "Chiến thắng hôm nay là minh chứng cho lòng dũng cảm của ngài. Demacia sẽ luôn ghi nhớ ngày này!"
                }
            }, () => EnemyInvasionManager.Ins?.SetAutomaticRaidsPaused(false));
        }
    }

    /// <summary>
    /// Trận kịch bản chỉ xuất hiện một lần. Thua sẽ kết thúc chuỗi thay vì tự spawn lại.
    /// </summary>
    public void OnScriptedDefenseDefeat(bool wasDragonDefense)
    {
        if (presentationBattlePhase != PresentationBattlePhase.FirstDefenseActive &&
            presentationBattlePhase != PresentationBattlePhase.DragonDefenseActive)
        {
            return;
        }

        presentationBattlePhase = PresentationBattlePhase.Failed;
        SavePresentationBattleState();
        EnemyInvasionManager.Ins?.SetAutomaticRaidsPaused(false);
        Debug.LogWarning($"[CampaignTutorialManager] Thất bại trận {(wasDragonDefense ? "Rồng" : "phòng thủ nhỏ")}; trận kịch bản không tự lặp lại.");
    }

    /// <summary>
    /// SettlementZone gọi khi một căn cứ địch bị đánh bại. EVENMOOR mở sẵn Mỏ/Kho Đá
    /// từ danh sách unlockedBuildingTypes trong Scene, đồng thời mở mốc chuẩn bị Khiên Binh.
    /// </summary>
    public void OnSettlementConquered(SettlementZone conqueredZone)
    {
        if (conqueredZone == null) return;
        AutoDetectZones();

        bool isEvenmoor = conqueredZone == evenmoorZone ||
                          conqueredZone.settlementName.ToUpper().Contains("EVENMOOR");
        if (!isEvenmoor) return;

        PlayerPrefs.SetInt(ShieldTroopUnlockedPrefKey, 1);
        PlayerPrefs.SetInt(StoneBuildingsUnlockedPrefKey, 1);

        // Bảo đảm Zone Evenmoor cũng thực sự mang công nghệ Mỏ/Kho Đá, không
        // chỉ hiện thông báo hay mở khóa Khiên Binh.
        if (conqueredZone.unlockedBuildingTypes == null)
        {
            conqueredZone.unlockedBuildingTypes = new List<BuildingType>();
        }
        if (!conqueredZone.unlockedBuildingTypes.Contains(BuildingType.StoneMine))
        {
            conqueredZone.unlockedBuildingTypes.Add(BuildingType.StoneMine);
        }
        if (!conqueredZone.unlockedBuildingTypes.Contains(BuildingType.StoneStorage))
        {
            conqueredZone.unlockedBuildingTypes.Add(BuildingType.StoneStorage);
        }
        conqueredZone.SaveSettlementState();
        PlayerPrefs.Save();
        const string msg = "⛏️ EVENMOOR đã được chinh phục! Mỏ Đá đã mở khóa; bạn có thể chuẩn bị Khiên Binh cho trận Rồng.";
        UIManager.Ins?.ShowWarning(msg);
        Debug.Log($"[CampaignTutorialManager] {msg}");
    }

    private void UpdateDragonDefenseCountdown()
    {
        // Tương thích save cũ: trước đây DragonCountdown chờ 5 Wave trước
        // khi hiện Rồng. Luồng mới hiện Rồng ngay và để đoàn quân đi 5 Wave.
        if (presentationBattlePhase == PresentationBattlePhase.DragonCountdown)
        {
            presentationBattlePhase = PresentationBattlePhase.DragonDefenseActive;
            dragonDefenseWave = -1;
            SavePresentationBattleState();
            StartDragonDefenseFromAssignedSpawnPoint();
            return;
        }

        // Nếu gán Dragon Raid Spawn Point muộn, thử gọi lại ở Wave sau.
        if (presentationBattlePhase == PresentationBattlePhase.DragonDefenseActive)
        {
            if (EnemyInvasionManager.Ins != null && !EnemyInvasionManager.Ins.isInvasionActive)
            {
                StartDragonDefenseFromAssignedSpawnPoint();
            }
            return;
        }
    }

    private void StartDragonDefenseFromAssignedSpawnPoint()
    {
        if (EnemyInvasionManager.Ins == null ||
            !EnemyInvasionManager.Ins.StartScriptedDragonRaid(enemyZone, 0))
        {
            Debug.LogWarning("[CampaignTutorialManager] Không thể bắt đầu trận Rồng. Hãy gán Dragon Raid Spawn Point bằng EnemySpawnManager riêng.");
            UIManager.Ins?.ShowWarning("Chưa gán điểm spawn Rồng riêng. Hãy kéo EnemySpawnManager vào Dragon Raid Spawn Point.");
        }
    }

    private void LoadPresentationBattleState()
    {
        presentationBattlePhase = (PresentationBattlePhase)PlayerPrefs.GetInt(
            PresentationBattlePhasePrefKey,
            (int)presentationBattlePhase);
        dragonDefenseWave = PlayerPrefs.GetInt(DragonDefenseWavePrefKey, dragonDefenseWave);
    }

    private void SavePresentationBattleState()
    {
        PlayerPrefs.SetInt(PresentationBattlePhasePrefKey, (int)presentationBattlePhase);
        PlayerPrefs.SetInt(DragonDefenseWavePrefKey, dragonDefenseWave);
        PlayerPrefs.Save();
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
        // Nhà Chính Vaskasia đã hoàn tất sau một hoặc nhiều lượt Qua Ngày.
        if (currentStage == DemaciaTutorialStage.Stage5_SkipDayTownHall &&
            buildingType == BuildingType.House &&
            IsStage5TownHallConstructionComplete())
        {
            CompleteTutorial();
            return;
        }

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
