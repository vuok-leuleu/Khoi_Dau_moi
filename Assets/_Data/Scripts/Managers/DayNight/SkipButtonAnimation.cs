using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SkipButtonAnimator : MonoBehaviour
{
    public enum AnimMode { UnityAnimator, SpriteArray }

    [Header("--- CHẾ ĐỘ ANIMATION ---")]
    public AnimMode mode = AnimMode.UnityAnimator;

    [Header("--- CHẾ ĐỘ 1: UNITY ANIMATOR ---")]
    [Tooltip("Tên State Animation trong cửa sổ Animator Controller")]
    public string stateName = "Button_DayNight_Anim";

    [Header("--- CHẾ ĐỘ 2: SPRITE ARRAY ---")]
    [Tooltip("Kéo 15 tấm ảnh Sprite (từ Frame 0 -> 7 -> 0) vào đây")]
    public Sprite[] frames;

    private Animator animator;
    private Image buttonImage;
    private Button button;
    private Coroutine animCoroutine;
    private bool isAnimating = false;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        buttonImage = GetComponent<Image>();
        button = GetComponent<Button>();
    }

    private void Start()
    {
        // Đăng ký sự kiện Click trực tiếp trên nút UI
        if (button != null)
        {
            button.onClick.AddListener(PlayAnimation);
        }
    }

    private void OnEnable()
    {
        if (DayNightManager.Ins != null)
        {
            // CHỈ LẮNG NGHE SỰ KIỆN KHI BẤM NÚT SÁNG/TỐI (Bỏ OnNightStart để tránh chạy 2 lần)
            DayNightManager.Ins.OnWaveSkipped += OnWaveSkippedHandler;
        }
    }

    private void OnDisable()
    {
        if (DayNightManager.Ins != null)
        {
            DayNightManager.Ins.OnWaveSkipped -= OnWaveSkippedHandler;
        }
    }

    private void OnWaveSkippedHandler(int wave, float timeSaved)
    {
        PlayAnimation();
    }

    public void PlayAnimation()
    {
        // 🛑 BẢO VỆ CHỐNG CHẠY 2 LẦN TRONG 1 CHU KỲ 3.0 GIÂY
        if (isAnimating) return;

        if (mode == AnimMode.UnityAnimator)
        {
            if (animator != null)
            {
                isAnimating = true;
                animator.Play(stateName, -1, 0f);
                float totalTime = (DayNightManager.Ins != null) ? DayNightManager.Ins.lightTransitionDuration : 3.0f;
                StartCoroutine(ResetAnimatingFlagRoutine(totalTime));
            }
            else
            {
                Debug.LogWarning("[SkipButtonAnimator] ⚠️ Không tìm thấy Animator Component trên GameObject này!");
            }
        }
        else
        {
            if (frames == null || frames.Length == 0 || buttonImage == null) return;
            if (animCoroutine != null) StopCoroutine(animCoroutine);
            animCoroutine = StartCoroutine(PlayAnimationRoutine());
        }
    }

    private IEnumerator ResetAnimatingFlagRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        isAnimating = false;
    }

    private IEnumerator PlayAnimationRoutine()
    {
        isAnimating = true;
        float totalTime = (DayNightManager.Ins != null) ? DayNightManager.Ins.lightTransitionDuration : 3.0f;
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
    }
}
