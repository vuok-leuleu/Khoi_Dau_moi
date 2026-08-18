using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChapterQuestObjectiveItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Image statusIcon;
    [SerializeField] private GameObject lockedOverlay;
    [SerializeField] private Color activeColor = new Color32(74, 46, 24, 255);
    [SerializeField] private Color completedColor = new Color32(138, 129, 122, 255);
    [SerializeField] private Color lockedColor = new Color32(92, 74, 56, 255);

    public void Setup(string title, int currentProgress, int targetProgress, bool isActive, bool isCompleted, bool isLocked)
    {
        if (titleText == null) titleText = GetComponentInChildren<TextMeshProUGUI>();

        if (titleText != null)
        {
            titleText.text = isLocked ? "???" : isCompleted ? $"<s>{title}</s>" : isActive ? title : title;
            titleText.color = isCompleted ? completedColor : isLocked || !isActive ? lockedColor : activeColor;
        }

        if (progressText != null)
        {
            progressText.gameObject.SetActive(!isLocked && targetProgress > 1);
            progressText.text = $"{Mathf.Min(currentProgress, targetProgress)}/{targetProgress}";
        }

        if (statusIcon != null) statusIcon.color = isCompleted ? completedColor : isLocked || !isActive ? lockedColor : activeColor;
        if (lockedOverlay != null) lockedOverlay.SetActive(isLocked);
    }
}
