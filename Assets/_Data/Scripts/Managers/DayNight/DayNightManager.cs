using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quản lý Hệ thống Wave thuần túy (Pure Wave System - Bỏ hoàn toàn khái niệm Ban ngày / Ban đêm)
/// - Trạng thái: Preparation (Chuẩn bị giữa các Wave) và Combat (Wave quái đang tấn công).
/// - Người chơi bấm nút Skip/Start để bắt đầu Wave tiếp theo bất kỳ lúc nào (Wave 1 -> Wave 2 -> Wave 3...).
/// </summary>
public class DayNightManager : Singleton<DayNightManager>
{
    public enum WaveState { Preparation, Combat }

    [Header("--- TRẠNG THÁI WAVE ---")]
    [SerializeField] private WaveState currentWaveState = WaveState.Preparation;
    [SerializeField] private int currentWave = 0;
    [SerializeField] private float timer;
    [SerializeField] private bool isWaveActive = false;

    public WaveState CurrentWaveState => currentWaveState;
    public int CurrentWave => currentWave;
    public float CurrentTimer => timer;
    public bool IsWaveActive => isWaveActive;


    [Header("--- CẤU HÌNH SKIP WAVE ---")]
    [Tooltip("Thưởng vàng/tài nguyên khi Skip Wave sớm")]
    public bool enableSkipBonus = false;

    [Tooltip("Số vàng thưởng cho mỗi giây skip sớm")]
    public int bonusPerSecondSkipped = 0;

    [Header("--- UI NÚT SKIP WAVE ---")]
    [Tooltip("Kéo Nút Skip Wave trong Canvas vào đây (hoặc tự gắn hàm SkipPreparation vào OnClick)")]
    public Button skipWaveButton;

    [Tooltip("Text hiển thị trên nút Skip Wave (Ví dụ: 'START WAVE 1')")]
    public TextMeshProUGUI skipButtonTextTMP;

    [Header("--- HIỆU ỨNG ÁNH SÁNG KHI SKIP (3 GIÂY) ---")]
    [Tooltip("Nguồn sáng Directional Light (Tự động tìm trong scene nếu bỏ trống)")]
    public Light directionalLight;

    [Tooltip("Thời gian quay 1 vòng ánh sáng (mặc định 3.0 giây)")]
    public float lightTransitionDuration = 3.0f;

    [Tooltip("Cường độ ánh sáng tối thiểu ở giữa chu kỳ ban đêm (0.15 = tối nhẹ)")]
    public float minLightIntensity = 0.15f;

    private Coroutine lightTransitionCoroutine;
    private float defaultLightIntensity = 1.0f;
    private Vector3 defaultLightRotation;
    private bool isLightAnimating = false;

    // --- SỰ KIỆN WAVE (PURE WAVE EVENTS) ---
    public event Action<int> OnWaveStart;          // Wave [waveIndex] chính thức xuất chiến
    public event Action<int> OnWaveCompleted;      // Wave [waveIndex] đã tiêu diệt xong
    public event Action<int> OnPreparationStart;   // Bắt đầu thời gian chuẩn bị cho Wave [waveIndex]
    public event Action<int, float> OnWaveSkipped; // Ngưỡng Skip (waveIndex, số giây tiết kiệm được)

    // --- THUỘC TÍNH TƯƠNG THÍCH VỚI CÁC SCRIPT CŨ TRONG DỰ ÁN ---
    public enum Mode { Day, Night }
    public Mode CurrentMode => currentWaveState == WaveState.Preparation ? Mode.Day : Mode.Night;
    public int CurrentDay => currentWave;
    public float DayDuration { get => 0f; set { } }
    public float NightDuration { get => 0f; set { } }
    public event Action OnDayStart;
    public event Action OnNightStart;
    public bool IsDay() => currentWaveState == WaveState.Preparation;
    public bool IsNight() => currentWaveState == WaveState.Combat;

    protected override void Awake()
    {
        base.Awake();
        currentWave = 0;
        currentWaveState = WaveState.Preparation;
        timer = 0f;
        isWaveActive = false;
        isLightAnimating = false;
    }

