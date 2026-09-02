using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Controls a manually authored World Space Canvas for an enemy route.
/// </summary>
public class EnemyRouteWarningUI : MonoBehaviour
{
    [Header("Assign in the prefab")]
    [Tooltip("Thân mũi tên. Chỉ phần này bị kéo giãn theo quãng đường.")]
    [SerializeField] private RectTransform arrowShaft;
    [Tooltip("Đầu mũi tên. Giữ nguyên tỉ lệ ở cuối thân mũi tên.")]
    [SerializeField] private RectTransform arrowHead;
    [SerializeField] private RectTransform waveLabel;
    [SerializeField] private TextMeshProUGUI waveText;
    [Tooltip("Text trong prefab hiển thị từng loại Enemy và số lượng của wave.")]
    [SerializeField] private TextMeshProUGUI compositionText;
    [SerializeField] private float arrowWidth = 1f;
    [SerializeField] private float arrowHeightAtOneMeter = 1f;
    [SerializeField] private float labelHeight = 2.2f;
    [Header("Wave Label Size")]
    [SerializeField, Min(80f)] private float waveLabelWidth = 360f;
    [SerializeField, Min(40f)] private float waveLabelHeight = 128f;
    [SerializeField, Min(10f)] private float waveFontSize = 52f;
    [SerializeField] private LayerMask groundMask = ~0;
    [Header("Fallback Visual")]
    [SerializeField] private Color arrowColor = new Color(0.56f, 0.14f, 0.20f, 1f);
    [SerializeField] private Color arrowOutlineColor = new Color(0.93f, 0.76f, 0.35f, 1f);
    [SerializeField, Min(0.05f)] private float worldArrowWidth = 0.55f;
    [SerializeField] private Sprite worldArrowShaftSprite;
    [SerializeField] private Sprite worldArrowHeadSprite;

    private Transform startPoint;
    private Transform targetPoint;
    private Transform waveSource;
    private float groundOffset;
    private readonly List<GameObject> enemyComposition = new List<GameObject>();
    private MeshFilter generatedArrowHead;
    private static Sprite solidSprite;

    private Canvas routeCanvas;
    private SpriteRenderer worldArrowShaft;
    private SpriteRenderer worldArrowHead;

    public void Setup(Transform start, Transform target, Transform waveEnemy, float heightOffset)
    {
        Setup(start, target, waveEnemy, heightOffset, null);
    }

    /// <summary>
    /// Hiển thị đường đi cùng đúng thành phần Enemy đã được EnemySpawn sinh ra.
    /// Danh sách này cũng là danh sách được chuyển vào trận đấu.
    /// </summary>
    public void Setup(Transform start, Transform target, Transform waveEnemy, float heightOffset,
        IEnumerable<GameObject> spawnedEnemyPrefabs)
    {
        startPoint = start;
        targetPoint = target;
        waveSource = waveEnemy;
        groundOffset = heightOffset;
        SetEnemyComposition(spawnedEnemyPrefabs);
        gameObject.SetActive(true);
        EnsureWorldRouteVisual();
        UpdateRoute();
    }

    private void SetEnemyComposition(IEnumerable<GameObject> spawnedEnemyPrefabs)
    {
        enemyComposition.Clear();
        if (spawnedEnemyPrefabs == null) return;

        foreach (GameObject prefab in spawnedEnemyPrefabs)
        {
            if (prefab != null) enemyComposition.Add(prefab);
        }
    }

    private void LateUpdate()
    {
        if (startPoint == null || targetPoint == null || waveSource == null || !targetPoint.gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
            return;
        }

        UpdateRoute();
    }

    private void Awake()
    {
        routeCanvas = GetComponent<Canvas>();
        AssignWorldCamera();
        DisableLegacyCanvasArrow();
    }

    private void Start()
    {
        AssignWorldCamera();
    }

    private void DisableLegacyCanvasArrow()
    {
        // The old Canvas Images stretch in canvas-local Y and can stand upright in World Space.
        // The legacy Canvas arrow is disabled; the route uses runtime sprites.
        if (arrowShaft != null) arrowShaft.gameObject.SetActive(false);
        if (arrowHead != null) arrowHead.gameObject.SetActive(false);
        if (waveLabel != null)
        {
            // Remove leftover editor-only arrow children that can cover the label.
            Transform oldHead = waveLabel.parent != null ? waveLabel.parent.Find("ArrowHead") : null;
            if (oldHead != null) oldHead.gameObject.SetActive(false);
        }
    }

