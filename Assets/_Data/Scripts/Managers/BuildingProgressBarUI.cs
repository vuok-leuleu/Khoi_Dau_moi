using UnityEngine;
using UnityEngine.UI;
using TMPro;

/*
 * BuildingProgressBarUI.cs
 * Folder: Scripts/UI/
 * Dự án: KHẨN HOANG (PENTA DEV)
 * Người thực hiện: VŨ (Luồng UI)
 * * ĐÃ SỬA LỖI:
 * 1. Ẩn Slider triệt để khi Load Game / OnEnable (Chỉ hiện khi UpdateProgress thực sự chạy).
 * 2. Khắc phục lỗi Slider đơ khi công trình đạt Max Level.
 */
public class BuildingProgressBarUI : MonoBehaviour
{
    [Header("[Cấu Hình Thành Phần UI (Kéo Thả Inspector)]")]
    public Slider upgradeProgressBar;       
    public TMP_Text upgradeTimerText;       

    [Header("[VFX 3 Chức Năng Riêng Biệt]")]
    [Tooltip("Bụi mịn bám đất khi vừa đặt/di chuyển nhà")]
    public ParticleSystem placementDustVFX; 
    
    [Tooltip("Khói + vụn vỡ thi công (Chạy liên tục suốt thời gian xây/nâng cấp)")]
    public ParticleSystem constructionLoopVFX;     
    
    [Tooltip("Hào quang Aura hoàn thành (Quét 1 lần khi vừa xây xong)")]
    public ParticleSystem completionAuraVFX;      

    [Header("[Nguồn Âm Thanh - Audio Source]")]
    [SerializeField] private AudioSource[] upgradeAudioSources;
    [SerializeField] private AudioSource[] placementAudioSources;
    [SerializeField] private AudioSource[] completionAudioSources;

    [Header("[File Âm Thanh - SFX Pool]")]
    [SerializeField] private AudioClip[] upgradeLoopSFXPool;
    [SerializeField] private AudioClip placementSFX;
    [SerializeField] private AudioClip completionSFX;

    [Header("[Giới Hạn Thời Gian Âm Thanh Thi Công]")]
    [Tooltip("Thời gian phát tiếng gõ búa tối đa (giây). Đặt <= 0 để phát suốt cả quá trình.")]
    [SerializeField] private float maxConstructionSoundDuration = 4f;

    private UpgradeableBuilding _ownerBuilding;
    private bool _hasStoppedLoopSFX = false;

    private void Awake()
    {
        _ownerBuilding = GetComponentInParent<UpgradeableBuilding>();
        
        // Tự động tìm VFX nếu chưa kéo thả
        if (placementDustVFX == null) placementDustVFX = transform.Find("PlacementDustVFX")?.GetComponent<ParticleSystem>();
        if (constructionLoopVFX == null) constructionLoopVFX = transform.Find("ConstructionLoopVFX")?.GetComponent<ParticleSystem>();
        if (completionAuraVFX == null) completionAuraVFX = transform.Find("CompletionAuraVFX")?.GetComponent<ParticleSystem>();

        InitAudioSources(upgradeAudioSources);
        InitAudioSources(placementAudioSources);
        InitAudioSources(completionAudioSources);

        // Mặc định ẩn UI đếm số khi khởi tạo
        HideProgress();
        DeactivateAllVFX();
    }

    private void InitAudioSources(AudioSource[] sources)
    {
        if (sources == null) return;
        foreach (var src in sources)
        {
            if (src != null) src.playOnAwake = false;
        }
    }

    private float _realTimeActive = 0f;

    private void OnEnable()
    {
        _realTimeActive = 0f;
        if (_ownerBuilding != null)
        {
            BuildingProgressBridge.RegisterUI(_ownerBuilding, this);

            // CHỈ BẬT UI khi nhà đang trong tiến trình Nâng cấp/Xây mới thực sự
            // SỬA DÒNG NÀY (Thêm kiểm tra !_ownerBuilding.IsRuined)
            if (!_ownerBuilding.IsUpgrading && !_ownerBuilding.IsRuined)
            {
                HideProgress();
                DeactivateAllVFX();
            }
        }
        else
        {
            HideProgress();
        }
    }

    private void OnDisable()
    {
        if (_ownerBuilding != null)
        {
            BuildingProgressBridge.UnregisterUI(_ownerBuilding);
        }

        StopAllUpgradeLoopSFX();
    }

    public void DeactivateAllVFX()
    {
        if (placementDustVFX != null) placementDustVFX.gameObject.SetActive(false);
        if (constructionLoopVFX != null)
        {
            if (constructionLoopVFX.isPlaying) constructionLoopVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            constructionLoopVFX.gameObject.SetActive(false);
        }
        if (completionAuraVFX != null) completionAuraVFX.gameObject.SetActive(false);
    }

    /// <summary>
    /// Phát hiệu ứng bụi bám đất (Chỉ gọi khi đặt/di chuyển nhà xong)
    /// </summary>
    public void PlayPlacementVFX()
    {
        StopConstructionVFXSmoothly();

        if (placementDustVFX != null)
        {
            placementDustVFX.gameObject.SetActive(true);
            placementDustVFX.Stop();
            placementDustVFX.Play();
        }

        if (placementSFX != null && placementAudioSources != null && placementAudioSources.Length > 0)
        {
            if (placementAudioSources[0] != null)
            {
                placementAudioSources[0].PlayOneShot(placementSFX);
            }
        }
    }

