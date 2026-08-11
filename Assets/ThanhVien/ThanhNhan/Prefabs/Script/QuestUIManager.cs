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
        // Phím ESC: Ưu tiên đóng bất kỳ bảng nào đang mở
        if (Input.GetKeyDown(closeAllKey))
        {
            if (chapterVerticalWindow != null && chapterVerticalWindow.activeSelf)
            {
                CloseChapterPanel();
                return;
            }
            if (horizontalQuestLog != null && horizontalQuestLog.activeSelf)
            {
                CloseJournalPanel();
                return;
            }
        }

        // Phím Q: Bật/Tắt Bảng Dọc Chapter
        if (Input.GetKeyDown(toggleChapterKey))
        {
            if (chapterVerticalWindow != null && chapterVerticalWindow.activeSelf)
                CloseChapterPanel();
            else
                OpenChapterPanel();
        }

        // Phím L: Bật/Tắt Bảng Ngang Sổ Tay
        if (Input.GetKeyDown(toggleJournalKey))
        {
            if (horizontalQuestLog != null && horizontalQuestLog.activeSelf)
                CloseJournalPanel();
            else
                OpenJournalPanel();
        }
    }

    /// <summary>
    /// Trạng thái bình thường khi chơi game
    /// </summary>
    public void SetDefaultHUDState()
    {
        if (questTrackerHUD != null) questTrackerHUD.SetActive(true);
        if (chapterVerticalWindow != null) chapterVerticalWindow.SetActive(false);
        if (horizontalQuestLog != null) horizontalQuestLog.SetActive(false);
    }

    /// <summary>
    /// Mở Bảng Dọc Cốt Truyện (Ẩn Tracker, Ẩn Bảng Ngang)
    /// </summary>
    public void OpenChapterPanel()
    {
        if (horizontalQuestLog != null) horizontalQuestLog.SetActive(false);
        if (questTrackerHUD != null) questTrackerHUD.SetActive(false);

        if (chapterVerticalWindow != null)
        {
            chapterVerticalWindow.SetActive(true);
            // Kích hoạt render lại dữ liệu chương nếu có Controller
            if (ChapterQuestTestController.Instance != null)
            {
                ChapterQuestTestController.Instance.DisplayChapter(0);
            }
        }
    }

    /// <summary>
    /// Đóng Bảng Dọc (Hiện lại Tracker)
    /// </summary>
    public void CloseChapterPanel()
    {
        if (chapterVerticalWindow != null) chapterVerticalWindow.SetActive(false);
        if (questTrackerHUD != null) questTrackerHUD.SetActive(true);
    }

    /// <summary>
    /// Mở Bảng Ngang Sổ Tay (Ẩn Bảng Dọc)
    /// </summary>
    public void OpenJournalPanel()
    {
        if (chapterVerticalWindow != null) chapterVerticalWindow.SetActive(false);
        if (horizontalQuestLog != null) horizontalQuestLog.SetActive(true);
    }

    /// <summary>
    /// Đóng Bảng Ngang Sổ Tay (Trở về trạng thái HUD bình thường)
    /// </summary>
    public void CloseJournalPanel()
    {
        if (horizontalQuestLog != null) horizontalQuestLog.SetActive(false);
        if (questTrackerHUD != null) questTrackerHUD.SetActive(true);
    }
}