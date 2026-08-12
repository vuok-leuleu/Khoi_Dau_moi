using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestHUDTracker : MonoBehaviour
{
    public static QuestHUDTracker Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI chapterTitleText;
    [SerializeField] private TextMeshProUGUI objectiveText;
    [SerializeField] private Button arrowButton;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Bấm nút Mũi tên -> Mở bảng Quest Log lớn ở Canvas kia
        if (arrowButton != null)
        {
            arrowButton.onClick.AddListener(() =>
            {
                if (QuestUIController.Instance != null)
                {
                    QuestUIController.Instance.OpenWindow();
                }
            });
        }

        UpdateHUD();
    }

    /// <summary>
    /// Cập nhật nội dung nhiệm vụ hiện tại lên HUD
    /// </summary>
    public void UpdateHUD()
    {
        if (QuestUIController.Instance == null) return;

        string currentChapterStr = GetChapterName(QuestUIController.Instance.CurrentTab);
        QuestDataDemo activeQuest = QuestUIController.Instance.GetFirstActiveQuest();

        if (chapterTitleText != null)
        {
            chapterTitleText.text = currentChapterStr;
        }

        if (objectiveText != null)
        {
            if (activeQuest != null)
            {
                objectiveText.text = activeQuest.description;
            }
            else
            {
                objectiveText.text = "Đã hoàn thành tất cả nhiệm vụ!";
            }
        }
    }

    private string GetChapterName(QuestType type)
    {
        switch (type)
        {
            case QuestType.Chapter1: return "Chapter One";
            case QuestType.Chapter2: return "Chapter Two";
            case QuestType.Chapter3: return "Chapter Three";
            default: return "Daily Quests";
        }
    }
}