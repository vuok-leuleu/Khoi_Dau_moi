using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SkipButtonAnimator : MonoBehaviour
{
    public enum AnimMode { UnityAnimator, SpriteArray, SlotMachine }

    [Header("--- CHẾ ĐỘ ANIMATION ---")]
    public AnimMode mode = AnimMode.UnityAnimator;

    [Header("--- CHẾ ĐỘ 1: UNITY ANIMATOR ---")]
    [Tooltip("Tên State Animation trong cửa sổ Animator Controller")]
    public string stateName = "Button_DayNight_Anim";

    [Tooltip("State nghỉ được phát sau khi animation chuyển ngày kết thúc")]
    public string idleStateName = "Idle";

    [Header("--- CHẾ ĐỘ 2: SPRITE ARRAY ---")]
    [Tooltip("Kéo 15 tấm ảnh Sprite (từ Frame 0 -> 7 -> 0) vào đây")]
    public Sprite[] frames;

    [Header("--- CHẾ ĐỘ 3: QUAY KIỂU MÁY ĐÁNH BẠC ---")]
    [Tooltip("Số vòng quay của mặt trời trong một lần chuyển Wave.")]
    [Min(1)] public int slotSpinCycles = 4;

    [Tooltip("Phần đầu dùng để tăng tốc trước khi mặt trời quay đều.")]
    [Range(0.01f, 0.45f)] public float slotAccelerationDuration = 0.12f;

    [Tooltip("Thời điểm bắt đầu giảm tốc. Giá trị lớn hơn sẽ giữ tốc độ cao lâu hơn.")]
    [Range(0.5f, 0.98f)] public float slotSlowdownStart = 0.68f;

    [Tooltip("Mốc normalized time trong clip tương ứng với một vòng quay hoàn chỉnh. Với SunAnimation hiện tại, 0.5 là chuỗi frame 0 -> 8.")]
    [Range(0.05f, 1f)] public float slotCycleEndNormalizedTime = 0.5f;

    private Animator animator;
    private Image buttonImage;
    private Coroutine animCoroutine;
    private Coroutine resetCoroutine;
    private DayNightManager subscribedManager;
    private float defaultAnimatorSpeed = 1f;
    private bool isAnimating = false;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        buttonImage = GetComponent<Image>();
        if (animator != null)
        {
            defaultAnimatorSpeed = animator.speed;
        }
    }

    private void OnEnable()
    {
        SubscribeToDayNightManager();
    }

    private void Start()
    {
        // Bảo đảm Singleton đã khởi tạo xong trước khi đăng ký sự kiện.
        SubscribeToDayNightManager();
    }

    private void Update()
    {
        // Hỗ trợ DayNightManager được spawn sau UI.
        if (subscribedManager == null)
        {
            SubscribeToDayNightManager();
        }
    }

    private void OnDisable()
    {
        if (subscribedManager != null)
        {
            subscribedManager.OnTransitionStarted -= OnTransitionStartedHandler;
            subscribedManager = null;
        }

        StopCurrentAnimation();
    }

    private void SubscribeToDayNightManager()
    {
        DayNightManager manager = DayNightManager.Ins;
        if (manager == null || subscribedManager == manager) return;

        if (subscribedManager != null)
        {
            subscribedManager.OnTransitionStarted -= OnTransitionStartedHandler;
        }

        subscribedManager = manager;
        subscribedManager.OnTransitionStarted += OnTransitionStartedHandler;
    }

    private void OnTransitionStartedHandler(float transitionDuration)
    {
        PlayAnimation(transitionDuration);
    }

    // Giữ hàm public để các Button/Event cũ trong scene không bị mất liên kết.
    public void PlayAnimation()
    {
        float duration = DayNightManager.Ins != null
            ? DayNightManager.Ins.lightTransitionDuration
            : 3.0f;

        PlayAnimation(duration);
    }

    private void PlayAnimation(float transitionDuration)
    {
        float duration = Mathf.Max(0.01f, transitionDuration);
        StopCurrentAnimation();

        if (mode == AnimMode.UnityAnimator)
        {
            PlayAnimatorAnimation(duration);
        }
        else if (mode == AnimMode.SpriteArray)
        {
            if (frames == null || frames.Length == 0 || buttonImage == null) return;

            isAnimating = true;
            animCoroutine = StartCoroutine(PlayAnimationRoutine(duration));
        }
        else
        {
            PlaySlotMachineAnimation(duration);
        }
    }

    private void PlayAnimatorAnimation(float duration)
    {
        if (animator == null)
        {
            Debug.LogWarning("[SkipButtonAnimator] ⚠️ Không tìm thấy Animator Component trên GameObject này!");
            return;
        }

        if (!TryGetState(stateName, out int layerIndex, out int stateHash))
        {
            Debug.LogWarning($"[SkipButtonAnimator] ⚠️ Không tìm thấy Animator State '{stateName}'.");
            return;
        }

        isAnimating = true;
        animator.speed = 1f;
        animator.Play(stateHash, layerIndex, 0f);
        animator.Update(0f);

        // Tự điều chỉnh tốc độ để dù clip là 1s, 3s hay được đổi sau này,
        // nó vẫn kết thúc đồng thời với lightTransitionDuration.
        float stateLength = animator.GetCurrentAnimatorStateInfo(layerIndex).length;
        if (stateLength > 0.0001f)
        {
            animator.speed = stateLength / duration;
        }

        resetCoroutine = StartCoroutine(FinishAnimatorRoutine(duration, layerIndex));
    }

    private void PlaySlotMachineAnimation(float duration)
    {
        if (animator == null)
        {
            Debug.LogWarning("[SkipButtonAnimator] Không tìm thấy Animator Component để quay kiểu máy đánh bạc!", this);
            return;
        }

        if (!TryGetState(stateName, out int layerIndex, out int stateHash))
        {
            Debug.LogWarning($"[SkipButtonAnimator] Không tìm thấy Animator State '{stateName}'.", this);
            return;
        }

        isAnimating = true;
        animator.speed = 1f;
        resetCoroutine = StartCoroutine(SlotMachineRoutine(duration, layerIndex, stateHash));
    }

    private bool TryGetState(string state, out int layerIndex, out int stateHash)
    {
        layerIndex = -1;
        stateHash = 0;

        if (animator == null || string.IsNullOrWhiteSpace(state)) return false;

        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            int shortNameHash = Animator.StringToHash(state);
            if (animator.HasState(layer, shortNameHash))
            {
                layerIndex = layer;
                stateHash = shortNameHash;
                return true;
            }

            string fullName = $"{animator.GetLayerName(layer)}.{state}";
            int fullNameHash = Animator.StringToHash(fullName);
            if (animator.HasState(layer, fullNameHash))
            {
                layerIndex = layer;
                stateHash = fullNameHash;
                return true;
            }
        }

        return false;
    }

    private IEnumerator FinishAnimatorRoutine(float duration, int layerIndex)
    {
        yield return new WaitForSeconds(duration);

        RestoreAnimatorIdle();

        resetCoroutine = null;
        isAnimating = false;
    }

    private IEnumerator SlotMachineRoutine(float duration, int layerIndex, int stateHash)
    {
        float elapsed = 0f;
        int cycles = Mathf.Max(1, slotSpinCycles);
        float cycleEnd = Mathf.Clamp(slotCycleEndNormalizedTime, 0.05f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            // Mô phỏng cuộn slot: tăng tốc ngắn, quay đều, rồi chậm dần trước khi dừng.
            float completedRotations = cycles * GetSlotMachineProgress(progress);
            float currentCycle = completedRotations - Mathf.Floor(completedRotations);
            float normalizedTime = currentCycle * cycleEnd;

            animator.Play(stateHash, layerIndex, normalizedTime);
            animator.Update(0f);
            yield return null;
        }

        // Dừng chính xác ở frame đầu để sprite không bị lệch khi chuyển sang Idle.
        animator.Play(stateHash, layerIndex, 0f);
        animator.Update(0f);
        RestoreAnimatorIdle();

        resetCoroutine = null;
        isAnimating = false;
    }

    private float GetSlotMachineProgress(float time)
    {
        float accelerationEnd = Mathf.Clamp(slotAccelerationDuration, 0.01f, 0.45f);
        float slowdownStart = Mathf.Clamp(slotSlowdownStart, accelerationEnd + 0.01f, 0.98f);
        float decelerationDuration = 1f - slowdownStart;

        // Diện tích dưới biểu đồ tốc độ (tăng tốc -> đều -> giảm tốc) được chuẩn hoá thành 1.
        float maxSpeed = 2f / (1f + slowdownStart - accelerationEnd);
        if (time <= accelerationEnd)
        {
            return 0.5f * maxSpeed * time * time / accelerationEnd;
        }

        float accelerationDistance = 0.5f * maxSpeed * accelerationEnd;
        if (time <= slowdownStart)
        {
            return accelerationDistance + maxSpeed * (time - accelerationEnd);
        }

        float decelerationTime = time - slowdownStart;
        return accelerationDistance
               + maxSpeed * (slowdownStart - accelerationEnd)
               + maxSpeed * (decelerationTime
                             - (decelerationTime * decelerationTime) / (2f * decelerationDuration));
    }

    private void RestoreAnimatorIdle()
    {
        if (animator == null) return;

        animator.speed = defaultAnimatorSpeed;
        if (TryGetState(idleStateName, out int idleLayerIndex, out int idleStateHash))
        {
            animator.Play(idleStateHash, idleLayerIndex, 0f);
            animator.Update(0f);
        }
        else
        {
            animator.Rebind();
            animator.Update(0f);
        }
    }

    private void StopCurrentAnimation()
    {
        if (animCoroutine != null)
        {
            StopCoroutine(animCoroutine);
            animCoroutine = null;
        }

        if (resetCoroutine != null)
        {
            StopCoroutine(resetCoroutine);
            resetCoroutine = null;
        }

        if (animator != null)
        {
            animator.speed = defaultAnimatorSpeed;
        }

        isAnimating = false;
    }

    private IEnumerator PlayAnimationRoutine(float totalTime)
    {
        float timePerFrame = totalTime / frames.Length; // 3.0s / 15 = 0.2s mỗi hình

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
            {
                buttonImage.sprite = frames[i];
            }
            yield return new WaitForSeconds(timePerFrame);
        }

        // Chạy xong 3s trả về hình Mặt trời ban đầu (Frame 0)
        if (frames[0] != null)
        {
            buttonImage.sprite = frames[0];
        }

        isAnimating = false;
        animCoroutine = null;
    }
}
