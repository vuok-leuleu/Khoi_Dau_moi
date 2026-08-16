using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

public class ResourceGainPopup : MonoBehaviour
{
    [Header("UI Containers")]
    [SerializeField] private RectTransform containerRect;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Transform itemsParent;
    [SerializeField] private ResourceGainPopupItem itemPrefab;

    [Header("Animation Settings")]
    [SerializeField] private float appearDuration = 0.35f;
    [SerializeField] private float stayDuration = 0.8f;
    [SerializeField] private float floatUpDistance = 60f;
    [SerializeField] private float fadeOutDuration = 0.4f;

    private List<GameObject> _spawnedItems = new List<GameObject>();
    private Sequence _animSequence;

    private void Awake()
    {
        if (containerRect == null) containerRect = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (itemsParent == null) itemsParent = containerRect != null ? containerRect : transform;
    }

    public void PlayPopup(List<(ResourceType type, int amount, Sprite icon)> resourceList, Vector2 startAnchoredPos)
    {
        if (itemPrefab == null)
        {
            Debug.LogWarning("[ResourceGainPopup] itemPrefab chưa được gán!");
            return;
        }

        // 1. Dọn dẹp item cũ nếu có
        foreach (var item in _spawnedItems)
        {
            if (item != null) Destroy(item);
        }
        _spawnedItems.Clear();

        // 2. Tạo các ô tài nguyên
        foreach (var res in resourceList)
        {
            if (res.amount == 0) continue;
            var itemObj = Instantiate(itemPrefab, itemsParent);
            itemObj.Setup(res.icon, res.amount);
            _spawnedItems.Add(itemObj.gameObject);
        }

        // 3. Setup vị trí ban đầu
        containerRect.anchoredPosition = startAnchoredPos;
        containerRect.localScale = Vector3.zero;
        canvasGroup.alpha = 1f;

        // 4. Sequence Animation
        _animSequence?.Kill();
        _animSequence = DOTween.Sequence().SetTarget(gameObject);

        _animSequence.Append(containerRect.DOScale(Vector3.one, appearDuration).SetEase(Ease.OutBack));
        _animSequence.AppendInterval(stayDuration);
        _animSequence.Append(containerRect.DOAnchorPosY(startAnchoredPos.y + floatUpDistance, fadeOutDuration).SetEase(Ease.OutQuad));
        _animSequence.Join(canvasGroup.DOFade(0f, fadeOutDuration));

        _animSequence.OnComplete(() =>
        {
            if (gameObject != null) Destroy(gameObject);
        });
    }

    private void OnDestroy()
    {
        _animSequence?.Kill();
        if (containerRect != null) DOTween.Kill(containerRect);
        if (canvasGroup != null) DOTween.Kill(canvasGroup);
    }
}