    private void Start()
    {
        // Tự động kết nối sự kiện OnClick cho nút Skip Wave nếu được kéo thả trong Inspector
        if (skipWaveButton != null)
        {
            skipWaveButton.onClick.RemoveAllListeners();
            skipWaveButton.onClick.AddListener(SkipPreparation);
        }

        // Tự động tìm Directional Light nếu chưa kéo thả trong Inspector
        if (directionalLight == null)
        {
            directionalLight = UnityEngine.Object.FindFirstObjectByType<Light>();
        }

        if (directionalLight != null)
        {
            defaultLightIntensity = directionalLight.intensity;
            defaultLightRotation = directionalLight.transform.localEulerAngles;
        }

        UpdateSkipButtonUI();
        Debug.Log($"[WaveManager] Hệ thống Wave đã sẵn sàng! Đang ở giai đoạn Chuẩn bị cho Wave 1");
    }

    private void Update()
    {
        if (Ins != this) return;
    }

    /// <summary>
    /// NÚT SKIP / START WAVE:
    /// Kích hoạt chu kỳ ánh sáng quay 3 giây. HẾT 3 GIÂY MỚI CHÍNH THỨC TĂNG SỐ NGÀY & BẮT ĐẦU WAVE MỚI.
    /// </summary>
    public void SkipPreparation()
    {
        if (isLightAnimating) return; // Tránh bấm liên tục khi ánh sáng đang xoay 3s

        // NẾU WAVE ĐANG DIỄN RỦA: Kết thúc Wave hiện tại trước khi quay ánh sáng cho Wave mới
        if (currentWaveState == WaveState.Combat)
        {
            Debug.Log($"[WaveManager] ⚡ SKIP WAVE {currentWave}! Bắt đầu chu kỳ chuyển sang Day/Wave tiếp theo.");
            
            isWaveActive = false;
            currentWaveState = WaveState.Preparation;
            OnWaveCompleted?.Invoke(currentWave);
        }
        else
        {
            float timeSaved = timer;
            int bonusReward = Mathf.FloorToInt(timeSaved * bonusPerSecondSkipped);

            Debug.Log($"[WaveManager] ⚡ NGƯỜI CHƠI BẤM START WAVE! Tiết kiệm {timeSaved:F1}s.");

            OnWaveSkipped?.Invoke(currentWave + 1, timeSaved);

            if (enableSkipBonus && bonusReward > 0)
            {
                AddSkipBonusReward(bonusReward);
            }
        }

        // Kích hoạt chu kỳ ánh sáng 3s (Hết 3s mới tăng Ngày & bắt đầu Wave)
        TriggerLightTransitionEffect();
    }

    /// <summary>
    /// Kích hoạt Wave chiến đấu (Chạy chu kỳ ánh sáng trước)
    /// </summary>
    public void StartCombatWave()
    {
        if (isLightAnimating) return;
        TriggerLightTransitionEffect();
    }

    /// <summary>
    /// Kích hoạt hiệu ứng ánh sáng xoay 360 độ theo trục X trong 3 giây
    /// (HẾT 3 GIÂY MỚI CHÍNH THỨC TĂNG SỐ NGÀY VÀ PHÁT SỰ KIỆN WAVE MỚI)
    /// </summary>
    public void TriggerLightTransitionEffect()
    {
        if (directionalLight == null)
        {
            directionalLight = UnityEngine.Object.FindFirstObjectByType<Light>();
        }

        if (lightTransitionCoroutine != null)
        {
            StopCoroutine(lightTransitionCoroutine);
        }

        lightTransitionCoroutine = StartCoroutine(AnimateLightRotationRoutine());
    }

    private System.Collections.IEnumerator AnimateLightRotationRoutine()
    {
        isLightAnimating = true;
        if (skipWaveButton != null) skipWaveButton.interactable = false;

        float elapsedTime = 0f;
        Vector3 startRot = directionalLight != null ? directionalLight.transform.localEulerAngles : Vector3.zero;
        float originalIntensity = defaultLightIntensity > 0 ? defaultLightIntensity : (directionalLight != null ? directionalLight.intensity : 1.0f);

        while (elapsedTime < lightTransitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / lightTransitionDuration);

            if (directionalLight != null)
            {
                // 1. Quay 360 độ theo trục X từ góc ban đầu trong 3 giây
                float currentX = startRot.x + (t * 360f);
                directionalLight.transform.localRotation = Quaternion.Euler(currentX, startRot.y, startRot.z);

                // 2. Ánh sáng giảm dần ở giữa chu kỳ (t = 0.5 -> tối) rồi sáng dần lại (t = 1.0 -> sáng hoàn toàn)
                float intensityMultiplier = 0.5f * (1f + Mathf.Cos(t * 2f * Mathf.PI));
                directionalLight.intensity = Mathf.Lerp(minLightIntensity, originalIntensity, intensityMultiplier);
            }

            yield return null;
        }

