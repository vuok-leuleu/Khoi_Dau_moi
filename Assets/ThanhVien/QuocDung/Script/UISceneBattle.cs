using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Quản lý UI cho Scene Battle:
/// 1. Thanh hiển thị danh sách các đơn vị lính mình đang có trong trận (In-Battle Unit Roster / Army Bar - Hình 2)
///    - Dùng các thẻ UI tạo sẵn trong Hierarchy (nếu có sẵn Card_Kiếm Sĩ / UnitCard / Card_...) hoặc tự tạo nếu chưa có.
///    - Đảm bảo thẻ con hiển thị chuẩn xác, không bị đè, không sinh trùng lặp đối tượng.
/// 2. Panel Kết Quả Chiến Thắng (Victory) & Thất Bại (Defeat) theo phong cách Demacia Rising.
/// </summary>
public class UISceneBattle : MonoBehaviour
{
    [System.Serializable]
    public class UnitDisplayInfo
    {
        public string unitName = "Binh Lính";
        public Sprite unitIcon;
        public int count = 1;
        public int maxCount = 1;
        public BuildingType unitType = BuildingType.BarracksMelee;

        public UnitDisplayInfo() { }
        public UnitDisplayInfo(string name, Sprite icon, int count, int maxCount = 0)
        {
            this.unitName = name;
            this.unitIcon = icon;
            this.count = count;
            this.maxCount = maxCount > 0 ? maxCount : count;
        }
    }

    [System.Serializable]
    public class RewardItem
    {
        public Sprite icon;
        public string amountText;

        public RewardItem() { }
        public RewardItem(Sprite icon, string amountText)
        {
            this.icon = icon;
            this.amountText = amountText;
        }
    }

    [System.Serializable]
    public class UnitLostItem
    {
        public Sprite icon;
        public string unitName;
        public int count = 1;

        public UnitLostItem() { }
        public UnitLostItem(Sprite icon, string unitName = "", int count = 1)
        {
            this.icon = icon;
            this.unitName = unitName;
            this.count = count;
        }
    }

    private class UnitCardUI
    {
        public GameObject rootObject;
        public Image iconImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI countText;
        public int lastCount = -1;
    }

    [Header("=== 1. IN-BATTLE UNIT ROSTER (Hình 2 - Danh Sách Lính Đang Có) ===")]
    [Tooltip("GameObject Panel chứa thanh danh sách các đơn vị lính (Góc dưới màn hình)")]
    public GameObject armyRosterPanel;
    [Tooltip("Container chứa các thẻ lính (Horizontal Layout Group)")]
    public Transform armyUnitContainer;
    [Tooltip("Tự động cập nhật số lượng lính thời gian thực theo trận đấu")]
    public bool autoTrackLiveUnits = true;
    [Tooltip("Thời gian lặp lại quét số lượng lính (giây)")]
    public float unitScanInterval = 0.3f;

    [Header("--- Cấu hình Tự Động Co Giãn Khung Background ---")]
    [Tooltip("Bật chế độ tự động co giãn khung background theo số lượng thẻ lính")]
    public bool autoResizeBackground = true;
    [Tooltip("Chiều rộng của 1 thẻ lính")]
    public float cardWidth = 86f;
    [Tooltip("Chiều cao của 1 thẻ lính")]
    public float cardHeight = 114f;
    [Tooltip("Khoảng cách giữa các thẻ lính")]
    public float cardSpacing = 12f;
    [Tooltip("Padding lề trái/phải của background")]
    public float paddingHorizontal = 16f;
    [Tooltip("Padding lề trên/dưới của background")]
    public float paddingVertical = 10f;

    [Header("--- Cấu hình Icon & Tên cho từng loại Lính ---")]
    public Sprite meleeSoldierIcon;
    public string meleeSoldierName = "Kiếm Sĩ";
    public Sprite archerSoldierIcon;
    public string archerSoldierName = "Cung Thủ";
    public Sprite spearSoldierIcon;
    public string spearSoldierName = "Thương Thủ";
    public Sprite tankSoldierIcon;
    public string tankSoldierName = "Khiên Binh";
    public Sprite defaultSoldierIcon;

