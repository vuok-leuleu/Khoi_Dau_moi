using UnityEngine;

public class NotificationPulse : MonoBehaviour
{
    [Header("Pulse Settings")]
    [SerializeField] private float pulseSpeed = 4f;       // Tốc độ nhấp nháy
    [SerializeField] private float scaleAmount = 0.15f;    // Độ phóng to (15%)

    [Header("Bounce Settings (Tùy chọn)")]
    [SerializeField] private bool enableBounce = true;
    [SerializeField] private float bounceSpeed = 5f;      // Tốc độ nảy
    [SerializeField] private float bounceHeight = 3f;     // Độ cao nảy (Pixel)

    private Vector3 baseScale;
    private Vector3 basePosition;

    private void Awake()
    {
        baseScale = transform.localScale;
        basePosition = transform.localPosition;
    }

    private void OnEnable()
    {
        // Reset lại vị trí/kích thước chuẩn mỗi khi hiện icon
        transform.localScale = baseScale;
        transform.localPosition = basePosition;
    }

    private void Update()
    {
        // 1. Hiệu ứng Nhấp Nháy (Pulse Scale)
        float scaleOffset = Mathf.Sin(Time.time * pulseSpeed) * scaleAmount;
        transform.localScale = baseScale + new Vector3(scaleOffset, scaleOffset, 0f);

        // 2. Hiệu ứng Nảy Nhẹ (Floating Bounce)
        if (enableBounce)
        {
            float yOffset = Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;
            transform.localPosition = basePosition + new Vector3(0f, yOffset, 0f);
        }
    }
}