    private void AssignWorldCamera()
    {
        if (routeCanvas == null) routeCanvas = GetComponent<Canvas>();
        if (routeCanvas != null && routeCanvas.renderMode == RenderMode.WorldSpace && routeCanvas.worldCamera == null)
        {
            routeCanvas.worldCamera = Camera.main != null
                ? Camera.main
                : Object.FindFirstObjectByType<Camera>();
        }
    }

    private static Sprite GetSolidSprite()
    {
        if (solidSprite != null) return solidSprite;
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        return solidSprite;
    }
    private void EnsureWorldRouteVisual()
    {
        if (worldArrowShaftSprite == null || worldArrowHeadSprite == null) return;

        if (worldArrowShaft == null)
        {
            worldArrowShaft = CreateWorldArrowSprite("RuntimeRouteArrowShaft", worldArrowShaftSprite, arrowOutlineColor);
        }

        if (worldArrowHead == null)
        {
            worldArrowHead = CreateWorldArrowSprite("RuntimeRouteArrowHead", worldArrowHeadSprite, arrowOutlineColor);
        }
    }

    private SpriteRenderer CreateWorldArrowSprite(string objectName, Sprite sprite, Color color)
    {
        GameObject arrowPart = new GameObject(objectName);
        SpriteRenderer renderer = arrowPart.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 10;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private void UpdateWorldRouteVisual(Vector3 start, Vector3 end, Vector3 direction)
    {
        EnsureWorldRouteVisual();
        if (worldArrowShaft == null || worldArrowHead == null) return;

        Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z).normalized;
        if (flatDirection.sqrMagnitude < 0.001f) return;

        Vector3 raisedStart = start + Vector3.up * 0.08f;
        Vector3 raisedEnd = end + Vector3.up * 0.08f;
        float routeLength = Vector3.Distance(raisedStart, raisedEnd);

        const float shaftLengthRatio = 0.8f;
        const float headLengthRatio = 0.2f;
        float headWidth = GetMatchedHeadWidth(worldArrowShaftSprite, worldArrowHeadSprite, worldArrowWidth);
        float shaftLength = routeLength * shaftLengthRatio;
        float headLength = routeLength * headLengthRatio;
        Quaternion groundRotation = Quaternion.LookRotation(Vector3.up, -flatDirection);

        worldArrowShaft.transform.SetPositionAndRotation(raisedStart + flatDirection * (shaftLength * 0.5f), groundRotation);
        worldArrowShaft.transform.localScale = GetSpriteScale(worldArrowShaftSprite, worldArrowWidth, shaftLength);
        worldArrowHead.transform.SetPositionAndRotation(raisedStart + flatDirection * (shaftLength + headLength * 0.5f), groundRotation);
        worldArrowHead.transform.localScale = GetSpriteScale(worldArrowHeadSprite, headWidth, headLength);
    }

    private static float GetMatchedHeadWidth(Sprite shaftSprite, Sprite headSprite, float shaftWidth)
    {
        if (shaftSprite == null || headSprite == null) return shaftWidth;

        return shaftWidth * headSprite.bounds.size.x / Mathf.Max(0.001f, shaftSprite.bounds.size.x);
    }

    private static Vector3 GetSpriteScale(Sprite sprite, float width, float length)
    {
        if (sprite == null) return Vector3.one;
        Vector2 spriteSize = sprite.bounds.size;
        return new Vector3(width / Mathf.Max(0.001f, spriteSize.x), length / Mathf.Max(0.001f, spriteSize.y), 1f);
    }


