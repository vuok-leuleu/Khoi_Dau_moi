using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RewardItemUI : MonoBehaviour
{
    [SerializeField] private Image rewardIcon;
    [SerializeField] private TextMeshProUGUI rewardAmountText;

    /// <summary>
    /// Gán Sprite và Text số lượng cho 1 ô phần thưởng
    /// </summary>
    public void SetupReward(Sprite icon, string amountText)
    {
        if (rewardIcon != null && icon != null)
        {
            rewardIcon.sprite = icon;
        }

        if (rewardAmountText != null)
        {
            rewardAmountText.text = amountText;
        }
    }
}