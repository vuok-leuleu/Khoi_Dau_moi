using System;
using UnityEngine;

public class DayNightManager : Singleton<DayNightManager>
{
    public event Action OnDayStart;
    public event Action OnDayEnd;
    public event Action OnNightStart;
    public event Action OnWaveStart;

    [Header("Wave Settings")]
    [Tooltip("Số wave hiện tại. Wave 0 tương đương tutorial ban đầu.")]
    [SerializeField] private int currentWave = 0;

    [Tooltip("Cho phép Skip Wave bằng code hoặc UI nếu có gắn Button.")]
    public bool enableSkipWave = true;

    [Header("End of Day Rewards")]
    [SerializeField] private int normalDailyGoldReward = 10;
    [SerializeField] private int specialDayGoldReward = 50;

    private bool hasInitializedFirstWave;

    public int CurrentWave => currentWave;
    public int CurrentDay => currentWave;

    public int GetGoldRewardForDay(int day)
    {
        if (day == 0) return 0;
        if (day % 5 == 0) return specialDayGoldReward;
        return normalDailyGoldReward;
    }

    public int GetPendingGoldReward() => GetGoldRewardForDay(CurrentDay);

    protected override void Awake()
    {
        base.Awake(); // Gọi Singleton

        currentWave = 1;
        hasInitializedFirstWave = false;

        Debug.Log($"[DayNightManager] Đã khởi tạo thành công! Bắt đầu Wave {currentWave}");
    }

    private void LateUpdate()
    {
        if (Ins != this) return;

        if (!hasInitializedFirstWave)
        {
            hasInitializedFirstWave = true;
            BeginWave();
        }
    }

    private void BeginWave()
    {
        Debug.Log($"[DayNightManager] ---> Bắt đầu Wave {currentWave}");
        OnWaveStart?.Invoke();
        OnDayStart?.Invoke(); // compatibility hook for legacy subscribers
    }

    public void NextWave()
    {
        currentWave++;
        Debug.Log($"[DayNightManager] ---> Chuyển sang Wave {currentWave}");
        OnWaveStart?.Invoke();
        OnDayStart?.Invoke(); // compatibility hook for legacy subscribers
    }

    public void SkipWave()
    {
        if (!enableSkipWave) return;
        Debug.Log("[DayNightManager] Skip Wave requested.");
        NextWave();
    }

    public void EndDay()
    {
        if (!enableSkipWave) return;

        int reward = GetPendingGoldReward();
        Debug.Log($"[DayNightManager] Kết thúc Ngày {CurrentDay}. Thưởng vàng {reward}.");

        if (JsonDataManager.Ins != null)
        {
            JsonDataManager.Ins.AddGold(reward);
            JsonDataManager.Ins.BroadcastAllResources();
        }

        UIManager.Ins?.ShowWarning($"Kết thúc ngày {CurrentDay}. Bạn nhận {reward} vàng.");
        OnDayEnd?.Invoke();
        NextWave();
    }

    public void SkipDay() => EndDay();

    public bool IsDay() => true;
    public bool IsNight() => false;
}
