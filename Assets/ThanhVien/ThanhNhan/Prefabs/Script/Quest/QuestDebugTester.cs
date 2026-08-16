using UnityEngine;

public class QuestDebugTester : MonoBehaviour
{
    private int currentChapter = 0;
    private int currentObjective = 0;

    void Update()
    {
        // 1. Bấm phím [C] hoặc [Space] để hoàn thành nhiệm vụ kế tiếp
        if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.Space))
        {
            TriggerNextQuest();
        }

        // 2. Bấm phím [1], [2], [3], [4] để nhảy nhanh đến Chương tương ứng
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchChapter(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchChapter(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchChapter(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SwitchChapter(3);
    }

    private void TriggerNextQuest()
    {
        if (ChapterQuestController.Instance == null)
        {
            Debug.LogWarning("[QuestTester] Không tìm thấy ChapterQuestController.Instance trong Scene!");
            return;
        }

        Debug.Log($"<color=yellow>[TESTING]</color> Hoàn thành nhiệm vụ: <b>Chương {currentChapter} - Mục tiêu {currentObjective}</b>");
        
        // Gọi hoàn thành nhiệm vụ và nhận thưởng
        ChapterQuestController.Instance.CompleteObjective(currentChapter, currentObjective);

        currentObjective++;

        // Khi xong 6 quest của Prologue (hoặc 5 quest của Ch1), tự động nhảy sang test Chương kế tiếp
        if (currentObjective >= 6)
        {
            currentObjective = 0;
            currentChapter++;
        }
    }

    private void SwitchChapter(int index)
    {
        if (ChapterQuestController.Instance != null)
        {
            ChapterQuestController.Instance.DisplayChapter(index);
            Debug.Log($"<color=cyan>[TESTING]</color> Đã chuyển sang xem Chương {index + 1}");
        }
    }
}