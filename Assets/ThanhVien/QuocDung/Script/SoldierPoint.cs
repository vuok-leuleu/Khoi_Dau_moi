using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the World Space Canvas route arrow for soldiers.
/// Uses the ArrowShaft and ArrowHead UI elements supplied by SoldierPointUI.
/// </summary>
public class SoldierPoint : MonoBehaviour
{
    [Header("Assign in the prefab")]
    [SerializeField] private RectTransform arrowShaft;
    [SerializeField] private RectTransform arrowHead;
    [SerializeField] private RectTransform waveLabel;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private float labelHeight = 2.2f;

    [Header("Wave Label Size")]
    [SerializeField, Min(80f)] private float waveLabelWidth = 360f;
    [SerializeField, Min(40f)] private float waveLabelHeight = 128f;
    [SerializeField, Min(10f)] private float waveFontSize = 52f;
    [SerializeField] private LayerMask groundMask = ~0;

    private Transform startPoint;
    private Transform targetPoint;
    private Transform soldierSource;
    private UnitController unitController;
    private float groundOffset = 0.08f;
    private static Sprite solidSprite;
    private Canvas routeCanvas;
    private Image arrowShaftImage;
    private Image arrowHeadImage;
    private Vector2 arrowHeadDefaultSize;

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
        UpdateRoute();
    }

    private void Awake()
    {
        routeCanvas = GetComponent<Canvas>();
        arrowShaftImage = arrowShaft != null ? arrowShaft.GetComponent<Image>() : null;
        arrowHeadImage = arrowHead != null ? arrowHead.GetComponent<Image>() : null;
        if (arrowHead != null)
        {
            arrowHeadDefaultSize = arrowHead.sizeDelta;
        }
        AssignWorldCamera();
    }

    private void Start()
    {
        AssignWorldCamera();
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

    private void UpdatePrefabRouteVisual(Vector3 start, Vector3 end, Vector3 direction)
    {
        if (arrowShaft == null || arrowHead == null) return;

        Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z).normalized;
        if (flatDirection.sqrMagnitude < 0.001f) return;

        float routeLength = Vector3.Distance(start, end);
        const float shaftLengthRatio = 0.6f;
        const float headLengthRatio = 0.4f;
        float canvasScale = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.x));
        float shaftLength = routeLength * shaftLengthRatio;
        float headLength = routeLength * headLengthRatio;
        Quaternion shaftRotation = Quaternion.LookRotation(Vector3.up, -flatDirection);
        Quaternion headRotation = Quaternion.LookRotation(Vector3.up, -flatDirection);

        arrowShaft.gameObject.SetActive(shaftLength > 0.01f);
        arrowShaft.position = end - flatDirection * headLength;
        arrowShaft.rotation = shaftRotation;
        arrowShaft.sizeDelta = new Vector2(arrowShaft.sizeDelta.x, shaftLength / canvasScale);

        arrowHead.gameObject.SetActive(true);
        arrowHead.position = end;
        arrowHead.rotation = headRotation;
        float headWidth = arrowShaft.sizeDelta.x;
        if (arrowShaftImage != null && arrowHeadImage != null &&
            arrowShaftImage.sprite != null && arrowHeadImage.sprite != null)
        {
            headWidth *= arrowHeadImage.sprite.rect.width / arrowShaftImage.sprite.rect.width;
        }

        arrowHead.sizeDelta = new Vector2(headWidth, headLength / canvasScale);
    }

    private void UpdateRoute()
    {
        if (startPoint == null || targetPoint == null) return;

        Vector3 centerDirection = targetPoint.position - startPoint.position;
        centerDirection.y = 0f;
        if (centerDirection.sqrMagnitude < 0.01f) return;

        Vector3 start = SampleGround(startPoint.position);
        Vector3 end = SampleGround(targetPoint.position);
        float routeElevation = Mathf.Max(start.y, end.y);
        start.y = routeElevation;
        end.y = routeElevation;
        Vector3 flatDirection = end - start;
        flatDirection.y = 0f;
        float distance = flatDirection.magnitude;
        if (distance < 0.1f) return;

        transform.position = start;
        transform.rotation = Quaternion.identity;
        UpdatePrefabRouteVisual(start, end, flatDirection.normalized);

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

        if (waveText == null) return;

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
        int remaining = unitController != null
            ? Mathf.Max(0, unitController.marchTargetWave - currentWave)
            : Mathf.Max(1, Mathf.CeilToInt(distance / 15f));

        waveText.text = $"Còn {remaining} Wave";
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