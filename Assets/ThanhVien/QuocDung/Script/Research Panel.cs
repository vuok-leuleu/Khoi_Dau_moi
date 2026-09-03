using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Điều khiển cây nghiên cứu được dựng sẵn trong Canvas bằng Unity Editor.
/// Script chỉ cập nhật các object đã được gán trong Inspector, không tạo UI bằng code.
/// </summary>
public class ResearchPanel : MonoBehaviour
{
    private const string SaveKeyPrefix = "ResearchNode_";

    /// <summary>Raised whenever the set of researched nodes changes.</summary>
    public event Action ResearchStateChanged;

    [Serializable]
    public class ResearchNode
    {
        [Header("Identity and content")]
        public string id;
        public string displayName;
        [TextArea(2, 5)] public string description;
        [TextArea(1, 3)] public string benefitDescription;
        public string[] requiredNodeIds;

        [Header("Unlock cost")]
        [Min(0)] public int woodCost;
        [Min(0)] public int stoneCost;
        [Min(0)] public int goldCost;
        public bool unlockedAtStart;

        [Header("Existing Canvas references")]
        public Button button;
        public Image icon;
        public GameObject lockedOverlay;
        public UnityEvent onUnlocked;

        [NonSerialized] public bool unlocked;
    }

    [Serializable]
    public class ResearchConnection
    {
        public string fromNodeId;
        public string toNodeId;
        public Image line;
    }

    [Header("Tree data")]
    [SerializeField] private List<ResearchNode> nodes = new List<ResearchNode>();
    [SerializeField] private List<ResearchConnection> connections = new List<ResearchConnection>();
    [SerializeField] private bool keepProgressBetweenEnable = true;

    [Header("Existing Canvas controls")]
    [SerializeField] private Button closeButton;

    [Header("Detail panel - create and assign this panel in the Editor")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private Button detailCloseButton;
    [SerializeField] private Button researchButton;
    [SerializeField] private Image detailIcon;
    [SerializeField] private TMP_Text detailTitleText;
    [SerializeField] private TMP_Text detailDescriptionText;
    [SerializeField] private TMP_Text detailBenefitText;
    [SerializeField] private TMP_Text detailCostText;
    [SerializeField] private TMP_Text detailRequirementText;
    [SerializeField] private TMP_Text detailStatusText;

    [Header("Visual state")]
    [SerializeField] private Color lockedColor = new Color(0.25f, 0.20f, 0.12f, 1f);
    [SerializeField] private Color availableColor = new Color(0.25f, 0.20f, 0.12f, 1f);
    [SerializeField] private Color unlockedColor = new Color(0.95f, 0.65f, 0.15f, 1f);
    [SerializeField] private Color inactiveConnectionColor = new Color(0.25f, 0.20f, 0.12f, 1f);
    [SerializeField] private Color activeConnectionColor = new Color(0.95f, 0.65f, 0.15f, 1f);

    private readonly Dictionary<string, ResearchNode> nodeById = new Dictionary<string, ResearchNode>();
    private readonly Dictionary<Button, UnityAction> nodeButtonActions = new Dictionary<Button, UnityAction>();
    private JsonDataManager resourceManager;
    private string selectedNodeId;
    private bool initialized;
    private bool hasRuntimeState;
    private bool resourceEventsBound;
    private GameObject lastSelectedObject;

    private void OnEnable()
    {
        // Lưu selection đã dùng để mở panel, tránh tự đóng ngay trong frame kế tiếp.
        lastSelectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        Initialize();
    }

    private void OnDisable()
    {
        UnbindButtons();
        UnbindResourceEvents();
        initialized = false;
    }

    private void LateUpdate()
    {
        if (!initialized || EventSystem.current == null) return;

        GameObject selectedObject = EventSystem.current.currentSelectedGameObject;
        if (selectedObject == lastSelectedObject) return;
        lastSelectedObject = selectedObject;

        // Khi Button/Toggle/Dropdown của UI khác được bấm, đóng bảng nghiên cứu.
        // Các selectable thuộc cây nghiên cứu hoặc detail card thì được giữ nguyên.
        if (selectedObject != null
            && !IsResearchUiObject(selectedObject)
            && selectedObject.GetComponentInParent<Selectable>() != null)
        {
            ClosePanel();
        }
    }

    // DetailPanel được đặt sẵn trong Canvas và có thể là sibling của ResearchPanel,
    // nên các Button bên trong nó vẫn thuộc UI nghiên cứu, không được kích hoạt auto-close.
    private bool IsResearchUiObject(GameObject uiObject)
    {
        if (uiObject.transform.IsChildOf(transform)) return true;
        return detailPanel != null && uiObject.transform.IsChildOf(detailPanel.transform);
    }

    public void Initialize()
    {
        if (initialized) return;

        nodeById.Clear();
        bool isFirstRuntimeInitialization = !hasRuntimeState;
        bool resetRuntimeState = !keepProgressBetweenEnable || !hasRuntimeState;
        foreach (ResearchNode node in nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.id)) continue;
            SetNodeLabel(node);

            if (nodeById.ContainsKey(node.id))
            {
                Debug.LogWarning($"[ResearchPanel] Duplicate node id: {node.id}", this);
                continue;
            }

            // Tiến trình nghiên cứu là dữ liệu gameplay, không phải trạng thái
            // hiển thị của Canvas. Khi scene/map được nạp lại, panel có thể bị
            // tắt lúc khởi động nên cần khôi phục từ PlayerPrefs ngay lần đầu
            // nó được tạo trong runtime này.
            if (resetRuntimeState)
            {
                node.unlocked = node.unlockedAtStart ||
                                (isFirstRuntimeInitialization && IsNodeSavedAsUnlocked(node.id));
            }
            nodeById.Add(node.id, node);
        }

