using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

/*
 * HUDController.cs
 * Folder: Scripts/UI/
 * Dự án: KHẨN HOANG (PENTA DEV)
 * Popup giữ cố định 1s PHÍA DƯỚI CẠNH PHẢI CỦA ICON TÀI NGUYÊN TƯƠNG ỨNG (Gỗ hiện dưới icon Gỗ, Đá dưới icon Đá, v.v.), hết 1s popup biến mất ngay và số trên UI mới bắt đầu chạy tăng/giảm.
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

    [Header("Top UI – Icon Vật Phẩm")]
    public RectTransform goldIcon;
    public RectTransform woodIcon;
    public RectTransform stoneIcon;
    public RectTransform foodIcon;

    [Header("Floating Text FX")]
    public GameObject floatingTextPrefab;
    public Transform floatingTextParent;

    [Header("Popup Position & Timing Settings")]
    [Tooltip("Độ lệch vị trí X so với cạnh phải Icon (+ là sang phải thêm, - là lùi về trái)")]
    public float popupOffsetX = 0f;
    [Tooltip("Độ lệch vị trí Y so với Icon (VD: -35 là nằm cố định dưới Icon 35px)")]
    public float popupOffsetY = -35f;
    [Tooltip("Thời gian popup đứng yên cố định trước khi biến mất (mặc định 1 giây)")]
    public float popupStayDuration = 1.0f;
    [Tooltip("Thời gian chạy số tăng/giảm sau khi popup biến mất (giây)")]
    public float numberCountDuration = 0.35f;

    private int _currentGold;
    private int _currentWood;
    private int _currentStone;
    private int _currentFood;

    // Giá trị hiển thị đang chạy thực tế trên Text (để counter đếm chính xác từng đợt)
    private int _displayedGold;
    private int _displayedWood;
    private int _displayedStone;
    private int _displayedFood;

    // Delta phát sinh trong lúc popup hiện; sẽ xử lý sau khi counter đầu tiên kết thúc.
    private int _queuedGoldDelta;
    private int _queuedWoodDelta;
    private int _queuedStoneDelta;
    private int _queuedFoodDelta;
    private bool _goldPopupBusy;
    private bool _woodPopupBusy;
    private bool _stonePopupBusy;
    private bool _foodPopupBusy;
    private bool _suppressGoldPositivePopups;

    // Bỏ qua toàn bộ popup/counter trong lần JsonDataManager broadcast dữ liệu Save đầu tiên.
    // Sau khi đủ cả 4 loại tài nguyên được đồng bộ, các thay đổi gameplay mới dùng popup bình thường.
    private bool _isInitialResourceSyncComplete;
    private bool _receivedInitialGold;
    private bool _receivedInitialWood;
    private bool _receivedInitialStone;
    private bool _receivedInitialFood;

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

        _displayedGold = _currentGold;
        _displayedWood = _currentWood;
        _displayedStone = _currentStone;
        _displayedFood = _currentFood;

        SetTextInstant(goldText, _currentGold);
        SetTextInstant(woodText, _currentWood);
        SetTextInstant(stoneText, _currentStone);
        RefreshFoodDisplay();

        Debug.Log("[HUDController] ✅ Khởi tạo HUD thành công với thông số ban đầu.");
    }

    /// <summary>
    /// 🌾 Hiển thị Lúa mì trên HUD theo định dạng {Đang Dùng}/{Tổng Số} (VD: 0/1, 1/1, 1/2...)
    /// </summary>
    public void RefreshFoodDisplay()
    {
        if (foodText == null) return;

        if (TroopTrainingManager.Ins != null)
        {
            int used = TroopTrainingManager.Ins.GetTotalUsedFoodCount();
            int max = TroopTrainingManager.Ins.GetTotalFoodCapacity();
            foodText.text = $"{used}/{max}";
        }
        else
        {
            foodText.text = "0/1";
        }
    }

    /// <summary>
    /// Tự động quét và gán chính xác theo Object Cha (Hỗ trợ cả tên có khoảng trắng 'GoldGroup ', 'WoodGroup ', v.v.)
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

        // 🎯 LẤY CHÍNH XÁC OBJECT CHA NHÓM TÀI NGUYÊN (Tìm đệ quy trong toàn bộ topPanel nếu cần)
        if (goldGroup == null)
        {
            goldGroup = (topPanel.Find("RightGroup/GoldGroup ") ?? topPanel.Find("RightGroup/GoldGroup") ??
                         topPanel.Find("GoldGroup ") ?? topPanel.Find("GoldGroup") ??
                         topPanel.Find("RightGroup/WheatGroup ") ?? topPanel.Find("RightGroup/WheatGroup") ??
                         FindChildRecursive(topPanel, "GoldGroup ") ?? FindChildRecursive(topPanel, "GoldGroup")) as RectTransform;
        }
        if (woodGroup == null)
        {
            woodGroup = (topPanel.Find("RightGroup/WoodGroup ") ?? topPanel.Find("RightGroup/WoodGroup") ??
                         topPanel.Find("WoodGroup ") ?? topPanel.Find("WoodGroup") ??
                         topPanel.Find("RightGroup/LumberGroup ") ?? topPanel.Find("RightGroup/LumberGroup") ??
                         FindChildRecursive(topPanel, "WoodGroup ") ?? FindChildRecursive(topPanel, "WoodGroup")) as RectTransform;
        }
        if (stoneGroup == null)
        {
            stoneGroup = (topPanel.Find("RightGroup/StoneGroup ") ?? topPanel.Find("RightGroup/StoneGroup") ??
                          topPanel.Find("StoneGroup ") ?? topPanel.Find("StoneGroup") ??
                          topPanel.Find("RightGroup/RockGroup ") ?? topPanel.Find("RightGroup/RockGroup") ??
                          FindChildRecursive(topPanel, "StoneGroup ") ?? FindChildRecursive(topPanel, "StoneGroup")) as RectTransform;
        }
        if (foodGroup == null)
        {
            foodGroup = (topPanel.Find("RightGroup/FoodGroup ") ?? topPanel.Find("RightGroup/FoodGroup") ??
                         topPanel.Find("FoodGroup ") ?? topPanel.Find("FoodGroup") ??
                         topPanel.Find("RightGroup/MeatGroup ") ?? topPanel.Find("RightGroup/MeatGroup") ??
                         FindChildRecursive(topPanel, "FoodGroup ") ?? FindChildRecursive(topPanel, "FoodGroup")) as RectTransform;
        }

        // Gán Text và Icon cho từng nhóm
        if (goldGroup != null)
        {
            if (goldText == null) goldText = goldGroup.GetComponentInChildren<TextMeshProUGUI>(true);
            if (goldIcon == null) goldIcon = (goldGroup.Find("WheatIcon ") ?? goldGroup.Find("WheatIcon") ?? goldGroup.Find("GoldIcon ") ?? goldGroup.Find("GoldIcon") ?? goldGroup.GetComponentInChildren<Image>(true)?.rectTransform) as RectTransform;
        }

        if (woodGroup != null)
        {
            if (woodText == null) woodText = woodGroup.GetComponentInChildren<TextMeshProUGUI>(true);
            if (woodIcon == null) woodIcon = (woodGroup.Find("WoodIcon") ?? woodGroup.Find("WoodIcon ") ?? woodGroup.GetComponentInChildren<Image>(true)?.rectTransform) as RectTransform;
        }

        if (stoneGroup != null)
        {
            if (stoneText == null) stoneText = stoneGroup.GetComponentInChildren<TextMeshProUGUI>(true);
            if (stoneIcon == null) stoneIcon = (stoneGroup.Find("StoneIcon ") ?? stoneGroup.Find("StoneIcon") ?? stoneGroup.GetComponentInChildren<Image>(true)?.rectTransform) as RectTransform;
        }

        if (foodGroup != null)
        {
            if (foodText == null) foodText = foodGroup.GetComponentInChildren<TextMeshProUGUI>(true);
            if (foodIcon == null) foodIcon = (foodGroup.Find("Foodicon") ?? foodGroup.Find("Foodicon ") ?? foodGroup.Find("FoodIcon") ?? foodGroup.GetComponentInChildren<Image>(true)?.rectTransform) as RectTransform;
        }

        // Tự động tìm Canvas cho FloatingText
        if (floatingTextParent == null || !floatingTextParent.gameObject.scene.IsValid())
        {
            var separateCanvas = GameObject.Find("Canvas_FloatingText");
            if (separateCanvas != null) floatingTextParent = separateCanvas.transform;
            else
            {
                var sceneCanvas = Object.FindFirstObjectByType<Canvas>();
                if (sceneCanvas != null) floatingTextParent = sceneCanvas.transform;
                else floatingTextParent = topPanel;
            }
        }

        Debug.Log($"[HUDController] 🔄 Đã liên kết: GoldGroup={goldGroup?.name}(Icon:{goldIcon?.name}), WoodGroup={woodGroup?.name}(Icon:{woodIcon?.name}), StoneGroup={stoneGroup?.name}(Icon:{stoneIcon?.name}), FoodGroup={foodGroup?.name}(Icon:{foodIcon?.name})");
    }

    private Transform FindChildRecursive(Transform parent, string nameToFind)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == nameToFind || child.name.Trim() == nameToFind.Trim()) return child;
            var found = FindChildRecursive(child, nameToFind);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// Lấy RectTransform của ICON tài nguyên tương ứng làm tâm neo cho popup
    /// </summary>
    private RectTransform GetIconAnchor(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Gold:
                if (goldIcon != null) return goldIcon;
                if (goldGroup != null) return goldGroup;
                if (goldText != null) return goldText.rectTransform;
                break;
            case ResourceType.Wood:
                if (woodIcon != null) return woodIcon;
                if (woodGroup != null) return woodGroup;
                if (woodText != null) return woodText.rectTransform;
                break;
            case ResourceType.Stone:
                if (stoneIcon != null) return stoneIcon;
                if (stoneGroup != null) return stoneGroup;
                if (stoneText != null) return stoneText.rectTransform;
                break;
            case ResourceType.Food:
                if (foodIcon != null) return foodIcon;
                if (foodGroup != null) return foodGroup;
                if (foodText != null) return foodText.rectTransform;
                break;
        }

        return transform as RectTransform;
    }

    public void SuppressGoldPositivePopups(float duration = 2f)
    {
        _suppressGoldPositivePopups = true;
        CancelInvoke(nameof(ClearGoldPositivePopupSuppression));
        Invoke(nameof(ClearGoldPositivePopupSuppression), duration);
    }

    private void ClearGoldPositivePopupSuppression() => _suppressGoldPositivePopups = false;

    // --- CÁC HÀM CẬP NHẬT TỪ EVENT QUẢN LÝ ---

    public void UpdateGold(int value)
    {
        if (!TryCompleteInitialResourceSync(ResourceType.Gold, value)) return;

        int delta = value - _currentGold;
        _currentGold = value;
        if (delta == 0) return;
        if (_goldPopupBusy) { _queuedGoldDelta += delta; return; }
        if (_suppressGoldPositivePopups && delta > 0)
        {
            AnimateNumber(goldText, _displayedGold, _displayedGold + delta, () => _displayedGold += delta);
            return;
        }

        _goldPopupBusy = true;
        ShowFloatingTextOptimized(delta, GetIconAnchor(ResourceType.Gold), () =>
        {
            AnimateNumber(goldText, _displayedGold, _displayedGold + delta, () =>
            {
                _displayedGold += delta;
                ProcessQueuedGold();
            });
        });
    }

    public void UpdateWood(int value)
    {
        if (!TryCompleteInitialResourceSync(ResourceType.Wood, value)) return;

        int delta = value - _currentWood;
        _currentWood = value;
        if (delta == 0) return;
        if (_woodPopupBusy) { _queuedWoodDelta += delta; return; }

        _woodPopupBusy = true;
        ShowFloatingTextOptimized(delta, GetIconAnchor(ResourceType.Wood), () =>
        {
            AnimateNumber(woodText, _displayedWood, _displayedWood + delta, () =>
            {
                _displayedWood += delta;
                ProcessQueuedWood();
            });
        });
    }

    public void UpdateStone(int value)
    {
        if (!TryCompleteInitialResourceSync(ResourceType.Stone, value)) return;

        int delta = value - _currentStone;
        _currentStone = value;
        if (delta == 0) return;
        if (_stonePopupBusy) { _queuedStoneDelta += delta; return; }

        _stonePopupBusy = true;
        ShowFloatingTextOptimized(delta, GetIconAnchor(ResourceType.Stone), () =>
        {
            AnimateNumber(stoneText, _displayedStone, _displayedStone + delta, () =>
            {
                _displayedStone += delta;
                ProcessQueuedStone();
            });
        });
    }

    public void UpdateFood(int value)
    {
        if (!TryCompleteInitialResourceSync(ResourceType.Food, value)) return;

        _currentFood = value;
        RefreshFoodDisplay();
    }

    // JsonDataManager.LoadGame -> BroadcastAllResources gọi lần lượt 4 Update này.
    // Trong giai đoạn đó chỉ đồng bộ Text ngay lập tức; không dùng popup/counter.
    private bool TryCompleteInitialResourceSync(ResourceType type, int value)
    {
        if (_isInitialResourceSyncComplete) return true;

        switch (type)
        {
            case ResourceType.Gold:
                _currentGold = _displayedGold = value;
                SetTextInstant(goldText, value);
                _receivedInitialGold = true;
                break;
            case ResourceType.Wood:
                _currentWood = _displayedWood = value;
                SetTextInstant(woodText, value);
                _receivedInitialWood = true;
                break;
            case ResourceType.Stone:
                _currentStone = _displayedStone = value;
                SetTextInstant(stoneText, value);
                _receivedInitialStone = true;
                break;
            case ResourceType.Food:
                _currentFood = _displayedFood = value;
                RefreshFoodDisplay();
                _receivedInitialFood = true;
                break;
        }

        if (_receivedInitialGold && _receivedInitialWood && _receivedInitialStone && _receivedInitialFood)
        {
            _isInitialResourceSyncComplete = true;
            Debug.Log("[HUDController] ✅ Đồng bộ tài nguyên Save ban đầu hoàn tất, bật popup/counter gameplay.");
        }

        return false;
    }
    private void ProcessQueuedGold()
    {
        int delta = _queuedGoldDelta;
        _queuedGoldDelta = 0;
        if (delta == 0) { _goldPopupBusy = false; return; }
        AnimateNumber(goldText, _displayedGold, _displayedGold + delta, () =>
        {
            _displayedGold += delta;
            ProcessQueuedGold();
        });
    }

    private void ProcessQueuedWood()
    {
        int delta = _queuedWoodDelta;
        _queuedWoodDelta = 0;
        if (delta == 0) { _woodPopupBusy = false; return; }
        AnimateNumber(woodText, _displayedWood, _displayedWood + delta, () =>
        {
            _displayedWood += delta;
            ProcessQueuedWood();
        });
    }

    private void ProcessQueuedStone()
    {
        int delta = _queuedStoneDelta;
        _queuedStoneDelta = 0;
        if (delta == 0) { _stonePopupBusy = false; return; }
        AnimateNumber(stoneText, _displayedStone, _displayedStone + delta, () =>
        {
            _displayedStone += delta;
            ProcessQueuedStone();
        });
    }

    private void ProcessQueuedFood()
    {
        _foodPopupBusy = false;
        _queuedFoodDelta = 0;
        RefreshFoodDisplay();
    }
    // ──────────────────────────────────────────────────────────────
    // PRIVATE – ANIMATION & FORMAT SỐ
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

    /// <summary>
    /// Chạy số counter tăng/giảm mượt mà từ giá trị cũ đến giá trị mới (KHÔNG phóng to thu nhỏ)
    /// </summary>
    private void AnimateNumber(TextMeshProUGUI text, int fromValue, int toValue, System.Action onComplete = null)
    {
        if (text == null)
        {
            onComplete?.Invoke();
            return;
        }

        DOTween.Kill(text);
        int temp = fromValue;

        DOTween.To(() => temp, x =>
        {
            temp = x;
            if (text != null) text.text = FormatNumber(x);
        }, toValue, numberCountDuration)
        .SetEase(Ease.OutQuad)
        .SetId(text)
        .OnComplete(() => onComplete?.Invoke());
    }

    private void SetTextInstant(TextMeshProUGUI text, int value)
    {
        if (text != null) text.text = FormatNumber(value);
    }

    /// <summary>
    /// Sinh popup giữ CỐ ĐỊNH, tính toạ độ neo theo CẠNH PHẢI CỦA ĐÚNG ICON TÀI NGUYÊN TƯƠNG ỨNG
    /// </summary>
    private void ShowFloatingTextOptimized(int amount, RectTransform anchorRect, System.Action onComplete)
    {
        if (floatingTextPrefab == null || anchorRect == null)
        {
            onComplete?.Invoke();
            return;
        }

        Transform targetParent = floatingTextParent;
        if (targetParent == null || !targetParent.gameObject.scene.IsValid())
        {
            var separateCanvas = GameObject.Find("Canvas_FloatingText");
            if (separateCanvas != null) targetParent = separateCanvas.transform;
            else
            {
                Canvas rootCanvas = anchorRect.GetComponentInParent<Canvas>();
                if (rootCanvas != null) targetParent = rootCanvas.transform;
                else targetParent = transform;
            }
        }

        // Spawn trực tiếp vào Canvas cha
        GameObject fxObj = Instantiate(floatingTextPrefab, targetParent, false);

        RectTransform fxRect = fxObj.GetComponent<RectTransform>();
        RectTransform parentRect = targetParent as RectTransform;

        if (fxRect != null)
        {
            // Đảm bảo Pivot của popup luôn là chính giữa (0.5, 0.5)
            fxRect.pivot = new Vector2(0.5f, 0.5f);

            if (parentRect != null)
            {
                // 🎯 Lấy vị trí CẠNH PHẢI của chính Icon tài nguyên đó (xMax)
                Vector3 iconRightEdgeCenter = anchorRect.TransformPoint(new Vector2(anchorRect.rect.xMax, anchorRect.rect.center.y));

                Canvas parentCanvas = targetParent.GetComponentInParent<Canvas>();
                Camera cam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? parentCanvas.worldCamera : null;

                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, iconRightEdgeCenter);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, cam, out Vector2 localPoint))
                {
                    fxRect.anchoredPosition = new Vector2(localPoint.x + popupOffsetX, localPoint.y + popupOffsetY);
                }
                else
                {
                    fxRect.position = iconRightEdgeCenter;
                    fxRect.anchoredPosition = new Vector2(fxRect.anchoredPosition.x + popupOffsetX, fxRect.anchoredPosition.y + popupOffsetY);
                }
            }
            else
            {
                fxRect.position = anchorRect.position;
                fxRect.anchoredPosition = new Vector2(fxRect.anchoredPosition.x + popupOffsetX, fxRect.anchoredPosition.y + popupOffsetY);
            }

            fxRect.localScale = Vector3.one;
        }

        FloatingText ft = fxObj.GetComponent<FloatingText>();
        if (ft != null)
        {
            string sign = amount > 0 ? "+" : "";
            ft.Setup($"{sign}{FormatNumber(amount)}", duration: popupStayDuration, onComplete: onComplete);
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    private void OnDestroy()
    {
        if (goldText != null) DOTween.Kill(goldText);
        if (woodText != null) DOTween.Kill(woodText);
        if (stoneText != null) DOTween.Kill(stoneText);
        if (foodText != null) DOTween.Kill(foodText);

        if (goldIcon != null) DOTween.Kill(goldIcon);
        if (woodIcon != null) DOTween.Kill(woodIcon);
        if (stoneIcon != null) DOTween.Kill(stoneIcon);
        if (foodIcon != null) DOTween.Kill(foodIcon);
    }
}