    [Header("=== 2. VICTORY & DEFEAT RESULT PANELS ===")]
    [Tooltip("Panel khung Victory (Chiến thắng)")]
    public GameObject victoryPanel;
    [Tooltip("Image nền của Panel Victory")]
    public Image victoryBackgroundImage;

    [Tooltip("Panel khung Defeat (Thất bại)")]
    public GameObject defeatPanel;
    [Tooltip("Image nền của Panel Defeat")]
    public Image defeatBackgroundImage;

    [Header("--- Return Buttons ---")]
    public Button victoryReturnButton;
    public Button defeatReturnButton;
    [Tooltip("Tên Scene chuyển về khi bấm Return (Mặc định: MainScene)")]
    public string returnSceneName = "MainScene";

    [Header("--- Victory Elements ---")]
    [Tooltip("Container chứa danh sách phần thưởng (Horizontal Layout Group)")]
    public Transform victoryRewardContainer;
    [Tooltip("Container chứa danh sách lính tử trận màn Victory")]
    public Transform victoryUnitsLostContainer;
    [Tooltip("Màu chữ số lượng phần thưởng")]
    public Color rewardTextColor = new Color(0.24f, 0.15f, 0.09f, 1f);

    [Header("--- Defeat Elements ---")]
    [Tooltip("Container chứa danh sách lính tử trận màn Defeat")]
    public Transform defeatUnitsLostContainer;
    [Tooltip("Text hiển thị mẹo / câu nói khi thất bại")]
    public TextMeshProUGUI defeatTipText;
    [TextArea(2, 4)]
    public string defaultDefeatTip = "Drakehounds are quick and hunt in packs.";

    [Header("--- Prefabs Tùy Chọn ---")]
    [Tooltip("Prefab hiển thị 1 phần thưởng (Image icon và Text amount)")]
    public GameObject rewardItemPrefab;
    [Tooltip("Prefab hiển thị 1 lính bị mất")]
    public GameObject unitLostItemPrefab;

    [Header("--- Sample Demo Data (Test trong Inspector) ---")]
    public List<UnitDisplayInfo> sampleArmyUnits = new List<UnitDisplayInfo>();
    public List<RewardItem> sampleRewards = new List<RewardItem>();
    public List<UnitLostItem> sampleUnitsLost = new List<UnitLostItem>();

    [Header("--- Settings & Debug Keys ---")]
    [Tooltip("Tự động kiểm tra kết quả trong BattleData khi Start")]
    public bool checkBattleDataOnStart = true;
    [Tooltip("Bật phím tắt test: U (Bật/Tắt Army Roster), V (Victory), F (Defeat), H (Hide All)")]
    public bool enableDebugKeys = true;

    [Header("--- Sound & Audio Effects ---")]
    public AudioClip returnButtonSFX;
    public AudioSource audioSource;

    public static UISceneBattle Instance { get; private set; }
    private bool hasShownResultUI = false;
    private bool isReturning = false;
    private float nextScanTime = 0f;

    private Dictionary<string, UnitCardUI> cachedCards = new Dictionary<string, UnitCardUI>();
    private int lastVisibleCardCount = -1;

    private void Awake()
    {
        Instance = this;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        SetupReturnButtons();
    }

    private void OnDisable()
    {
        cachedCards.Clear();
        lastVisibleCardCount = -1;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        cachedCards.Clear();
    }

    private void Start()
    {
        cachedCards.Clear();
        lastVisibleCardCount = -1;

        HideResultPanels();
        SetupReturnButtons();
        CheckAndShowBattleDataResult();

        if (armyRosterPanel != null)
        {
            armyRosterPanel.SetActive(true);
        }

        SetupContainerLayout();
        CollectPreExistingCards();
        RefreshArmyUnitsRoster();
    }

