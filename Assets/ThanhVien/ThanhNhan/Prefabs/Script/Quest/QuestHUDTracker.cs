using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class QuestTrackerHUD : MonoBehaviour
{
    public static QuestTrackerHUD Instance { get; private set; }

    [Header("--- UI COMPONENTS ---")]
    [SerializeField] private TextMeshProUGUI chapterTitleText;
    [SerializeField] private TextMeshProUGUI activeQuestText;
    [SerializeField] private Button openDetailBtn;

    [Header("✨ ANIMATION SETTINGS")]
    [SerializeField] private bool enableAnimations = true;
    [SerializeField] private bool useTextPunchAnim = true;
    [SerializeField] private bool useButtonPunchAnim = true;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (openDetailBtn != null)
        {
            openDetailBtn.onClick.AddListener(OnTrackerClicked);
        }
    }

    public void UpdateTrackerInfo(string chapterTitle, string activeQuestTitle)
    {
        if (chapterTitleText != null) chapterTitleText.text = chapterTitle;

        if (activeQuestText != null)
        {
            if (enableAnimations && useTextPunchAnim && activeQuestText.text != activeQuestTitle)
            {
                DOTween.Kill(activeQuestText.transform);
                activeQuestText.transform.DOPunchScale(Vector3.one * 0.12f, 0.25f, 5, 1).SetUpdate(true);
            }
            activeQuestText.text = activeQuestTitle;
        }
    }

    public void OnTrackerClicked()
    {
        if (enableAnimations && useButtonPunchAnim && openDetailBtn != null)
        {
            DOTween.Kill(openDetailBtn.transform);
            openDetailBtn.transform.DOPunchScale(Vector3.one * 0.2f, 0.15f, 5, 1).SetUpdate(true);
        }

        if (ChapterQuestController.Instance != null)
        {
            ChapterQuestController.Instance.OpenWindow();
        }
    }

    public void ShowTracker()
    {
        gameObject.SetActive(true);
        if (enableAnimations)
        {
            RectTransform rect = GetComponent<RectTransform>();
            CanvasGroup cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

            DOTween.Kill(rect);
            DOTween.Kill(cg);

            rect.localScale = new Vector3(1f, 0.2f, 1f);
            cg.alpha = 0f;

            Sequence seq = DOTween.Sequence();
            seq.SetUpdate(true);
            seq.Append(rect.DOScaleY(1f, 0.25f).SetEase(Ease.OutBack));
            seq.Join(cg.DOFade(1f, 0.2f));
        }
    }

    public void HideTracker()
    {
        if (enableAnimations && gameObject.activeInHierarchy)
        {
            RectTransform rect = GetComponent<RectTransform>();
            CanvasGroup cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = GetComponent<CanvasGroup>();

            if (rect != null && cg != null)
            {
                DOTween.Kill(rect);
                DOTween.Kill(cg);

                Sequence seq = DOTween.Sequence();
                seq.SetUpdate(true);
                seq.Append(rect.DOScaleY(0.1f, 0.18f).SetEase(Ease.InCubic));
                seq.Join(cg.DOFade(0f, 0.18f));
                seq.OnComplete(() =>
                {
                    gameObject.SetActive(false);
                    rect.localScale = Vector3.one;
                    cg.alpha = 1f;
                });
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}