        // Kết thúc chu kỳ 3s, trả lại góc xoay và cường độ ban đầu
        if (directionalLight != null)
        {
            directionalLight.transform.localRotation = Quaternion.Euler(startRot);
            directionalLight.intensity = originalIntensity;
        }
        lightTransitionCoroutine = null;

        // === CHÍNH THỨC TĂNG SỐ NGÀY / WAVE SAU KHI HẾT CHU KỲ ÁNH SÁNG (3S) ===
        currentWave++;
        currentWaveState = WaveState.Combat;
        isWaveActive = true;
        timer = 0;
        isLightAnimating = false;

        if (skipWaveButton != null)
        {
            skipWaveButton.interactable = true;
        }

        Debug.Log($"[WaveManager] 🔥 HẾT CHU KỲ ÁNH SÁNG (3S)! CHÍNH THỨC TĂNG LÊN DAY {currentWave} (WAVE {currentWave})!");

        OnNightStart?.Invoke();
        OnWaveStart?.Invoke(currentWave);

        // 🌾 Tự động cộng tài nguyên từ toàn bộ công trình sản xuất khi bắt đầu Wave mới
        WaveResourceManager.CollectBuildingResourcesForWave(currentWave);

        // 🎓 Thông báo tiến trình cho Tutorial
        if (CampaignTutorialManager.Ins != null)
        {
            CampaignTutorialManager.Ins.OnDayOrWaveIncremented();
        }

        UpdateSkipButtonUI();
    }

    /// <summary>
    /// HÀM HOÀN THÀNH WAVE:
    /// Gọi khi spawner/GameManager xác nhận đã dọn sạch toàn bộ quái trong Wave.
    /// Chuyển về trạng thái Chuẩn bị cho Wave tiếp theo.
    /// </summary>
    public void CompleteWave()
    {
        if (!isWaveActive && currentWaveState == WaveState.Preparation) return;

        Debug.Log($"[WaveManager]  WAVE {currentWave} ĐÃ TIÊU DIỆT HOÀN TOÀN!");

        OnWaveCompleted?.Invoke(currentWave);

        // Chuyển về trạng thái Chuẩn bị cho Wave tiếp theo
        currentWaveState = WaveState.Preparation;
        isWaveActive = false;
        timer = 0f;

        if (skipWaveButton != null)
        {
            skipWaveButton.interactable = true;
        }

        Debug.Log($"[WaveManager] ⏱ BẮT ĐẦU CHUẨN BỊ CHO WAVE {currentWave + 1}");

        OnDayStart?.Invoke();
        OnPreparationStart?.Invoke(currentWave + 1);
        UpdateSkipButtonUI();
    }

    /// <summary>
    /// Cập nhật hiển thị giao diện Nút UI Skip Wave (Sạch sẽ, không hiện +G)
    /// </summary>
    private void UpdateSkipButtonUI()
    {
        if (skipWaveButton != null)
        {
            skipWaveButton.interactable = true;

            if (skipButtonTextTMP != null)
            {
                if (currentWaveState == WaveState.Preparation)
                {
                    skipButtonTextTMP.text = $"START WAVE {currentWave + 1}";
                }
                else
                {
                    skipButtonTextTMP.text = $"SKIP TO WAVE {currentWave + 1}";
                }
            }
        }
    }

    /// <summary>
    /// Khôi phục trạng thái Wave (dùng khi quay lại từ SceneBattle)
    /// </summary>
    public void RestoreWaveState(int wave, WaveState state, bool active)
    {
        currentWave = wave;
        currentWaveState = state;
        isWaveActive = active;
        UpdateSkipButtonUI();
    }

    private void AddSkipBonusReward(int rewardAmount)
    {
        Debug.Log($"[WaveManager] 💰 Cộng thêm {rewardAmount} vàng thưởng Skip sớm.");
    }
}