    /// <summary>
    /// Thu thập các Card đã có sẵn trong Hierarchy từ trước để tái sử dụng, không spawn đè
    /// </summary>
    private void CollectPreExistingCards()
    {
        Transform container = armyUnitContainer != null ? armyUnitContainer : (armyRosterPanel != null ? armyRosterPanel.transform : null);
        if (container == null) return;

        foreach (Transform child in container)
        {
            if (child == null) continue;
            string objName = child.gameObject.name;

            UnitCardUI cardUI = BuildCardUIFromObject(child.gameObject);
            
            // Xác định tên loại lính dựa theo tên GameObject
            string unitKey = "";
            if (objName.ToLower().Contains("kiếm") || objName.ToLower().Contains("melee") || objName.ToLower().Contains("soldier"))
            {
                unitKey = meleeSoldierName;
            }
            else if (objName.ToLower().Contains("cung") || objName.ToLower().Contains("archer"))
            {
                unitKey = archerSoldierName;
            }
            else if (objName.ToLower().Contains("thương") || objName.ToLower().Contains("spear"))
            {
                unitKey = spearSoldierName;
            }
            else if (objName.ToLower().Contains("tank") || objName.ToLower().Contains("shield") || objName.ToLower().Contains("khiên"))
            {
                unitKey = tankSoldierName;
            }
            else
            {
                unitKey = objName.Replace("Card_", "").Trim();
            }

            if (!string.IsNullOrEmpty(unitKey) && !cachedCards.ContainsKey(unitKey))
            {
                cachedCards[unitKey] = cardUI;
            }
        }
    }

