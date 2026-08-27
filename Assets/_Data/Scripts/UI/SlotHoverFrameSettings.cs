using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cung cấp khung viền dùng chung cho các slot trong cùng một panel UI.
/// Gắn component này vào Canvas/panel cha và gán sprite khung trong Inspector.
/// </summary>
public sealed class SlotHoverFrameSettings : MonoBehaviour
{
    [SerializeField] private Sprite hoverFrameSprite;
    [SerializeField, Min(0f)] private float framePadding = 8f;

    public Sprite HoverFrameSprite => hoverFrameSprite;
    public float FramePadding => framePadding;
}

/// <summary>
/// Khởi tạo và điều khiển khung hover ở runtime, để frame luôn nằm trên cùng
/// nhưng không nhận raycast hay làm ảnh hưởng Button của slot.
/// </summary>
public sealed class SlotHoverFrame
{
    private readonly RectTransform frameTransform;
    private readonly CanvasGroup canvasGroup;

    public SlotHoverFrame(RectTransform slotTransform, SlotHoverFrameSettings settings)
    {
        GameObject frameObject = new GameObject("HoverFrame_Img", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        frameTransform = frameObject.GetComponent<RectTransform>();
        frameTransform.SetParent(slotTransform, false);
        frameTransform.anchorMin = Vector2.zero;
        frameTransform.anchorMax = Vector2.one;
        frameTransform.offsetMin = Vector2.one * -settings.FramePadding;
        frameTransform.offsetMax = Vector2.one * settings.FramePadding;
        frameTransform.SetAsLastSibling();

        Image frameImage = frameObject.GetComponent<Image>();
        frameImage.sprite = settings.HoverFrameSprite;
        frameImage.preserveAspect = false;
        frameImage.raycastTarget = false;

        canvasGroup = frameObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    public void Show()
    {
        frameTransform.SetAsLastSibling();
        canvasGroup.alpha = 1f;
    }

    public void Hide()
    {
        canvasGroup.alpha = 0f;
    }
}
