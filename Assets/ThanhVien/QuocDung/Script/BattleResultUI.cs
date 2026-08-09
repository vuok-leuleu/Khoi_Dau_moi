using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class BattleRewardData
{
    public string rewardName;
    public int amount;
    public Sprite icon;
}

public class BattleResultUI : MonoBehaviour
{
    [Header("CANVAS & PREFAB SETTINGS")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private int sortingOrder = 9500;

    [Header("UI SPRITES & ICONS (Optional Inspector Overrides)")]
    [SerializeField] private Sprite panelBackgroundSprite;
    [SerializeField] private Sprite headerBannerSprite;
    [SerializeField] private Sprite rewardCrestIcon;
    [SerializeField] private Sprite rewardWoodIcon;
    [SerializeField] private Sprite rewardGoldIcon;
    [SerializeField] private Sprite unitLostIcon;
    [SerializeField] private Sprite returnButtonSprite;

    [Header("FONTS & STYLES")]
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private Color victoryTitleColor = new Color32(230, 194, 101, 255);
    [SerializeField] private Color defeatTitleColor = new Color32(220, 74, 74, 255);
    [SerializeField] private Color sectionTitleColor = new Color32(140, 100, 67, 255);
    [SerializeField] private Color amountTextColor = new Color32(45, 35, 25, 255);
    [SerializeField] private Color cardBgColor = new Color32(244, 241, 234, 255);
    [SerializeField] private Color cardBorderColor = new Color32(212, 175, 87, 255);
    [SerializeField] private Color dividerColor = new Color32(200, 160, 90, 180);
    [SerializeField] private Color buttonBgColor = new Color32(90, 70, 56, 255);
    [SerializeField] private Color buttonTextColor = new Color32(245, 238, 220, 255);

    // Runtime Generated UI references
    private GameObject rootUI;
    private TMP_Text titleText;
    private Transform rewardsContainer;
    private Transform unitsLostContainer;
    private Button returnButton;
    private Action onReturnAction;

    public Sprite RewardCrestIcon => rewardCrestIcon;
    public Sprite RewardWoodIcon => rewardWoodIcon;
    public Sprite RewardGoldIcon => rewardGoldIcon;
    public Sprite UnitLostIcon => unitLostIcon;

    private void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null && UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[BattleResultUI] 🟢 Auto-created missing EventSystem in scene for UI button interaction.");
        }
    }

    public void ShowResult(bool isVictory, int unitsLost, List<BattleRewardData> rewards, Action onReturn)
    {
        EnsureEventSystem();
        onReturnAction = onReturn;

        if (rootUI == null)
        {
            BuildDemaciaRisingUI();
        }

        rootUI.SetActive(true);

        // Update Title (VICTORY / DEFEAT)
        if (titleText != null)
        {
            titleText.text = isVictory ? "VICTORY" : "DEFEAT";
            titleText.color = isVictory ? victoryTitleColor : defeatTitleColor;
        }

        // Populate Rewards
        PopulateRewards(rewards);

        // Populate Units Lost
        PopulateUnitsLost(unitsLost);
    }

    public void HideResult()
    {
        if (rootUI != null)
        {
            rootUI.SetActive(false);
        }
    }

    private void PopulateRewards(List<BattleRewardData> rewards)
    {
        if (rewardsContainer == null) return;

        foreach (Transform child in rewardsContainer)
        {
            Destroy(child.gameObject);
        }

        if (rewards == null || rewards.Count == 0)
        {
            CreateEmptyRewardLabel(rewardsContainer);
            return;
        }

        foreach (var reward in rewards)
        {
            if (reward.amount <= 0) continue;

            GameObject itemObj = new GameObject($"Reward_{reward.rewardName}", typeof(RectTransform), typeof(CanvasRenderer));
            itemObj.transform.SetParent(rewardsContainer, false);

            VerticalLayoutGroup itemLayout = itemObj.AddComponent<VerticalLayoutGroup>();
            itemLayout.childAlignment = TextAnchor.UpperCenter;
            itemLayout.childControlHeight = false;
            itemLayout.childControlWidth = false;
            itemLayout.spacing = 8f;

            LayoutElement le = itemObj.AddComponent<LayoutElement>();
            le.minWidth = 60f;
            le.minHeight = 80f;

            // Icon Image
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObj.transform.SetParent(itemObj.transform, false);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(48f, 48f);

            Image iconImg = iconObj.GetComponent<Image>();
            iconImg.sprite = reward.icon != null ? reward.icon : CreateFallbackSprite(reward.rewardName);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // Amount Text
            GameObject textObj = new GameObject("AmountText", typeof(RectTransform), typeof(CanvasRenderer));
            textObj.transform.SetParent(itemObj.transform, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(60f, 28f);

            TMP_Text amountLabel = textObj.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null) amountLabel.font = fontAsset;
            amountLabel.text = reward.amount.ToString();
            amountLabel.fontSize = 22;
            amountLabel.fontStyle = FontStyles.Bold;
            amountLabel.color = amountTextColor;
            amountLabel.alignment = TextAlignmentOptions.Center;
            amountLabel.raycastTarget = false;
        }
    }

    private void PopulateUnitsLost(int unitsLost)
    {
        if (unitsLostContainer == null) return;

        foreach (Transform child in unitsLostContainer)
        {
            Destroy(child.gameObject);
        }

        GameObject itemObj = new GameObject("UnitLost_Item", typeof(RectTransform), typeof(CanvasRenderer));
        itemObj.transform.SetParent(unitsLostContainer, false);

        VerticalLayoutGroup itemLayout = itemObj.AddComponent<VerticalLayoutGroup>();
        itemLayout.childAlignment = TextAnchor.UpperCenter;
        itemLayout.childControlHeight = false;
        itemLayout.childControlWidth = false;
        itemLayout.spacing = 8f;

        LayoutElement le = itemObj.AddComponent<LayoutElement>();
        le.minWidth = 60f;
        le.minHeight = 80f;

        // Unit Icon Image
        GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObj.transform.SetParent(itemObj.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(48f, 48f);

        Image iconImg = iconObj.GetComponent<Image>();
        iconImg.sprite = unitLostIcon != null ? unitLostIcon : CreateFallbackSprite("Soldier");
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // Amount Text
        GameObject textObj = new GameObject("AmountText", typeof(RectTransform), typeof(CanvasRenderer));
        textObj.transform.SetParent(itemObj.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(60f, 28f);

        TMP_Text amountLabel = textObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) amountLabel.font = fontAsset;
        amountLabel.text = unitsLost.ToString();
        amountLabel.fontSize = 22;
        amountLabel.fontStyle = FontStyles.Bold;
        amountLabel.color = amountTextColor;
        amountLabel.alignment = TextAlignmentOptions.Center;
        amountLabel.raycastTarget = false;
    }

    private void CreateEmptyRewardLabel(Transform parent)
    {
        GameObject textObj = new GameObject("NoneText", typeof(RectTransform), typeof(CanvasRenderer));
        textObj.transform.SetParent(parent, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(100f, 30f);

        TMP_Text label = textObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) label.font = fontAsset;
        label.text = "0";
        label.fontSize = 22;
        label.color = amountTextColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    /// <summary>
    /// Constructs Demacia Rising styled UI canvas dynamically at runtime
    /// </summary>
    private void BuildDemaciaRisingUI()
    {
        EnsureEventSystem();

        // 1. Check or Create Canvas
        Canvas canvas = targetCanvas;
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("BattleResultCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // 2. Root UI Overlay
        rootUI = new GameObject("DemaciaResultBoard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rootUI.transform.SetParent(canvas.transform, false);

        RectTransform rootRT = rootUI.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        Image rootDim = rootUI.GetComponent<Image>();
        rootDim.color = new Color(0f, 0f, 0f, 0.45f);
        rootDim.raycastTarget = true;

        // 3. Main Card Panel
        GameObject cardObj = new GameObject("CardPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cardObj.transform.SetParent(rootUI.transform, false);

        RectTransform cardRT = cardObj.GetComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(700f, 500f);
        cardRT.anchoredPosition = new Vector2(0f, -20f);

        Image cardImg = cardObj.GetComponent<Image>();
        cardImg.color = cardBgColor;
        cardImg.raycastTarget = false;
        if (panelBackgroundSprite != null) cardImg.sprite = panelBackgroundSprite;

        Outline cardOutline = cardObj.AddComponent<Outline>();
        cardOutline.effectColor = cardBorderColor;
        cardOutline.effectDistance = new Vector2(3f, -3f);

        // Golden handle trims on left/right edges (Demacia Rising accent)
        CreateGoldSideHandles(cardObj.transform);

        // 4. Header Banner & Title
        GameObject headerObj = new GameObject("HeaderBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        headerObj.transform.SetParent(cardObj.transform, false);

        RectTransform headerRT = headerObj.GetComponent<RectTransform>();
        headerRT.sizeDelta = new Vector2(420f, 80f);
        headerRT.anchoredPosition = new Vector2(0f, 255f);

        Image headerImg = headerObj.GetComponent<Image>();
        headerImg.color = new Color32(40, 32, 24, 240);
        headerImg.raycastTarget = false;
        if (headerBannerSprite != null) headerImg.sprite = headerBannerSprite;

        Outline headerOutline = headerObj.AddComponent<Outline>();
        headerOutline.effectColor = cardBorderColor;
        headerOutline.effectDistance = new Vector2(2f, -2f);

        // Header Diamond Gem Accent
        GameObject gemObj = new GameObject("GemAccent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gemObj.transform.SetParent(headerObj.transform, false);
        RectTransform gemRT = gemObj.GetComponent<RectTransform>();
        gemRT.sizeDelta = new Vector2(20f, 20f);
        gemRT.anchoredPosition = new Vector2(0f, 40f);
        gemRT.localRotation = Quaternion.Euler(0, 0, 45);
        Image gemImg = gemObj.GetComponent<Image>();
        gemImg.color = new Color32(80, 160, 240, 255);
        gemImg.raycastTarget = false;

        // Title Text (VICTORY / DEFEAT)
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer));
        titleObj.transform.SetParent(headerObj.transform, false);
        RectTransform titleRT = titleObj.GetComponent<RectTransform>();
        titleRT.sizeDelta = new Vector2(380f, 70f);

        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) titleText.font = fontAsset;
        titleText.text = "VICTORY";
        titleText.fontSize = 44;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = victoryTitleColor;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.characterSpacing = 8f;
        titleText.raycastTarget = false;

        // 5. Content Area (Two Columns: REWARDS | UNITS LOST)
        GameObject contentArea = new GameObject("ContentArea", typeof(RectTransform));
        contentArea.transform.SetParent(cardObj.transform, false);
        RectTransform contentRT = contentArea.GetComponent<RectTransform>();
        contentRT.sizeDelta = new Vector2(640f, 320f);
        contentRT.anchoredPosition = new Vector2(0f, 20f);

        // --- LEFT COLUMN: REWARDS ---
        GameObject leftCol = new GameObject("LeftColumn_Rewards", typeof(RectTransform));
        leftCol.transform.SetParent(contentArea.transform, false);
        RectTransform leftRT = leftCol.GetComponent<RectTransform>();
        leftRT.sizeDelta = new Vector2(300f, 300f);
        leftRT.anchoredPosition = new Vector2(-160f, 0f);

        // REWARDS Header
        GameObject rewardsTitleObj = new GameObject("RewardsTitle", typeof(RectTransform), typeof(CanvasRenderer));
        rewardsTitleObj.transform.SetParent(leftCol.transform, false);
        RectTransform rTitleRT = rewardsTitleObj.GetComponent<RectTransform>();
        rTitleRT.sizeDelta = new Vector2(280f, 40f);
        rTitleRT.anchoredPosition = new Vector2(0f, 110f);

        TMP_Text rewardsTitleText = rewardsTitleObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) rewardsTitleText.font = fontAsset;
        rewardsTitleText.text = "REWARDS";
        rewardsTitleText.fontSize = 22;
        rewardsTitleText.fontStyle = FontStyles.Bold;
        rewardsTitleText.color = sectionTitleColor;
        rewardsTitleText.alignment = TextAlignmentOptions.Center;
        rewardsTitleText.characterSpacing = 2f;
        rewardsTitleText.raycastTarget = false;

        // REWARDS Container (Horizontal Layout)
        GameObject rewardsContObj = new GameObject("RewardsContainer", typeof(RectTransform));
        rewardsContObj.transform.SetParent(leftCol.transform, false);
        RectTransform rContRT = rewardsContObj.GetComponent<RectTransform>();
        rContRT.sizeDelta = new Vector2(280f, 180f);
        rContRT.anchoredPosition = new Vector2(0f, -20f);

        HorizontalLayoutGroup rLayout = rewardsContObj.AddComponent<HorizontalLayoutGroup>();
        rLayout.childAlignment = TextAnchor.MiddleCenter;
        rLayout.spacing = 16f;
        rLayout.childControlWidth = false;
        rLayout.childControlHeight = false;

        rewardsContainer = rewardsContObj.transform;

        // --- RIGHT COLUMN: UNITS LOST ---
        GameObject rightCol = new GameObject("RightColumn_UnitsLost", typeof(RectTransform));
        rightCol.transform.SetParent(contentArea.transform, false);
        RectTransform rightRT = rightCol.GetComponent<RectTransform>();
        rightRT.sizeDelta = new Vector2(300f, 300f);
        rightRT.anchoredPosition = new Vector2(160f, 0f);

        // UNITS LOST Header
        GameObject unitsTitleObj = new GameObject("UnitsLostTitle", typeof(RectTransform), typeof(CanvasRenderer));
        unitsTitleObj.transform.SetParent(rightCol.transform, false);
        RectTransform uTitleRT = unitsTitleObj.GetComponent<RectTransform>();
        uTitleRT.sizeDelta = new Vector2(280f, 40f);
        uTitleRT.anchoredPosition = new Vector2(0f, 110f);

        TMP_Text unitsTitleText = unitsTitleObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) unitsTitleText.font = fontAsset;
        unitsTitleText.text = "UNITS LOST";
        unitsTitleText.fontSize = 22;
        unitsTitleText.fontStyle = FontStyles.Bold;
        unitsTitleText.color = sectionTitleColor;
        unitsTitleText.alignment = TextAlignmentOptions.Center;
        unitsTitleText.characterSpacing = 2f;
        unitsTitleText.raycastTarget = false;

        // UNITS LOST Container (Horizontal Layout)
        GameObject unitsContObj = new GameObject("UnitsLostContainer", typeof(RectTransform));
        unitsContObj.transform.SetParent(rightCol.transform, false);
        RectTransform uContRT = unitsContObj.GetComponent<RectTransform>();
        uContRT.sizeDelta = new Vector2(280f, 180f);
        uContRT.anchoredPosition = new Vector2(0f, -20f);

        HorizontalLayoutGroup uLayout = unitsContObj.AddComponent<HorizontalLayoutGroup>();
        uLayout.childAlignment = TextAnchor.MiddleCenter;
        uLayout.spacing = 16f;
        uLayout.childControlWidth = false;
        uLayout.childControlHeight = false;

        unitsLostContainer = unitsContObj.transform;

        // 6. Bottom RETURN Button
        GameObject btnObj = new GameObject("ReturnButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(cardObj.transform, false);
        RectTransform btnRT = btnObj.GetComponent<RectTransform>();
        btnRT.sizeDelta = new Vector2(200f, 52f);
        btnRT.anchoredPosition = new Vector2(0f, -190f);

        Image btnImg = btnObj.GetComponent<Image>();
        btnImg.color = buttonBgColor;
        btnImg.raycastTarget = true;
        if (returnButtonSprite != null) btnImg.sprite = returnButtonSprite;

        Outline btnOutline = btnObj.AddComponent<Outline>();
        btnOutline.effectColor = cardBorderColor;
        btnOutline.effectDistance = new Vector2(2f, -2f);

        returnButton = btnObj.GetComponent<Button>();
        Navigation nav = returnButton.navigation;
        nav.mode = Navigation.Mode.None;
        returnButton.navigation = nav;

        returnButton.onClick.AddListener(() =>
        {
            Debug.Log("[BattleResultUI] 🔘 RETURN button clicked!");
            HideResult();
            onReturnAction?.Invoke();
        });

        GameObject btnTextObj = new GameObject("ButtonText", typeof(RectTransform), typeof(CanvasRenderer));
        btnTextObj.transform.SetParent(btnObj.transform, false);
        RectTransform bTextRT = btnTextObj.GetComponent<RectTransform>();
        bTextRT.sizeDelta = new Vector2(180f, 40f);

        TMP_Text bText = btnTextObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) bText.font = fontAsset;
        bText.text = "RETURN";
        bText.fontSize = 20;
        bText.fontStyle = FontStyles.Bold;
        bText.color = buttonTextColor;
        bText.alignment = TextAlignmentOptions.Center;
        bText.characterSpacing = 2f;
        bText.raycastTarget = false;
    }

    private void CreateGoldSideHandles(Transform parent)
    {
        // Left gold side trim
        GameObject leftTrim = new GameObject("LeftGoldTrim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        leftTrim.transform.SetParent(parent, false);
        RectTransform leftTRT = leftTrim.GetComponent<RectTransform>();
        leftTRT.sizeDelta = new Vector2(16f, 440f);
        leftTRT.anchoredPosition = new Vector2(-350f, 0f);
        Image leftImg = leftTrim.GetComponent<Image>();
        leftImg.color = cardBorderColor;

        // Right gold side trim
        GameObject rightTrim = new GameObject("RightGoldTrim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rightTrim.transform.SetParent(parent, false);
        RectTransform rightTRT = rightTrim.GetComponent<RectTransform>();
        rightTRT.sizeDelta = new Vector2(16f, 440f);
        rightTRT.anchoredPosition = new Vector2(350f, 0f);
        Image rightImg = rightTrim.GetComponent<Image>();
        rightImg.color = cardBorderColor;
    }

    private Sprite CreateFallbackSprite(string typeName)
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color mainColor = Color.white;

        if (typeName.Contains("Wood") || typeName.Contains("Gỗ"))
        {
            mainColor = new Color(0.55f, 0.35f, 0.18f);
        }
        else if (typeName.Contains("Gold") || typeName.Contains("Crest") || typeName.Contains("Vàng"))
        {
            mainColor = new Color(0.85f, 0.65f, 0.15f);
        }
        else if (typeName.Contains("Soldier") || typeName.Contains("Lính"))
        {
            mainColor = new Color(0.25f, 0.45f, 0.75f);
        }
        else
        {
            mainColor = new Color(0.6f, 0.5f, 0.8f);
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - size * 0.5f;
                float dy = y - size * 0.5f;
                float distSq = dx * dx + dy * dy;
                float radiusSq = (size * 0.42f) * (size * 0.42f);

                if (distSq <= radiusSq)
                {
                    texture.SetPixel(x, y, mainColor);
                }
                else
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
