using UnityEngine;
using UnityEngine.UI;
using TMPro; // Đảm bảo giữ nguyên vì font chữ nghiêng của nhóm bạn rất đẹp!

public class TimeUIController : MonoBehaviour
{
    [Header("[1. Cấu Hình Hiển Thị Wave / Day]")]
    [Tooltip("Kéo Text hiển thị Wave / Day vào đây")]
    public TextMeshProUGUI clockTextTMP;
    //public Text clockTextLegacy; // Dự phòng nếu dùng UI Text thường

    [Header("[2. Cấu Hình Số Ngày Ở Giữa UI]")]
    [Tooltip("Kéo Object DayText trong CenterGroup vào đây")]
    public TextMeshProUGUI dayCounterTextTMP;
    //public Text dayCounterTextLegacy;

    [Header("[3. End Day]")]
    public UnityEngine.UI.Button skipDayButton;
    public TextMeshProUGUI skipDayButtonTextTMP;
    public string endDayButtonLabel = "Kết thúc ngày";
    public bool createEndDayButtonIfMissing = true;

    [Header("End Day Summary")]
    public GameObject endDaySummaryPanel;
    public TextMeshProUGUI summaryTitleText;
    public TextMeshProUGUI summaryDetailsText;
    public TextMeshProUGUI summaryBuiltText;
    public TextMeshProUGUI summaryResourceText;
    public TextMeshProUGUI summaryRewardText;

    private bool isConfirmingEndDay = false;

    private void Start()
    {
        if (skipDayButton == null && createEndDayButtonIfMissing)
        {
            CreateEndDayButton();
        }

        if (endDaySummaryPanel != null)
        {
            endDaySummaryPanel.SetActive(false);
        }

        SetupSkipButton();
        SubscribeDayEvents();
    }

    private void Update()
    {
        if (DayNightManager.Ins == null) return;
        UpdateWaveUI();
    }

    private void SetupSkipButton()
    {
        if (skipDayButton == null) return;

        skipDayButton.onClick.RemoveAllListeners();
        skipDayButton.onClick.AddListener(OnEndDayClicked);

        if (skipDayButtonTextTMP != null)
            skipDayButtonTextTMP.text = endDayButtonLabel;
    }

    private void CreateEndDayButton()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = transform;
        if (canvas == null)
        {
            canvas = Object.FindFirstObjectByType<Canvas>();
        }

        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("RuntimeUICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            parent = canvasGO.transform;
        }
        else
        {
            parent = canvas.transform;
        }

        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystemGO = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            DontDestroyOnLoad(eventSystemGO);
        }

        GameObject buttonGO = new GameObject("EndDayButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        buttonGO.transform.SetParent(parent, false);

        RectTransform rect = buttonGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(180f, 45f);
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-20f, -20f);

        var image = buttonGO.GetComponent<UnityEngine.UI.Image>();
        image.color = new Color(0.16f, 0.48f, 0.8f, 0.95f);

        var button = buttonGO.GetComponent<UnityEngine.UI.Button>();
        button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
        button.targetGraphic = image;
        skipDayButton = button;

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(buttonGO.transform, false);
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textGO.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 22;
        text.text = endDayButtonLabel;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        skipDayButtonTextTMP = text;
    }

    private void OnEndDayClicked()
    {
        if (DayNightManager.Ins == null) return;

        if (!isConfirmingEndDay)
        {
            ShowEndDayConfirmation();
            return;
        }

        isConfirmingEndDay = false;
        HideEndDaySummary();
        DayNightManager.Ins.EndDay();
    }

    private void SubscribeDayEvents()
    {
        if (DayNightManager.Ins != null)
        {
            DayNightManager.Ins.OnDayEnd += HandleDayEnd;
        }
    }

    private void HandleDayEnd()
    {
        ShowEndDaySummary();
    }

    private void ShowEndDayConfirmation()
    {
        isConfirmingEndDay = true;
        string reward = DayNightManager.Ins != null ? DayNightManager.Ins.GetPendingGoldReward().ToString() : "0";
        if (summaryTitleText != null) summaryTitleText.text = "Xác nhận kết thúc ngày";
        if (summaryDetailsText != null) summaryDetailsText.text = $"Kết thúc ngày hiện tại và nhận {reward} vàng?";
        if (summaryRewardText != null) summaryRewardText.text = $"Thưởng vàng: {reward}";
        if (endDaySummaryPanel != null) endDaySummaryPanel.SetActive(true);
        if (skipDayButtonTextTMP != null) skipDayButtonTextTMP.text = "Xác nhận";
    }

    private void HideEndDaySummary()
    {
        isConfirmingEndDay = false;
        if (endDaySummaryPanel != null) endDaySummaryPanel.SetActive(false);
        if (skipDayButtonTextTMP != null) skipDayButtonTextTMP.text = endDayButtonLabel;
    }

    private void ShowEndDaySummary()
    {
        if (DayNightManager.Ins == null) return;

        int day = DayNightManager.Ins.CurrentDay;
        int reward = DayNightManager.Ins.GetGoldRewardForDay(day);

        if (summaryTitleText != null) summaryTitleText.text = "Tổng kết cuối ngày";
        if (summaryDetailsText != null) summaryDetailsText.text = $"Kết thúc Ngày {day}. Bạn nhận được {reward} vàng.";
        if (summaryRewardText != null) summaryRewardText.text = $"Vàng hiện tại: {JsonDataManager.Ins?.gold ?? 0}";
        if (endDaySummaryPanel != null) endDaySummaryPanel.SetActive(true);

        isConfirmingEndDay = false;
        if (skipDayButtonTextTMP != null) skipDayButtonTextTMP.text = endDayButtonLabel;
    }

    private void UpdateWaveUI()
    {
        int currentDayNumber = DayNightManager.Ins.CurrentDay;
        string formattedWaveText = $"Wave {currentDayNumber}";

        if (clockTextTMP != null) clockTextTMP.text = formattedWaveText;
        //if (clockTextLegacy != null) clockTextLegacy.text = formattedWaveText;

        string formattedDayText = $"Day {currentDayNumber}";
        if (dayCounterTextTMP != null) dayCounterTextTMP.text = formattedDayText;
        //if (dayCounterTextLegacy != null) dayCounterTextLegacy.text = formattedDayText;
    }

    private void OnSkipDayClicked()
    {
        DayNightManager.Ins?.SkipDay();
    }
}