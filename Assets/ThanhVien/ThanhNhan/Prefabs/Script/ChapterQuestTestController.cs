using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[Serializable]
public class TestSubObjective
{
    public string objectiveID;
    public string title;
    [TextArea(2, 3)]
    public string hintText;
    public bool isCompleted;
}

[Serializable]
public class TestChapterData
{
    public int chapterIndex;           // 0: Prologue, 1: Chương I, 2: Chương II...
    public string chapterName;         // "Mở Đầu", "Chương I"...
    public bool isUnlocked = true;     // Trạng thái mở khóa

    [Header("Phần Thưởng Cơ Bản")]
    public int goldReward = 50;
    public int woodReward = 100;
    public int stoneReward = 50;

    [Header("Phần Thưởng Đặc Biệt")]
    public Sprite keyRewardIcon;
    public string keyRewardName = "Huy Hiệu Tân Binh Tiên Phong";

    [Header("Danh Sách Mục Tiêu")]
    public List<TestSubObjective> objectives = new List<TestSubObjective>();
}

public class ChapterQuestTestController : MonoBehaviour
{
    public static ChapterQuestTestController Instance { get; private set; }

    [Header("--- 1. TOP HEADER (ĐIỀU HƯỚNG) ---")]
    [SerializeField] private Button prevChapterBtn;
    [SerializeField] private Button nextChapterBtn;
    [SerializeField] private TextMeshProUGUI chapterTitleText;

    [Header("--- 2. REWARD HEADER (PHẦN THƯỞNG) ---")]
    [SerializeField] private GameObject rewardAreaPanel;
    [SerializeField] private TextMeshProUGUI goldRewardText;
    [SerializeField] private TextMeshProUGUI woodRewardText;
    [SerializeField] private TextMeshProUGUI stoneRewardText;
    [SerializeField] private Image keyRewardIcon;
    [SerializeField] private TextMeshProUGUI keyRewardTitleText;

    [Header("--- 3. LOCKED STATE PANEL ---")]
    [SerializeField] private GameObject lockedPanel; // Panel hiện Ổ Khóa khi Chapter chưa mở

    [Header("--- 4. CONTENT AREA (DANH SÁCH NHIỆM VỤ) ---")]
    [SerializeField] private Transform contentArea;         // Content của ScrollView
    [SerializeField] private GameObject questItemPrefab;    // Prefab dòng nhiệm vụ

    [Header("--- 5. RETURN & HOTKEY ---")]
    [SerializeField] private Button returnButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private KeyCode toggleHotkey = KeyCode.Q;

    [Header("--- 6. DATA TEST LIST ---")]
    [SerializeField] private int currentChapterIndex = 0;
    [SerializeField] private List<TestChapterData> chapterList = new List<TestChapterData>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Gán sự kiện nút bấm
        if (prevChapterBtn != null) prevChapterBtn.onClick.AddListener(PrevChapter);
        if (nextChapterBtn != null) nextChapterBtn.onClick.AddListener(NextChapter);
        if (returnButton != null) returnButton.onClick.AddListener(CloseWindow);
        if (closeButton != null) closeButton.onClick.AddListener(CloseWindow);

        // Khởi tạo data mẫu nếu danh sách đang rỗng
        InitDefaultTestData();

