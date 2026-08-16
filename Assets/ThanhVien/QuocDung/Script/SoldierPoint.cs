using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls World Space Canvas and Runtime Sprite Route Arrow for Soldiers.
/// Hoàn toàn đồng bộ và dùng đúng Sprite Asset của EnemyRouteWarningUI nhưng có màu sắc quân ta.
/// </summary>
public class SoldierPoint : MonoBehaviour
{
    [Header("Assign in the prefab")]
    [Tooltip("Thân mũi tên (Canvas cũ, tự ẩn).")]
    [SerializeField] private RectTransform arrowShaft;
    [Tooltip("Đầu mũi tên (Canvas cũ, tự ẩn).")]
    [SerializeField] private RectTransform arrowHead;
    [SerializeField] private RectTransform waveLabel;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private float arrowWidth = 1f;
    [SerializeField] private float arrowHeightAtOneMeter = 1f;
    [SerializeField] private float labelHeight = 2.2f;

    [Header("Wave Label Size")]
    [SerializeField, Min(80f)] private float waveLabelWidth = 360f;
    [SerializeField, Min(40f)] private float waveLabelHeight = 128f;
    [SerializeField, Min(10f)] private float waveFontSize = 52f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Visual Colors & Sprites (Soldier Theme)")]
    [SerializeField] private Color arrowColor = Color.white;
    [SerializeField] private Color arrowOutlineColor = Color.white;
    [SerializeField, Min(0.05f)] private float worldArrowWidth = 1.3f;
    [SerializeField] private Sprite worldArrowShaftSprite;
    [SerializeField] private Sprite worldArrowHeadSprite;

    private Transform startPoint;
    private Transform targetPoint;
    private Transform soldierSource;
    private UnitController unitController;
    private float groundOffset = 0.08f;
    private static Sprite solidSprite;

    private Canvas routeCanvas;
    private SpriteRenderer worldArrowShaft;
    private SpriteRenderer worldArrowHead;

    public void Setup(Transform start, Transform target, Transform soldier, float heightOffset = 0.08f)
    {
        startPoint = start;
        targetPoint = target;
        soldierSource = soldier;
        unitController = soldier != null ? soldier.GetComponent<UnitController>() : null;
        if (unitController == null && soldier != null)
        {
            unitController = soldier.GetComponentInParent<UnitController>();
        }
        groundOffset = heightOffset;
        gameObject.SetActive(true);
        EnsureWorldRouteVisual();
        UpdateRoute();
    }

    public void Setup(Transform start, Transform target, UnitController unit, float heightOffset = 0.08f)
    {
        startPoint = start;
        targetPoint = target;
        unitController = unit;
        soldierSource = unit != null ? unit.transform : null;
        groundOffset = heightOffset;
        gameObject.SetActive(true);
        EnsureWorldRouteVisual();
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

    private void OnDisable()
    {
        if (worldArrowShaft != null) worldArrowShaft.gameObject.SetActive(false);
        if (worldArrowHead != null) worldArrowHead.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (worldArrowShaft != null) Destroy(worldArrowShaft.gameObject);
        if (worldArrowHead != null) Destroy(worldArrowHead.gameObject);
    }

    private void LateUpdate()
    {
        if (startPoint == null || targetPoint == null || !targetPoint.gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
            return;
        }

        if (soldierSource != null && !soldierSource.gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
            return;
        }

        UpdateRoute();
    }

    private void DisableLegacyCanvasArrow()
    {
        if (arrowShaft != null) arrowShaft.gameObject.SetActive(false);
        if (arrowHead != null) arrowHead.gameObject.SetActive(false);
        if (waveLabel != null)
        {
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
            worldArrowShaft = CreateWorldArrowSprite("RuntimeSoldierRouteShaft", worldArrowShaftSprite, arrowOutlineColor);
        }

        if (worldArrowHead == null)
        {
            worldArrowHead = CreateWorldArrowSprite("RuntimeSoldierRouteHead", worldArrowHeadSprite, arrowOutlineColor);
        }

        if (worldArrowShaft != null) worldArrowShaft.gameObject.SetActive(true);
        if (worldArrowHead != null) worldArrowHead.gameObject.SetActive(true);
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
        float shaftLength = routeLength * 0.7f;
        float headLength = routeLength * 0.3f;
        float headWidth = worldArrowWidth * 1.8f;
        Quaternion groundRotation = Quaternion.LookRotation(Vector3.up, -flatDirection);

        worldArrowShaft.transform.SetPositionAndRotation(raisedStart + flatDirection * (shaftLength * 0.5f), groundRotation);
        worldArrowShaft.transform.localScale = GetSpriteScale(worldArrowShaftSprite, worldArrowWidth, shaftLength);
        worldArrowHead.transform.SetPositionAndRotation(raisedStart + flatDirection * (shaftLength + headLength * 0.5f), groundRotation);
        worldArrowHead.transform.localScale = GetSpriteScale(worldArrowHeadSprite, headWidth, headLength);
    }

    private static Vector3 GetSpriteScale(Sprite sprite, float width, float length)
    {
        Vector2 spriteSize = sprite.bounds.size;
        return new Vector3(width / spriteSize.x, length / spriteSize.y, 1f);
    }

    private void UpdateRoute()
    {
        if (startPoint == null || targetPoint == null) return;

        Vector3 start = SampleGround(startPoint.position);
        Vector3 end = SampleGround(GetTargetPosition(targetPoint));
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
            float canvasScale = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));
            waveLabel.gameObject.SetActive(true);
            waveLabel.sizeDelta = new Vector2(waveLabelWidth, waveLabelHeight);
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
            waveText.fontSize = waveFontSize;
            waveText.rectTransform.sizeDelta = new Vector2(waveLabelWidth - 20f, waveLabelHeight - 10f);
            waveText.alignment = TextAlignmentOptions.Center;
            waveText.transform.SetAsLastSibling();

            int currentWave = DayNightManager.HasInstance && DayNightManager.Ins != null
                ? DayNightManager.Ins.CurrentWave
                : 1;

            int remaining = 0;
            if (unitController != null && unitController.isExpeditionMarching)
            {
                remaining = Mathf.Max(0, unitController.marchTargetWave - currentWave);
            }
            else
            {
                remaining = Mathf.Max(1, Mathf.CeilToInt(distance / 15f));
            }

            waveText.text = remaining > 0 ? $"Còn {remaining} Wave" : "Đã đến mục tiêu!";
        }
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
                backgroundImage.color = new Color(0.08f, 0.22f, 0.45f, 0.96f);
                backgroundImage.raycastTarget = false;
            }
        }

        if (waveText != null)
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
