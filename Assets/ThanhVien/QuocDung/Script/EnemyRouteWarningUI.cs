using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    private Transform startPoint;
    private Transform targetPoint;
    private Transform waveSource;
    private float groundOffset;
    private MeshFilter generatedArrowHead;
    private static Sprite solidSprite;

    private Canvas routeCanvas;
    private LineRenderer worldRouteLine;
    private Transform worldArrowHead;

    public void Setup(Transform start, Transform target, Transform waveEnemy, float heightOffset)
    {
        startPoint = start;
        targetPoint = target;
        waveSource = waveEnemy;
        groundOffset = heightOffset;
        gameObject.SetActive(true);
        EnsureWorldRouteVisual();
        UpdateRoute();
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
        // The route is now drawn exclusively by the flat LineRenderer and runtime mesh head.
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

    private void EnsureFallbackVisuals()
    {
        EnsureSolidImage(arrowShaft, arrowColor);

        if (arrowHead == null) return;
        Image headImage = arrowHead.GetComponent<Image>();
        if (headImage != null)
        {
            if (headImage.sprite == null) headImage.sprite = GetSolidSprite();
            headImage.color = arrowOutlineColor;
            headImage.enabled = true;
            headImage.raycastTarget = false;
            arrowHead.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return;
        }

        // A procedural mesh avoids requiring an imported arrowhead sprite.
        generatedArrowHead = arrowHead.GetComponent<MeshFilter>();
        if (generatedArrowHead == null) generatedArrowHead = arrowHead.gameObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = arrowHead.GetComponent<MeshRenderer>();
        if (renderer == null) renderer = arrowHead.gameObject.AddComponent<MeshRenderer>();

        Mesh mesh = new Mesh { name = "RouteArrowHead" };
        mesh.vertices = new[]
        {
            new Vector3(0f, 0.6f, 0f),
            new Vector3(-0.36f, -0.2f, 0f),
            new Vector3(-0.14f, -0.2f, 0f),
            new Vector3(-0.14f, -0.6f, 0f),
            new Vector3(0.14f, -0.6f, 0f),
            new Vector3(0.14f, -0.2f, 0f),
            new Vector3(0.36f, -0.2f, 0f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 5, 0, 5, 6, 2, 3, 4, 2, 4, 5 };
        mesh.uv = new[] { Vector2.up, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        generatedArrowHead.sharedMesh = mesh;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        Material material = new Material(shader) { color = arrowOutlineColor };
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static void EnsureSolidImage(RectTransform rect, Color color)
    {
        if (rect == null) return;
        Image image = rect.GetComponent<Image>();
        if (image == null) return;
        if (image.sprite == null) image.sprite = GetSolidSprite();
        image.color = color;
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
        if (worldRouteLine == null)
        {
            worldRouteLine = GetComponent<LineRenderer>();
            if (worldRouteLine == null) worldRouteLine = gameObject.AddComponent<LineRenderer>();
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            worldRouteLine.sharedMaterial = new Material(shader);
            worldRouteLine.startColor = arrowColor;
            worldRouteLine.endColor = arrowColor;
            worldRouteLine.positionCount = 2;
            worldRouteLine.useWorldSpace = true;
            worldRouteLine.alignment = LineAlignment.View;
            worldRouteLine.numCapVertices = 4;
            worldRouteLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            worldRouteLine.receiveShadows = false;
        }

        if (worldArrowHead == null)
        {
            GameObject head = new GameObject("RuntimeRouteArrowHead");
            head.transform.SetParent(transform, false);
            MeshFilter filter = head.AddComponent<MeshFilter>();
            MeshRenderer renderer = head.AddComponent<MeshRenderer>();
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            renderer.sharedMaterial = new Material(shader) { color = arrowOutlineColor };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Mesh mesh = new Mesh { name = "WorldRouteArrowHead" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0.03f, 0.8f), new Vector3(-0.48f, 0.03f, -0.55f),
                new Vector3(0.48f, 0.03f, -0.55f), new Vector3(0f, 0.16f, 0f)
            };
            mesh.triangles = new[] { 0, 1, 3, 0, 3, 2, 1, 2, 3, 0, 2, 1 };
            mesh.RecalculateNormals();
            filter.sharedMesh = mesh;
            worldArrowHead = head.transform;
        }
    }

    private void UpdateWorldRouteVisual(Vector3 start, Vector3 end, Vector3 direction)
    {
        EnsureWorldRouteVisual();
        Vector3 raisedStart = start + Vector3.up * 0.08f;
        Vector3 raisedEnd = end + Vector3.up * 0.08f;
        worldRouteLine.startWidth = worldArrowWidth;
        worldRouteLine.endWidth = worldArrowWidth;
        worldRouteLine.SetPosition(0, raisedStart);
        worldRouteLine.SetPosition(1, raisedEnd);

        // The generated mesh is already on the XZ plane, so no extra 90-degree rotation is needed.
        worldArrowHead.position = raisedEnd;
        Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z).normalized;
        if (flatDirection.sqrMagnitude < 0.001f) return;
        worldArrowHead.rotation = Quaternion.LookRotation(flatDirection, Vector3.up);
        worldArrowHead.localScale = new Vector3(worldArrowWidth, 1f, 1.5f);
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
            EnemyAI enemy = waveSource.GetComponent<EnemyAI>();
            int remaining = enemy != null ? Mathf.Max(0, enemy.targetWave - currentWave) : 0;
            waveText.text = remaining > 0 ? $"Còn {remaining} Wave" : "Đã đến thành!";
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