    /// <summary>
    /// Cập nhật thanh tiến trình & đếm ngược thời gian
    /// </summary>
    public void UpdateProgress(float currentTimer, float totalDuration, bool isWaveMode = false)
    {
        _realTimeActive += Time.deltaTime;
        // Bật UI Slider & Text lên CHỈ khi hàm này được gọi
        if (upgradeProgressBar != null)
        {
            if (!upgradeProgressBar.gameObject.activeSelf) upgradeProgressBar.gameObject.SetActive(true);
            upgradeProgressBar.maxValue = totalDuration;
            upgradeProgressBar.value = currentTimer;
        }

        if (upgradeTimerText != null)
        {
            if (!upgradeTimerText.gameObject.activeSelf) upgradeTimerText.gameObject.SetActive(true);
            float timeLeft = Mathf.Max(0f, totalDuration - currentTimer);
            if (isWaveMode)
            {
                upgradeTimerText.text = $"{Mathf.CeilToInt(timeLeft)} Wave";
            }
            else
            {
                upgradeTimerText.text = $"{timeLeft:F1}s";
            }
        }

        // Bật VFX khói thi công mượt mà
        if (constructionLoopVFX != null)
        {
            if (!constructionLoopVFX.gameObject.activeSelf) constructionLoopVFX.gameObject.SetActive(true);
            if (!constructionLoopVFX.isPlaying) constructionLoopVFX.Play();
        }

        // Xử lý âm thanh gõ thi công trong giới hạn thời gian thực tế
        bool isSoundWithinDuration = maxConstructionSoundDuration <= 0f || _realTimeActive < maxConstructionSoundDuration;

        if (isSoundWithinDuration)
        {
            _hasStoppedLoopSFX = false;
            PlayUpgradeLoopSFX();
        }
        else if (!_hasStoppedLoopSFX)
        {
            _hasStoppedLoopSFX = true;
            StopAllUpgradeLoopSFX();
        }
    }

    /// <summary>
    /// Xử lý chuỗi hiệu ứng khi xây dựng/nâng cấp hoàn tất
    /// </summary>
    public void HandleCompleteSequence()
    {
        StopAllUpgradeLoopSFX();

        // Tắt khói thi công mượt mà (để hạt cũ tan dần tự nhiên)
        StopConstructionVFXSmoothly();

        // Bật Aura hào quang hoàn thành
        if (completionAuraVFX != null)
        {
            completionAuraVFX.gameObject.SetActive(true);
            completionAuraVFX.Stop();
            completionAuraVFX.Play();
        }

        // Phát tiếng hoàn thành (1 nguồn duy nhất)
        if (completionSFX != null && completionAudioSources != null && completionAudioSources.Length > 0)
        {
            if (completionAudioSources[0] != null)
            {
                completionAudioSources[0].PlayOneShot(completionSFX);
            }
        }

        HideProgress();
    }

    private void StopConstructionVFXSmoothly()
    {
        if (constructionLoopVFX != null)
        {
            if (constructionLoopVFX.isPlaying)
            {
                constructionLoopVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            constructionLoopVFX.gameObject.SetActive(false);
        }
    }

    private void PlayUpgradeLoopSFX()
    {
        if (upgradeAudioSources == null || upgradeLoopSFXPool == null) return;
        
        int loopCount = Mathf.Min(upgradeAudioSources.Length, upgradeLoopSFXPool.Length);
        for (int i = 0; i < loopCount; i++)
        {
            AudioSource src = upgradeAudioSources[i];
            AudioClip clip = upgradeLoopSFXPool[i];

            if (src != null && clip != null && !src.isPlaying)
            {
                src.clip = clip;
                src.loop = true;
                src.Play();
            }
        }
    }

    private void StopAllUpgradeLoopSFX()
    {
        if (upgradeAudioSources == null) return;
        foreach (var src in upgradeAudioSources)
        {
            if (src != null && src.isPlaying)
            {
                src.Stop();
                src.loop = false;
                src.clip = null; 
            }
        }
    }

    public void HideProgress()
    {
        if (upgradeProgressBar != null) upgradeProgressBar.gameObject.SetActive(false);
        if (upgradeTimerText != null) upgradeTimerText.gameObject.SetActive(false);
    }
}

public static class BuildingProgressBridge
{
    private static System.Collections.Generic.Dictionary<UpgradeableBuilding, BuildingProgressBarUI> _uiRegistry = 
        new System.Collections.Generic.Dictionary<UpgradeableBuilding, BuildingProgressBarUI>();

    public static void RegisterUI(UpgradeableBuilding building, BuildingProgressBarUI ui)
    {
        if (!_uiRegistry.ContainsKey(building))
        {
            _uiRegistry.Add(building, ui);
        }
        else
        {
            _uiRegistry[building] = ui;
        }
    }

    public static void UnregisterUI(UpgradeableBuilding building)
    {
        if (_uiRegistry.ContainsKey(building))
        {
            _uiRegistry.Remove(building);
        }
    }

    public static BuildingProgressBarUI GetUI(UpgradeableBuilding building)
    {
        if (building != null && _uiRegistry.TryGetValue(building, out var ui)) return ui;
        return null;
    }
}

public static class UIManagerExtensions
{
    // Cập nhật tiến độ chỉ cho 1 công trình chỉ định
    public static void UpdateUpgradeProgress(this UIManager uiManager, UpgradeableBuilding building, float currentTimer, float totalDuration, bool isWaveMode = false)
    {
        if (building == null) return;
        var targetUI = BuildingProgressBridge.GetUI(building);
        if (targetUI != null)
        {
            targetUI.UpdateProgress(currentTimer, totalDuration, isWaveMode);
        }
    }

    // Ẩn tiến độ chỉ cho 1 công trình chỉ định
    public static void HideUpgradeProgress(this UIManager uiManager, UpgradeableBuilding building)
    {
        if (building == null) return;
        var targetUI = BuildingProgressBridge.GetUI(building);
        if (targetUI != null)
        {
            targetUI.HandleCompleteSequence();
        }
    }
}