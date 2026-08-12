using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum QuestType
{
    Chapter1,       // Tab 1: Chương 1
    Chapter2,       // Tab 2: Chương 2
    Chapter3,       // Tab 3: Chương 3
    Daily,          // Tab 4: Nhiệm vụ Hằng Ngày

    // Giữ tương thích ngược với Unity Inspector cũ
    ActStory = Chapter1,
    MainQuest = Chapter1,
    Combat = Chapter2,
    Settings = Chapter3,
    Weekly = Chapter3,
    Achievement = Daily
}

public enum RewardType
{
    Gold,           // Vàng
    Wood,           // Gỗ
    Stone,          // Đá
    Food,           // Lúa / Thức ăn
    Exp,            // Kinh nghiệm
    Gem,            // Đá quý
    Valor,          // Dũng khí
    SilverShield = Gold
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

[System.Serializable]
public class EventMilestoneData
{
    public string milestoneID;
    public string title;
    public int requiredGold;
    public bool isClaimed;
    public List<QuestReward> rewards = new List<QuestReward>();
}

public class QuestUIController : MonoBehaviour
{
    public static QuestUIController Instance { get; private set; }

    [Header("⚔️ DEMACIA RISING EVENT HEADER")]
    [SerializeField] private string actTitle = "HỒI 1: KHỞI NGUYÊN DEMACIA";
    [SerializeField] private TextMeshProUGUI actTitleText;
    [SerializeField] private TextMeshProUGUI goldProgressText;
    [SerializeField] private Slider actProgressBar;
    [SerializeField] private int totalGoldEarned = 0;
    [SerializeField] private int maxActGold = 3000;
    [SerializeField] private List<EventMilestoneData> eventMilestones = new List<EventMilestoneData>();

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
    [SerializeField] private KeyCode toggleHotkey = KeyCode.Q;

    [Header("Tab Buttons")]
    [SerializeField] private Button tabChapter1Btn;    // Nút Tab 1: Chương 1
    [SerializeField] private Button tabChapter2Btn;    // Nút Tab 2: Chương 2
    [SerializeField] private Button tabChapter3Btn;    // Nút Tab 3: Chương 3
    [SerializeField] private Button tabDailyBtn;       // Nút Tab 4: Nhiệm vụ Hằng Ngày

    // Aliases tương thích Unity Inspector cũ
    [SerializeField] private Button tabQuestBtn;       
    [SerializeField] private Button tabCombatBtn;      
    [SerializeField] private Button tabSettingsBtn;    
    [SerializeField] private Button tabAchievementBtn; 

    [Header("Data List")]
    [SerializeField] private List<QuestDataDemo> questList = new List<QuestDataDemo>();

    [Header("🛠️ DEBUG / TEST SETTINGS")]
    [SerializeField] private bool enableDebugHotkeys = true;
    [SerializeField] private KeyCode addProgressHotkey = KeyCode.T;
    [SerializeField] private KeyCode completeAllHotkey = KeyCode.Y;
    [SerializeField] private KeyCode resetAllHotkey = KeyCode.R;

    private QuestType currentTab = QuestType.Chapter1;

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

        Button b1 = tabChapter1Btn != null ? tabChapter1Btn : tabQuestBtn;
        Button b2 = tabChapter2Btn != null ? tabChapter2Btn : tabCombatBtn;
        Button b3 = tabChapter3Btn != null ? tabChapter3Btn : tabSettingsBtn;
        Button b4 = tabDailyBtn != null ? tabDailyBtn : tabAchievementBtn;

        if (b1 != null) b1.onClick.AddListener(() => SwitchTab(QuestType.Chapter1));
        if (b2 != null) b2.onClick.AddListener(() => SwitchTab(QuestType.Chapter2));
        if (b3 != null) b3.onClick.AddListener(() => SwitchTab(QuestType.Chapter3));
        if (b4 != null) b4.onClick.AddListener(() => SwitchTab(QuestType.Daily));

        InitDemoDemaciaQuests();
        UpdateEventProgressUI();
        SwitchTab(QuestType.Chapter1);
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
            Destroy(child.gameObject);
        }

        foreach (var quest in questList)
        {
            if (quest.isClaimed || quest.questType != currentTab) continue;

            GameObject cardObj = Instantiate(questItemPrefab, contentArea);
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

        // Đồng bộ với HUD Canvas ngoài màn hình
        if (QuestHUDTracker.Instance != null)
        {
            QuestHUDTracker.Instance.UpdateHUD();
        }
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
                    if (QuestHUDTracker.Instance != null) QuestHUDTracker.Instance.UpdateHUD();
                }

                break;
            }
        }
    }

    public QuestDataDemo GetFirstActiveQuest()
    {
        foreach (var quest in questList)
        {
            if (!quest.isClaimed && quest.questType == currentTab)
            {
                return quest;
            }
        }
        return null;
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
        gameObject.SetActive(true);
        UpdateEventProgressUI();
        RefreshQuestList();
    }

    public void CloseWindow()
    {
        gameObject.SetActive(false);
        CheckNotification();
    }

    private void InitDemoDemaciaQuests()
    {
        if (questList.Count > 0) return;

        questList.Add(new QuestDataDemo
        {
            questID = "ch1_farm",
            questType = QuestType.Chapter1,
            title = "[Chương 1] Xây Nông Trại Zeffira",
            description = "Establish a new Settlement near Vaskasia.",
            currentProgress = 0,
            maxProgress = 1,
            isClaimed = false,
            rewards = new List<QuestReward>
            {
                new QuestReward { rewardType = RewardType.Gold, amount = 150 },
                new QuestReward { rewardType = RewardType.Food, amount = 50 }
            }
        });

        questList.Add(new QuestDataDemo
        {
            questID = "ch1_research",
            questType = QuestType.Chapter1,
            title = "[Chương 1] Nghiên Cứu Cung Thủ",
            description = "Mở khóa đơn vị quân Cung Thủ (Archers) trong bảng Nghiên Cứu.",
            currentProgress = 0,
            maxProgress = 1,
            isClaimed = false,
            rewards = new List<QuestReward>
            {
                new QuestReward { rewardType = RewardType.Gold, amount = 150 },
                new QuestReward { rewardType = RewardType.Wood, amount = 50 }
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