using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResourceGainPopupItem : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI amountText;

    public void Setup(Sprite icon, int amount, Color? textColor = null)
    {
        if (iconImage != null && icon != null)
        {
            iconImage.sprite = icon;
            iconImage.gameObject.SetActive(true);
        }

        if (amountText != null)
        {
            string prefix = amount > 0 ? "+" : "";
            amountText.text = $"{prefix}{HUDController.FormatNumber(amount)}";
            if (textColor.HasValue)
            {
                amountText.color = textColor.Value;
            }
        }
    }
}