using UnityEngine;
using UnityEngine.UI;

public class QuestUIManager : MonoBehaviour
{
    public static QuestUIManager Instance { get; private set; }

    [Header("--- 1. TRACKER NHỎ (HUD GÓC PHẢI) ---")]
    [Tooltip("GameObject của bảng Tracker nhỏ trên màn hình chính")]
    [SerializeField] private GameObject questTrackerHUD;
    [Tooltip("Nút bấm trên Tracker để mở bảng Chapter (hoặc cả khung tracker)")]
    [SerializeField] private Button trackerOpenBtn;

    [Header("--- 2. BẢNG DỌC CỐT TRUYỆN (CHAPTER WINDOW) ---")]
    [Tooltip("GameObject/Canvas của bảng Chapter dọc bên phải")]
    [SerializeField] private GameObject chapterVerticalWindow;
    [Tooltip("Nút Return ở đáy bảng Dọc")]
    [SerializeField] private Button chapterReturnBtn;

    [Header("--- 3. BẢNG NGANG SỔ TAY (HORIZONTAL LOG) ---")]
    [Tooltip("GameObject/Canvas của bảng Ngang ở giữa")]
    [SerializeField] private GameObject horizontalQuestLog;
    [Tooltip("Nút icon Cuộn Giấy ở thanh Toolbar đáy")]
    [SerializeField] private Button openJournalToolbarBtn;
    [Tooltip("Nút đóng [X] màu đỏ trên bảng Ngang")]
    [SerializeField] private Button closeJournalBtn;

    [Header("--- 4. PHÍM TẮT (HOTKEYS) ---")]
    [SerializeField] private KeyCode toggleChapterKey = KeyCode.Q;
    [SerializeField] private KeyCode toggleJournalKey = KeyCode.L;
    [SerializeField] private KeyCode closeAllKey = KeyCode.Escape;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // 1. Gán sự kiện cho các nút bấm
        if (trackerOpenBtn != null) trackerOpenBtn.onClick.AddListener(OpenChapterPanel);
        if (chapterReturnBtn != null) chapterReturnBtn.onClick.AddListener(CloseChapterPanel);
        if (openJournalToolbarBtn != null) openJournalToolbarBtn.onClick.AddListener(OpenJournalPanel);
        if (closeJournalBtn != null) closeJournalBtn.onClick.AddListener(CloseJournalPanel);

        // 2. Thiết lập trạng thái mặc định khi bắt đầu vào game:
        // Chỉ hiện Tracker nhỏ, ẩn 2 bảng lớn
        SetDefaultHUDState();
    }

    private void Update()
    {
        HandleHotkeys();
    }

    private void HandleHotkeys()
    {
        // Phím ESC: Đóng Bảng Main Quest đang mở
        if (Input.GetKeyDown(closeAllKey))
        {
            if (ChapterQuestController.Instance != null && ChapterQuestController.Instance.gameObject.activeSelf)
            {
                CloseChapterPanel();
                return;
            }
            if (chapterVerticalWindow != null && chapterVerticalWindow.activeSelf)
            {
                CloseChapterPanel();
                return;
            }
        }

        // Phím Q: Bật/Tắt Bảng Dọc Chapter (Main Quest)
        if (Input.GetKeyDown(toggleChapterKey))
        {
            if (ChapterQuestController.Instance != null)
            {
                if (ChapterQuestController.Instance.gameObject.activeSelf)
                    CloseChapterPanel();
                else
                    OpenChapterPanel();
            }
            else if (chapterVerticalWindow != null)
            {
                if (chapterVerticalWindow.activeSelf)
                    CloseChapterPanel();
                else
                    OpenChapterPanel();
            }
        }
    }

    /// <summary>
    /// Trạng thái bình thường khi chơi game
    /// </summary>
    public void SetDefaultHUDState()
    {
        if (QuestTrackerHUD.Instance != null) QuestTrackerHUD.Instance.ShowTracker();
        else if (questTrackerHUD != null) questTrackerHUD.SetActive(true);

        if (ChapterQuestController.Instance != null) ChapterQuestController.Instance.CloseWindow();
        else if (chapterVerticalWindow != null) chapterVerticalWindow.SetActive(false);

        if (QuestUIController.Instance != null) QuestUIController.Instance.CloseWindow();
        else if (horizontalQuestLog != null) horizontalQuestLog.SetActive(false);
    }

    /// <summary>
    /// Mở Bảng Dọc Cốt Truyện (Ẩn Tracker, Ẩn Bảng Ngang)
    /// </summary>
    public void OpenChapterPanel()
    {
        if (QuestUIController.Instance != null) QuestUIController.Instance.CloseWindow();
        else if (horizontalQuestLog != null) horizontalQuestLog.SetActive(false);

        if (QuestTrackerHUD.Instance != null) QuestTrackerHUD.Instance.HideTracker();
        else if (questTrackerHUD != null) questTrackerHUD.SetActive(false);

        if (ChapterQuestController.Instance != null)
        {
            ChapterQuestController.Instance.OpenWindow();
        }
        else if (chapterVerticalWindow != null)
        {
            chapterVerticalWindow.SetActive(true);
        }
    }

    /// <summary>
    /// Đóng Bảng Dọc (Hiện lại Tracker)
    /// </summary>
    public void CloseChapterPanel()
    {
        if (ChapterQuestController.Instance != null)
        {
            ChapterQuestController.Instance.CloseWindow();
        }
        else if (chapterVerticalWindow != null)
        {
            chapterVerticalWindow.SetActive(false);
        }

        if (QuestTrackerHUD.Instance != null) QuestTrackerHUD.Instance.ShowTracker();
        else if (questTrackerHUD != null) questTrackerHUD.SetActive(true);
    }

    /// <summary>
    /// Mở Bảng Ngang Sổ Tay (Ẩn Bảng Dọc)
    /// </summary>
    public void OpenJournalPanel()
    {
        if (ChapterQuestController.Instance != null) ChapterQuestController.Instance.CloseWindow();
        else if (chapterVerticalWindow != null) chapterVerticalWindow.SetActive(false);

        if (QuestTrackerHUD.Instance != null) QuestTrackerHUD.Instance.HideTracker();
        else if (questTrackerHUD != null) questTrackerHUD.SetActive(false);

        if (QuestUIController.Instance != null)
        {
            QuestUIController.Instance.OpenWindow();
        }
        else if (horizontalQuestLog != null)
        {
            horizontalQuestLog.SetActive(true);
        }
    }

    /// <summary>
    /// Đóng Bảng Ngang Sổ Tay (Trở về trạng thái HUD bình thường)
    /// </summary>
    public void CloseJournalPanel()
    {
        if (QuestUIController.Instance != null)
        {
            QuestUIController.Instance.CloseWindow();
        }
        else if (horizontalQuestLog != null)
        {
            horizontalQuestLog.SetActive(false);
        }

        if (QuestTrackerHUD.Instance != null) QuestTrackerHUD.Instance.ShowTracker();
        else if (questTrackerHUD != null) questTrackerHUD.SetActive(true);
    }
}