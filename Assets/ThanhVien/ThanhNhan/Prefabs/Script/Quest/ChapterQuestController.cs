using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.Serialization;

public class ChapterQuestController : MonoBehaviour
{
    public static ChapterQuestController Instance { get; private set; }

    [Header("--- 1. TOP HEADER (ĐIỀU HƯỚNG CHƯƠNG) ---")]
    [SerializeField] private TextMeshProUGUI chapterTitleText;
    [SerializeField] private TextMeshProUGUI chapterDescriptionText;
    [SerializeField] private TextMeshProUGUI chapterProgressText;
    [SerializeField] private Image chapterProgressFill;
    [SerializeField] private Button prevChapterBtn;
    [SerializeField] private Button nextChapterBtn;

    [Header("--- 2. CỤM TÀI NGUYÊN THƯỞNG (REWARD HEADER) ---")]
    [FormerlySerializedAs("rewardAreaPanel")]
    [SerializeField] private GameObject baseResourcesContainer;
    [FormerlySerializedAs("goldRewardText")]
    [SerializeField] private TextMeshProUGUI goldAmountText;
    [FormerlySerializedAs("woodRewardText")]
    [SerializeField] private TextMeshProUGUI woodAmountText;
    [FormerlySerializedAs("stoneRewardText")]
    [SerializeField] private TextMeshProUGUI stoneAmountText;

    [Header("--- 3. PHẦN THƯỞNG KHÓA (KeyRewardCard) ---")]
    [SerializeField] private Image keyRewardIcon;
    [FormerlySerializedAs("keyRewardTitleText")]
    [SerializeField] private TextMeshProUGUI keyRewardNameText;

    [Header("--- TRẠNG THÁI KHÓA CHƯƠNG (TÙY CHỌN) ---")]
    [FormerlySerializedAs("lockedPanel")]
    [SerializeField] private GameObject chapterLockedOverlay;
    [SerializeField] private TextMeshProUGUI chapterLockedMessageText;

    [Header("--- 4. VÙNG DANH SÁCH MỤC TIÊU (ContentArea) ---")]
    [FormerlySerializedAs("contentArea")]
    [SerializeField] private Transform objectiveContainer; // Viewport -> Content
    [FormerlySerializedAs("questItemPrefab")]
    [SerializeField] private GameObject objectiveItemPrefab; // Prefab dòng nhiệm vụ

    [Header("--- 5. NÚT ĐÓNG ---")]
    [SerializeField] private Button returnButton;

    [Header("✨ ANIMATION CONFIG")]
    [SerializeField] private bool enableAnimations = true;
    [SerializeField] private float unfoldDuration = 0.32f;
    [SerializeField] private RectTransform questWindowPanel;
    [SerializeField] private float windowSlideDistance = 90f;
    [SerializeField] private Ease openEase = Ease.OutCubic;
    [SerializeField] private Ease closeEase = Ease.InCubic;

    [Header("--- 6. DỮ LIỆU 4 CHƯƠNG ---")]
    [SerializeField] private List<ChapterData> chapterList = new List<ChapterData>();

    [Header("DEBUG / XEM TRƯỚC")]
    [Tooltip("Bật để hiển thị tên toàn bộ nhiệm vụ của cả 4 chương. Chỉ thay đổi hiển thị, không mở khóa hoặc cho phép hoàn thành nhiệm vụ.")]
    [SerializeField] private bool showAllChapterObjectives;
    private int currentChapterIndex = 0;
    private int highestUnlockedChapterIndex = 0;
    private CanvasGroup windowCanvasGroup;
    private Vector2 windowRestPosition;
    private bool hasWindowRestPosition;
    private float gameplayObjectiveSyncTimer;
    private const float GameplayObjectiveSyncInterval = 0.5f;

    // Tiến độ Chapter Quest phải tồn tại qua SceneBattle; tutorial cũng đổi scene giữa Prologue.
    // V3 bỏ objective phòng thủ và thay objective Rồng bằng chinh phục TYLBURNE.
    // Không đọc save cũ để objective theo index không bị gán nhầm sang quest mới.
    private const string QuestProgressSaveKey = "ChapterQuestProgress_V3";
    private const string HighestUnlockedChapterSaveKey = "ChapterQuestHighestUnlocked_V3";
    private const string CurrentChapterSaveKey = "ChapterQuestCurrentChapter_V3";

    // Bảng màu chuẩn
    private readonly string colorActiveQuest = "#4A2E18";   // Nâu đậm nổi bật
    private readonly string colorQuestionMark = "#5C4A38";  // Nâu xám tiệp màu chữ
    private readonly string colorCompleted = "#8A817A";     // Nâu xám mờ

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        InitAllFourChapters();
        LoadQuestProgress();
        SynchronizePrologueProgressFromTutorial();
        CacheWindowAnimationReferences();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (prevChapterBtn != null) prevChapterBtn.onClick.AddListener(PrevChapter);
        if (nextChapterBtn != null) nextChapterBtn.onClick.AddListener(NextChapter);
        if (returnButton != null) returnButton.onClick.AddListener(CloseWindow);

