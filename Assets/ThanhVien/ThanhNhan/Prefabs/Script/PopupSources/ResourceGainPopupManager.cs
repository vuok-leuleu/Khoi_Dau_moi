using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ResourceGainPopupManager : MonoBehaviour
{
    public static ResourceGainPopupManager Instance { get; private set; }

    [Header("Prefabs & Canvas")]
    [SerializeField] private ResourceGainPopup popupPrefab;
    [SerializeField] private Transform popupParentCanvas;

    [Header("Resource Icons")]
    public Sprite goldIcon;
    public Sprite woodIcon;
    public Sprite stoneIcon;
    public Sprite foodIcon;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Tự động tìm Canvas nếu chưa kéo
        if (popupParentCanvas == null)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null) popupParentCanvas = canvas.transform;
        }
    }

    /// <summary>
    /// Popup nhận nhiều loại tài nguyên cùng lúc (như nhận thưởng quest / wave)
    /// </summary>
    public void ShowGainMultiple(List<(ResourceType type, int amount)> gains, Vector2? screenPosition = null)
    {
        if (popupPrefab == null)
        {
            Debug.LogWarning("[ResourceGainPopupManager] Chưa gán popupPrefab trong Inspector!");
            return;
        }

        if (popupParentCanvas == null)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null) popupParentCanvas = canvas.transform;
            if (popupParentCanvas == null) return;
        }

        if (gains == null || gains.Count == 0) return;

        List<(ResourceType type, int amount, Sprite icon)> listWithIcons = new List<(ResourceType, int, Sprite)>();

        foreach (var gain in gains)
        {
            if (gain.amount == 0) continue;
            Sprite icon = GetIconByType(gain.type);
            listWithIcons.Add((gain.type, gain.amount, icon));
        }

        if (listWithIcons.Count == 0) return;

        Vector2 spawnPos = screenPosition ?? new Vector2(0, 50f);
        ResourceGainPopup popup = Instantiate(popupPrefab, popupParentCanvas);
        popup.PlayPopup(listWithIcons, spawnPos);
    }

    /// <summary>
    /// Popup nhanh cho 1 loại tài nguyên duy nhất
    /// </summary>
    public void ShowGainSingle(ResourceType type, int amount, Vector2? screenPosition = null)
    {
        ShowGainMultiple(new List<(ResourceType, int)> { (type, amount) }, screenPosition);
    }

    public Sprite GetIconByType(ResourceType type)
    {
        Sprite result = null;
        switch (type)
        {
            case ResourceType.Gold: result = goldIcon; break;
            case ResourceType.Wood: result = woodIcon; break;
            case ResourceType.Stone: result = stoneIcon; break;
            case ResourceType.Food: result = foodIcon; break;
        }

        // Fallback tự động lấy từ HUDController nếu chưa kéo trong Inspector
        if (result == null && HUDController.Instance != null)
        {
            switch (type)
            {
                case ResourceType.Gold:
                    result = HUDController.Instance.goldIcon?.GetComponent<Image>()?.sprite;
                    break;
                case ResourceType.Wood:
                    result = HUDController.Instance.woodIcon?.GetComponent<Image>()?.sprite;
                    break;
                case ResourceType.Stone:
                    result = HUDController.Instance.stoneIcon?.GetComponent<Image>()?.sprite;
                    break;
                case ResourceType.Food:
                    result = HUDController.Instance.foodIcon?.GetComponent<Image>()?.sprite;
                    break;
            }
        }

        return result;
    }
}