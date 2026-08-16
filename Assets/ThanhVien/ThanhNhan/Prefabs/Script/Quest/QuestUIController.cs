using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum QuestType
{
    SideQuest,      // Tab 1: Nhiệm vụ Phụ
    Daily,          // Tab 2: Nhiệm vụ Hằng Ngày
    Weekly,         // Tab 3: Nhiệm vụ Hằng Tuần
    Achievement     // Tab 4: Thành Tựu / Thách Thức
}

public enum RewardType
{
    Gold,           // Vàng
    Wood,           // Gỗ
    Stone,          // Đá
    Food,           // Lúa / Thức ăn
    Exp,            // Kinh nghiệm
    Gem,            // Đá quý
    Valor           // Dũng khí
}

[System.Serializable]
public class QuestReward
{
    public RewardType rewardType;
    public Sprite customIcon;
    public int amount;
}

[System.Serializable]
public class QuestDataDemo
{
    public string questID;
    public QuestType questType;
    public Sprite icon;
    public string title;
    public string description;
    public int currentProgress;
    public int maxProgress;
    public bool isClaimed;

    public List<QuestReward> rewards = new List<QuestReward>();
}

public class QuestUIController : MonoBehaviour
{
    public static QuestUIController Instance { get; private set; }

    [Header("⚔️ SIDE QUEST & EVENT HEADER")]
    [SerializeField] private string actTitle = "DANH SÁCH NHIỆM VỤ PHỤ & THÁCH THỨC";
    [SerializeField] private TextMeshProUGUI actTitleText;
    [SerializeField] private TextMeshProUGUI goldProgressText;
    [SerializeField] private Slider actProgressBar;
    [SerializeField] private int totalGoldEarned = 0;
    [SerializeField] private int maxActGold = 3000;

    [Header("UI References")]
    [SerializeField] private GameObject windowPanel;   // Bảng giao diện chính cần Ẩn/Hiện (nếu bỏ trống sẽ dùng gameObject này)
    [SerializeField] private Transform contentArea;
    [SerializeField] private GameObject questItemPrefab;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backgroundOverlayButton;
    [SerializeField] private Button openQuestButton;

    [Header("Notification")]
    [SerializeField] private GameObject notificationIcon;

    [Header("HotKey Config")]
    [SerializeField] private KeyCode toggleHotkey = KeyCode.L;

    [Header("Tab Buttons (4 Tabs)")]
    [SerializeField] private Button tabSideQuestBtn;   // Nút Tab 1: Nhiệm vụ Phụ
    [SerializeField] private Button tabDailyBtn;       // Nút Tab 2: Nhiệm vụ Hằng Ngày
    [SerializeField] private Button tabWeeklyBtn;      // Nút Tab 3: Nhiệm vụ Hằng Tuần
    [SerializeField] private Button tabAchievementBtn; // Nút Tab 4: Thành Tựu

    [Header("Data List")]
    [SerializeField] private List<QuestDataDemo> questList = new List<QuestDataDemo>();

    [Header("🛠️ DEBUG / TEST SETTINGS")]
    [SerializeField] private bool enableDebugHotkeys = true;
    [SerializeField] private KeyCode addProgressHotkey = KeyCode.T;
    [SerializeField] private KeyCode completeAllHotkey = KeyCode.Y;
    [SerializeField] private KeyCode resetAllHotkey = KeyCode.R;

    private QuestType currentTab = QuestType.SideQuest;

    public int TotalGoldEarned => totalGoldEarned;
    public QuestType CurrentTab => currentTab;
    public bool IsWindowOpen => windowPanel != null ? windowPanel.activeSelf : gameObject.activeSelf;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (closeButton != null) closeButton.onClick.AddListener(CloseWindow);
        if (backgroundOverlayButton != null) backgroundOverlayButton.onClick.AddListener(CloseWindow);
        if (openQuestButton != null) openQuestButton.onClick.AddListener(ToggleWindow);

        if (tabSideQuestBtn != null) tabSideQuestBtn.onClick.AddListener(() => SwitchTab(QuestType.SideQuest));
        if (tabDailyBtn != null) tabDailyBtn.onClick.AddListener(() => SwitchTab(QuestType.Daily));
        if (tabWeeklyBtn != null) tabWeeklyBtn.onClick.AddListener(() => SwitchTab(QuestType.Weekly));
        if (tabAchievementBtn != null) tabAchievementBtn.onClick.AddListener(() => SwitchTab(QuestType.Achievement));

        InitDemoSideQuests();
        UpdateEventProgressUI();
        SwitchTab(QuestType.SideQuest);
        CheckNotification();