    private void UpdateRoute()
    {
        Vector3 start = SampleGround(startPoint.position);
        Vector3 end = SampleGround(GetTargetPosition(targetPoint));
        // A route marker is intentionally flat: terrain variation must not create a vertical arrow.
        end.y = start.y;
        Vector3 flatDirection = end - start;
        flatDirection.y = 0f;
        float distance = flatDirection.magnitude;
        if (distance < 0.1f) return;

        transform.position = start;
        transform.rotation = Quaternion.identity;
        UpdateWorldRouteVisual(start, end, flatDirection.normalized);

        if (waveLabel != null)
        {
            // Label height is authored in canvas pixels; convert it through the World Space Canvas scale.
            float canvasScale = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));
            waveLabel.gameObject.SetActive(true);
            waveLabel.sizeDelta = new Vector2(waveLabelWidth, GetRequiredLabelHeight());
            StretchWaveLabelChildren();
            waveLabel.position = start + Vector3.up * (labelHeight * canvasScale);
            Camera cameraToFace = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (cameraToFace != null)
            {
                waveLabel.rotation = cameraToFace.transform.rotation;
            }
        }

        if (waveText != null)
        {
            waveText.gameObject.SetActive(true);
            waveText.enabled = true;
            waveText.raycastTarget = false;
            waveText.fontSize = enemyComposition.Count > 0 ? waveFontSize * 0.7f : waveFontSize;
            ConfigureWaveTextLayout();
            waveText.alignment = TextAlignmentOptions.Center;
            waveText.transform.SetAsLastSibling();
            int currentWave = DayNightManager.HasInstance && DayNightManager.Ins != null
                ? DayNightManager.Ins.CurrentWave
                : 1;
            EnemyAI enemy = waveSource != null ? waveSource.GetComponent<EnemyAI>() : null;
            int remaining = enemy != null
                ? Mathf.Max(0, enemy.targetWave - currentWave)
                : SoldierPoint.CalculateWaveCount(distance);
            waveText.text = remaining > 0 ? $"Còn {remaining} Wave" : "Đã đến thành!";
        }

        UpdateCompositionText();
    }

    private float GetRequiredLabelHeight()
    {
        if (enemyComposition.Count == 0) return waveLabelHeight;

        HashSet<GameObject> enemyTypes = new HashSet<GameObject>(enemyComposition);
        return Mathf.Max(waveLabelHeight, 82f + enemyTypes.Count * 32f);
    }

    private void ConfigureWaveTextLayout()
    {
        if (waveText == null) return;

        RectTransform textRect = waveText.rectTransform;
        if (enemyComposition.Count == 0)
        {
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 5f);
            textRect.offsetMax = new Vector2(-10f, -5f);
            return;
        }

        textRect.anchorMin = new Vector2(0f, 0.68f);
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 0f);
        textRect.offsetMax = new Vector2(-10f, -4f);
    }

    private void UpdateCompositionText()
    {
        if (compositionText == null) return;

        if (enemyComposition.Count == 0)
        {
            compositionText.gameObject.SetActive(false);
            return;
        }

        List<GameObject> typesInOrder = new List<GameObject>();
        Dictionary<GameObject, int> counts = new Dictionary<GameObject, int>();
        foreach (GameObject prefab in enemyComposition)
        {
            if (!counts.ContainsKey(prefab))
            {
                counts.Add(prefab, 0);
                typesInOrder.Add(prefab);
            }
            counts[prefab]++;
        }

        List<string> lines = new List<string>();
        foreach (GameObject prefab in typesInOrder)
        {
            string enemyName = prefab.name.Replace("(Clone)", string.Empty).Trim();
            lines.Add($"{enemyName} x{counts[prefab]}");
        }

        compositionText.gameObject.SetActive(true);
        compositionText.enabled = true;
        compositionText.raycastTarget = false;
        compositionText.fontSize = Mathf.Max(20f, waveFontSize * 0.46f);
        compositionText.alignment = TextAlignmentOptions.Center;
        compositionText.text = string.Join("\n", lines);

        RectTransform compositionRect = compositionText.rectTransform;
        compositionRect.anchorMin = Vector2.zero;
        compositionRect.anchorMax = new Vector2(1f, 0.7f);
        compositionRect.offsetMin = new Vector2(10f, 6f);
        compositionRect.offsetMax = new Vector2(-10f, 0f);
        compositionText.transform.SetAsLastSibling();
    }

    private Vector3 GetTargetPosition(Transform target)
    {
        Collider collider = target.GetComponentInChildren<Collider>();
        if (collider == null) return target.position;

        Vector3 closest = collider.ClosestPoint(startPoint != null ? startPoint.position : target.position);
        return closest == collider.bounds.center ? collider.bounds.center : closest;
    }

    private void StretchWaveLabelChildren()
    {
        if (waveLabel == null) return;

        // Background was created as a fixed 100x100 Image in the prefab. Stretch it with the label.
        Transform background = waveLabel.Find("BackGround");
        if (background == null) background = waveLabel.Find("Background");
        if (background != null && background is RectTransform backgroundRect)
        {
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            Image backgroundImage = background.GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.sprite = GetSolidSprite();
                backgroundImage.color = new Color(0.38f, 0.04f, 0.08f, 0.96f);
                backgroundImage.raycastTarget = false;
            }
        }

        if (waveText != null && enemyComposition.Count == 0)
        {
            RectTransform textRect = waveText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 5f);
            textRect.offsetMax = new Vector2(-10f, -5f);
        }
    }

    private Vector3 SampleGround(Vector3 position)
    {
        Ray ray = new Ray(position + Vector3.up * 100f, Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 250f, groundMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * groundOffset;
        }

        return position + Vector3.up * groundOffset;
    }
}
