using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

/*
 * HUDController.cs
 * Folder: Scripts/UI/
 * Dự án: KHẨN HOANG (PENTA DEV)
 * Tự động đồng bộ tài nguyên, nảy Icon vật phẩm với DOTween và chống lỗi NullReferenceException.
 */

public class HUDController : MonoBehaviour
{
    [Header("Top UI – Text Số Lượng Tài Nguyên")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI woodText;
    public TextMeshProUGUI stoneText;
    public TextMeshProUGUI foodText;

    [Header("Top UI – Object Cha nhóm (GoldGroup, WoodGroup, StoneGroup, FoodGroup)")]
    public RectTransform goldGroup;
    public RectTransform woodGroup;
    public RectTransform stoneGroup;
    public RectTransform foodGroup;

    [Header("Top UI – Icon Vật Phẩm (Tùy chọn)")]
    public RectTransform goldIcon;
    public RectTransform woodIcon;
    public RectTransform stoneIcon;
    public RectTransform foodIcon;

    [Header("Floating Text FX")]
    public GameObject floatingTextPrefab;
    public Transform floatingTextParent;

    private int _currentGold;
    private int _currentWood;
    private int _currentStone;
    private int _currentFood;

    // --- HỆ THỐNG OBJECT POOL MINI ---
    private Queue<GameObject> _floatingTextPool = new Queue<GameObject>();

    // --- CẤU TRÚC GỘP TÀI NGUYÊN (CHỐNG SPAM REBUILD MESH UI) ---
    private Dictionary<TextMeshProUGUI, int> _pendingDeltas = new Dictionary<TextMeshProUGUI, int>();
    private Dictionary<TextMeshProUGUI, float> _cooldownTimers = new Dictionary<TextMeshProUGUI, float>();
    private const float UI_REFRESH_COOLDOWN = 0.05f;

    public static HUDController Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        AutoFindUIReferences();
    }

    private void Start()
    {
        AutoFindUIReferences();

        // Khởi tạo giá trị ban đầu trực tiếp từ dữ liệu thực tế
        if (JsonDataManager.Ins != null)
        {
            _currentGold = JsonDataManager.Ins.gold;
            _currentWood = JsonDataManager.Ins.wood;
            _currentStone = JsonDataManager.Ins.stone;
            _currentFood = JsonDataManager.Ins.food;
        }

        SetTextInstant(goldText, _currentGold);
        SetTextInstant(woodText, _currentWood);
        SetTextInstant(stoneText, _currentStone);
        SetTextInstant(foodText, _currentFood);

        Debug.Log("[HUDController] ✅ Khởi tạo HUD thành công với thông số ban đầu.");
    }

    /// <summary>
    /// Tự động quét và gán chính xác theo Object Cha (GoldGroup, WoodGroup, StoneGroup, FoodGroup)
    /// </summary>
    [ContextMenu("Rebind All UI Groups")]
    public void AutoFindUIReferences()
    {
        Transform topPanel = transform;
        if (topPanel.name != "Gameplay_Panel_Top")
        {
            var found = GameObject.Find("Gameplay_Panel_Top");
            if (found != null) topPanel = found.transform;
        }

        if (topPanel == null) return;

        // 🎯 LẤY CHÍNH XÁC OBJECT CHA NHÓM TÀI NGUYÊN
        if (goldGroup == null) goldGroup = (topPanel.Find("RightGroup/GoldGroup") ?? topPanel.Find("GoldGroup")) as RectTransform;
        if (woodGroup == null) woodGroup = (topPanel.Find("RightGroup/WoodGroup") ?? topPanel.Find("WoodGroup")) as RectTransform;
        if (stoneGroup == null) stoneGroup = (topPanel.Find("RightGroup/StoneGroup") ?? topPanel.Find("StoneGroup")) as RectTransform;
        if (foodGroup == null) foodGroup = (topPanel.Find("RightGroup/FoodGroup") ?? topPanel.Find("FoodGroup")) as RectTransform;

        if (goldGroup != null)
        {
            goldText = goldGroup.GetComponentInChildren<TextMeshProUGUI>(true);
            goldIcon = goldGroup.Find("GoldIcon") as RectTransform ?? goldGroup.Find("WheatIcon") as RectTransform ?? goldGroup.GetComponentInChildren<Image>(true)?.rectTransform;
        }

        if (woodGroup != null)
        {
            woodText = woodGroup.GetComponentInChildren<TextMeshProUGUI>(true);
            woodIcon = woodGroup.Find("WoodIcon") as RectTransform ?? woodGroup.GetComponentInChildren<Image>(true)?.rectTransform;
        }

        if (stoneGroup != null)
        {
            stoneText = stoneGroup.GetComponentInChildren<TextMeshProUGUI>(true);
            stoneIcon = stoneGroup.Find("StoneIcon") as RectTransform ?? stoneGroup.GetComponentInChildren<Image>(true)?.rectTransform;
        }

        if (foodGroup != null)
        {
            foodText = foodGroup.GetComponentInChildren<TextMeshProUGUI>(true);
            foodIcon = foodGroup.Find("FoodIcon") as RectTransform ?? foodGroup.GetComponentInChildren<Image>(true)?.rectTransform;
        }

        if (floatingTextParent == null) floatingTextParent = topPanel.Find("RightGroup/FloatingText");

        Debug.Log($"[HUDController] 🔄 Đã liên kết với Object Cha: GoldGroup={goldGroup?.name}, WoodGroup={woodGroup?.name}, StoneGroup={stoneGroup?.name}, FoodGroup={foodGroup?.name}");
    }