        if (resetRuntimeState) hasRuntimeState = true;
        ConfigureDemaciaConnections();
        BindButtons();
        BindResourceEvents();
        HideDetails();
        RefreshVisuals();
        initialized = true;
        ResearchStateChanged?.Invoke();
    }

    private void BindButtons()
    {
        UnbindButtons();
        foreach (ResearchNode node in nodes)
        {
            if (node == null || node.button == null || string.IsNullOrWhiteSpace(node.id)) continue;
            string capturedId = node.id;
            UnityAction action = () => SelectNode(capturedId);
            node.button.onClick.AddListener(action);
            nodeButtonActions[node.button] = action;
        }

        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
        if (detailCloseButton != null) detailCloseButton.onClick.AddListener(HideDetails);
        if (researchButton != null) researchButton.onClick.AddListener(UnlockSelectedNode);
    }

    private void UnbindButtons()
    {
        foreach (KeyValuePair<Button, UnityAction> pair in nodeButtonActions)
            if (pair.Key != null) pair.Key.onClick.RemoveListener(pair.Value);
        nodeButtonActions.Clear();

        if (closeButton != null) closeButton.onClick.RemoveListener(ClosePanel);
        if (detailCloseButton != null) detailCloseButton.onClick.RemoveListener(HideDetails);
        if (researchButton != null) researchButton.onClick.RemoveListener(UnlockSelectedNode);
    }

    private void BindResourceEvents()
    {
        resourceManager = FindFirstObjectByType<JsonDataManager>();
        if (resourceManager == null || resourceEventsBound) return;

        resourceManager.OnWoodChanged += OnResourcesChanged;
        resourceManager.OnStoneChanged += OnResourcesChanged;
        resourceManager.OnGoldChanged += OnResourcesChanged;
        resourceEventsBound = true;
    }

    private void UnbindResourceEvents()
    {
        if (resourceManager == null || !resourceEventsBound) return;

        resourceManager.OnWoodChanged -= OnResourcesChanged;
        resourceManager.OnStoneChanged -= OnResourcesChanged;
        resourceManager.OnGoldChanged -= OnResourcesChanged;
        resourceEventsBound = false;
    }

    private void OnResourcesChanged(int _) => RefreshVisuals();

    /// <summary>Gọi khi người chơi bấm node. Node luôn mở được panel để xem thông tin.</summary>
    public void SelectNode(string nodeId)
    {
        if (!nodeById.ContainsKey(nodeId)) return;

        selectedNodeId = nodeId;
        if (detailPanel != null) detailPanel.SetActive(true);
        else Debug.LogWarning("[ResearchPanel] Chưa gán Detail Panel trong Inspector.", this);
        RefreshVisuals();
    }

    /// <summary>Gọi bởi Button Nghiên cứu của detail panel.</summary>
    public void UnlockSelectedNode()
    {
        if (!string.IsNullOrWhiteSpace(selectedNodeId)) TryUnlock(selectedNodeId);
    }

    public bool TryUnlock(string nodeId)
    {
        if (!nodeById.TryGetValue(nodeId, out ResearchNode node) || node.unlocked) return false;
        if (!PrerequisitesMet(node)) return false;

        if (resourceManager == null) BindResourceEvents();
        if (resourceManager == null)
        {
            Debug.LogWarning("[ResearchPanel] Không tìm thấy JsonDataManager để trừ tài nguyên.", this);
            RefreshVisuals();
            return false;
        }

        if (!resourceManager.TrySpendCombined(woodCost: node.woodCost, stoneCost: node.stoneCost, goldCost: node.goldCost))
        {
            RefreshVisuals();
            return false;
        }

        node.unlocked = true;
        SaveNodeUnlockState(node.id, true);
        node.onUnlocked?.Invoke();

        // Không phụ thuộc vào ResearchUpgradeEffects đang enabled hay không:
        // ResearchPanel thường bị SetActive(false) khi đóng UI, nhưng nâng cấp
        // vừa mua vẫn phải lập tức áp dụng cho lính đang có trên bản đồ.
        ResearchUpgradeEffects.ApplyResearchState(this);
        ChapterQuestController.Instance?.ReportResearchUnlocked(node.id);
        // Nghiên cứu thành công thì đóng thẻ chi tiết. Bấm một node khác sẽ gọi
        // SelectNode và mở lại DetailPanel như bình thường.
        HideDetails();
        RefreshVisuals();
        ResearchStateChanged?.Invoke();
        return true;
    }

    public void ResetTree()
    {
        foreach (ResearchNode node in nodes)
        {
            if (node == null) continue;
            node.unlocked = node.unlockedAtStart;
            SaveNodeUnlockState(node.id, node.unlockedAtStart);
        }
        PlayerPrefs.Save();
        selectedNodeId = null;
        HideDetails();
        RefreshVisuals();
        ResearchUpgradeEffects.ApplyResearchState(this);
        ResearchStateChanged?.Invoke();
    }

    public void ClosePanel()
    {
        HideDetails();
        gameObject.SetActive(false);
    }

    public void HideDetails()
    {
        selectedNodeId = null;
        if (detailPanel != null) detailPanel.SetActive(false);
    }

    private bool PrerequisitesMet(ResearchNode node)
    {
        if (node == null) return false;

        // `requiredNodeIds` giữ các yêu cầu gameplay được cấu hình riêng trong
        // Inspector. Ngoài ra, mỗi đường nối đi vào node trong cây cũng là một
        // điều kiện bắt buộc: chưa nâng node ở trước thì không thể nâng node ở
        // sau. Nhờ vậy cây nghiên cứu không thể bị bỏ qua chỉ vì một node trước
        // đó chưa được thêm thủ công vào requiredNodeIds.
        HashSet<string> requiredIds = new HashSet<string>();
        if (node.requiredNodeIds != null)
        {
            foreach (string requiredId in node.requiredNodeIds)
            {
                if (!string.IsNullOrWhiteSpace(requiredId)) requiredIds.Add(requiredId);
            }
        }

        foreach (ResearchConnection connection in connections)
        {
            if (connection == null || connection.toNodeId != node.id ||
                string.IsNullOrWhiteSpace(connection.fromNodeId)) continue;

            requiredIds.Add(connection.fromNodeId);
        }

        foreach (string requiredId in requiredIds)
        {
            if (!nodeById.TryGetValue(requiredId, out ResearchNode required) || !required.unlocked)
                return false;
        }

        return true;
    }

    /// <summary>Lets gameplay systems query research without exposing the serialized list.</summary>
    public bool IsUnlocked(string nodeId)
    {
        return !string.IsNullOrWhiteSpace(nodeId)
               && nodeById.TryGetValue(nodeId, out ResearchNode node)
               && node.unlocked;
    }

    /// <summary>
    /// Đọc tiến trình khi ResearchCanvas chưa được bật. Gameplay dùng hàm này
    /// để không bị phụ thuộc vào UI đang hiển thị hay bị ẩn.
    /// </summary>
    public static bool IsNodeSavedAsUnlocked(string nodeId)
    {
        return !string.IsNullOrWhiteSpace(nodeId) &&
               PlayerPrefs.GetInt(SaveKeyPrefix + nodeId, 0) == 1;
    }

    private static void SaveNodeUnlockState(string nodeId, bool unlocked)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return;

        PlayerPrefs.SetInt(SaveKeyPrefix + nodeId, unlocked ? 1 : 0);
        PlayerPrefs.Save();
    }

    // The visual cards already contain a TMP label. Keep it synchronized with
    // the serialized node content so the card and detail panel cannot diverge.
    private static void SetNodeLabel(ResearchNode node)
    {
        if (node == null || node.button == null) return;
        TMP_Text label = node.button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = node.displayName;
    }

    // Keep the connection records aligned with the authored connector artwork.
    // The visual paths form branches; gameplay prerequisites remain the
    // authoritative requirements shown in the detail card.
    private void ConfigureDemaciaConnections()
    {
        if (connections.Count < 18 || !nodeById.ContainsKey("command_1")) return;

        string[,] pairs =
        {
            { "command_1", "formation_1" },
            { "shield_damage_1", "formation_3" },
            { "formation_2", "sword_defense_1" },
            { "formation_1", "sword_damage_1" },
            { "formation_1", "shield_damage_1" },
            { "sword_damage_1", "bow_damage_1" },
            { "formation_3", "cannon_defense_1" },
            { "shield_defense_1", "crossbow_damage_1" },
            { "sword_damage_1", "sword_defense_1" },
            { "sword_defense_1", "shield_defense_1" },
            { "bow_damage_1", "crossbow_damage_1" },
            { "crossbow_damage_1", "crossbow_defense_1" },
            { "crossbow_damage_1", "bow_defense_1" },
            { "bow_defense_1", "unlock_cannon_tower_1" },
            { "unlock_cannon_tower_1", "cannon_damage_1" },
            { "cannon_damage_1", "cannon_defense_1" },
            { "crossbow_defense_1", "army_doctrine_1" },
            { "cannon_defense_1", "army_doctrine_1" }
        };

        for (int i = 0; i < pairs.GetLength(0); i++)
        {
            connections[i].fromNodeId = pairs[i, 0];
            connections[i].toNodeId = pairs[i, 1];
        }
    }

    private bool HasEnoughResources(ResearchNode node)
    {
        return resourceManager != null && resourceManager.HasEnoughResources(
            node.woodCost, node.stoneCost, 0, node.goldCost);
    }

    private void RefreshVisuals()
    {
        foreach (ResearchNode node in nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.id)) continue;
            bool prerequisitesMet = PrerequisitesMet(node);
            // Không khóa Button: cả node chưa đủ điều kiện vẫn phải mở được panel thông tin.
            if (node.button != null) node.button.interactable = true;
            if (node.lockedOverlay != null) node.lockedOverlay.SetActive(!node.unlocked && !prerequisitesMet);
            // Tất cả node chưa nâng cấp dùng cùng màu tối; node đã nâng cấp dùng vàng.
            if (node.icon != null) node.icon.color = node.unlocked ? unlockedColor : lockedColor;
        }

        foreach (ResearchConnection connection in connections)
        {
            if (connection == null || connection.line == null) continue;
            bool active = nodeById.TryGetValue(connection.fromNodeId, out ResearchNode from) && from.unlocked;
            connection.line.color = active ? activeConnectionColor : inactiveConnectionColor;
        }

        RefreshDetailPanel();
    }

    private void RefreshDetailPanel()
    {
        if (string.IsNullOrWhiteSpace(selectedNodeId) || !nodeById.TryGetValue(selectedNodeId, out ResearchNode node)) return;

        bool prerequisitesMet = PrerequisitesMet(node);
        bool canAfford = HasEnoughResources(node);
        if (detailTitleText != null) detailTitleText.text = node.displayName;
        if (detailDescriptionText != null) detailDescriptionText.text = node.description;
        if (detailBenefitText != null) detailBenefitText.text = node.benefitDescription;
        if (detailCostText != null) detailCostText.text = FormatCost(node);
        if (detailRequirementText != null) detailRequirementText.text = FormatRequirements(node);
        if (detailIcon != null) detailIcon.sprite = node.icon != null ? node.icon.sprite : null;

        if (node.unlocked)
        {
            SetStatus("ĐÃ NGHIÊN CỨU", unlockedColor);
            if (researchButton != null) researchButton.interactable = false;
        }
        else if (!prerequisitesMet)
        {
            SetStatus("CHƯA ĐỦ ĐIỀU KIỆN", lockedColor);
            if (researchButton != null) researchButton.interactable = false;
        }
        else if (!canAfford)
        {
            SetStatus(resourceManager == null ? "CHƯA TẢI KHO TÀI NGUYÊN" : "THIẾU TÀI NGUYÊN", lockedColor);
            if (researchButton != null) researchButton.interactable = false;
        }
        else
        {
            SetStatus("SẴN SÀNG NGHIÊN CỨU", availableColor);
            if (researchButton != null) researchButton.interactable = true;
        }
    }

    private void SetStatus(string status, Color color)
    {
        if (detailStatusText == null) return;
        detailStatusText.text = status;
        detailStatusText.color = color;
    }

    private string FormatCost(ResearchNode node)
    {
        List<string> costs = new List<string>();
        if (node.woodCost > 0) costs.Add($"Gỗ: {node.woodCost}");
        if (node.stoneCost > 0) costs.Add($"Đá: {node.stoneCost}");
        if (node.goldCost > 0) costs.Add($"Vàng: {node.goldCost}");
        return costs.Count == 0 ? "Chi phí: Miễn phí" : "Chi phí: " + string.Join("  •  ", costs);
    }

    private string FormatRequirements(ResearchNode node)
    {
        if (node == null) return "Yêu cầu: Không có";

        // Hiển thị đúng các điều kiện đang được TryUnlock kiểm tra, bao gồm cả
        // node đi vào từ đường nối trên ResearchCanvas.
        HashSet<string> requiredIds = new HashSet<string>();
        if (node.requiredNodeIds != null)
        {
            foreach (string requiredId in node.requiredNodeIds)
            {
                if (!string.IsNullOrWhiteSpace(requiredId)) requiredIds.Add(requiredId);
            }
        }

        foreach (ResearchConnection connection in connections)
        {
            if (connection == null || connection.toNodeId != node.id ||
                string.IsNullOrWhiteSpace(connection.fromNodeId)) continue;

            requiredIds.Add(connection.fromNodeId);
        }

        if (requiredIds.Count == 0) return "Yêu cầu: Không có";

        List<string> names = new List<string>();
        foreach (string requiredId in requiredIds)
        {
            if (nodeById.TryGetValue(requiredId, out ResearchNode required)) names.Add(required.displayName);
            else names.Add(requiredId);
        }
        return "Yêu cầu: " + string.Join(" + ", names);
    }

}