        // Ẩn windowPanel khi vừa khởi chạy nếu được gán
        if (windowPanel != null)
        {
            windowPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleHotkey)) ToggleWindow();

        if (enableDebugHotkeys)
        {
            if (Input.GetKeyDown(addProgressHotkey)) Test_AddProgress();
            if (Input.GetKeyDown(completeAllHotkey)) Test_CompleteAll();
            if (Input.GetKeyDown(resetAllHotkey)) Test_ResetAll();
        }
    }

    public void ToggleWindow()
    {
        if (IsWindowOpen) CloseWindow();
        else OpenWindow();
    }

    public void SwitchTab(QuestType newTab)
    {
        currentTab = newTab;
        RefreshQuestList();
    }

    public void UpdateEventProgressUI()
    {
        if (actTitleText != null) actTitleText.text = actTitle;

        if (goldProgressText != null) goldProgressText.text = $"{totalGoldEarned} / {maxActGold} Vàng";

        if (actProgressBar != null)
        {
            actProgressBar.maxValue = maxActGold;
            actProgressBar.value = totalGoldEarned;
        }
    }

    public void RefreshQuestList()
    {
        if (contentArea == null || questItemPrefab == null) return;

        foreach (Transform child in contentArea)
        {
            if (child.gameObject == questItemPrefab)
            {
                child.gameObject.SetActive(false);
                continue;
            }
            Destroy(child.gameObject);
        }

        foreach (var quest in questList)
        {
            if (quest.isClaimed || quest.questType != currentTab) continue;

            GameObject cardObj = Instantiate(questItemPrefab, contentArea);
            cardObj.SetActive(true);
            cardObj.transform.localScale = Vector3.one;

            CanvasGroup cg = cardObj.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;

            QuestItemUI itemUI = cardObj.GetComponent<QuestItemUI>();

            if (itemUI != null)
            {
                itemUI.SetupQuest(
                    quest.icon,
                    quest.title,
                    quest.description,
                    quest.currentProgress,
                    quest.maxProgress,
                    quest.rewards,
                    quest.isClaimed,
                    () => OnClaimReward(quest)
                );
            }
        }

        CheckNotification();
    }

    private void OnClaimReward(QuestDataDemo quest)
    {
        quest.isClaimed = true;

        foreach (var reward in quest.rewards)
        {
            if (reward.rewardType == RewardType.Gold)
            {
                totalGoldEarned += reward.amount;
            }

            if (JsonDataManager.Ins != null)
            {
                switch (reward.rewardType)
                {
                    case RewardType.Wood:  JsonDataManager.Ins.AddWood(reward.amount); break;
                    case RewardType.Stone: JsonDataManager.Ins.AddStone(reward.amount); break;
                    case RewardType.Food:  JsonDataManager.Ins.AddFood(reward.amount); break;
                }
            }
        }

        UpdateEventProgressUI();
        RefreshQuestList();
    }

    public void AddQuestProgress(string questID, int amount)
    {
        foreach (var quest in questList)
        {
            if (!quest.isClaimed && quest.questID == questID)
            {
                quest.currentProgress = Mathf.Min(quest.currentProgress + amount, quest.maxProgress);

                if (gameObject.activeSelf)
                    RefreshQuestList();
                else
                {
                    CheckNotification();
                }

                break;
            }
        }
    }

    public void CheckNotification()
    {
        if (notificationIcon == null) return;

        bool hasClaimableQuest = false;
        foreach (var quest in questList)
        {
            if (!quest.isClaimed && quest.currentProgress >= quest.maxProgress)
            {
                hasClaimableQuest = true;
                break;
            }
        }

        notificationIcon.SetActive(hasClaimableQuest);
    }

    public void OpenWindow()
    {
        if (windowPanel != null) windowPanel.SetActive(true);
        else gameObject.SetActive(true);

        UpdateEventProgressUI();
        RefreshQuestList();
    }

    public void CloseWindow()
    {
        if (windowPanel != null) windowPanel.SetActive(false);
        else gameObject.SetActive(false);

        CheckNotification();
    }

    private void InitDemoSideQuests()
    {
        if (questList.Count > 0) return;

        // 1. Nhiệm vụ Phụ (SideQuest)
        questList.Add(new QuestDataDemo
        {
            questID = "side_farm",
            questType = QuestType.SideQuest,
            title = "[Nhiệm vụ Phụ] Xây Nông Trại Phụ",
            description = "Mở rộng vùng canh tác để tăng thêm sản lượng Lúa Mì.",
            currentProgress = 0,
            maxProgress = 1,
            isClaimed = false,
            rewards = new List<QuestReward>
            {
                new QuestReward { rewardType = RewardType.Gold, amount = 100 },
                new QuestReward { rewardType = RewardType.Food, amount = 50 }
            }
        });

        // 2. Nhiệm vụ Hằng Ngày (Daily)
        questList.Add(new QuestDataDemo
        {
            questID = "daily_wood",
            questType = QuestType.Daily,
            title = "[Hằng Ngày] Thu Thập Gỗ",
            description = "Chặt các cây xung quanh căn cứ để lấy nguyên liệu.",
            currentProgress = 0,
            maxProgress = 100,
            isClaimed = false,
            rewards = new List<QuestReward>
            {
                new QuestReward { rewardType = RewardType.Gold, amount = 50 },
                new QuestReward { rewardType = RewardType.Exp, amount = 25 }
            }
        });

        // 3. Nhiệm vụ Hằng Tuần (Weekly)
        questList.Add(new QuestDataDemo
        {
            questID = "weekly_monster",
            questType = QuestType.Weekly,
            title = "[Hằng Tuần] Quét Sạch Đàn Sói",
            description = "Tiêu diệt 3 đợt quái hoang dã bảo vệ vùng biên giới.",
            currentProgress = 0,
            maxProgress = 3,
            isClaimed = false,
            rewards = new List<QuestReward>
            {
                new QuestReward { rewardType = RewardType.Gold, amount = 300 },
                new QuestReward { rewardType = RewardType.Gem, amount = 10 }
            }
        });
    }

    // DEBUG HOTKEYS (T / Y / R)
    public void Test_AddProgress()
    {
        if (questList.Count > 0)
        {
            var q = questList[0];
            if (q.currentProgress < q.maxProgress) q.currentProgress++;
            RefreshQuestList();
        }
    }

    public void Test_CompleteAll()
    {
        foreach (var q in questList) q.currentProgress = q.maxProgress;
        RefreshQuestList();
    }

    public void Test_ResetAll()
    {
        totalGoldEarned = 0;
        foreach (var q in questList)
        {
            q.currentProgress = 0;
            q.isClaimed = false;
        }
        UpdateEventProgressUI();
        RefreshQuestList();
    }
}