    private void Update()
    {
        if (_cooldownTimers.Count == 0) return;

        // Xử lý bộ đếm thời gian gộp tài nguyên an toàn (tránh NullReferenceException)
        List<TextMeshProUGUI> keys = new List<TextMeshProUGUI>(_cooldownTimers.Keys);
        foreach (var textKey in keys)
        {
            if (textKey == null) continue;

            if (_cooldownTimers.TryGetValue(textKey, out float timerVal) && timerVal > 0)
            {
                _cooldownTimers[textKey] -= Time.deltaTime;
                if (_cooldownTimers[textKey] <= 0 && _pendingDeltas.TryGetValue(textKey, out int deltaVal) && deltaVal != 0)
                {
                    TriggerFloatingTextAndFx(textKey, deltaVal);
                    _pendingDeltas[textKey] = 0;
                }
            }
        }
    }

    // --- CÁC HÀM CẬP NHẬT TỪ EVENT QUẢN LÝ ---

    public void UpdateGold(int value)
    {
        int delta = value - _currentGold;
        AnimateNumber(goldText, _currentGold, value);
        _currentGold = value;
        HandleResourceChange(goldText, delta);
    }

    public void UpdateWood(int value)
    {
        int delta = value - _currentWood;
        AnimateNumber(woodText, _currentWood, value);
        _currentWood = value;
        HandleResourceChange(woodText, delta);
    }

    public void UpdateStone(int value)
    {
        int delta = value - _currentStone;
        AnimateNumber(stoneText, _currentStone, value);
        _currentStone = value;
        HandleResourceChange(stoneText, delta);
    }

    public void UpdateFood(int value)
    {
        int delta = value - _currentFood;
        AnimateNumber(foodText, _currentFood, value);
        _currentFood = value;
        HandleResourceChange(foodText, delta);
    }

    // ──────────────────────────────────────────────────────────────
    // LOGIC XỬ LÝ GỘP DỮ LIỆU & ANIMATION VẬT PHẨM (DOTWEEN)
    // ──────────────────────────────────────────────────────────────

    private void HandleResourceChange(TextMeshProUGUI textTarget, int delta)
    {
        if (delta == 0 || textTarget == null) return;

        if (!_pendingDeltas.ContainsKey(textTarget)) _pendingDeltas[textTarget] = 0;
        if (!_cooldownTimers.ContainsKey(textTarget)) _cooldownTimers[textTarget] = 0f;

        _pendingDeltas[textTarget] += delta;

        if (_cooldownTimers[textTarget] <= 0f)
        {
            TriggerFloatingTextAndFx(textTarget, _pendingDeltas[textTarget]);
            _pendingDeltas[textTarget] = 0;
            _cooldownTimers[textTarget] = UI_REFRESH_COOLDOWN;
        }
    }

