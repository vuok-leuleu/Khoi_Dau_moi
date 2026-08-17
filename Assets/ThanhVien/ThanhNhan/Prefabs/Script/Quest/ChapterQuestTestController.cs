using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class ChapterQuestController : MonoBehaviour
{
    public static ChapterQuestController Instance { get; private set; }

    [Header("--- 1. TOP HEADER (ĐIỀU HƯỚNG CHƯƠNG) ---")]
    [SerializeField] private TextMeshProUGUI chapterTitleText;
    [SerializeField] private Button prevChapterBtn;
    [SerializeField] private Button nextChapterBtn;

    [Header("--- 2. CỤM TÀI NGUYÊN THƯỞNG (REWARD HEADER) ---")]
    [SerializeField] private GameObject baseResourcesContainer;
    [SerializeField] private TextMeshProUGUI goldAmountText;
    [SerializeField] private TextMeshProUGUI woodAmountText;
    [SerializeField] private TextMeshProUGUI stoneAmountText;

    [Header("--- 3. PHẦN THƯỞNG KHÓA (KeyRewardCard) ---")]
    [SerializeField] private Image keyRewardIcon;
    [SerializeField] private TextMeshProUGUI keyRewardNameText;

    [Header("--- 4. VÙNG DANH SÁCH MỤC TIÊU (ContentArea) ---")]
    [SerializeField] private Transform objectiveContainer; // Viewport -> Content
    [SerializeField] private GameObject objectiveItemPrefab; // Prefab dòng nhiệm vụ

    [Header("--- 5. NÚT ĐÓNG ---")]
    [SerializeField] private Button returnButton;

    [Header("✨ ANIMATION CONFIG")]
    [SerializeField] private bool enableAnimations = true;
    [SerializeField] private float unfoldDuration = 0.32f;
    [SerializeField] private Ease openEase = Ease.OutCubic;
    [SerializeField] private Ease closeEase = Ease.InCubic;

    [Header("--- 6. DỮ LIỆU 4 CHƯƠNG ---")]
    [SerializeField] private List<ChapterData> chapterList = new List<ChapterData>();
    private int currentChapterIndex = 0;
    private int highestUnlockedChapterIndex = 0;

    // Bảng màu chuẩn
    private readonly string colorActiveQuest = "#4A2E18";   // Nâu đậm nổi bật
    private readonly string colorQuestionMark = "#5C4A38";  // Nâu xám tiệp màu chữ
    private readonly string colorCompleted = "#8A817A";     // Nâu xám mờ

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        InitAllFourChapters();
    }

    private void Start()
    {
        if (prevChapterBtn != null) prevChapterBtn.onClick.AddListener(PrevChapter);
        if (nextChapterBtn != null) nextChapterBtn.onClick.AddListener(NextChapter);
        if (returnButton != null) returnButton.onClick.AddListener(CloseWindow);

        InitAllFourChapters();
        DisplayChapter(currentChapterIndex);
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

        // 1. Cập nhật Tiêu Đề Chương
        if (chapterTitleText != null)
        {
            chapterTitleText.text = currentChapter.chapterName;
        }

        // 2. Cập nhật nút chuyển Chương (Prev / Next)
        if (prevChapterBtn != null) prevChapterBtn.interactable = (currentChapterIndex > 0);
        if (nextChapterBtn != null) nextChapterBtn.interactable = (currentChapterIndex < highestUnlockedChapterIndex && currentChapterIndex < chapterList.Count - 1);

        // 3. Cập nhật Card Phần Thưởng Khóa (Key Reward)
        if (keyRewardNameText != null) keyRewardNameText.text = currentChapter.keyRewardName;
        if (keyRewardIcon != null && currentChapter.keyRewardIcon != null)
        {
            keyRewardIcon.sprite = currentChapter.keyRewardIcon;
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

        bool isFutureLockedChapter = (currentChapterIndex > highestUnlockedChapterIndex);
        bool foundActiveQuest = false;
        string activeQuestTitle = "";

        int displayRewardGold = 0;
        int displayRewardWood = 0;
        int displayRewardStone = 0;

        for (int i = 0; i < chapter.objectives.Count; i++)
        {
            QuestObjective obj = chapter.objectives[i];
            GameObject item = Instantiate(objectiveItemPrefab, objectiveContainer);
            TextMeshProUGUI itemText = item.GetComponentInChildren<TextMeshProUGUI>();

            if (itemText == null) continue;

            string cleanTitle = obj.title.Replace("✦", "").Replace("□", "").Trim();

            if (isFutureLockedChapter)
            {
                itemText.text = $"<color={colorQuestionMark}>???</color>";
            }
            else if (obj.isCompleted)
            {
                itemText.text = $"<color={colorCompleted}><s>{cleanTitle}</s></color>";
            }
            else
            {
                if (!foundActiveQuest)
                {
                    itemText.text = $"<color={colorActiveQuest}>✦ {cleanTitle}</color>";
                    foundActiveQuest = true;
                    activeQuestTitle = cleanTitle;

                    displayRewardGold = obj.rewardGold;
                    displayRewardWood = obj.rewardWood;
                    displayRewardStone = obj.rewardStone;
                }
                else
                {
                    itemText.text = $"<color={colorQuestionMark}>□ {cleanTitle}</color>";
                }
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
        QuestObjective obj = chapter.objectives[objectiveIndex];
        if (obj.isCompleted) return true;

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

            if (allDone && c == highestUnlockedChapterIndex && highestUnlockedChapterIndex < chapterList.Count - 1)
            {
                ChapterData doneChapter = chapterList[c];
                GiveReward(doneChapter.rewardGold, doneChapter.rewardWood, doneChapter.rewardStone, 0);

                highestUnlockedChapterIndex++;
                currentChapterIndex = highestUnlockedChapterIndex;
                Debug.Log($"<color=cyan>[QUEST] 🎉 Mở khóa chương mới: {chapterList[highestUnlockedChapterIndex].chapterName}</color>");
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

    public void OpenWindow()
    {
        gameObject.SetActive(true);
        DisplayChapter(currentChapterIndex);

        if (QuestTrackerHUD.Instance != null)
        {
            QuestTrackerHUD.Instance.gameObject.SetActive(false);
        }

        if (enableAnimations)
        {
            RectTransform rect = GetComponent<RectTransform>();
            CanvasGroup cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

            DOTween.Kill(rect);
            DOTween.Kill(cg);

            if (rect != null)
            {
                rect.pivot = new Vector2(rect.pivot.x, 1f);
                rect.localScale = new Vector3(1f, 0.05f, 1f);
                cg.alpha = 0f;

                Sequence seq = DOTween.Sequence();
                seq.SetUpdate(true);
                seq.Append(rect.DOScaleY(1f, unfoldDuration).SetEase(openEase));
                seq.Join(cg.DOFade(1f, unfoldDuration * 0.7f));
            }
        }
    }

    public void CloseWindow()
    {
        if (enableAnimations && gameObject.activeInHierarchy)
        {
            RectTransform rect = GetComponent<RectTransform>();
            CanvasGroup cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = GetComponent<CanvasGroup>();

            if (rect != null && cg != null)
            {
                DOTween.Kill(rect);
                DOTween.Kill(cg);

                Sequence seq = DOTween.Sequence();
                seq.SetUpdate(true);
                seq.Append(rect.DOScaleY(0.05f, unfoldDuration * 0.8f).SetEase(closeEase));
                seq.Join(cg.DOFade(0f, unfoldDuration * 0.7f));
                seq.OnComplete(() =>
                {
                    gameObject.SetActive(false);
                    rect.localScale = Vector3.one;
                    cg.alpha = 1f;

                    if (QuestTrackerHUD.Instance != null)
                    {
                        QuestTrackerHUD.Instance.ShowTracker();
                    }
                });
            }
            else
            {
                gameObject.SetActive(false);
                if (QuestTrackerHUD.Instance != null) QuestTrackerHUD.Instance.ShowTracker();
            }
        }
        else
        {
            gameObject.SetActive(false);
            if (QuestTrackerHUD.Instance != null) QuestTrackerHUD.Instance.ShowTracker();
        }
    }
}
