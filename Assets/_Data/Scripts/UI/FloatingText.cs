using UnityEngine;
using TMPro;
using DG.Tweening;

public class FloatingText : MonoBehaviour
{
    [SerializeField] public TextMeshProUGUI text;
    [SerializeField] public CanvasGroup canvasGroup;

    private void Awake()
    {
        if (text == null) text = GetComponentInChildren<TextMeshProUGUI>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>
    /// Hiển thị popup xuất hiện dần từ cạnh dưới (trượt nhẹ lên + fade in) trong đúng 0.1s,
    /// sau đó đứng yên cố định rõ nét, hết thời gian (duration) biến mất ngay lập tức và gọi callback chạy số.
    /// </summary>
    public void Setup(string content, float duration = 1.0f, System.Action onComplete = null)
    {
        if (text == null) text = GetComponentInChildren<TextMeshProUGUI>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (text != null)
        {
            text.text = content;
        }

        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            Vector2 targetPos = rect.anchoredPosition;
            float slideDistance = 15f; // Xuất phát từ cạnh dưới 15px

            // Trạng thái ban đầu trước khi xuất hiện:
            rect.anchoredPosition = new Vector2(targetPos.x, targetPos.y - slideDistance);
            rect.localScale = Vector3.one;
            canvasGroup.alpha = 0f; // Bắt đầu từ trong suốt để xuất hiện dần

            Sequence seq = DOTween.Sequence().SetTarget(gameObject);

            // 1. Xuất hiện dần (Fade In) và trượt từ cạnh dưới lên vị trí chuẩn trong 0.1 giây
            seq.Append(canvasGroup.DOFade(1f, 0.1f).SetEase(Ease.Linear));
            seq.Join(rect.DOAnchorPosY(targetPos.y, 0.1f).SetEase(Ease.OutQuad));

            // 2. Đứng yên cố định rõ nét trong thời gian còn lại (1.0s - 0.1s = 0.9s)
            float stayTime = Mathf.Max(0.05f, duration - 0.1f);
            seq.AppendInterval(stayTime);

            // 3. Hết thời gian biến mất ngay lập tức và kích hoạt số trên UI bắt đầu chạy
            seq.OnComplete(() =>
            {
                onComplete?.Invoke();
                if (gameObject != null) Destroy(gameObject);
            });
        }
        else
        {
            onComplete?.Invoke();
            Destroy(gameObject, duration);
        }
    }
}