        // Một công trình có thể đã được xây trước khi ChapterQuestController
        // được tạo lại sau khi quay về từ SceneBattle hoặc tải save.
        SynchronizeCurrentWorldObjective();
        UpdateTrackerHUD();
        CloseWindowImmediately();
    }

    private void Update()
    {
        // Một vài hệ thống khôi phục UnitController sau Start. Đồng bộ định kỳ
        // giúp objective đang mở nhận đúng trạng thái world/load-save mà không
        // phải phụ thuộc vào thứ tự khởi tạo các GameObject.
        gameplayObjectiveSyncTimer += Time.unscaledDeltaTime;
        if (gameplayObjectiveSyncTimer < GameplayObjectiveSyncInterval) return;

        gameplayObjectiveSyncTimer = 0f;
        SynchronizeCurrentWorldObjective();
    }

    private void UpdateTrackerHUD()
    {
        InitAllFourChapters();
        if (chapterList == null || chapterList.Count == 0 || QuestTrackerHUD.Instance == null) return;

        int activeChapterIndex = Mathf.Clamp(highestUnlockedChapterIndex, 0, chapterList.Count - 1);
        ChapterData activeChapter = chapterList[activeChapterIndex];
        QuestObjective activeObjective = activeChapter.objectives?.Find(objective => !objective.isCompleted);
        string activeQuestTitle = activeObjective != null
            ? activeObjective.title.Replace("✦", "").Replace("□", "").Trim()
            : "Hoàn thành toàn bộ chương!";

        QuestTrackerHUD.Instance.UpdateTrackerInfo(activeChapter.chapterName, activeQuestTitle);
    }

    public void InitAllFourChapters()
    {
        if (chapterList != null && chapterList.Count > 0) return;

        chapterList = new List<ChapterData>();

        // 0. PROLOGUE
        ChapterData prologue = new ChapterData
        {
            chapterName = "Prologue: Mở Đầu",
            rewardGold = 50,
            rewardWood = 50,
            rewardStone = 20,
            keyRewardName = "Demacian Orb & Petricite Park Icon",
            objectives = new List<QuestObjective>
            {
                new QuestObjective { title = "Bấm vào Zeffira để mở giao diện Thành Phố", isCompleted = false, rewardGold = 0, rewardWood = 10, rewardStone = 0 },
                new QuestObjective { title = "Xây dựng Xưởng Gỗ tại Zeffira", isCompleted = false, rewardGold = 0, rewardWood = 20, rewardStone = 0 },
                new QuestObjective { title = "Huấn luyện 1 Kiếm Sĩ tại Zeffira", isCompleted = false, rewardGold = 20, rewardWood = 0, rewardStone = 0 },
                new QuestObjective { title = "Di chuyển quân đến lãnh thổ địch phía Đông Zeffira", isCompleted = false, rewardGold = 30, rewardWood = 0, rewardStone = 0 },
                new QuestObjective { title = "Chuẩn bị và giành chiến thắng trong trận đánh đầu", isCompleted = false, rewardGold = 50, rewardWood = 20, rewardStone = 0 },
                new QuestObjective { title = "Xây dựng Vaskasia trên vùng đất trống", isCompleted = false, rewardGold = 0, rewardWood = 30, rewardStone = 20 }
            }
        };
        chapterList.Add(prologue);

        // 1. CHƯƠNG I
        ChapterData ch1 = new ChapterData
        {
            chapterId = "chapter_1",
            chapterName = "Chương I: Khai Nguyên",
            chapterDescription = "Củng cố Zeffira, lập đội Cung Thủ và chinh phục EVENMOOR.",
            rewardGold = 200,
            rewardWood = 200,
            rewardStone = 200,
            keyRewardName = "Honorary Crownguard",
            objectives = new List<QuestObjective>
            {
                new QuestObjective { questId = "ch1_build_food_storage", title = "Xây Kho Lúa tại Zeffira", isCompleted = false, rewardGold = 100, rewardWood = 150, rewardStone = 0 },
                new QuestObjective { questId = "ch1_research_archer", title = "Nghiên cứu mở khóa Cung Thủ tại Viện Binh", isCompleted = false, rewardGold = 100, rewardWood = 0, rewardStone = 100 },
                new QuestObjective { questId = "ch1_train_archers", title = "Huấn luyện 6 Cung Thủ bảo vệ Zeffira", targetProgress = 6, isCompleted = false, rewardGold = 150, rewardWood = 100, rewardStone = 0 },
                new QuestObjective { questId = "ch1_upgrade_zeffira_level_2", title = "Nâng cấp Nhà Chính Zeffira lên Cấp 2", isCompleted = false, rewardGold = 300, rewardWood = 200, rewardStone = 200 },
                new QuestObjective { questId = "ch1_conquer_evenmoor", title = "Chinh phục EVENMOOR để mở khóa công nghệ đá", isCompleted = false, rewardGold = 150, rewardWood = 0, rewardStone = 100 }
            }
        };
        chapterList.Add(ch1);

        // 2. CHƯƠNG II
        ChapterData ch2 = new ChapterData
        {
            chapterId = "chapter_2",
            chapterName = "Chương II: Chinh Phạt BROOKHOLLOW",
            chapterDescription = "Khai thác công nghệ đá, tập hợp Khiên Binh và chinh phục BROOKHOLLOW.",
            rewardGold = 500,
            rewardWood = 400,
            rewardStone = 300,
            keyRewardName = "Vanguard Protector",
            objectives = new List<QuestObjective>
            {
                new QuestObjective { questId = "ch2_build_stone_storage", title = "Xây Kho Đá tại Vaskasia", isCompleted = false, rewardGold = 200, rewardWood = 200, rewardStone = 0 },
                new QuestObjective { questId = "ch2_research_shield", title = "Nghiên cứu mở khóa Khiên Binh tại Viện Binh", isCompleted = false, rewardGold = 200, rewardWood = 0, rewardStone = 250 },
                new QuestObjective { questId = "ch2_train_shields", title = "Huấn luyện 3 Khiên Binh cho cuộc chinh phạt", targetProgress = 3, isCompleted = false, rewardGold = 250, rewardWood = 250, rewardStone = 0 },
                new QuestObjective { questId = "ch2_conquer_brookhollow", title = "Chinh phục BROOKHOLLOW để mở đường tới Terbisia", isCompleted = false, rewardGold = 350, rewardWood = 200, rewardStone = 200 }
            }
        };
        chapterList.Add(ch2);

        // 3. CHƯƠNG III
        ChapterData ch3 = new ChapterData
        {
            chapterId = "chapter_3",
            chapterName = "Chương III: Vùng Đất Terbisia",
            chapterDescription = "Mở rộng đến Terbisia, hoàn thiện lực lượng và chinh phục TYLBURNE.",
            rewardGold = 1000,
            rewardWood = 800,
            rewardStone = 600,
            keyRewardName = "Demacia Commander",
            objectives = new List<QuestObjective>
            {
                new QuestObjective { questId = "ch3_conquer_terbisia", title = "Chinh phục TERBISIA", isCompleted = false, rewardGold = 300, rewardWood = 300, rewardStone = 0 },
                new QuestObjective { questId = "ch3_establish_terbisia", title = "Xây Nhà Chính tại Terbisia", isCompleted = false, rewardGold = 350, rewardWood = 0, rewardStone = 300 },
                new QuestObjective { questId = "ch3_upgrade_zeffira_level_3", title = "Nâng cấp Nhà Chính Zeffira lên Cấp 3 (Thành Trì)", isCompleted = false, rewardGold = 500, rewardWood = 500, rewardStone = 500 },
                new QuestObjective { questId = "ch3_research_crossbow_tower", title = "Nghiên cứu mở khóa Tháp Nỏ", isCompleted = false, rewardGold = 300, rewardWood = 0, rewardStone = 300 },
                new QuestObjective { questId = "ch3_conquer_tylburne", title = "Chinh phục TYLBURNE, vùng đất cuối cùng", isCompleted = false, rewardGold = 600, rewardWood = 400, rewardStone = 400 }
            }
        };
        chapterList.Add(ch3);
    }

    public void DisplayChapter(int index)
    {
        InitAllFourChapters();
        if (chapterList == null || chapterList.Count == 0) return;

        currentChapterIndex = Mathf.Clamp(index, 0, chapterList.Count - 1);
        ChapterData currentChapter = chapterList[currentChapterIndex];
        bool isLockedChapter = !showAllChapterObjectives && currentChapterIndex > highestUnlockedChapterIndex;

        // 1. Cập nhật Tiêu Đề Chương
        if (chapterTitleText != null)
        {
            chapterTitleText.text = currentChapter.chapterName;
        }
        if (chapterDescriptionText != null) chapterDescriptionText.text = currentChapter.chapterDescription;

        int completedObjectives = CountCompletedObjectives(currentChapter);
        int objectiveCount = currentChapter.objectives.Count;
        if (chapterProgressText != null) chapterProgressText.text = $"Tiến độ: {completedObjectives}/{objectiveCount}";
        if (chapterProgressFill != null) chapterProgressFill.fillAmount = objectiveCount == 0 ? 0f : (float)completedObjectives / objectiveCount;

        // 2. Cập nhật nút chuyển Chương (Prev / Next)
        if (prevChapterBtn != null) prevChapterBtn.interactable = (currentChapterIndex > 0);
        if (nextChapterBtn != null) nextChapterBtn.interactable = currentChapterIndex < chapterList.Count - 1;

        if (chapterLockedOverlay != null) chapterLockedOverlay.SetActive(isLockedChapter);
        if (chapterLockedMessageText != null)
        {
            chapterLockedMessageText.text = isLockedChapter
                ? "Hoàn thành chương trước để mở khóa chương này."
                : string.Empty;
        }

        // 3. Cập nhật Card Phần Thưởng Khóa (Key Reward)
        if (keyRewardNameText != null) keyRewardNameText.text = currentChapter.keyRewardName;
        if (keyRewardIcon != null)
        {
            keyRewardIcon.sprite = currentChapter.keyRewardIcon;
            keyRewardIcon.enabled = currentChapter.keyRewardIcon != null;
        }

        // 4. Sinh danh sách các Mục Tiêu (Quest Items)
        if (objectiveContainer != null && objectiveItemPrefab != null)
        {
            PopulateObjectives(currentChapter);
        }
    }

    private void PopulateObjectives(ChapterData chapter)
    {
        foreach (Transform child in objectiveContainer)
        {
            Destroy(child.gameObject);
        }

        bool isFutureLockedChapter = !showAllChapterObjectives && currentChapterIndex > highestUnlockedChapterIndex;
        bool foundActiveQuest = false;
        string activeQuestTitle = "";

        int displayRewardGold = 0;
        int displayRewardWood = 0;
        int displayRewardStone = 0;

        for (int i = 0; i < chapter.objectives.Count; i++)
        {
            QuestObjective obj = chapter.objectives[i];
            GameObject item = Instantiate(objectiveItemPrefab, objectiveContainer);
            string cleanTitle = obj.title.Replace("✦", "").Replace("□", "").Trim();
            bool isActive = !isFutureLockedChapter && !obj.isCompleted && !foundActiveQuest;
            ChapterQuestObjectiveItemUI itemUI = item.GetComponent<ChapterQuestObjectiveItemUI>();

            if (itemUI != null)
            {
                bool isObjectiveLocked = !showAllChapterObjectives && (isFutureLockedChapter || (!obj.isCompleted && !isActive));
                itemUI.Setup(cleanTitle, obj.currentProgress, obj.targetProgress, isActive, obj.isCompleted, isObjectiveLocked);
            }
            else
            {
                TextMeshProUGUI itemText = item.GetComponentInChildren<TextMeshProUGUI>();
                if (itemText == null) continue;

                if (isFutureLockedChapter) itemText.text = $"<color={colorQuestionMark}>???</color>";
                else if (obj.isCompleted) itemText.text = $"<color={colorCompleted}><s>{cleanTitle}</s></color>";
                else if (isActive) itemText.text = $"<color={colorActiveQuest}>{cleanTitle}</color>";
                else itemText.text = $"<color={colorQuestionMark}>???</color>";
            }

            if (isFutureLockedChapter)
            {
            }
            else if (obj.isCompleted)
            {
            }
            else if (isActive)
            {
                foundActiveQuest = true;
                activeQuestTitle = cleanTitle;
                displayRewardGold = obj.rewardGold;
                displayRewardWood = obj.rewardWood;
                displayRewardStone = obj.rewardStone;
            }
        }

        UpdateRewardHeaderDisplay(displayRewardGold, displayRewardWood, displayRewardStone);

        if (QuestTrackerHUD.Instance != null && currentChapterIndex == highestUnlockedChapterIndex)
        {
            string displayTracker = foundActiveQuest ? activeQuestTitle : "Hoàn thành toàn bộ chương!";
            QuestTrackerHUD.Instance.UpdateTrackerInfo(chapter.chapterName, displayTracker);
        }
    }

    private void UpdateRewardHeaderDisplay(int gold, int wood, int stone)
    {
        if (goldAmountText != null) goldAmountText.text = gold.ToString();
        if (woodAmountText != null) woodAmountText.text = wood.ToString();
        if (stoneAmountText != null) stoneAmountText.text = stone.ToString();

        if (enableAnimations && baseResourcesContainer != null)
        {
            baseResourcesContainer.transform.DOKill();
            baseResourcesContainer.transform.localScale = Vector3.one;
        }
    }

    public bool CompletePrologueObjective(int objectiveIndex)
    {
        return CompleteObjective(0, objectiveIndex);
    }

    /// <summary>
    /// Khôi phục các tick Prologue nếu ChapterQuestController bị tạo lại sau khi tutorial đã đổi Scene.
    /// Đồng bộ này không cộng lại phần thưởng, vì phần thưởng của các mục tiêu trước đó có thể đã được nhận.
    /// </summary>
    public bool SynchronizePrologueObjectivesThrough(int objectiveIndex)
    {
        InitAllFourChapters();
        if (chapterList == null || chapterList.Count == 0 || objectiveIndex < 0) return false;

        ChapterData prologue = chapterList[0];
        if (prologue.objectives == null || prologue.objectives.Count == 0) return false;

        int lastObjectiveIndex = Mathf.Min(objectiveIndex, prologue.objectives.Count - 1);
        bool changed = false;

        for (int i = 0; i <= lastObjectiveIndex; i++)
        {
            QuestObjective objective = prologue.objectives[i];
            if (objective.isCompleted) continue;

            objective.targetProgress = Mathf.Max(1, objective.targetProgress);
            objective.currentProgress = objective.targetProgress;
            objective.isCompleted = true;
            changed = true;
        }

        if (AreAllObjectivesCompletedInChapter(prologue))
        {
            if (!prologue.isCompleted || !prologue.isRewardClaimed)
            {
                changed = true;
            }

            prologue.isCompleted = true;
            prologue.isRewardClaimed = true;
            int unlockedChapterIndex = Mathf.Max(highestUnlockedChapterIndex, Mathf.Min(1, chapterList.Count - 1));
            if (highestUnlockedChapterIndex != unlockedChapterIndex)
            {
                changed = true;
            }

            highestUnlockedChapterIndex = unlockedChapterIndex;
            if (currentChapterIndex != highestUnlockedChapterIndex)
            {
                changed = true;
            }
            currentChapterIndex = highestUnlockedChapterIndex;
        }

        if (changed) SaveQuestProgress();
        return true;
    }

    public bool IsObjectiveCompleted(int chapterIndex, int objectiveIndex)
    {
        InitAllFourChapters();
        return chapterIndex >= 0 && chapterIndex < chapterList.Count &&
               objectiveIndex >= 0 && objectiveIndex < chapterList[chapterIndex].objectives.Count &&
               chapterList[chapterIndex].objectives[objectiveIndex].isCompleted;
    }

    public int GetNextActiveObjectiveIndex(int chapterIndex)
    {
        InitAllFourChapters();
        if (chapterIndex < 0 || chapterIndex >= chapterList.Count) return -1;
        return chapterList[chapterIndex].objectives.FindIndex(objective => !objective.isCompleted);
    }

    /// <summary>
    /// Nhận mốc một công trình đã xây xong từ gameplay. Quest chỉ hoàn thành
    /// nếu đó đang là objective mở khóa hiện tại.
    /// </summary>
    public void ReportBuildingConstructionCompleted(BuildingType buildingType, SettlementZone zone)
    {
        if (buildingType == BuildingType.FoodStorage && IsZoneNamed(zone, "ZEFFIRA"))
        {
            TryCompleteActiveQuestById("ch1_build_food_storage");
        }

        // Prefab "Xưởng Đá" đang được khai báo là StoneStorage trong bảng
        // công trình. Chỉ nhận công trình đã hoàn tất ở đúng Vaskasia.
        if (buildingType == BuildingType.StoneStorage && IsZoneNamed(zone, "VASKASIA"))
        {
            TryCompleteActiveQuestById("ch2_build_stone_storage");
        }
    }

    /// <summary>
    /// Nhận mốc nghiên cứu vừa được mở khóa. Cung Thủ hiện được gameplay mở
    /// bằng node sword_damage_1 trong ResearchUpgradeEffects.
    /// </summary>
    public void ReportResearchUnlocked(string nodeId)
    {
        if (string.Equals(nodeId, "sword_damage_1", System.StringComparison.Ordinal) &&
            ResearchUpgradeEffects.ArcherUnlocked)
        {
            TryCompleteActiveQuestById("ch1_research_archer");
        }

        if (string.Equals(nodeId, "shield_damage_1", System.StringComparison.Ordinal) &&
            ResearchUpgradeEffects.ShieldUnlocked)
        {
            TryCompleteActiveQuestById("ch2_research_shield");
        }

        if (string.Equals(nodeId, "unlock_crossbow_tower_1", System.StringComparison.Ordinal) &&
            ResearchUpgradeEffects.CrossbowTowerUnlocked)
        {
            TryCompleteActiveQuestById("ch3_research_crossbow_tower");
        }
    }

    /// <summary>
    /// Nhận kết quả huấn luyện sau khi TroopTrainingManager đã spawn các
    /// UnitController thật. Không đếm ô UI để tránh tính nhầm lính hologram.
    /// </summary>
    public void ReportTroopsTrained(BuildingType troopType, int spawnedUnitCount, SettlementZone zone)
    {
        if (spawnedUnitCount <= 0) return;

        if (troopType == BuildingType.BarracksArcher &&
            IsZoneNamed(zone, "ZEFFIRA") &&
            IsQuestActive("ch1_train_archers"))
        {
            int activeObjectiveIndex = GetNextActiveObjectiveIndex(highestUnlockedChapterIndex);
            AddObjectiveProgress(highestUnlockedChapterIndex, activeObjectiveIndex, spawnedUnitCount);
        }

        if (troopType == BuildingType.BarracksSpear &&
            IsZoneNamed(zone, "VASKASIA") &&
            IsQuestActive("ch2_train_shields"))
        {
            int activeObjectiveIndex = GetNextActiveObjectiveIndex(highestUnlockedChapterIndex);
            AddObjectiveProgress(highestUnlockedChapterIndex, activeObjectiveIndex, spawnedUnitCount);
        }
    }

    /// <summary>
    /// Nhận mốc nâng cấp Nhà Chính đã hoàn tất. Đọc SettlementLevel thay vì
    /// dữ liệu hiển thị trên UI để cả save cũ và nâng cấp mới đều chính xác.
    /// </summary>
    public void ReportBuildingUpgradeCompleted(UpgradeableBuilding building, SettlementZone zone)
    {
        if (building == null ||
            !IsZoneNamed(zone, "ZEFFIRA") ||
            !SettlementZone.IsTownHallBuilding(building, zone))
        {
            return;
        }

        if (zone.SettlementLevel >= 2 && IsQuestActive("ch1_upgrade_zeffira_level_2"))
        {
            TryCompleteActiveQuestById("ch1_upgrade_zeffira_level_2");
        }

        if (zone.SettlementLevel >= 3 && IsQuestActive("ch3_upgrade_zeffira_level_3"))
        {
            TryCompleteActiveQuestById("ch3_upgrade_zeffira_level_3");
        }
    }

    /// <summary>
    /// Nhận vùng đất vừa chinh phục sau khi căn cứ địch đã bị phá hủy. Chỉ
    /// chấp nhận vùng đang IsConquered để không nhầm với vùng mới chỉ mở khóa.
    /// </summary>
    public void ReportSettlementConquered(SettlementZone zone)
    {
        if (zone == null || !zone.IsConquered) return;

        if (IsZoneNamed(zone, "EVENMOOR"))
        {
            TryCompleteActiveQuestById("ch1_conquer_evenmoor");
        }
        else if (IsZoneNamed(zone, "BROOKHOLLOW"))
        {
            TryCompleteActiveQuestById("ch2_conquer_brookhollow");
        }
        else if (IsZoneNamed(zone, "TERBISIA"))
        {
            TryCompleteActiveQuestById("ch3_conquer_terbisia");
        }
        else if (IsZoneNamed(zone, "TYLBURNE"))
        {
            TryCompleteActiveQuestById("ch3_conquer_tylburne");
        }
    }

    /// <summary>
    /// Nhận mốc lập Nhà Chính cho vùng đất mới. Đây là thao tác người chơi trả
    /// chi phí và bắt đầu xây Nhà Chính, khác với việc nâng cấp Nhà Chính cũ.
    /// </summary>
    public void ReportTownHallEstablished(SettlementZone zone)
    {
        if (zone != null &&
            zone.isTownHallEstablished &&
            !zone.hasEnemyOutpost &&
            IsZoneNamed(zone, "TERBISIA"))
        {
            TryCompleteActiveQuestById("ch3_establish_terbisia");
        }
    }

    private void SynchronizeCurrentWorldObjective()
    {
        const string foodStorageQuestId = "ch1_build_food_storage";
        if (IsQuestActive(foodStorageQuestId))
        {
            SettlementZone zeffira = FindSettlementByName("ZEFFIRA");
            if (HasCompletedBuilding(zeffira, BuildingType.FoodStorage))
            {
                TryCompleteActiveQuestById(foodStorageQuestId);
            }
        }

        // Khôi phục được objective nếu người chơi đã mở research trước khi
        // controller quest được tạo lại (ví dụ sau khi tải save/đổi Scene).
        if (IsQuestActive("ch1_research_archer") && ResearchUpgradeEffects.ArcherUnlocked)
        {
            TryCompleteActiveQuestById("ch1_research_archer");
        }

        if (IsQuestActive("ch1_train_archers"))
        {
            SettlementZone zeffira = FindSettlementByName("ZEFFIRA");
            SynchronizeActiveObjectiveProgress(
                "ch1_train_archers",
                CountStationedUnitsByResearchType(zeffira, SoldierResearchType.Bow));
        }

        if (IsQuestActive("ch2_build_stone_storage"))
        {
            SettlementZone vaskasia = FindSettlementByName("VASKASIA");
            if (HasCompletedBuilding(vaskasia, BuildingType.StoneStorage))
            {
                TryCompleteActiveQuestById("ch2_build_stone_storage");
            }
        }

        if (IsQuestActive("ch2_research_shield") && ResearchUpgradeEffects.ShieldUnlocked)
        {
            TryCompleteActiveQuestById("ch2_research_shield");
        }

        if (IsQuestActive("ch2_train_shields"))
        {
            SettlementZone vaskasia = FindSettlementByName("VASKASIA");
            SynchronizeActiveObjectiveProgress(
                "ch2_train_shields",
                CountStationedUnitsByResearchType(vaskasia, SoldierResearchType.Shield));
        }

        // Quest có thể trở thành objective hiện tại sau khi người chơi đã
        // nâng cấp Nhà Chính, nên luôn đọc cấp độ settlement đã lưu.
        SettlementZone zeffiraTownHall = FindSettlementByName("ZEFFIRA");
        if (zeffiraTownHall != null &&
            zeffiraTownHall.SettlementLevel >= 2 &&
            IsQuestActive("ch1_upgrade_zeffira_level_2"))
        {
            TryCompleteActiveQuestById("ch1_upgrade_zeffira_level_2");
        }

        if (zeffiraTownHall != null &&
            zeffiraTownHall.SettlementLevel >= 3 &&
            IsQuestActive("ch3_upgrade_zeffira_level_3"))
        {
            TryCompleteActiveQuestById("ch3_upgrade_zeffira_level_3");
        }

        SettlementZone terbisia = FindSettlementByName("TERBISIA");
        if (IsQuestActive("ch3_establish_terbisia") &&
            terbisia != null &&
            terbisia.isTownHallEstablished &&
            !terbisia.hasEnemyOutpost)
        {
            TryCompleteActiveQuestById("ch3_establish_terbisia");
        }

        if (IsQuestActive("ch3_research_crossbow_tower") && ResearchUpgradeEffects.CrossbowTowerUnlocked)
        {
            TryCompleteActiveQuestById("ch3_research_crossbow_tower");
        }

        SynchronizeConquestObjective("ch1_conquer_evenmoor", "EVENMOOR");
        SynchronizeConquestObjective("ch2_conquer_brookhollow", "BROOKHOLLOW");
        SynchronizeConquestObjective("ch3_conquer_terbisia", "TERBISIA");
        SynchronizeConquestObjective("ch3_conquer_tylburne", "TYLBURNE");
    }

    private void SynchronizeConquestObjective(string questId, string settlementName)
    {
        if (!IsQuestActive(questId)) return;

        SettlementZone zone = FindSettlementByName(settlementName);
        if (zone != null && zone.IsConquered)
        {
            TryCompleteActiveQuestById(questId);
        }
    }

    private bool IsQuestActive(string questId)
    {
        InitAllFourChapters();
        if (string.IsNullOrWhiteSpace(questId) ||
            highestUnlockedChapterIndex < 0 ||
            highestUnlockedChapterIndex >= chapterList.Count)
        {
            return false;
        }

        ChapterData chapter = chapterList[highestUnlockedChapterIndex];
        int activeObjectiveIndex = chapter.objectives?.FindIndex(objective => !objective.isCompleted) ?? -1;
        return activeObjectiveIndex >= 0 &&
               string.Equals(chapter.objectives[activeObjectiveIndex].questId, questId, System.StringComparison.Ordinal);
    }

    private bool TryCompleteActiveQuestById(string questId)
    {
        if (!IsQuestActive(questId)) return false;
        return CompleteObjective(highestUnlockedChapterIndex, GetNextActiveObjectiveIndex(highestUnlockedChapterIndex));
    }

    private void SynchronizeActiveObjectiveProgress(string questId, int observedProgress)
    {
        if (!IsQuestActive(questId) || observedProgress <= 0) return;

        int chapterIndex = highestUnlockedChapterIndex;
        int objectiveIndex = GetNextActiveObjectiveIndex(chapterIndex);
        QuestObjective objective = chapterList[chapterIndex].objectives[objectiveIndex];
        objective.targetProgress = Mathf.Max(1, objective.targetProgress);

        int synchronizedProgress = Mathf.Clamp(
            Mathf.Max(objective.currentProgress, observedProgress),
            0,
            objective.targetProgress);

        if (synchronizedProgress == objective.currentProgress) return;

        objective.currentProgress = synchronizedProgress;
        if (objective.currentProgress >= objective.targetProgress)
        {
            CompleteObjective(chapterIndex, objectiveIndex);
            return;
        }

        SaveQuestProgress();
        if (gameObject.activeInHierarchy) DisplayChapter(currentChapterIndex);
        else UpdateTrackerHUD();
    }

    private static SettlementZone FindSettlementByName(string settlementName)
    {
        if (SettlementManager.Ins != null)
        {
            SettlementZone managedZone = SettlementManager.Ins.GetZoneByName(settlementName);
            if (managedZone != null) return managedZone;
        }

        SettlementZone[] zones = FindObjectsByType<SettlementZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (SettlementZone zone in zones)
        {
            if (IsZoneNamed(zone, settlementName)) return zone;
        }

        return null;
    }

    private static bool HasCompletedBuilding(SettlementZone zone, BuildingType buildingType)
    {
        if (zone == null) return false;

        UpgradeableBuilding[] buildings = zone.GetComponentsInChildren<UpgradeableBuilding>(true);
        foreach (UpgradeableBuilding building in buildings)
        {
            if (building != null &&
                building.buildingType == buildingType &&
                !building.IsInitialBuildNeeded &&
                !building.IsUpgrading)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountStationedUnitsByResearchType(SettlementZone zone, SoldierResearchType researchType)
    {
        if (zone == null) return 0;

        int count = 0;
        UnitController[] units = FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        foreach (UnitController unit in units)
        {
            if (unit == null || unit.isDead || unit.isExpeditionMarching ||
                unit.ResearchType != researchType)
            {
                continue;
            }

            bool isStationedInZone = unit.IsStationedInZone(zone.settlementName) ||
                                    unit.GetComponentInParent<SettlementZone>() == zone;
            if (isStationedInZone) count++;
        }

        return count;
    }

    private static bool IsZoneNamed(SettlementZone zone, string expectedName)
    {
        return zone != null &&
               string.Equals(zone.settlementName, expectedName, System.StringComparison.OrdinalIgnoreCase);
    }

    public bool CompleteObjective(int chapterIndex, int objectiveIndex)
    {
        InitAllFourChapters();

        if (chapterIndex < 0 || chapterIndex >= chapterList.Count) return false;

        if (chapterIndex != highestUnlockedChapterIndex)
        {
            Debug.LogWarning($"[QUEST] Cannot complete an objective in locked Chapter {chapterIndex}.");
            return false;
        }

        var chapter = chapterList[chapterIndex];
        if (objectiveIndex < 0 || objectiveIndex >= chapter.objectives.Count) return false;

        // Hoàn thành objective tương ứng (hỗ trợ đánh dấu tuần tự chính xác)
        int activeObjectiveIndex = chapter.objectives.FindIndex(objective => !objective.isCompleted);
        if (activeObjectiveIndex != objectiveIndex)
        {
            Debug.LogWarning($"[QUEST] Objective {objectiveIndex + 1} is locked. Complete objective {activeObjectiveIndex + 1} first.");
            return false;
        }

        QuestObjective obj = chapter.objectives[objectiveIndex];
        if (obj.isCompleted) return true;

        obj.targetProgress = Mathf.Max(1, obj.targetProgress);
        obj.currentProgress = Mathf.Max(obj.currentProgress, obj.targetProgress);
        obj.isCompleted = true;
        Debug.Log($"<color=cyan>[QUEST] ✅ Đã hoàn thành nhiệm vụ {objectiveIndex + 1}: {obj.title}</color>");

        GiveReward(obj.rewardGold, obj.rewardWood, obj.rewardStone, obj.rewardWheat);
        CheckChapterProgress();
        SaveQuestProgress();

        if (gameObject.activeInHierarchy)
        {
            DisplayChapter(currentChapterIndex);
        }
        else if (QuestTrackerHUD.Instance != null && chapterList.Count > highestUnlockedChapterIndex)
        {
            var curCh = chapterList[highestUnlockedChapterIndex];
            int nextActiveIdx = curCh.objectives.FindIndex(o => !o.isCompleted);
            string nextTitle = nextActiveIdx >= 0 ? curCh.objectives[nextActiveIdx].title : "Hoàn thành toàn bộ chương!";
            QuestTrackerHUD.Instance.UpdateTrackerInfo(curCh.chapterName, nextTitle);
        }

        return true;
    }

    public bool AddObjectiveProgress(int chapterIndex, int objectiveIndex, int amount = 1)
    {
        InitAllFourChapters();

        if (amount <= 0 || chapterIndex < 0 || chapterIndex >= chapterList.Count) return false;
        if (chapterIndex != highestUnlockedChapterIndex) return false;

        ChapterData chapter = chapterList[chapterIndex];
        if (objectiveIndex < 0 || objectiveIndex >= chapter.objectives.Count) return false;

        int activeObjectiveIndex = chapter.objectives.FindIndex(objective => !objective.isCompleted);
        if (activeObjectiveIndex != objectiveIndex) return false;

        QuestObjective objective = chapter.objectives[objectiveIndex];
        objective.targetProgress = Mathf.Max(1, objective.targetProgress);
        objective.currentProgress = Mathf.Min(objective.currentProgress + amount, objective.targetProgress);

        if (objective.currentProgress < objective.targetProgress)
        {
            SaveQuestProgress();
            if (gameObject.activeInHierarchy) DisplayChapter(currentChapterIndex);
            return true;
        }

        return CompleteObjective(chapterIndex, objectiveIndex);
    }

    private void CheckChapterProgress()
    {
        for (int c = 0; c < chapterList.Count; c++)
        {
            bool allDone = true;
            foreach (var q in chapterList[c].objectives)
            {
                if (!q.isCompleted)
                {
                    allDone = false;
                    break;
                }
            }

            if (allDone && c == highestUnlockedChapterIndex && !chapterList[c].isRewardClaimed)
            {
                ChapterData doneChapter = chapterList[c];
                doneChapter.isCompleted = true;
                doneChapter.isRewardClaimed = true;
                GiveReward(doneChapter.rewardGold, doneChapter.rewardWood, doneChapter.rewardStone, 0);

                if (highestUnlockedChapterIndex < chapterList.Count - 1)
                {
                    highestUnlockedChapterIndex++;
                    currentChapterIndex = highestUnlockedChapterIndex;
                    Debug.Log($"<color=cyan>[QUEST] 🎉 Mở khóa chương mới: {chapterList[highestUnlockedChapterIndex].chapterName}</color>");
                }
            }
        }
    }

    private bool AreAllObjectivesCompletedInChapter(ChapterData chapter)
    {
        return chapter != null &&
               chapter.objectives != null &&
               chapter.objectives.Count > 0 &&
               !chapter.objectives.Exists(objective => !objective.isCompleted);
    }

    private void SaveQuestProgress()
    {
        InitAllFourChapters();
        if (chapterList == null) return;

        PlayerPrefs.SetInt(QuestProgressSaveKey, 1);
        PlayerPrefs.SetInt(HighestUnlockedChapterSaveKey, highestUnlockedChapterIndex);
        PlayerPrefs.SetInt(CurrentChapterSaveKey, currentChapterIndex);

        for (int chapterIndex = 0; chapterIndex < chapterList.Count; chapterIndex++)
        {
            ChapterData chapter = chapterList[chapterIndex];
            PlayerPrefs.SetInt(GetChapterProgressKey(chapterIndex, "Completed"), chapter.isCompleted ? 1 : 0);
            PlayerPrefs.SetInt(GetChapterProgressKey(chapterIndex, "RewardClaimed"), chapter.isRewardClaimed ? 1 : 0);

            if (chapter.objectives == null) continue;

            for (int objectiveIndex = 0; objectiveIndex < chapter.objectives.Count; objectiveIndex++)
            {
                QuestObjective objective = chapter.objectives[objectiveIndex];
                PlayerPrefs.SetInt(GetObjectiveProgressKey(chapterIndex, objectiveIndex, "Completed"), objective.isCompleted ? 1 : 0);
                PlayerPrefs.SetInt(GetObjectiveProgressKey(chapterIndex, objectiveIndex, "Current"), objective.currentProgress);
                PlayerPrefs.SetInt(GetObjectiveProgressKey(chapterIndex, objectiveIndex, "Target"), objective.targetProgress);
            }
        }

        PlayerPrefs.Save();
    }

    private void LoadQuestProgress()
    {
        InitAllFourChapters();
        if (chapterList == null || chapterList.Count == 0 || !PlayerPrefs.HasKey(QuestProgressSaveKey)) return;

        highestUnlockedChapterIndex = Mathf.Clamp(
            PlayerPrefs.GetInt(HighestUnlockedChapterSaveKey, highestUnlockedChapterIndex),
            0,
            chapterList.Count - 1);
        currentChapterIndex = Mathf.Clamp(
            PlayerPrefs.GetInt(CurrentChapterSaveKey, currentChapterIndex),
            0,
            chapterList.Count - 1);

        for (int chapterIndex = 0; chapterIndex < chapterList.Count; chapterIndex++)
        {
            ChapterData chapter = chapterList[chapterIndex];
            chapter.isCompleted = PlayerPrefs.GetInt(
                GetChapterProgressKey(chapterIndex, "Completed"),
                chapter.isCompleted ? 1 : 0) == 1;
            chapter.isRewardClaimed = PlayerPrefs.GetInt(
                GetChapterProgressKey(chapterIndex, "RewardClaimed"),
                chapter.isRewardClaimed ? 1 : 0) == 1;

            if (chapter.objectives == null) continue;

            for (int objectiveIndex = 0; objectiveIndex < chapter.objectives.Count; objectiveIndex++)
            {
                QuestObjective objective = chapter.objectives[objectiveIndex];
                objective.isCompleted = PlayerPrefs.GetInt(
                    GetObjectiveProgressKey(chapterIndex, objectiveIndex, "Completed"),
                    objective.isCompleted ? 1 : 0) == 1;
                objective.targetProgress = Mathf.Max(1, PlayerPrefs.GetInt(
                    GetObjectiveProgressKey(chapterIndex, objectiveIndex, "Target"),
                    objective.targetProgress));
                objective.currentProgress = Mathf.Clamp(PlayerPrefs.GetInt(
                    GetObjectiveProgressKey(chapterIndex, objectiveIndex, "Current"),
                    objective.currentProgress),
                    0,
                    objective.targetProgress);

                if (objective.isCompleted) objective.currentProgress = objective.targetProgress;
            }
        }
    }

    private void SynchronizePrologueProgressFromTutorial()
    {
        int completedObjectiveCount = GetCompletedPrologueObjectiveCountFromTutorial();
        if (completedObjectiveCount > 0)
        {
            SynchronizePrologueObjectivesThrough(completedObjectiveCount - 1);
        }
    }

    private int GetCompletedPrologueObjectiveCountFromTutorial()
    {
        if (PlayerPrefs.GetInt("TutorialCompleted", 0) == 1) return 6;

        DemaciaTutorialStage savedStage = (DemaciaTutorialStage)PlayerPrefs.GetInt(
            "PrologueTutorialStage",
            (int)DemaciaTutorialStage.Stage0_OpenSettlementView);

        switch (savedStage)
        {
            case DemaciaTutorialStage.Stage1_BuildWood:
            case DemaciaTutorialStage.Stage1_SkipDayWood:
                return 1;
            case DemaciaTutorialStage.Stage2_TrainGuard:
            case DemaciaTutorialStage.Stage2_SkipDayTroop:
                return 2;
            case DemaciaTutorialStage.Stage3_MarchToEnemyEast:
                return 3;
            case DemaciaTutorialStage.Stage4_AttackEnemyBattle:
                return 4;
            case DemaciaTutorialStage.Stage4_VictoryComplete:
            case DemaciaTutorialStage.Stage5_EstablishVaskasia:
            case DemaciaTutorialStage.Stage5_SkipDayTownHall:
                return 5;
            case DemaciaTutorialStage.Completed:
                return 6;
            default:
                return 0;
        }
    }

    private static string GetChapterProgressKey(int chapterIndex, string field)
    {
        return $"{QuestProgressSaveKey}_Chapter_{chapterIndex}_{field}";
    }

    private static string GetObjectiveProgressKey(int chapterIndex, int objectiveIndex, string field)
    {
        return $"{QuestProgressSaveKey}_Chapter_{chapterIndex}_Objective_{objectiveIndex}_{field}";
    }

    private void GiveReward(int gold, int wood, int stone, int wheat)
    {
        Debug.Log($"<color=green>[NHẬN THƯỞNG NHIỆM VỤ]</color> +{gold} Vàng, +{wood} Gỗ, +{stone} Đá, +{wheat} Lúa");

        if (JsonDataManager.Ins != null)
        {
            if (gold > 0) JsonDataManager.Ins.AddGold(gold);
            if (wood > 0) JsonDataManager.Ins.AddWood(wood);
            if (stone > 0) JsonDataManager.Ins.AddStone(stone);
            if (wheat > 0) JsonDataManager.Ins.AddFood(wheat);
        }
    }

    public void NextChapter()
    {
        if (currentChapterIndex < chapterList.Count - 1)
        {
            currentChapterIndex++;
            AnimateChapterChange();
        }
    }

    public void PrevChapter()
    {
        if (currentChapterIndex > 0)
        {
            currentChapterIndex--;
            AnimateChapterChange();
        }
    }
    public bool AreAllObjectivesCompleted()
    {
        InitAllFourChapters();

        if (chapterList == null || chapterList.Count == 0) return false;

        foreach (ChapterData chapter in chapterList)
        {
            if (chapter.objectives == null || chapter.objectives.Count == 0) return false;
            if (chapter.objectives.Exists(objective => !objective.isCompleted)) return false;
        }

        return true;
    }

    public void SetAllObjectivesCompletedForInspector(bool completed)
    {
        InitAllFourChapters();

        if (chapterList == null || chapterList.Count == 0) return;

        for (int chapterIndex = 0; chapterIndex < chapterList.Count; chapterIndex++)
        {
            ChapterData chapter = chapterList[chapterIndex];
            chapter.isCompleted = completed;
            chapter.isRewardClaimed = completed;

            if (chapter.objectives == null) continue;

            foreach (QuestObjective objective in chapter.objectives)
            {
                objective.targetProgress = Mathf.Max(1, objective.targetProgress);
                objective.isCompleted = completed;
                objective.currentProgress = completed ? objective.targetProgress : 0;
            }
        }

        highestUnlockedChapterIndex = completed ? chapterList.Count - 1 : 0;
        currentChapterIndex = completed ? highestUnlockedChapterIndex : 0;

        if (gameObject.activeInHierarchy) DisplayChapter(currentChapterIndex);
    }

    private int CountCompletedObjectives(ChapterData chapter)
    {
        return chapter.objectives.FindAll(objective => objective.isCompleted).Count;
    }

    private void AnimateChapterChange()
    {
        if (enableAnimations && objectiveContainer != null)
        {
            CanvasGroup cg = objectiveContainer.GetComponent<CanvasGroup>();
            if (cg == null) cg = objectiveContainer.gameObject.AddComponent<CanvasGroup>();

            DOTween.Kill(cg);
            cg.alpha = 0.3f;
            cg.DOFade(1f, 0.2f).SetUpdate(true);
        }
        DisplayChapter(currentChapterIndex);
    }

    public void CloseWindowImmediately()
    {
        CacheWindowAnimationReferences();

        if (questWindowPanel != null)
        {
            DOTween.Kill(questWindowPanel);
            questWindowPanel.anchoredPosition = windowRestPosition + Vector2.right * windowSlideDistance;
        }

        if (windowCanvasGroup != null)
        {
            DOTween.Kill(windowCanvasGroup);
            windowCanvasGroup.alpha = 0f;
            windowCanvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
    }

    private void CacheWindowAnimationReferences()
    {
        if (questWindowPanel == null)
        {
            questWindowPanel = transform.Find("QuestWindow") as RectTransform;
        }

        if (questWindowPanel != null && !hasWindowRestPosition)
        {
            windowRestPosition = questWindowPanel.anchoredPosition;
            hasWindowRestPosition = true;
        }

        if (windowCanvasGroup == null)
        {
            windowCanvasGroup = GetComponent<CanvasGroup>();
            if (windowCanvasGroup == null) windowCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void RestoreWindowState()
    {
        if (questWindowPanel != null && hasWindowRestPosition)
        {
            questWindowPanel.anchoredPosition = windowRestPosition;
        }

        if (windowCanvasGroup != null)
        {
            windowCanvasGroup.alpha = 1f;
            windowCanvasGroup.blocksRaycasts = true;
        }
    }

    public void OpenWindow()
    {
        gameObject.SetActive(true);
        CacheWindowAnimationReferences();
        DisplayChapter(currentChapterIndex);

        if (QuestTrackerHUD.Instance != null)
        {
            QuestTrackerHUD.Instance.HideTracker();
        }

        if (enableAnimations && questWindowPanel != null && windowCanvasGroup != null)
        {
            DOTween.Kill(questWindowPanel);
            DOTween.Kill(windowCanvasGroup);

            questWindowPanel.anchoredPosition = windowRestPosition + Vector2.right * windowSlideDistance;
            windowCanvasGroup.alpha = 0f;
            windowCanvasGroup.blocksRaycasts = false;

            Sequence seq = DOTween.Sequence();
            seq.SetUpdate(true);
            seq.Append(questWindowPanel.DOAnchorPos(windowRestPosition, unfoldDuration).SetEase(openEase));
            seq.Join(windowCanvasGroup.DOFade(1f, unfoldDuration * 0.8f));
            seq.OnComplete(() => windowCanvasGroup.blocksRaycasts = true);
        }
        else
        {
            RestoreWindowState();
        }
    }

    public void CloseWindow()
    {
        CacheWindowAnimationReferences();

        if (enableAnimations && gameObject.activeInHierarchy && questWindowPanel != null && windowCanvasGroup != null)
        {
            DOTween.Kill(questWindowPanel);
            DOTween.Kill(windowCanvasGroup);

            windowCanvasGroup.blocksRaycasts = false;

            Sequence seq = DOTween.Sequence();
            seq.SetUpdate(true);
            seq.Append(questWindowPanel.DOAnchorPos(windowRestPosition + Vector2.right * windowSlideDistance, unfoldDuration * 0.8f).SetEase(closeEase));
            seq.Join(windowCanvasGroup.DOFade(0f, unfoldDuration * 0.7f));
            seq.OnComplete(() =>
            {
                gameObject.SetActive(false);
                RestoreWindowState();

                if (QuestTrackerHUD.Instance != null)
                {
                    QuestTrackerHUD.Instance.ShowTracker();
                }
            });
        }
        else
        {
            gameObject.SetActive(false);
            RestoreWindowState();
            if (QuestTrackerHUD.Instance != null) QuestTrackerHUD.Instance.ShowTracker();
        }
    }
}
