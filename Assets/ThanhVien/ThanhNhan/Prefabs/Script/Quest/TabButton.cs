using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(LayoutElement))]
public class TabButton : MonoBehaviour, IPointerClickHandler
{
    [Header("Quest Type")]
    [SerializeField] private QuestType questType;

    [Header("Sprites")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite selectedSprite;

    [Header("Sizes")]
    [SerializeField] private Vector2 normalSize = new Vector2(70f, 70f);
    [SerializeField] private Vector2 selectedSize = new Vector2(85f, 85f); // To hơn xíu khi chọn

    private Image buttonImage;
    private LayoutElement layoutElement;
    private TabGroupManager tabManager;

    public QuestType QuestType => questType;

    private void Awake()
    {
        buttonImage = GetComponent<Image>();
        layoutElement = GetComponent<LayoutElement>();
        tabManager = GetComponentInParent<TabGroupManager>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (tabManager != null)
        {
            tabManager.OnTabSelected(this);
        }
    }

    public void SetSelected(bool isSelected)
    {
        // 1. Đổi Sprite viền vàng / viền xám
        if (buttonImage != null && normalSprite != null && selectedSprite != null)
        {
            buttonImage.sprite = isSelected ? selectedSprite : normalSprite;
        }

        // 2. Đổi kích thước trong Layout Group (tự động đẩy các nút khác dời ra)
        if (layoutElement != null)
        {
            Vector2 targetSize = isSelected ? selectedSize : normalSize;
            layoutElement.preferredWidth = targetSize.x;
            layoutElement.preferredHeight = targetSize.y;
        }
    }
}