    private void SetupContainerLayout()
    {
        Transform target = armyUnitContainer != null ? armyUnitContainer : (armyRosterPanel != null ? armyRosterPanel.transform : null);
        if (target == null) return;

        HorizontalLayoutGroup layout = target.GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
        {
            layout = target.gameObject.AddComponent<HorizontalLayoutGroup>();
        }

        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = cardSpacing;
        layout.padding = new RectOffset((int)paddingHorizontal, (int)paddingHorizontal, (int)paddingVertical, (int)paddingVertical);
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    private void SetupReturnButtons()
    {
        if (victoryReturnButton == null && victoryPanel != null)
            victoryReturnButton = victoryPanel.GetComponentInChildren<Button>();

        if (defeatReturnButton == null && defeatPanel != null)
            defeatReturnButton = defeatPanel.GetComponentInChildren<Button>();

        if (victoryReturnButton != null)
        {
            victoryReturnButton.interactable = true;
            victoryReturnButton.onClick.RemoveAllListeners();
            victoryReturnButton.onClick.AddListener(OnReturnButtonClicked);
        }

        if (defeatReturnButton != null && defeatReturnButton != victoryReturnButton)
        {
            defeatReturnButton.interactable = true;
            defeatReturnButton.onClick.RemoveAllListeners();
            defeatReturnButton.onClick.AddListener(OnReturnButtonClicked);
        }
    }

    private void Update()
    {
        if (autoTrackLiveUnits && !hasShownResultUI && Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + unitScanInterval;
            RefreshArmyUnitsRoster();
        }

        if (checkBattleDataOnStart && BattleData.HasResult && !hasShownResultUI)
        {
            CheckAndShowBattleDataResult();
        }

        if (!enableDebugKeys) return;

        if (Input.GetKeyDown(KeyCode.U))
        {
            if (armyRosterPanel != null)
            {
                armyRosterPanel.SetActive(!armyRosterPanel.activeSelf);
            }
            if (sampleArmyUnits.Count > 0)
            {
                DisplayArmyUnits(sampleArmyUnits);
            }
            else
            {
                RefreshArmyUnitsRoster();
            }
        }
        else if (Input.GetKeyDown(KeyCode.V))
        {
            ShowVictory(sampleRewards, sampleUnitsLost);
        }
        else if (Input.GetKeyDown(KeyCode.F))
        {
            ShowDefeat(sampleUnitsLost);
        }
        else if (Input.GetKeyDown(KeyCode.H))
        {
            HideAll();
        }
    }

    #region Army Units Roster (Hình 2)

    public void RefreshArmyUnitsRoster()
    {
        if (armyUnitContainer == null && armyRosterPanel == null) return;

        List<UnitDisplayInfo> liveUnits = ScanCurrentLiveUnits();
        if (liveUnits.Count == 0 && sampleArmyUnits.Count > 0)
        {
            liveUnits = sampleArmyUnits;
        }

        DisplayArmyUnits(liveUnits);
    }

    public void DisplayArmyUnits(List<UnitDisplayInfo> units)
    {
        Transform container = armyUnitContainer != null ? armyUnitContainer : (armyRosterPanel != null ? armyRosterPanel.transform : null);
        if (container == null) return;

        if (units == null || units.Count == 0)
        {
            foreach (var kvp in cachedCards)
            {
                if (kvp.Value != null && kvp.Value.rootObject != null)
                {
                    kvp.Value.rootObject.SetActive(false);
                }
            }

            if (autoResizeBackground && armyRosterPanel != null)
            {
                RectTransform panelRect = armyRosterPanel.GetComponent<RectTransform>();
                if (panelRect != null)
                {
                    panelRect.sizeDelta = new Vector2(0, cardHeight + paddingVertical * 2);
                }
            }
            lastVisibleCardCount = 0;
            return;
        }

        HashSet<string> currentActiveNames = new HashSet<string>();

        for (int i = 0; i < units.Count; i++)
        {
            UnitDisplayInfo unit = units[i];
            currentActiveNames.Add(unit.unitName);

            UnitCardUI cardUI;
            if (!cachedCards.TryGetValue(unit.unitName, out cardUI) || cardUI == null || cardUI.rootObject == null)
            {
                cardUI = CreateCardUI(container, unit);
                cachedCards[unit.unitName] = cardUI;
            }

            if (cardUI.rootObject != null)
            {
                cardUI.rootObject.transform.SetSiblingIndex(i);
                cardUI.rootObject.SetActive(true);
            }

            Sprite iconToSet = unit.unitIcon != null ? unit.unitIcon : defaultSoldierIcon;
            if (cardUI.iconImage != null && iconToSet != null)
            {
                if (cardUI.iconImage.sprite != iconToSet)
                {
                    cardUI.iconImage.sprite = iconToSet;
                }
                cardUI.iconImage.enabled = true;
            }

            if (cardUI.nameText != null && cardUI.nameText.text != unit.unitName)
            {
                cardUI.nameText.text = unit.unitName;
            }

            if (cardUI.countText != null && cardUI.lastCount != unit.count)
            {
                cardUI.lastCount = unit.count;
                cardUI.countText.text = unit.count > 0 ? $"x{unit.count}" : "<color=red>0</color>";
                cardUI.countText.color = unit.count > 0 ? Color.white : Color.red;
            }
        }

        foreach (var kvp in cachedCards)
        {
            if (!currentActiveNames.Contains(kvp.Key) && kvp.Value != null && kvp.Value.rootObject != null)
            {
                kvp.Value.rootObject.SetActive(false);
            }
        }

        if (autoResizeBackground && lastVisibleCardCount != units.Count)
        {
            lastVisibleCardCount = units.Count;
            AdjustBackgroundSize(units.Count);
        }
    }

    private UnitCardUI BuildCardUIFromObject(GameObject cardObj)
    {
        UnitCardUI cardUI = new UnitCardUI();
        cardUI.rootObject = cardObj;

        Image[] images = cardObj.GetComponentsInChildren<Image>(true);
        foreach (var img in images)
        {
            if (img.gameObject != cardObj)
            {
                cardUI.iconImage = img;
                break;
            }
        }

        TextMeshProUGUI[] texts = cardObj.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var txt in texts)
        {
            string txtName = txt.gameObject.name.ToLower();
            if (txtName.Contains("name") || txtName.Contains("title"))
            {
                cardUI.nameText = txt;
            }
            else if (txtName.Contains("count") || txtName.Contains("amount") || txtName.Contains("num"))
            {
                cardUI.countText = txt;
            }
        }

        // Nếu không có tên phân biệt, gán text đầu tiên làm Name, text thứ 2 làm Count
        if (texts.Length > 0 && cardUI.nameText == null) cardUI.nameText = texts[0];
        if (texts.Length > 1 && cardUI.countText == null) cardUI.countText = texts[1];

        return cardUI;
    }

    private UnitCardUI CreateCardUI(Transform container, UnitDisplayInfo unit)
    {
        UnitCardUI cardUI = new UnitCardUI();
        GameObject cardObj = CreateRuntimeUnitCardObject(container, unit, cardUI);
        cardUI.rootObject = cardObj;
        return cardUI;
    }

