using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class QuestItemUI : MonoBehaviour
{
    [Header("UI Text & Icon Elements")]
    [SerializeField] private Image questIcon;
    [SerializeField] private TextMeshProUGUI questTitleText;
    [SerializeField] private TextMeshProUGUI questDescriptionText;
    [SerializeField] private TextMeshProUGUI progressText;

    [Header("Claim Button State")]
    [SerializeField] private Button claimButton;
    [SerializeField] private Image claimButtonImage;
    [SerializeField] private TextMeshProUGUI claimButtonText;

    [Header("Status Text (Hiển thị khi ẩn nút)")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Dynamic Reward System")]
    [SerializeField] private Transform rewardAreaContainer; // Drag GameObject RewardArea vào đây
    [SerializeField] private GameObject rewardItemPrefab;   // Drag RewardItemPrefab (Prefab) vào đây

    [Header("Default Reward Icons")]
    [SerializeField] private Sprite goldIcon;
    [SerializeField] private Sprite woodIcon;
    [SerializeField] private Sprite stoneIcon;
    [SerializeField] private Sprite foodIcon;
    [SerializeField] private Sprite expIcon;
    [SerializeField] private Sprite gemIcon;
    [SerializeField] private Sprite valorIcon;

    [Header("Text Strings Config")]
    [SerializeField] private string claimableTextStr = "NHẬN";
    [SerializeField] private string notClaimableTextStr = "CHƯA XONG";
    [SerializeField] private string claimedTextStr = "ĐÃ NHẬN";

    [Header("Display Mode")]
    [Tooltip("True: Khi chưa đủ điều kiện sẽ ẨN NÚT HÌNH và HIỆN statusText.")]
    [SerializeField] private bool hideButtonWhenNotClaimable = true;

    [Header("✨ ANIMATION SETTINGS (BẬT/TẮT TỤY CHỈNH)")]
    [SerializeField] private bool enableAnimations = true;
    [Tooltip("Bật/tắt hiệu ứng Nút nảy nhẹ khi bấm Nhận")]
    [SerializeField] private bool useButtonPunchAnim = true;
    [Tooltip("Bật/tắt hiệu ứng Thẻ Quest thu nhỏ biến mất khi Nhận xong")]
    [SerializeField] private bool useCardDisappearAnim = true;

    private Action onClaimClicked;

    private void Awake()
    {
        EnsureReferences();

        if (claimButton != null)
        {
            claimButton.onClick.RemoveAllListeners();
            claimButton.onClick.AddListener(() => onClaimClicked?.Invoke());
        }
    }

    private void EnsureReferences()
    {
        if (claimButton != null)
        {
            if (claimButtonImage == null) claimButtonImage = claimButton.GetComponent<Image>();
            if (claimButtonText == null) claimButtonText = claimButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        if (statusText == null)
        {
            Transform statusObj = transform.Find("StatusText");
            if (statusObj != null) statusText = statusObj.GetComponent<TextMeshProUGUI>();
        }
    }

    public void SetupQuest(Sprite icon, string title, string description, int currentProgress, int maxProgress, List<QuestReward> rewards, bool isCompleted, Action onClaim)
    {
        EnsureReferences();

        if (questIcon != null)
        {
            if (icon != null)
            {
                questIcon.sprite = icon;
                questIcon.gameObject.SetActive(true);
            }
            else
            {
                questIcon.gameObject.SetActive(false); // Tự động ẩn ô vuông trắng khi chưa có hình
            }
        }
        if (questTitleText != null) questTitleText.text = title;
        if (questDescriptionText != null) questDescriptionText.text = description;
        if (progressText != null) progressText.text = string.Format("{0}/{1}", currentProgress, maxProgress);

        onClaimClicked = onClaim;

        // Render động danh sách phần thưởng (cho dù là 1, 2, 3 hay 4 phần thưởng)
        SetupRewards(rewards);

        // Xử lý trạng thái hiển thị Nút / Chữ status
        bool canClaim = currentProgress >= maxProgress;

        if (isCompleted)
        {
            SetState(showButton: !hideButtonWhenNotClaimable, isInteractable: false, textStr: claimedTextStr);
        }
        else if (canClaim)
        {
            SetState(showButton: true, isInteractable: true, textStr: claimableTextStr);
        }
        else
        {
            SetState(showButton: !hideButtonWhenNotClaimable, isInteractable: false, textStr: notClaimableTextStr);
        }
    }

    private void SetupRewards(List<QuestReward> rewards)
    {
        if (rewardAreaContainer == null) return;

        // Xóa sạch các ô phần thưởng tĩnh cũ (Vàng/Exp cũ hardcode trong Prefab) đang có trong RewardArea
        foreach (Transform child in rewardAreaContainer)
        {
            Destroy(child.gameObject);
        }

        if (rewardItemPrefab == null)
        {
            Debug.LogWarning("[QuestItemUI] 'Reward Item Prefab' chưa được kéo vào Inspector! Hãy kéo RewardItemPrefab vào ô này.");
            return;
        }

        if (rewards == null || rewards.Count == 0) return;

        // Tạo mới đúng số lượng phần thưởng được cấu hình
        foreach (var reward in rewards)
        {
            GameObject itemObj = Instantiate(rewardItemPrefab, rewardAreaContainer);
            RewardItemUI rewardUI = itemObj.GetComponent<RewardItemUI>();

            if (rewardUI != null)
            {
                // Nếu customIcon null thì tự lấy Icon mặc định theo RewardType
                Sprite iconToUse = reward.customIcon != null ? reward.customIcon : GetDefaultIcon(reward.rewardType);
                string suffix = (reward.rewardType == RewardType.Exp) ? " XP" : "";
                string amountStr = $"+{FormatNumber(reward.amount)}{suffix}";

                rewardUI.SetupReward(iconToUse, amountStr);
            }
        }
    }

    private void SetState(bool showButton, bool isInteractable, string textStr)
    {
        if (claimButton != null)
        {
            claimButton.gameObject.SetActive(showButton);
            claimButton.interactable = isInteractable;

            if (claimButtonText != null) 
                claimButtonText.text = textStr;
        }

        if (statusText != null)
        {
            statusText.gameObject.SetActive(!showButton);
            statusText.text = textStr;
        }
    }

    private Sprite GetDefaultIcon(RewardType type)
    {
        switch (type)
        {
            case RewardType.Gold:         return goldIcon;
            case RewardType.Wood:         return woodIcon;
            case RewardType.Stone:        return stoneIcon;
            case RewardType.Food:         return foodIcon;
            case RewardType.Exp:          return expIcon;
            case RewardType.Gem:          return gemIcon;
            case RewardType.Valor:        return valorIcon;
            default: return null;
        }
    }

    private string FormatNumber(int num)
    {
        if (num >= 1000000) return (num / 1000000f).ToString("0.#") + "M";
        if (num >= 1000) return (num / 1000f).ToString("0.#") + "K";
        return num.ToString();
    }

    public void PlayClaimFX(Action onComplete)
    {
        if (!enableAnimations)
        {
            onComplete?.Invoke();
            return;
        }

        if (useButtonPunchAnim && claimButton != null)
        {
            DOTween.Kill(claimButton.transform);
            claimButton.transform.DOPunchScale(Vector3.one * 0.25f, 0.2f, 5, 1).SetUpdate(true);
        }

        if (useCardDisappearAnim)
        {
            DOTween.Kill(transform);
            CanvasGroup cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

            LayoutElement le = GetComponent<LayoutElement>();
            if (le == null) le = gameObject.AddComponent<LayoutElement>();

            RectTransform rect = GetComponent<RectTransform>();
            float initialHeight = (rect != null && rect.rect.height > 0) ? rect.rect.height : 120f;
            le.preferredHeight = initialHeight;

            Sequence seq = DOTween.Sequence();
            seq.SetUpdate(true);
            seq.Append(transform.DOScale(Vector3.one * 1.03f, 0.08f).SetUpdate(true));
            seq.Append(transform.DOScale(Vector3.zero, 0.22f).SetEase(Ease.InBack).SetUpdate(true));
            seq.Join(cg.DOFade(0f, 0.2f).SetUpdate(true));
            seq.Join(DOTween.To(() => le.preferredHeight, x =>
            {
                le.preferredHeight = x;
                if (transform.parent != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(transform.parent as RectTransform);
                }
            }, 0f, 0.22f).SetEase(Ease.InOutQuad).SetUpdate(true));

            seq.OnComplete(() => onComplete?.Invoke());
        }
        else
        {
            onComplete?.Invoke();
        }
    }
}