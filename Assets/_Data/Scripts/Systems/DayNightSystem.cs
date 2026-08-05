using UnityEngine;

public class DayNightSystem : Singleton<DayNightSystem>
{
    [Header("Lighting Settings")]
    [SerializeField] private Light sunLight;
    [SerializeField] private Gradient sunColor;
    [SerializeField] private AnimationCurve sunIntensity;
    [SerializeField] private Gradient ambientColor;
    [SerializeField] private AnimationCurve ambientIntensity;

    [Header("Wave / Day Lighting")]
    [Tooltip("Nếu tắt, hệ thống không tự tiến thời gian ban ngày/ban đêm dựa trên fullDayDuration.")]
    public bool enableTimeOfDay = false;

    [Header("Time Settings")]
    [SerializeField] private float fullDayDuration = 180f; // Full cycle duration (in seconds)
    private float timeOfDay; // 0 - 1 (Day)
    private bool isDayTime = true;

    [Header("Other Settings")]
    [SerializeField] private float nightTimeFactor = 0.25f; // Modifier for night light and ambient intensity

    private bool isNightTime;

    protected override void Awake()
    {
        base.Awake();
        if (sunLight == null) sunLight = RenderSettings.sun;
    }

    private void Update()
    {
        if (!enableTimeOfDay)
            return;

        timeOfDay += Time.deltaTime / fullDayDuration;

        if (timeOfDay >= 1f)
        {
            timeOfDay = 0f;
            ToggleDayNight();
        }

        ApplyLighting();
    }

    private void ApplyLighting()
    {
        // Update Sun Light Color and Intensity
        sunLight.color = sunColor.Evaluate(timeOfDay);
        sunLight.intensity = sunIntensity.Evaluate(timeOfDay);

        // Apply Ambient Lighting
        RenderSettings.ambientLight = ambientColor.Evaluate(timeOfDay);
        RenderSettings.ambientIntensity = ambientIntensity.Evaluate(timeOfDay);

        // Apply Night modifications
        if (isNightTime)
        {
            sunLight.intensity *= nightTimeFactor;
            RenderSettings.ambientIntensity *= nightTimeFactor;
        }
    }

    private void ToggleDayNight()
    {
        isNightTime = !isNightTime;

        if (isNightTime)
        {
            // Update Night related actions (e.g. AI logic)
            Debug.Log("Night time: Workers return home, Wolves attack.");
            // Trigger actions such as UI warnings, sounds for wolves
            // UIManager.Ins.ShowWarning("Wolves are coming!", true);
        }
        else
        {
            // Daytime actions (workers resume tasks)
            Debug.Log("Day time: Workers continue working.");
            //  UIManager.Ins.HideWarning();
        }
    }
}