    private GameObject CreateRuntimeUnitCardObject(Transform parent, UnitDisplayInfo unit, UnitCardUI cardUI)
    {
        // 1. Thẻ Card chính
        GameObject card = new GameObject($"Card_{unit.unitName}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        card.transform.SetParent(parent, false);

        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(cardWidth, cardHeight);
        cardRect.pivot = new Vector2(0.5f, 0.5f);

        LayoutElement cardLE = card.GetComponent<LayoutElement>();
        cardLE.minWidth = cardWidth;
        cardLE.minHeight = cardHeight;
        cardLE.preferredWidth = cardWidth;
        cardLE.preferredHeight = cardHeight;
        cardLE.flexibleWidth = 0;
        cardLE.flexibleHeight = 0;

        Image cardBg = card.GetComponent<Image>();
        cardBg.color = new Color(0.10f, 0.13f, 0.18f, 0.92f);

        VerticalLayoutGroup vlg = card.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 3;
        vlg.padding = new RectOffset(4, 4, 8, 4);
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        // 2. Icon lính (52 x 52)
        GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconObj.transform.SetParent(card.transform, false);

        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(52f, 52f);

        LayoutElement iconLE = iconObj.GetComponent<LayoutElement>();
        iconLE.minWidth = 52f;
        iconLE.minHeight = 52f;
        iconLE.preferredWidth = 52f;
        iconLE.preferredHeight = 52f;

        Image iconImg = iconObj.GetComponent<Image>();
        Sprite spr = unit.unitIcon != null ? unit.unitIcon : defaultSoldierIcon;
        iconImg.sprite = spr;
        iconImg.preserveAspect = true;
        cardUI.iconImage = iconImg;

        // 3. Text Tên Lính (78 x 18)
        GameObject nameObj = new GameObject("NameText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        nameObj.transform.SetParent(card.transform, false);

        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.sizeDelta = new Vector2(78f, 18f);

        LayoutElement nameLE = nameObj.GetComponent<LayoutElement>();
        nameLE.minWidth = 78f;
        nameLE.minHeight = 18f;
        nameLE.preferredWidth = 78f;
        nameLE.preferredHeight = 18f;

        TextMeshProUGUI nameTxt = nameObj.GetComponent<TextMeshProUGUI>();
        nameTxt.text = unit.unitName;
        nameTxt.fontSize = 11;
        nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.color = new Color(0.95f, 0.88f, 0.72f, 1f);
        nameTxt.enableWordWrapping = false;
        nameTxt.overflowMode = TextOverflowModes.Ellipsis;
        cardUI.nameText = nameTxt;

        // 4. Text Số Lượng Lính (78 x 20)
        GameObject countObj = new GameObject("CountText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        countObj.transform.SetParent(card.transform, false);

        RectTransform countRect = countObj.GetComponent<RectTransform>();
        countRect.sizeDelta = new Vector2(78f, 20f);

        LayoutElement countLE = countObj.GetComponent<LayoutElement>();
        countLE.minWidth = 78f;
        countLE.minHeight = 20f;
        countLE.preferredWidth = 78f;
        countLE.preferredHeight = 20f;

        TextMeshProUGUI countTxt = countObj.GetComponent<TextMeshProUGUI>();
        countTxt.text = unit.count > 0 ? $"x{unit.count}" : "<color=red>0</color>";
        countTxt.fontSize = 13;
        countTxt.fontStyle = FontStyles.Bold;
        countTxt.alignment = TextAlignmentOptions.Center;
        countTxt.color = unit.count > 0 ? Color.white : Color.red;
        countTxt.enableWordWrapping = false;
        cardUI.countText = countTxt;
        cardUI.lastCount = unit.count;

        return card;
    }

    private void AdjustBackgroundSize(int cardCount)
    {
        if (cardCount <= 0) return;

        float totalWidth = (cardCount * cardWidth) + ((cardCount - 1) * cardSpacing) + (paddingHorizontal * 2);
        float totalHeight = cardHeight + (paddingVertical * 2);

        if (armyRosterPanel != null)
        {
            RectTransform rosterRect = armyRosterPanel.GetComponent<RectTransform>();
            if (rosterRect != null)
            {
                rosterRect.sizeDelta = new Vector2(totalWidth, totalHeight);
            }
        }

        if (armyUnitContainer != null && armyUnitContainer.gameObject != armyRosterPanel)
        {
            RectTransform containerRect = armyUnitContainer.GetComponent<RectTransform>();
            if (containerRect != null)
            {
                containerRect.sizeDelta = new Vector2(totalWidth, totalHeight);
            }
        }
    }

    private List<UnitDisplayInfo> ScanCurrentLiveUnits()
    {
        List<UnitDisplayInfo> result = new List<UnitDisplayInfo>();

        UnitController[] allUnits = Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        int meleeCount = 0;
        int archerCount = 0;
        int spearCount = 0;
        int tankCount = 0;
        int otherCount = 0;

        foreach (var u in allUnits)
        {
            if (u == null || !u.gameObject.activeInHierarchy || u.isDead) continue;

            string objName = u.gameObject.name.ToLower();
            if (u.AttackMode == AttackMode.Tank || objName.Contains("tank") || objName.Contains("shield") || objName.Contains("khiên"))
            {
                tankCount++;
            }
            else if (u.AttackMode == AttackMode.Ranged || objName.Contains("archer"))
            {
                archerCount++;
            }
            else if (objName.Contains("spear"))
            {
                spearCount++;
            }
            else if (objName.Contains("soldier") || objName.Contains("melee") || u.AttackMode == AttackMode.Melee)
            {
                meleeCount++;
            }
            else
            {
                otherCount++;
            }
        }

        if (tankCount > 0)
        {
            result.Add(new UnitDisplayInfo(tankSoldierName, tankSoldierIcon != null ? tankSoldierIcon : defaultSoldierIcon, tankCount));
        }
        if (meleeCount > 0)
        {
            result.Add(new UnitDisplayInfo(meleeSoldierName, meleeSoldierIcon != null ? meleeSoldierIcon : defaultSoldierIcon, meleeCount));
        }
        if (archerCount > 0)
        {
            result.Add(new UnitDisplayInfo(archerSoldierName, archerSoldierIcon != null ? archerSoldierIcon : defaultSoldierIcon, archerCount));
        }
        if (spearCount > 0)
        {
            result.Add(new UnitDisplayInfo(spearSoldierName, spearSoldierIcon != null ? spearSoldierIcon : defaultSoldierIcon, spearCount));
        }
        if (otherCount > 0)
        {
            result.Add(new UnitDisplayInfo("Binh Lính", defaultSoldierIcon, otherCount));
        }

        if (result.Count == 0 && BattleData.TotalSoldiersInBase > 0)
        {
            result.Add(new UnitDisplayInfo(meleeSoldierName, meleeSoldierIcon != null ? meleeSoldierIcon : defaultSoldierIcon, BattleData.TotalSoldiersInBase));
        }

        return result;
    }

    #endregion

    #region Victory & Defeat Results

    private void CheckAndShowBattleDataResult()
    {
        if (BattleData.HasResult && !hasShownResultUI)
        {
            hasShownResultUI = true;
            if (BattleData.IsPlayerVictory)
            {
                ShowVictory(sampleRewards, sampleUnitsLost);
            }
            else
            {
                ShowDefeat(sampleUnitsLost);
            }
        }
    }

    public void ShowVictory(List<RewardItem> rewards = null, List<UnitLostItem> unitsLost = null)
    {
        hasShownResultUI = true;
        isReturning = false;
        if (armyRosterPanel != null) armyRosterPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(true);

        SetupReturnButtons();

        PopulateRewards(rewards ?? sampleRewards);
        PopulateUnitsLost(victoryUnitsLostContainer, unitsLost ?? sampleUnitsLost);
    }

    public void ShowDefeat(List<UnitLostItem> unitsLost = null, string tipMessage = "")
    {
        hasShownResultUI = true;
        isReturning = false;
        if (armyRosterPanel != null) armyRosterPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(true);

        SetupReturnButtons();

        if (defeatTipText != null)
        {
            defeatTipText.text = string.IsNullOrEmpty(tipMessage) ? defaultDefeatTip : tipMessage;
        }

        PopulateUnitsLost(defeatUnitsLostContainer, unitsLost ?? sampleUnitsLost);
    }

    public void HideAll()
    {
        HideResultPanels();
        if (armyRosterPanel != null) armyRosterPanel.SetActive(false);
    }

    public void HideResultPanels()
    {
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
    }

    public void OnReturnButtonClicked()
    {
        if (isReturning) return;
        isReturning = true;

        Time.timeScale = 1f;

        if (victoryReturnButton != null) victoryReturnButton.interactable = false;
        if (defeatReturnButton != null) defeatReturnButton.interactable = false;

        PlayReturnSound();
        HideAll();

        string targetScene = (BattleData.HasData && !string.IsNullOrEmpty(BattleData.MainSceneName)) ? BattleData.MainSceneName : returnSceneName;
        if (string.IsNullOrEmpty(targetScene)) targetScene = "MainScene";

        Debug.Log($"[UISceneBattle] ☁️ Bắt đầu hiệu ứng đóng mây chuyển về '{targetScene}'...");
        CloudSceneTransition.LoadSceneWithCloud(targetScene);
    }

    private void PlayReturnSound()
    {
        if (returnButtonSFX != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(returnButtonSFX);
            }
            else
            {
                AudioSource.PlayClipAtPoint(returnButtonSFX, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            }
        }

        if (UISoundManager.Instance != null)
        {
            UISoundManager.Instance.PlayClickSound();
        }
    }

    #endregion

    #region Helper Methods

    private void PopulateRewards(List<RewardItem> rewards)
    {
        if (victoryRewardContainer == null) return;

        ClearContainer(victoryRewardContainer);

        if (rewards == null || rewards.Count == 0) return;

        foreach (var item in rewards)
        {
            if (rewardItemPrefab != null)
            {
                GameObject obj = Instantiate(rewardItemPrefab, victoryRewardContainer);
                Image img = obj.GetComponentInChildren<Image>();
                TextMeshProUGUI txt = obj.GetComponentInChildren<TextMeshProUGUI>();

                if (img != null && item.icon != null) img.sprite = item.icon;
                if (txt != null) txt.text = item.amountText;
            }
            else
            {
                GameObject itemObj = new GameObject("RewardItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(VerticalLayoutGroup));
                itemObj.transform.SetParent(victoryRewardContainer, false);

                VerticalLayoutGroup layout = itemObj.GetComponent<VerticalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = false;

                GameObject imgObj = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                imgObj.transform.SetParent(itemObj.transform, false);
                RectTransform imgRect = imgObj.GetComponent<RectTransform>();
                imgRect.sizeDelta = new Vector2(48, 48);
                Image img = imgObj.GetComponent<Image>();
                if (item.icon != null) img.sprite = item.icon;

                if (!string.IsNullOrEmpty(item.amountText))
                {
                    GameObject txtObj = new GameObject("AmountText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    txtObj.transform.SetParent(itemObj.transform, false);
                    RectTransform txtRect = txtObj.GetComponent<RectTransform>();
                    txtRect.sizeDelta = new Vector2(80, 30);
                    TextMeshProUGUI txt = txtObj.GetComponent<TextMeshProUGUI>();
                    txt.text = item.amountText;
                    txt.fontSize = 22;
                    txt.fontStyle = FontStyles.Bold;
                    txt.alignment = TextAlignmentOptions.Center;
                    txt.color = rewardTextColor;
                }
            }
        }
    }

    private void PopulateUnitsLost(Transform container, List<UnitLostItem> unitsLost)
    {
        if (container == null) return;

        ClearContainer(container);

        if (unitsLost == null || unitsLost.Count == 0) return;

        foreach (var unit in unitsLost)
        {
            int countToSpawn = Mathf.Max(1, unit.count);
            for (int i = 0; i < countToSpawn; i++)
            {
                if (unitLostItemPrefab != null)
                {
                    GameObject obj = Instantiate(unitLostItemPrefab, container);
                    Image img = obj.GetComponentInChildren<Image>();
                    TextMeshProUGUI txt = obj.GetComponentInChildren<TextMeshProUGUI>();

                    if (img != null && unit.icon != null) img.sprite = unit.icon;
                    if (txt != null)
                    {
                        txt.text = unit.count > 1 ? $"x{unit.count}" : "";
                    }
                }
                else
                {
                    GameObject imgObj = new GameObject("UnitLostItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    imgObj.transform.SetParent(container, false);
                    RectTransform imgRect = imgObj.GetComponent<RectTransform>();
                    imgRect.sizeDelta = new Vector2(52, 52);
                    Image img = imgObj.GetComponent<Image>();
                    if (unit.icon != null) img.sprite = unit.icon;
                }
            }
        }
    }

    private void ClearContainer(Transform container)
    {
        if (container == null) return;
        foreach (Transform child in container)
        {
            if (child != null)
            {
                Destroy(child.gameObject, 0.01f);
            }
        }
    }

    #endregion
}
