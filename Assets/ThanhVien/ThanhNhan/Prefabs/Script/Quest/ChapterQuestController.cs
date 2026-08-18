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

    // Bảng màu chuẩn
    private readonly string colorActiveQuest = "#4A2E18";   // Nâu đậm nổi bật
    private readonly string colorQuestionMark = "#5C4A38";  // Nâu xám tiệp màu chữ
    private readonly string colorCompleted = "#8A817A";     // Nâu xám mờ

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        InitAllFourChapters();
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

        UpdateTrackerHUD();
        CloseWindowImmediately();
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
                new QuestObjective { title = "Bấm vào Zeffira để mở Settlement View", isCompleted = false, rewardGold = 0, rewardWood = 10, rewardStone = 0 },
                new QuestObjective { title = "Xây dựng Xưởng Gỗ tại Zeffira", isCompleted = false, rewardGold = 0, rewardWood = 20, rewardStone = 0 },
                new QuestObjective { title = "Huấn luyện 1 Hộ Vệ (Guard) tại Zeffira", isCompleted = false, rewardGold = 20, rewardWood = 0, rewardStone = 0 },
                new QuestObjective { title = "Di chuyển quân đến lãnh thổ địch phía Đông Zeffira", isCompleted = false, rewardGold = 30, rewardWood = 0, rewardStone = 0 },
                new QuestObjective { title = "Chuẩn bị và giành chiến thắng trong trận đánh đầu", isCompleted = false, rewardGold = 50, rewardWood = 20, rewardStone = 0 },
                new QuestObjective { title = "Xây dựng Vaskasia trên vùng đất trống", isCompleted = false, rewardGold = 0, rewardWood = 30, rewardStone = 20 }
            }
        };
        chapterList.Add(prologue);

        // 1. CHƯƠNG I
        ChapterData ch1 = new ChapterData
        {
            chapterName = "Chương I: Khai Nguyên",
            rewardGold = 200,
            rewardWood = 200,
            rewardStone = 200,
            keyRewardName = "Honorary Crownguard",
            objectives = new List<QuestObjective>
            {
                new QuestObjective { title = "Xây Nông Trại Zeffira", isCompleted = false, rewardGold = 100, rewardWood = 150, rewardStone = 0 },
                new QuestObjective { title = "Nghiên cứu Cung Thủ tại Viện Binh", isCompleted = false, rewardGold = 100, rewardWood = 0, rewardStone = 100 },
                new QuestObjective { title = "Chiêu mộ 5 Cung Thủ bảo vệ doanh trại", isCompleted = false, rewardGold = 150, rewardWood = 100, rewardStone = 0 },
                new QuestObjective { title = "Tiêu diệt 3 bãi quái thú biên giới", isCompleted = false, rewardGold = 150, rewardWood = 0, rewardStone = 100 },
                new QuestObjective { title = "Nâng cấp Nhà Chính Zeffira lên Cấp 2", isCompleted = false, rewardGold = 300, rewardWood = 200, rewardStone = 200 }
            }
        };
        chapterList.Add(ch1);

        // 2. CHƯƠNG II
        ChapterData ch2 = new ChapterData
        {
            chapterName = "Chương II: Vực Sâu Vaskasia",
            rewardGold = 500,
            rewardWood = 400,
            rewardStone = 300,
            keyRewardName = "Vanguard Protector",
            objectives = new List<QuestObjective>
            {
                new QuestObjective { title = "Mở rộng lãnh thổ sang mỏ đá Vaskasia", isCompleted = false, rewardGold = 200, rewardWood = 200, rewardStone = 0 },
                new QuestObjective { title = "Xây dựng Mỏ Khai Thác Đá Vaskasia", isCompleted = false, rewardGold = 200, rewardWood = 0, rewardStone = 250 },
                new QuestObjective { title = "Xây dựng 2 Tháp Canh Phòng Thủ", isCompleted = false, rewardGold = 250, rewardWood = 250, rewardStone = 0 },
                new QuestObjective { title = "Đẩy lùi 3 đợt quái vật xâm lăng biên cương", isCompleted = false, rewardGold = 300, rewardWood = 0, rewardStone = 200 }
            }
        };
        chapterList.Add(ch2);

        // 3. CHƯƠNG III
        ChapterData ch3 = new ChapterData
        {
            chapterName = "Chương III: Vùng Đất Terbisia",
            rewardGold = 1000,
            rewardWood = 800,
            rewardStone = 600,
            keyRewardName = "Demacia Commander",
            objectives = new List<QuestObjective>
            {
                new QuestObjective { title = "Khai phá toàn bộ vùng sương mù Terbisia", isCompleted = false, rewardGold = 300, rewardWood = 300, rewardStone = 0 },
                new QuestObjective { title = "Xây dựng Trại Rèn & Nâng cấp Giáp Hoàng Gia", isCompleted = false, rewardGold = 350, rewardWood = 0, rewardStone = 300 },
                new QuestObjective { title = "Nâng cấp Nhà Chính Zeffira lên Cấp 3 (Thành Trì)", isCompleted = false, rewardGold = 500, rewardWood = 500, rewardStone = 500 }
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
                else itemText.text = $"<color={colorQuestionMark}>{cleanTitle}</color>";
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
