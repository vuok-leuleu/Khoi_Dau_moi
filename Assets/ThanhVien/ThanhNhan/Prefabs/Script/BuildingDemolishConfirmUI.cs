using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hộp xác nhận phá dỡ công trình. Được tạo lúc cần dùng, nên không phải gán
/// thêm prefab hay tham chiếu thủ công trong từng Scene.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-240)]
public sealed class BuildingDemolishConfirmUI : MonoBehaviour
{
    public static BuildingDemolishConfirmUI Ins { get; private set; }

    [Header("=== NỘI DUNG XÁC NHẬN ===")]
    [SerializeField] private string title = "Xóa công trình?";
    [SerializeField] private string cancelLabel = "HỦY";
    [SerializeField] private string confirmLabel = "XÁC NHẬN";

    private GameObject dialogRoot;
    private TextMeshProUGUI messageTMP;
    private UpgradeableBuilding pendingBuilding;
    private Action<UpgradeableBuilding> confirmAction;
    private BuildingUpgradeSidePanelUI upgradePanel;

    private void Awake()
    {
        if (Ins != null && Ins != this)
        {
            Destroy(this);
            return;
        }

        Ins = this;
    }

    private void OnDestroy()
    {
        if (Ins == this) Ins = null;
    }

    private void Update()
    {
        // Nếu người chơi chuyển sang UI khác/đóng panel nâng cấp, không để
        // hộp xác nhận cũ tiếp tục hiện hoặc xóa nhầm công trình đã chọn trước đó.
        if (IsShowing && (upgradePanel == null || !upgradePanel.gameObject.activeInHierarchy))
        {
            Hide();
        }
    }

    public bool IsShowing => dialogRoot != null && dialogRoot.activeSelf;

    /// <summary>
    /// Mở hộp hỏi xác nhận. Hành động chỉ được gọi sau khi người chơi bấm Xác nhận.
    /// </summary>
    public bool Show(UpgradeableBuilding building, Action<UpgradeableBuilding> onConfirmed)
    {
        if (building == null || onConfirmed == null) return false;

        upgradePanel = BuildingUpgradeSidePanelUI.Ins;
        if (upgradePanel == null)
        {
            upgradePanel = FindFirstObjectByType<BuildingUpgradeSidePanelUI>(FindObjectsInactive.Include);
        }

        if (!EnsureDialog()) return false;

        pendingBuilding = building;
        confirmAction = onConfirmed;
        messageTMP.text = $"Bạn có chắc muốn xóa\n<b>{building.buildingName}</b>?";

        dialogRoot.SetActive(true);
        dialogRoot.transform.SetAsLastSibling();
        return true;
    }

    public void Hide()
    {
        if (dialogRoot != null) dialogRoot.SetActive(false);
        pendingBuilding = null;
        confirmAction = null;
    }

    private void Confirm()
    {
        UpgradeableBuilding buildingToDemolish = pendingBuilding;
        Action<UpgradeableBuilding> action = confirmAction;
        Hide();

        if (buildingToDemolish != null)
        {
            action?.Invoke(buildingToDemolish);
        }
    }

    private bool EnsureDialog()
    {
        if (dialogRoot != null) return true;
        if (upgradePanel == null || upgradePanel.transform.parent == null)
        {
            Debug.LogWarning("[BuildingDemolishConfirmUI] Không tìm thấy bảng nâng cấp để đặt hộp xác nhận.", this);
            return false;
        }

        RectTransform upgradeRect = upgradePanel.transform as RectTransform;
        Transform parent = upgradePanel.transform.parent;

        dialogRoot = new GameObject("DemolishConfirmPanel_Runtime", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform dialogRect = dialogRoot.GetComponent<RectTransform>();
        dialogRect.SetParent(parent, false);
        dialogRect.anchorMin = upgradeRect != null ? upgradeRect.anchorMin : new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = upgradeRect != null ? upgradeRect.anchorMax : new Vector2(0.5f, 0.5f);
        dialogRect.pivot = new Vector2(0.5f, 0.5f);
        dialogRect.sizeDelta = new Vector2(310f, 160f);
        dialogRect.anchoredPosition = upgradeRect != null
            ? upgradeRect.anchoredPosition + new Vector2(upgradeRect.rect.width * 0.75f, -upgradeRect.rect.height * 0.15f)
            : new Vector2(300f, -120f);

        Image background = dialogRoot.GetComponent<Image>();
        Image upgradeBackground = upgradePanel.GetComponent<Image>();
        if (upgradeBackground != null && upgradeBackground.sprite != null)
        {
            background.sprite = upgradeBackground.sprite;
            background.type = upgradeBackground.type;
            background.pixelsPerUnitMultiplier = upgradeBackground.pixelsPerUnitMultiplier;
            background.color = Color.white;
        }
        else
        {
            background.color = new Color(0.87f, 0.72f, 0.47f, 0.98f);
        }

        background.raycastTarget = true;

        TextMeshProUGUI sampleText = upgradePanel.GetComponentInChildren<TextMeshProUGUI>(true);
        CreateText("Title", dialogRect, title, sampleText, 29f, FontStyles.Bold,
            new Vector2(0.08f, 0.67f), new Vector2(0.92f, 0.93f));
        messageTMP = CreateText("Message", dialogRect, string.Empty, sampleText, 21f, FontStyles.Normal,
            new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.68f));

        Button cancelButton = CreateButton("CancelButton", dialogRect, cancelLabel,
            new Vector2(0.08f, 0.08f), new Vector2(0.45f, 0.32f), new Color(0.38f, 0.29f, 0.22f, 1f), sampleText);
        Button confirmButton = CreateButton("ConfirmButton", dialogRect, confirmLabel,
            new Vector2(0.55f, 0.08f), new Vector2(0.92f, 0.32f), new Color(0.68f, 0.16f, 0.12f, 1f), sampleText);
        cancelButton.onClick.AddListener(Hide);
        confirmButton.onClick.AddListener(Confirm);

        dialogRoot.SetActive(false);
        return true;
    }

    private static TextMeshProUGUI CreateText(string objectName, RectTransform parent, string content,
        TextMeshProUGUI sampleText, float fontSize, FontStyles fontStyle, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(parent, false);
        textRect.anchorMin = anchorMin;
        textRect.anchorMax = anchorMax;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = sampleText != null ? sampleText.font : TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAlignmentOptions.Center;
        text.color = sampleText != null ? sampleText.color : new Color(0.22f, 0.14f, 0.08f, 1f);
        text.text = content;
        return text;
    }

    private static Button CreateButton(string objectName, RectTransform parent, string label,
        Vector2 anchorMin, Vector2 anchorMax, Color color, TextMeshProUGUI sampleText)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.SetParent(parent, false);
        buttonRect.anchorMin = anchorMin;
        buttonRect.anchorMax = anchorMax;
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        TextMeshProUGUI text = CreateText("Label", buttonRect, label, sampleText, 19f, FontStyles.Bold,
            Vector2.zero, Vector2.one);
        text.color = Color.white;
        text.raycastTarget = false;
        return button;
    }
}
