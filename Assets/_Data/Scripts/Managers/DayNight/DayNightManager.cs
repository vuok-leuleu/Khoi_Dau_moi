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

    [Header("--- UI NÚT SKIP WAVE & HIỂN THỊ NGÀY ---")]
    [Tooltip("Kéo Nút Skip Wave trong Canvas vào đây (hoặc tự gắn hàm SkipPreparation vào OnClick)")]
    public Button skipWaveButton;

    [Tooltip("Text hiển thị trên nút Skip Wave (Ví dụ: 'START WAVE 1')")]
    public TextMeshProUGUI skipButtonTextTMP;

    [Tooltip("Text hiển thị Ngày trên UI Canvas (Ví dụ: 'Ngày 0')")]
    public TextMeshProUGUI dayTextTMP;

    [Header("--- HIỆU ỨNG ÁNH SÁNG KHI SKIP (3 GIÂY) ---")]
    [Tooltip("Nguồn sáng Directional Light (Tự động tìm trong scene nếu bỏ trống)")]
    public Light directionalLight;

    [Tooltip("Thời gian quay 1 vòng ánh sáng (mặc định 3.0 giây)")]
    public float lightTransitionDuration = 3.0f;

    [Tooltip("Cường độ ánh sáng tối thiểu ở giữa chu kỳ ban đêm (0.15 = tối nhẹ)")]
    public float minLightIntensity = 0.15f;

    [Header("--- HIỆU ỨNG ĐÁM MÂY CHE MÀN HÌNH (CLOUD TRANSITION) ---")]
    [Tooltip("Bật/tắt hiệu ứng đám mây kéo che màn hình khi Skip Day")]
    public bool enableCloudTransition = true;

    [Tooltip("Kéo các Transform đám mây vào đây (Ví dụ: Cloud 1, Cloud 2, Cloud 3)")]
    public Transform[] cloudTransforms;

    [Tooltip("Khoảng cách/Hướng di chuyển đẩy mây ra ngoài màn hình ở trạng thái nghỉ.\n" +
             "Ví dụ:\n" +
             "Mây 1 (Trái): (-35, 0, 0)\n" +
             "Mây 2 (Phải): (35, 0, 0)\n" +
             "Mây 3 (Trái/Trên): (-30, 8, 0)")]
    public Vector3[] cloudOffscreenOffsets;

    [Tooltip("Tích vào nếu Mây thuộc UI Canvas (RectTransform)")]
    public bool isUICloud = false;

    private Vector3[] cloudClosedPositions; // Vị trí che màn hình (xếp sẵn trong Editor)
    private Vector3[] cloudOpenPositions;   // Vị trí ngoài màn hình (tự tính theo offset)
    private bool isCloudPosInitialized = false;

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
            skipWaveButton.onClick.RemoveListener(SkipPreparation);
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

        InitCloudPositions();
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

        InitCloudPositions();

        float elapsedTime = 0f;
        Vector3 startRot = directionalLight != null ? directionalLight.transform.localEulerAngles : Vector3.zero;
        float originalIntensity = defaultLightIntensity > 0 ? defaultLightIntensity : (directionalLight != null ? directionalLight.intensity : 1.0f);
        bool hasIncrementedDay = false;

        while (elapsedTime < lightTransitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / lightTransitionDuration);

            // 1. Ánh sáng xoay & đổi cường độ
            if (directionalLight != null)
            {
                float currentX = startRot.x + (t * 360f);
                directionalLight.transform.localRotation = Quaternion.Euler(currentX, startRot.y, startRot.z);

                float intensityMultiplier = 0.5f * (1f + Mathf.Cos(t * 2f * Mathf.PI));
                directionalLight.intensity = Mathf.Lerp(minLightIntensity, originalIntensity, intensityMultiplier);
            }

            // 2. Di chuyển các đám mây (Pha 1: Trái/Phải kéo vào giữa | Pha 2: Từ giữa mở ra 2 bên)
            if (enableCloudTransition)
            {
                UpdateCloudTransition(elapsedTime, lightTransitionDuration);
            }

            // 3. Khi mây che kín màn hình ở giữa chu kỳ (t >= 0.5f), chính thức tăng ngày ngay lập tức đằng sau đám mây
            if (t >= 0.5f && !hasIncrementedDay)
            {
                hasIncrementedDay = true;
                ExecuteDayIncrementLogic();
            }

            yield return null;
        }

        // Đảm bảo đã tăng ngày nếu chu kỳ hoàn tất
        if (!hasIncrementedDay)
        {
            hasIncrementedDay = true;
            ExecuteDayIncrementLogic();
        }

        // Trả lại góc xoay và cường độ ánh sáng ban đầu
        if (directionalLight != null)
        {
            directionalLight.transform.localRotation = Quaternion.Euler(startRot);
            directionalLight.intensity = originalIntensity;
        }

        // Trả các đám mây về vị trí mở hoàn toàn ngoài màn hình
        if (enableCloudTransition && cloudTransforms != null && cloudOpenPositions != null)
        {
            for (int i = 0; i < cloudTransforms.Length; i++)
            {
                if (cloudTransforms[i] != null && i < cloudOpenPositions.Length)
                {
                    SetCloudPosition(cloudTransforms[i], cloudOpenPositions[i]);
                }
            }
        }

        lightTransitionCoroutine = null;
        isLightAnimating = false;

        if (skipWaveButton != null)
        {
            skipWaveButton.interactable = true;
        }

        UpdateSkipButtonUI();
    }

    /// <summary>
    /// Thực hiện logic chính thức tăng Ngày / Wave khi màn hình đang được Đám mây che kín
    /// </summary>
    private void ExecuteDayIncrementLogic()
    {
        currentWave++;
        currentWaveState = WaveState.Combat;
        isWaveActive = true;
        timer = 0;

        Debug.Log($"[WaveManager] 🔥 ĐÁM MÂY CHE KÍN MÀN HÌNH! CHÍNH THỨC TĂNG LÊN DAY {currentWave} (WAVE {currentWave})!");

        OnNightStart?.Invoke();
        OnWaveStart?.Invoke(currentWave);

        // 🌾 Tự động cộng tài nguyên từ toàn bộ công trình sản xuất khi bắt đầu Wave mới
        WaveResourceManager.CollectBuildingResourcesForWave(currentWave);

        // 💾 TỰ ĐỘNG LƯU DỮ LIỆU TOÀN BỘ GAME MỖI KHI QUA 1 NGÀY / WAVE MỚI
        if (BuildingSystem.Ins != null) BuildingSystem.Ins.SaveBuildingsToSlot(1);
        if (SettlementManager.Ins != null) SettlementManager.Ins.SaveAllSettlementsState();
        
        UILinh uiLinh = UILinh.Ins != null ? UILinh.Ins : UnityEngine.Object.FindFirstObjectByType<UILinh>();
        if (uiLinh != null) uiLinh.SaveGame();

        PlayerPrefs.Save();
        Debug.Log($"[DayNightManager] 💾 Đã TỰ ĐỘNG LƯU TOÀN BỘ GAME khi trôi qua Ngày {currentWave}!");

        // 🎓 Thông báo tiến trình cho Tutorial
        if (CampaignTutorialManager.Ins != null)
        {
            CampaignTutorialManager.Ins.OnDayOrWaveIncremented();
        }

        UpdateSkipButtonUI();
    }

    // ================= LOGIC XỬ LÝ HIỆU ỨNG ĐÁM MÂY =================
    private Vector3 GetCloudPosition(Transform cloud)
    {
        if (cloud == null) return Vector3.zero;
        if (isUICloud && cloud is RectTransform rect)
        {
            return rect.anchoredPosition3D;
        }
        return cloud.localPosition;
    }

    private void SetCloudPosition(Transform cloud, Vector3 targetPos)
    {
        if (cloud == null) return;
        if (isUICloud && cloud is RectTransform rect)
        {
            rect.anchoredPosition3D = targetPos;
        }
        else
        {
            cloud.localPosition = targetPos;
        }
    }

    private Vector3 GetDefaultOffsetForCloud(int index, bool isChildOfCamera)
    {
        if (cloudOffscreenOffsets != null && index < cloudOffscreenOffsets.Length && cloudOffscreenOffsets[index] != Vector3.zero)
        {
            return cloudOffscreenOffsets[index];
        }

        if (isChildOfCamera)
        {
            switch (index % 3)
            {
                case 0: return new Vector3(-15f, 0f, 0f); // Mây 1: Từ Bên Trái
                case 1: return new Vector3(15f, 0f, 0f);  // Mây 2: Từ Bên Phải
                case 2: return new Vector3(-12f, 4f, 0f); // Mây 3: Từ Bên Trái / Trên
                default: return new Vector3(-15f, 0f, 0f);
            }
        }
        else
        {
            switch (index % 3)
            {
                case 0: return new Vector3(-35f, 0f, 0f); // Mây 1: Từ Bên Trái
                case 1: return new Vector3(35f, 0f, 0f);  // Mây 2: Từ Bên Phải
                case 2: return new Vector3(-30f, 8f, 0f); // Mây 3: Từ Bên Trái / Trên
                default: return new Vector3(-35f, 0f, 0f);
            }
        }
    }

    public void InitCloudPositions()
    {
        if (!enableCloudTransition) return;

        Camera mainCam = Camera.main;
        if (mainCam == null) mainCam = UnityEngine.Object.FindFirstObjectByType<Camera>();

        // Tự động tìm các đám mây dưới Main Camera nếu trong Inspector chưa kéo thả (Size = 0)
        if (cloudTransforms == null || cloudTransforms.Length == 0)
        {
            if (mainCam != null)
            {
                System.Collections.Generic.List<Transform> foundClouds = new System.Collections.Generic.List<Transform>();
                foreach (Transform child in mainCam.transform)
                {
                    if (child.name.ToLower().Contains("cloud"))
                    {
                        foundClouds.Add(child);
                    }
                }
                if (foundClouds.Count > 0)
                {
                    cloudTransforms = foundClouds.ToArray();
                    Debug.Log($"[DayNightManager] ☁️ Tự động tìm thấy {cloudTransforms.Length} đám mây dưới {mainCam.name}!");
                }
            }
        }

        if (cloudTransforms == null || cloudTransforms.Length == 0) return;

        if (!isCloudPosInitialized || cloudClosedPositions == null || cloudClosedPositions.Length != cloudTransforms.Length)
        {
            cloudClosedPositions = new Vector3[cloudTransforms.Length];
            cloudOpenPositions = new Vector3[cloudTransforms.Length];

            for (int i = 0; i < cloudTransforms.Length; i++)
            {
                if (cloudTransforms[i] != null)
                {
                    bool isChildOfCam = mainCam != null && cloudTransforms[i].IsChildOf(mainCam.transform);

                    // Nếu mây là con của Camera và Z đang = 0 (bị khuất sau kính camera), tự điều chỉnh Z = 3.5f để nằm trước ống kính camera
                    if (isChildOfCam && !isUICloud)
                    {
                        Vector3 currentLocal = cloudTransforms[i].localPosition;
                        if (Mathf.Abs(currentLocal.z) < 1.0f)
                        {
                            currentLocal.z = 3.5f;
                            cloudTransforms[i].localPosition = currentLocal;
                        }
                    }

                    cloudClosedPositions[i] = GetCloudPosition(cloudTransforms[i]);
                    Vector3 offset = GetDefaultOffsetForCloud(i, isChildOfCam);
                    cloudOpenPositions[i] = cloudClosedPositions[i] + offset;

                    // Ngay khi khởi động, ẩn mây ra ngoài màn hình
                    SetCloudPosition(cloudTransforms[i], cloudOpenPositions[i]);
                }
            }
            isCloudPosInitialized = true;
        }
    }

    private void UpdateCloudTransition(float elapsedTime, float totalDuration)
    {
        if (cloudTransforms == null || cloudClosedPositions == null) return;

        float halfDuration = totalDuration * 0.5f;

        for (int i = 0; i < cloudTransforms.Length; i++)
        {
            if (cloudTransforms[i] == null) continue;

            Vector3 startPos;
            Vector3 targetPos;
            float t;

            if (elapsedTime <= halfDuration)
            {
                // Pha 1 (0 -> 1.5s): Mây kéo từ ngoài VÀO GIỮA che màn hình
                startPos = cloudOpenPositions[i];
                targetPos = cloudClosedPositions[i];
                t = Mathf.Clamp01(elapsedTime / halfDuration);
            }
            else
            {
                // Pha 2 (1.5s -> 3.0s): Mây từ giữa MỞ RA 2 BÊN ra ngoài màn hình
                startPos = cloudClosedPositions[i];
                targetPos = cloudOpenPositions[i];
                t = Mathf.Clamp01((elapsedTime - halfDuration) / halfDuration);
            }

            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, smoothT);
            SetCloudPosition(cloudTransforms[i], currentPos);
        }
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
    /// Cập nhật hiển thị giao diện Text Ngày (1 Wave = 1 Ngày)
    /// </summary>
    public void UpdateDayTextUI()
    {
        if (dayTextTMP != null)
        {
            dayTextTMP.text = $"Ngày {currentWave}";
        }
    }

    /// <summary>
    /// Cập nhật hiển thị giao diện Nút UI Skip Wave (Sạch sẽ, không hiện +G) và Text Ngày
    /// </summary>
    private void UpdateSkipButtonUI()
    {
        UpdateDayTextUI();

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