        // Mặc định hiển thị Prologue (index 0)
        DisplayChapter(currentChapterIndex);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleHotkey))
        {
            ToggleWindow();
        }
    }

    public void ToggleWindow()
    {
        gameObject.SetActive(!gameObject.activeSelf);
        if (gameObject.activeSelf)
        {
            DisplayChapter(currentChapterIndex);
        }
    }

    public void OpenWindow()
    {
        gameObject.SetActive(true);
        DisplayChapter(currentChapterIndex);
    }

    public void CloseWindow()
    {
        gameObject.SetActive(false);
    }

    public void NextChapter()
    {
        if (currentChapterIndex < chapterList.Count - 1)
        {
            currentChapterIndex++;
            DisplayChapter(currentChapterIndex);
        }
    }

    public void PrevChapter()
    {
        if (currentChapterIndex > 0)
        {
            currentChapterIndex--;
            DisplayChapter(currentChapterIndex);
        }
    }

    public void DisplayChapter(int index)
    {
        if (chapterList == null || chapterList.Count == 0 || index < 0 || index >= chapterList.Count) return;

        TestChapterData chapter = chapterList[index];

        // 1. Cập nhật Tiêu Đề
        if (chapterTitleText != null)
        {
            chapterTitleText.text = chapter.chapterName;
        }

        // Cập nhật trạng thái tương tác cho 2 nút < và >
        if (prevChapterBtn != null) prevChapterBtn.interactable = (index > 0);
        if (nextChapterBtn != null) nextChapterBtn.interactable = (index < chapterList.Count - 1);

        // 2. Cập nhật Phần Thưởng
        if (goldRewardText != null) goldRewardText.text = chapter.goldReward.ToString();
        if (woodRewardText != null) woodRewardText.text = chapter.woodReward.ToString();
        if (stoneRewardText != null) stoneRewardText.text = chapter.stoneReward.ToString();

        if (keyRewardTitleText != null) keyRewardTitleText.text = chapter.keyRewardName;
        if (keyRewardIcon != null && chapter.keyRewardIcon != null)
        {
            keyRewardIcon.sprite = chapter.keyRewardIcon;
            keyRewardIcon.gameObject.SetActive(true);
        }

        // 3. Kiểm tra Trạng thái Mở / Khóa
        if (!chapter.isUnlocked)
        {
            if (lockedPanel != null) lockedPanel.SetActive(true);
            if (contentArea != null) contentArea.gameObject.SetActive(false);
            return;
        }

        if (lockedPanel != null) lockedPanel.SetActive(false);
        if (contentArea != null) contentArea.gameObject.SetActive(true);

        // 4. Render Danh Sách Mục Tiêu Của Chương (Đã bỏ hoàn toàn ký tự dấu sao)
        RenderObjectives(chapter);
    }

    private void RenderObjectives(TestChapterData chapter)
    {
        if (contentArea == null || questItemPrefab == null) return;

        // Xóa các dòng cũ trước khi vẽ danh sách mới
        foreach (Transform child in contentArea)
        {
            Destroy(child.gameObject);
        }

        bool foundFirstActive = false;

        foreach (var obj in chapter.objectives)
        {
            GameObject itemObj = Instantiate(questItemPrefab, contentArea);
            TextMeshProUGUI itemText = itemObj.GetComponentInChildren<TextMeshProUGUI>();

            if (itemText == null) continue;

            if (obj.isCompleted)
            {
                // Đã hoàn thành -> Gạch ngang và mờ xám (không có dấu sao)
                string hint = string.IsNullOrEmpty(obj.hintText) ? "" : $"\n<size=80%>{obj.hintText}</size>";
                itemText.text = $"<s><color=#8A817A>{obj.title}{hint}</color></s>";
            }
            else if (!foundFirstActive)
            {
                // Mục tiêu hiện tại đang làm -> Đậm, màu nâu đen rõ nét (không có dấu sao)
                foundFirstActive = true;
                string hint = string.IsNullOrEmpty(obj.hintText) ? "" : $"\n<size=80%><color=#6A6058>{obj.hintText}</color></size>";
                itemText.text = $"<color=#2A2421><b>{obj.title}</b>{hint}</color>";
            }
            else
            {
                // Các mục tiêu kế tiếp chưa mở -> Hiển thị dấu ??? (không có dấu sao)
                itemText.text = "<color=#8A817A>???</color>";
            }
        }
    }

    private void InitDefaultTestData()
    {
        if (chapterList.Count > 0) return;

        // 0. PROLOGUE (Chương Mở Đầu)
        TestChapterData prologue = new TestChapterData
        {
            chapterIndex = 0,
            chapterName = "Chương Mở Đầu",
            isUnlocked = true,
            goldReward = 3,
            woodReward = 100,
            stoneReward = 50,
            keyRewardName = "Huy Hiệu Tân Binh Tiên Phong"
        };
        prologue.objectives.Add(new TestSubObjective { title = "Sống sót qua đêm đầu tiên.", hintText = "Dựng một đống lửa trại để giữ ấm và xua đuổi dã thú.", isCompleted = true });
        prologue.objectives.Add(new TestSubObjective { title = "Thu thập 50 Gỗ.", hintText = "Chặt các cây xung quanh khu định cư.", isCompleted = true });
        prologue.objectives.Add(new TestSubObjective { title = "Xây dựng Nhà Chính (Town Hall).", hintText = "Chọn vị trí đất bằng phẳng để đặt nền móng.", isCompleted = false });
        prologue.objectives.Add(new TestSubObjective { title = "Nói chuyện với Trinh Sát Tiên Phong.", hintText = "Nhận chỉ dẫn về các mối đe dọa quanh vùng.", isCompleted = false });
        chapterList.Add(prologue);

        // 1. CHƯƠNG I (Khởi Nguyên Định Cư)
        TestChapterData ch1 = new TestChapterData
        {
            chapterIndex = 1,
            chapterName = "Chương I: Khởi Nguyên",
            isUnlocked = true,
            goldReward = 5,
            woodReward = 200,
            stoneReward = 75,
            keyRewardName = "Biểu Cảm: Khúc Khải Hoàn Demacia"
        };
        ch1.objectives.Add(new TestSubObjective { title = "Xây dựng thêm một Nông Trại.", hintText = "Nông trại đầu tiên tại vùng Heartland giúp tăng thêm Lúa mì.", isCompleted = true });
        ch1.objectives.Add(new TestSubObjective { title = "Mở khóa đơn vị Cung Thủ trong bảng Nghiên Cứu.", hintText = "Cung thủ hỗ trợ phòng thủ tầm xa từ tháp canh.", isCompleted = true });
        ch1.objectives.Add(new TestSubObjective { title = "Thiết lập Vùng Định Cư mới gần Vaskasia.", hintText = "Mở rộng lãnh thổ và khai hoang tài nguyên mới.", isCompleted = false });
        ch1.objectives.Add(new TestSubObjective { title = "Khai thác 300 Đá để gia cố công sự.", hintText = "", isCompleted = false });
        ch1.objectives.Add(new TestSubObjective { title = "Tiêu diệt Đàn Sói Hoang Dã.", hintText = "", isCompleted = false });
        chapterList.Add(ch1);

        // 2. CHƯƠNG II (Phòng Tuyến Vững Chắc)
        TestChapterData ch2 = new TestChapterData
        {
            chapterIndex = 2,
            chapterName = "Chương II: Phòng Tuyến Vững Chắc",
            isUnlocked = true,
            goldReward = 10,
            woodReward = 500,
            stoneReward = 150,
            keyRewardName = "Danh Hiệu: Hộ Vệ Hoàng Gia Tập Sự"
        };
        ch2.objectives.Add(new TestSubObjective { title = "Nâng cấp Nhà Chính lên Cấp 2.", hintText = "Yêu cầu: 500 Gỗ và 200 Đá.", isCompleted = true });
        ch2.objectives.Add(new TestSubObjective { title = "Xây dựng 2 Tháp Canh Phòng Thủ.", hintText = "Đặt tháp tại các hẻm núi trọng yếu ở biên giới.", isCompleted = false });
        ch2.objectives.Add(new TestSubObjective { title = "Huấn luyện 10 Chiến Binh Tiên Phong.", hintText = "Chiêu mộ binh lính từ Trại Lính.", isCompleted = false });
        ch2.objectives.Add(new TestSubObjective { title = "Quét sạch Trại Thổ Phỉ Vùng Ven.", hintText = "", isCompleted = false });
        ch2.objectives.Add(new TestSubObjective { title = "Thiết lập tuyến giao thương với Vùng High Silvermere.", hintText = "", isCompleted = false });
        chapterList.Add(ch2);

        // 3. CHƯƠNG III (Vinh Quang Tiên Phong)
        TestChapterData ch3 = new TestChapterData
        {
            chapterIndex = 3,
            chapterName = "Chương III: Vinh Quang Tiên Phong",
            isUnlocked = true,
            goldReward = 25,
            woodReward = 1000,
            stoneReward = 500,
            keyRewardName = "Cờ Hiệu: Đoàn Quân Tiên Phong Rạng Ngời"
        };
        ch3.objectives.Add(new TestSubObjective { title = "Xây dựng Đại Pháo Đài Kiên Cố.", hintText = "Trái tim phòng thủ của toàn bộ căn cứ.", isCompleted = false });
        ch3.objectives.Add(new TestSubObjective { title = "Nghiên cứu Giáp Kháng Ma Kháng Thạch (Petricite).", hintText = "Mở khóa nâng cấp kháng phép cho quân đội tinh nhuệ.", isCompleted = false });
        ch3.objectives.Add(new TestSubObjective { title = "Tập hợp Quân Đoàn gồm 30 Binh Lính.", hintText = "", isCompleted = false });
        ch3.objectives.Add(new TestSubObjective { title = "Đẩy lùi Cỗ Máy Công Thành Khổng Lồ.", hintText = "Chuẩn bị hỏa lực hạng nặng cho đợt tấn công quy mô lớn.", isCompleted = false });
        chapterList.Add(ch3);
    }
}