using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class QuestTrackerHUD : MonoBehaviour
{
    public static QuestTrackerHUD Instance { get; private set; }

    [Header("--- UI COMPONENTS ---")]
    [SerializeField] private TextMeshProUGUI chapterTitleText;
    [SerializeField] private TextMeshProUGUI objectiveText;
    [SerializeField] private Button arrowButton;

    [Header("✨ ANIMATION SETTINGS")]
    [SerializeField] private bool enableAnimations = true;
    [SerializeField] private bool useTextPunchAnim = true;
    [SerializeField] private bool useButtonPunchAnim = true;
    [SerializeField, Min(0f)] private float revealDuration = 0.2f;


    private RectTransform trackerRect;
    private RectTransform revealMask;
    private CanvasGroup canvasGroup;
    private Vector2 originalAnchorMin;
    private Vector2 originalAnchorMax;
    private Vector2 originalAnchoredPosition;
    private Vector2 originalSizeDelta;
    private Vector2 originalPivot;
    private float trackerHeight;
    private bool isShown;
    private bool isRevealing;
    private bool isHiding;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        isShown = gameObject.activeSelf;
    }

    private void Start()
    {
        if (arrowButton != null)
        {
            arrowButton.onClick.AddListener(OnTrackerClicked);
        }
    }

    public void UpdateTrackerInfo(string chapterTitle, string activeQuestTitle)
    {
        if (chapterTitleText != null) chapterTitleText.text = chapterTitle;

        if (objectiveText != null)
        {
            if (enableAnimations && useTextPunchAnim && objectiveText.text != activeQuestTitle)
            {
                DOTween.Kill(objectiveText.transform);
                objectiveText.transform.DOPunchScale(Vector3.one * 0.12f, 0.25f, 5, 1).SetUpdate(true);
            }
            objectiveText.text = activeQuestTitle;
        }
    }

    public void OnTrackerClicked()
    {
        if (enableAnimations && useButtonPunchAnim && arrowButton != null)
        {
            DOTween.Kill(arrowButton.transform);
            arrowButton.transform.DOPunchScale(Vector3.one * 0.2f, 0.15f, 5, 1).SetUpdate(true);
        }

        if (ChapterQuestController.Instance != null)
        {
            ChapterQuestController.Instance.OpenWindow();
        }
    }

    private bool EnsureRevealMask()
    {
        if (revealMask != null) return true;

        trackerRect = GetComponent<RectTransform>();
        RectTransform parent = trackerRect != null ? trackerRect.parent as RectTransform : null;
        if (trackerRect == null || parent == null) return false;

        originalAnchorMin = trackerRect.anchorMin;
        originalAnchorMax = trackerRect.anchorMax;
        originalAnchoredPosition = trackerRect.anchoredPosition;
        originalSizeDelta = trackerRect.sizeDelta;
        originalPivot = trackerRect.pivot;
        trackerHeight = trackerRect.rect.height;

        GameObject maskObject = new GameObject("QuestHUDRevealMask", typeof(RectTransform), typeof(RectMask2D));
        revealMask = maskObject.GetComponent<RectTransform>();
        revealMask.SetParent(parent, false);
        revealMask.anchorMin = originalAnchorMin;
        revealMask.anchorMax = originalAnchorMax;
        revealMask.sizeDelta = originalSizeDelta;
        revealMask.localRotation = trackerRect.localRotation;
        revealMask.localScale = trackerRect.localScale;

        trackerRect.SetParent(revealMask, false);
        trackerRect.localRotation = Quaternion.identity;
        trackerRect.localScale = Vector3.one;
        return true;
    }

    private void ConfigureRevealMask(bool revealFromBottom)
    {
        float verticalOffset = revealFromBottom
            ? -trackerHeight * originalPivot.y
            : trackerHeight * (1f - originalPivot.y);
        float contentOffset = revealFromBottom
            ? trackerHeight * originalPivot.y
            : -trackerHeight * (1f - originalPivot.y);
        float anchorY = revealFromBottom ? 0f : 1f;

        float currentMaskHeight = revealMask.rect.height;
        revealMask.pivot = new Vector2(originalPivot.x, anchorY);
        revealMask.anchoredPosition = originalAnchoredPosition + Vector2.up * verticalOffset;
        revealMask.sizeDelta = new Vector2(originalSizeDelta.x, currentMaskHeight);

        trackerRect.anchorMin = new Vector2(originalPivot.x, anchorY);
        trackerRect.anchorMax = new Vector2(originalPivot.x, anchorY);
        trackerRect.pivot = originalPivot;
        trackerRect.sizeDelta = originalSizeDelta;
        trackerRect.anchoredPosition = new Vector2(0f, contentOffset);
    }

    private void SetRevealHeight(float height)
    {
        revealMask.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    private CanvasGroup GetCanvasGroup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    public void ShowTracker()
    {
        if (isRevealing || (isShown && gameObject.activeSelf && revealMask != null && !isHiding)) return;
        if (!EnsureRevealMask())
        {
            gameObject.SetActive(true);
            return;
        }

        CanvasGroup cg = GetCanvasGroup();
        DOTween.Kill(revealMask);
        DOTween.Kill(trackerRect);
        DOTween.Kill(cg);

        isRevealing = true;
        isHiding = false;
        SetRevealHeight(0f);
        ConfigureRevealMask(true);
        trackerRect.localScale = Vector3.one;
        cg.alpha = 1f;
        cg.blocksRaycasts = false;
        gameObject.SetActive(true);

        if (!enableAnimations)
        {
            SetRevealHeight(trackerHeight);
            cg.blocksRaycasts = true;
            isRevealing = false;
            isShown = true;
            return;
        }

        DOTween.To(() => revealMask.rect.height, SetRevealHeight, trackerHeight, revealDuration)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                cg.blocksRaycasts = true;
                isRevealing = false;
                isShown = true;
            });
    }

    public void HideTracker()
    {
        if (isHiding || !gameObject.activeInHierarchy) return;
        if (!EnsureRevealMask())
        {
            gameObject.SetActive(false);
            isShown = false;
            return;
        }

        CanvasGroup cg = GetCanvasGroup();
        DOTween.Kill(revealMask);
        DOTween.Kill(trackerRect);
        DOTween.Kill(cg);

        isRevealing = false;
        isHiding = true;
        isShown = false;
        ConfigureRevealMask(false);
        SetRevealHeight(trackerHeight);
        trackerRect.localScale = Vector3.one;
        cg.alpha = 1f;
        cg.blocksRaycasts = false;

        if (!enableAnimations)
        {
            gameObject.SetActive(false);
            isHiding = false;
            return;
        }

        DOTween.To(() => revealMask.rect.height, SetRevealHeight, 0f, revealDuration)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                SetRevealHeight(trackerHeight);
                gameObject.SetActive(false);
                isHiding = false;
            });
    }
}