using UnityEngine;

/// <summary>
/// Xoay liên tục biểu tượng mặt trời ở tâm nút UI.
/// Gắn component này vào Image dùng SunButtonCore.png; không gắn lên khung nút.
/// </summary>
public sealed class SunButtonCoreRotator : MonoBehaviour
{
    [Header("Sun Rotation")]
    [SerializeField, Min(0f)] private float degreesPerSecond = 18f;
    [SerializeField] private bool clockwise = true;
    [SerializeField] private bool useUnscaledTime = true;

    private void Update()
    {
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float direction = clockwise ? -1f : 1f;
        transform.Rotate(0f, 0f, direction * degreesPerSecond * deltaTime, Space.Self);
    }
}
