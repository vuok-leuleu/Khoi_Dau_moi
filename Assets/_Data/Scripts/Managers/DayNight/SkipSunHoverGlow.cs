using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Giữ Button ở trạng thái khóa mà không làm mờ, đồng thời chỉ làm sáng Image
/// mặt trời khi con trỏ đang hover trên nút.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class SkipSunHoverGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Target")]
    [Tooltip("Image của mặt trời. Nếu bỏ trống sẽ dùng Image cùng GameObject.")]
    [SerializeField] private Image sunImage;

    [Header("Hover Glow")]
    [SerializeField, Min(1f)] private float glowBrightness = 1.35f;
    [SerializeField, Min(1f)] private float hoverScale = 1.08f;
    [SerializeField, Min(0f)] private float pulseScale = 0.02f;
    [SerializeField, Min(0f)] private float pulseSpeed = 4f;
    [SerializeField, Min(0f)] private float fadeSpeed = 14f;

    private Button button;
    private Color defaultColor;
    private Vector3 defaultScale;
    private bool isHovered;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (sunImage == null) sunImage = GetComponent<Image>();

        if (sunImage == null)
        {
            Debug.LogWarning("[SkipSunHoverGlow] Không tìm thấy Image mặt trời.", this);
            enabled = false;
            return;
        }

        defaultColor = sunImage.color;
        defaultScale = sunImage.rectTransform.localScale;

        // DayNightManager vẫn có thể đặt interactable = false để khóa click,
        // nhưng Button sẽ không tự áp Disabled Color làm mờ cả nút nữa.
        if (button != null) button.transition = Selectable.Transition.None;
    }

    private void Update()
    {
        bool shouldGlow = isHovered && (button == null || button.interactable);
        float blend = 1f - Mathf.Exp(-fadeSpeed * Time.unscaledDeltaTime);

        Color targetColor = defaultColor;
        Vector3 targetScale = defaultScale;

        if (shouldGlow)
        {
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseScale;
            targetColor.r *= glowBrightness;
            targetColor.g *= glowBrightness;
            targetColor.b *= glowBrightness;
            targetScale *= hoverScale * pulse;
        }

        sunImage.color = Color.Lerp(sunImage.color, targetColor, blend);
        sunImage.rectTransform.localScale = Vector3.Lerp(
            sunImage.rectTransform.localScale,
            targetScale,
            blend
        );
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = button == null || button.interactable;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }

    private void OnDisable()
    {
        if (sunImage == null) return;
        sunImage.color = defaultColor;
        sunImage.rectTransform.localScale = defaultScale;
    }
}