    private void TriggerFloatingTextAndFx(TextMeshProUGUI textTarget, int totalDelta)
    {
        if (totalDelta == 0 || textTarget == null) return;

        Color fxColor = Color.white;
        if (textTarget == goldText) fxColor = new Color(1f, 0.85f, 0f);
        else if (textTarget == woodText) fxColor = new Color(0.65f, 0.4f, 0.15f);
        else if (textTarget == stoneText) fxColor = new Color(0.75f, 0.75f, 0.8f);
        else if (textTarget == foodText) fxColor = new Color(0.3f, 0.9f, 0.3f);

        ShowFloatingTextOptimized(totalDelta, textTarget, fxColor);
        PulseOrShake(textTarget, totalDelta);

        // Nảy nhịp Object Cha chứa ô Text đang được cập nhật
        if (textTarget.transform.parent != null)
        {
            AnimateItemIcon(textTarget.transform.parent as RectTransform, totalDelta);
        }
    }

    /// <summary>
    /// Hiệu ứng DOTween nảy Icon/Group vật phẩm khi nhận hoặc trừ tài nguyên
    /// </summary>
    public void AnimateItemIcon(RectTransform iconTarget, int delta)
    {
        if (iconTarget == null) return;

        DOTween.Kill(iconTarget);
        iconTarget.localScale = Vector3.one;

        if (delta > 0)
        {
            iconTarget.DOPunchScale(new Vector3(0.4f, 0.4f, 0f), 0.35f, 6, 0.5f)
                .SetId(iconTarget);
        }
        else
        {
            iconTarget.DOShakePosition(0.25f, 4f, 10, 90f)
                .SetId(iconTarget);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // PRIVATE – ANIMATION & POOL (TỐI ƯU ĐỒ HỌA)
    // ──────────────────────────────────────────────────────────────

    public static string FormatNumber(int amount)
    {
        int absAmount = Mathf.Abs(amount);
        string sign = amount < 0 ? "-" : "";

        if (absAmount >= 1_000_000)
        {
            float val = absAmount / 1_000_000f;
            return sign + (val % 1 == 0 ? val.ToString("0") : val.ToString("0.#")) + "m";
        }
        if (absAmount >= 1_000)
        {
            float val = absAmount / 1_000f;
            return sign + (val % 1 == 0 ? val.ToString("0") : val.ToString("0.#")) + "k";
        }

        return amount.ToString();
    }

    private void AnimateNumber(TextMeshProUGUI text, int fromValue, int toValue)
    {
        if (text == null) return;

        DOTween.Kill(text);
        int temp = fromValue;

        DOTween.To(() => temp, x =>
        {
            temp = x;
            if (text != null) text.text = FormatNumber(x);
        }, toValue, 0.2f)
        .SetEase(Ease.OutQuad)
        .SetId(text);
    }

    private void SetTextInstant(TextMeshProUGUI text, int value)
    {
        if (text != null) text.text = FormatNumber(value);
    }

    private void PulseOrShake(TextMeshProUGUI text, int delta)
    {
        if (text == null || text.transform == null) return;

        DOTween.Kill(text.transform);
        text.transform.localScale = Vector3.one;

        if (delta > 0)
        {
            text.transform.DOScale(1.25f, 0.12f)
                .SetLoops(2, LoopType.Yoyo)
                .SetId(text.transform);
        }
        else
        {
            text.transform.DOShakeScale(0.15f, 0.3f)
                .SetId(text.transform);
        }
    }

    private void ShowFloatingTextOptimized(int amount, TextMeshProUGUI anchor, Color color)
    {
        // 🚫 ĐÃ TẮT HOÀN TOÀN HIỆU ỨNG CHỮ SỐ FLOATING TEXT (+/-) THEO YÊU CẦU
        return;
    }

    private void OnDestroy()
    {
        if (goldText != null) DOTween.Kill(goldText);
        if (woodText != null) DOTween.Kill(woodText);
        if (stoneText != null) DOTween.Kill(stoneText);
        if (foodText != null) DOTween.Kill(foodText);

        if (goldText != null && goldText.transform != null) DOTween.Kill(goldText.transform);
        if (woodText != null && woodText.transform != null) DOTween.Kill(woodText.transform);
        if (stoneText != null && stoneText.transform != null) DOTween.Kill(stoneText.transform);
        if (foodText != null && foodText.transform != null) DOTween.Kill(foodText.transform);

        if (goldIcon != null) DOTween.Kill(goldIcon);
        if (woodIcon != null) DOTween.Kill(woodIcon);
        if (stoneIcon != null) DOTween.Kill(stoneIcon);
        if (foodIcon != null) DOTween.Kill(foodIcon);
    }
}