using UnityEngine;
using TMPro;

public class TimeUIController : MonoBehaviour
{
    [Header("[1. Cấu Hình Đồng Hồ Display]")]
    [Tooltip("Kéo Text hiển thị thời gian trong TimeGroup vào đây")]
    public TextMeshProUGUI clockTextTMP;

    [Header("[2. Cấu Hình Số Ngày Ở Giữa UI]")]
    [Tooltip("Kéo Object DayText trong CenterGroup vào đây")]
    public TextMeshProUGUI dayCounterTextTMP;

    public void SetClockText(string timeText)
    {
        if (clockTextTMP != null) clockTextTMP.text = timeText;
    }

    public void SetDayText(string dayText)
    {
        if (dayCounterTextTMP != null) dayCounterTextTMP.text = dayText;
    }

    public void SetDayNumber(int dayNumber)
    {
        SetDayText($"Day {dayNumber}");
    }
}