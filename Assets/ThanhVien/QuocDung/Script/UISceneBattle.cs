using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Quản lý UI Chiến Thắng (Victory) và Thất Bại (Defeat) cho Scene Battle theo phong cách Demacia Rising (LoL).
/// </summary>
public class UISceneBattle : MonoBehaviour
{
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

    [Header("--- UI Panels & Backgrounds ---")]
    [Tooltip("Panel khung Victory (Chiến thắng)")]
    public GameObject victoryPanel;
    [Tooltip("Image nền của Panel Victory (Tùy chọn gán ảnh khung)")]
    public Image victoryBackgroundImage;

    [Tooltip("Panel khung Defeat (Thất bại)")]
    public GameObject defeatPanel;
    [Tooltip("Image nền của Panel Defeat (Tùy chọn gán ảnh khung)")]
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
    [Tooltip("Màu chữ số lượng phần thưởng (Mặc định: Nâu đậm #3E2617 cho nổi bật trên nền kem)")]
    public Color rewardTextColor = new Color(0.24f, 0.15f, 0.09f, 1f);

    [Header("--- Defeat Elements ---")]
    [Tooltip("Container chứa danh sách lính tử trận màn Defeat")]
    public Transform defeatUnitsLostContainer;
    [Tooltip("Text hiển thị mẹo / câu nói khi thất bại (Ví dụ: Drakehounds are quick and hunt in packs.)")]
    public TextMeshProUGUI defeatTipText;
    [TextArea(2, 4)]
    public string defaultDefeatTip = "Drakehounds are quick and hunt in packs.";

    [Header("--- Prefabs (Tùy chọn hiển thị icon) ---")]
    [Tooltip("Prefab hiển thị 1 phần thưởng (chứa Image icon và Text amount)")]
    public GameObject rewardItemPrefab;
    [Tooltip("Prefab hiển thị 1 lính bị mất (chứa Image icon và Text count nếu có)")]
    public GameObject unitLostItemPrefab;

    [Header("--- Sample Demo Data (Cấu hình mẫu trong Inspector) ---")]
    public List<RewardItem> sampleRewards = new List<RewardItem>();
    public List<UnitLostItem> sampleUnitsLost = new List<UnitLostItem>();

    [Header("--- Settings & Debug ---")]
    [Tooltip("Tự động kiểm tra kết quả trong BattleData khi Start")]
    public bool checkBattleDataOnStart = true;
    [Tooltip("Bật phím tắt test trong Editor: V (Victory), F (Defeat), H (Hide)")]
    public bool enableDebugKeys = true;

    public static UISceneBattle Instance { get; private set; }
    private bool hasShownResultUI = false;

    private void Awake()
    {
        Instance = this;

        // Tự động tìm Nút Return trong Panel nếu quên gán trong Inspector
        if (victoryReturnButton == null && victoryPanel != null)
            victoryReturnButton = victoryPanel.GetComponentInChildren<Button>();

        if (defeatReturnButton == null && defeatPanel != null)
            defeatReturnButton = defeatPanel.GetComponentInChildren<Button>();

        if (victoryReturnButton != null)
            victoryReturnButton.onClick.AddListener(OnReturnButtonClicked);

        if (defeatReturnButton != null)
            defeatReturnButton.onClick.AddListener(OnReturnButtonClicked);
    }

    private void Start()
    {
        HideAll();

        CheckAndShowBattleDataResult();
    }

    private void Update()
    {
        // Tự động bật UI ngay khi BattleData nhận kết quả trận đấu trong quá trình chơi
        if (checkBattleDataOnStart && BattleData.HasResult && !hasShownResultUI)
        {
            CheckAndShowBattleDataResult();
        }

        if (!enableDebugKeys) return;

        // Phím V: Test Màn Victory
        if (Input.GetKeyDown(KeyCode.V))
        {
            ShowVictory(sampleRewards, sampleUnitsLost);
        }
        // Phím F: Test Màn Defeat
        else if (Input.GetKeyDown(KeyCode.F))
        {
            ShowDefeat(sampleUnitsLost);
        }
        // Phím H: Ẩn tất cả UI kết thúc
        else if (Input.GetKeyDown(KeyCode.H))
        {
            HideAll();
        }
    }

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

    #region Public API

    /// <summary>
    /// Hiển thị bảng VICTORY
    /// </summary>
    public void ShowVictory(List<RewardItem> rewards = null, List<UnitLostItem> unitsLost = null)
    {
        hasShownResultUI = true;
        if (defeatPanel != null) defeatPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(true);

        PopulateRewards(rewards ?? sampleRewards);
        PopulateUnitsLost(victoryUnitsLostContainer, unitsLost ?? sampleUnitsLost);
    }

    /// <summary>
    /// Hiển thị bảng DEFEAT
    /// </summary>
    public void ShowDefeat(List<UnitLostItem> unitsLost = null, string tipMessage = "")
    {
        hasShownResultUI = true;
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(true);

        if (defeatTipText != null)
        {
            defeatTipText.text = string.IsNullOrEmpty(tipMessage) ? defaultDefeatTip : tipMessage;
        }

        PopulateUnitsLost(defeatUnitsLostContainer, unitsLost ?? sampleUnitsLost);
    }

    /// <summary>
    /// Ẩn tất cả các panel kết quả
    /// </summary>
    public void HideAll()
    {
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
    }

    /// <summary>
    /// Chuyển về Scene chính khi bấm nút RETURN
    /// </summary>
    public void OnReturnButtonClicked()
    {
        string targetScene = !string.IsNullOrEmpty(BattleData.MainSceneName) ? BattleData.MainSceneName : returnSceneName;
        if (!string.IsNullOrEmpty(targetScene))
        {
            SceneManager.LoadScene(targetScene);
        }
        else
        {
            Debug.LogWarning("[UISceneBattle] Chưa cài đặt tên Target Scene để Return!");
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
                // Tự động tạo UI Icon & Text nếu không gán Prefab
                GameObject itemObj = new GameObject("RewardItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(VerticalLayoutGroup));
                itemObj.transform.SetParent(victoryRewardContainer, false);

                VerticalLayoutGroup layout = itemObj.GetComponent<VerticalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = false;

                // 1. Tạo Image Icon
                GameObject imgObj = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                imgObj.transform.SetParent(itemObj.transform, false);
                RectTransform imgRect = imgObj.GetComponent<RectTransform>();
                imgRect.sizeDelta = new Vector2(48, 48);
                Image img = imgObj.GetComponent<Image>();
                if (item.icon != null) img.sprite = item.icon;

                // 2. Tạo Text Số lượng
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
                    // Tự động tạo Icon Lính tử trận nếu không gán Prefab
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
        foreach (Transform child in container)
        {
            Destroy(child.gameObject, 0.01f);
        }
    }

    #endregion
}

