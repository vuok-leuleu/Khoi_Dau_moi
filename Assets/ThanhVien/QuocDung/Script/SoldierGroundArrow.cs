using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class SoldierGroundArrow : MonoBehaviour
{
    [Header("Ground Arrow Config")]
    public Transform targetTransform;
    public Vector3 targetPosition;
    public bool hasTargetPosition = false;

    [Tooltip("Bật/Tắt hiển thị mũi tên dưới chân Soldier này")]
    public bool showGroundArrow = true;

    [Tooltip("Cờ bật/tắt toàn cục cho tất cả Lính")]
    public static bool globalShowSoldierGroundArrow = true;

    [Tooltip("Chiều rộng mũi tên bệt dưới chân (mét)")]
    [Range(0.1f, 5f)]
    public float arrowSize = 0.8f;

    [Tooltip("Hệ số điều chỉnh độ dài mũi tên")]
    [Range(0.1f, 5f)]
    public float arrowLengthMultiplier = 1.0f;

    [Tooltip("Độ dài cộng thêm cố định (mét)")]
    public float arrowExtraLength = 0.0f;

    [Tooltip("Độ cao của mũi tên sát mặt đất (tránh bị chìm dưới terrain)")]
    [Range(0.01f, 0.5f)]
    public float arrowGroundOffset = 0.05f;

    [Tooltip("Màu sắc của mũi tên dưới chân Lính (Mặc định Xanh Demacia)")]
    public Color arrowColor = new Color(0.2f, 0.55f, 1.0f, 0.95f);

    [Header("Cảnh Báo Thời Gian (Timer Text)")]
    [Tooltip("Điều chỉnh tỷ lệ kích thước chữ đếm ngược")]
    [Range(0.1f, 5f)]
    public float timerTextScale = 1.0f;
    [Tooltip("Độ cao chữ đếm ngược trên đầu/thân Soldier")]
    [Range(0.5f, 5f)]
    public float textHeightOffset = 1.8f;
    [Tooltip("Màu chữ đếm ngược")]
    public Color textColor = Color.yellow;

    [Header("Internal References")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private GameObject arrowQuadObj;
    [SerializeField] private MeshRenderer arrowMeshRenderer;
    [SerializeField] private TMPro.TextMeshProUGUI timerText;

    private Camera mainCamera;

    private static Material sharedSoldierArrowMaterial;
    private static Texture2D sharedSoldierArrowTexture;

    public static SoldierGroundArrow Create(Transform soldier)
    {
        if (soldier == null) return null;

        SoldierGroundArrow existing = soldier.GetComponentInChildren<SoldierGroundArrow>();
        if (existing != null) return existing;

        GameObject arrowObj = new GameObject("SoldierGroundArrow_WorldSpace");
        arrowObj.transform.SetParent(soldier, false);
        arrowObj.transform.localPosition = Vector3.zero;
        arrowObj.transform.localRotation = Quaternion.identity;

        SoldierGroundArrow comp = arrowObj.AddComponent<SoldierGroundArrow>();
        comp.BuildArrowGeometry();
        return comp;
    }

    private void Awake()
    {
        EnsureComponents();
    }

    private void Start()
    {
        EnsureComponents();
        UpdateVisuals();
    }

    public void SetTargetDestination(Vector3 targetPos)
    {
        targetPosition = targetPos;
        hasTargetPosition = true;
        showGroundArrow = true;
        EnsureComponents();
        UpdateVisuals();
    }

    public void SetTargetTransform(Transform targetTr)
    {
        targetTransform = targetTr;
        if (targetTr != null)
        {
            targetPosition = targetTr.position;
            hasTargetPosition = true;
        }
        showGroundArrow = true;
        EnsureComponents();
        UpdateVisuals();
    }

    private static Texture2D CreateStretchedArrowTexture()
    {
        if (sharedSoldierArrowTexture != null) return sharedSoldierArrowTexture;

        int width = 128;
        int height = 512;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color transparent = new Color(0, 0, 0, 0);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                tex.SetPixel(x, y, transparent);
            }
        }

        int centerX = width / 2;
        int headHeight = 128;
        int bodyHeight = height - headHeight;

        // 1. Thân mũi tên (Shaft)
        int shaftHalfWidth = 22;
        for (int y = 0; y < bodyHeight; y++)
        {
            for (int x = centerX - shaftHalfWidth; x <= centerX + shaftHalfWidth; x++)
            {
                if (x >= 0 && x < width)
                {
                    tex.SetPixel(x, y, Color.white);
                }
            }
        }

        // 2. Đầu mũi tên tam giác ở đỉnh (Arrowhead)
        for (int y = bodyHeight; y < height; y++)
        {
            float progress = (float)(height - y) / headHeight;
            int rowHalfWidth = Mathf.RoundToInt(progress * (width / 2 - 4));
            for (int x = centerX - rowHalfWidth; x <= centerX + rowHalfWidth; x++)
            {
                if (x >= 0 && x < width)
                {
                    tex.SetPixel(x, y, Color.white);
                }
            }
        }

        tex.Apply();
        sharedSoldierArrowTexture = tex;
        return sharedSoldierArrowTexture;
    }

    private static Material GetArrowMaterial()
    {
        if (sharedSoldierArrowMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("UI/Default");
            sharedSoldierArrowMaterial = new Material(shader);
            sharedSoldierArrowMaterial.mainTexture = CreateStretchedArrowTexture();
        }
        return sharedSoldierArrowMaterial;
    }

    private void EnsureComponents()
    {
        Transform arrowTr = transform.Find("GroundArrow3D_Soldier");
        if (arrowTr == null)
        {
            GameObject arrowObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            arrowObj.name = "GroundArrow3D_Soldier";
            arrowObj.transform.SetParent(transform, false);

            Collider col = arrowObj.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            MeshRenderer mr = arrowObj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.material = GetArrowMaterial();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            MeshFilter mf = arrowObj.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Mesh mesh = Instantiate(mf.sharedMesh);
                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i].y += 0.5f; // Pivot at bottom center
                }
                mesh.vertices = vertices;
                mesh.RecalculateBounds();
                mf.sharedMesh = mesh;
            }

            arrowQuadObj = arrowObj;
            arrowMeshRenderer = mr;
        }
        else
        {
            arrowQuadObj = arrowTr.gameObject;
            arrowMeshRenderer = arrowQuadObj.GetComponent<MeshRenderer>();
        }

        if (worldCanvas == null)
        {
            worldCanvas = GetComponent<Canvas>();
            if (worldCanvas == null)
            {
                worldCanvas = gameObject.AddComponent<Canvas>();
            }
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.worldCamera = Camera.main;

            UnityEngine.UI.CanvasScaler scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100;
        }

        // 2. Chữ đếm ngược Wave (Timer Text - World Space)
        if (timerText == null)
        {
            Transform textTr = transform.Find("SoldierTimerText");
            if (textTr != null)
            {
                timerText = textTr.GetComponent<TMPro.TextMeshProUGUI>();
            }
            else
            {
                GameObject textObj = new GameObject("SoldierTimerText");
                textObj.transform.SetParent(transform, false);
                timerText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            }
        }

        if (timerText != null)
        {
            timerText.enableWordWrapping = false;
            timerText.overflowMode = TMPro.TextOverflowModes.Overflow;
            timerText.raycastTarget = false;
        }
    }

    public void BuildArrowGeometry()
    {
        EnsureComponents();
        UpdateVisuals();
    }

    public void UpdateVisuals()
    {
        if (arrowMeshRenderer != null && arrowMeshRenderer.sharedMaterial != null)
        {
            arrowMeshRenderer.sharedMaterial.color = arrowColor;
        }

        if (timerText != null)
        {
            float baseScale = 0.015f;
            timerText.fontSize = 32;
            timerText.enableWordWrapping = false;
            timerText.overflowMode = TMPro.TextOverflowModes.Overflow;
            timerText.color = textColor;
            timerText.alignment = TMPro.TextAlignmentOptions.Center;
            timerText.rectTransform.sizeDelta = new Vector2(250f, 60f);
            timerText.rectTransform.localScale = Vector3.one * (baseScale * timerTextScale);
            timerText.rectTransform.localPosition = new Vector3(0f, textHeightOffset, 0f);
        }
    }

    private void Update()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        bool inBattle = currentScene.ToLower().Contains("battle");

        if (inBattle || !showGroundArrow || !globalShowSoldierGroundArrow)
        {
            if (timerText != null && timerText.gameObject.activeSelf) timerText.gameObject.SetActive(false);
        }
        else
        {
            UpdateTimerText();
        }

        UpdateStretchedArrowGeometry();
        UpdateVisuals();
    }

    private void UpdateTimerText()
    {
        if (timerText == null) return;

        Vector3 soldierPos = transform.parent != null ? transform.parent.position : transform.position;
        Vector3 destinationPos = targetPosition;
        if (targetTransform != null && targetTransform.gameObject.activeInHierarchy)
        {
            destinationPos = targetTransform.position;
        }

        float dist = Vector3.Distance(soldierPos, destinationPos);

        if (!hasTargetPosition || dist <= 2.5f)
        {
            if (timerText.gameObject.activeSelf) timerText.gameObject.SetActive(false);
            return;
        }

        if (!timerText.gameObject.activeSelf) timerText.gameObject.SetActive(true);

        UnitController uc = GetComponentInParent<UnitController>();
        int remainingWaves = 3;
        if (uc != null && uc.isExpeditionMarching)
        {
            int currentWave = (DayNightManager.HasInstance && DayNightManager.Ins != null) ? DayNightManager.Ins.CurrentWave : uc.marchStartWave;
            remainingWaves = Mathf.Max(0, uc.marchTargetWave - currentWave);
        }
        else
        {
            remainingWaves = Mathf.Max(1, Mathf.CeilToInt(dist / 15f));
        }

        timerText.text = $"⌛ {remainingWaves} Wave";

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Vector3 forward = timerText.transform.position - mainCamera.transform.position;
            if (forward.sqrMagnitude > 0.001f)
            {
                timerText.transform.rotation = Quaternion.LookRotation(forward);
            }
        }
    }

    private void UpdateStretchedArrowGeometry()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!showGroundArrow || !globalShowSoldierGroundArrow || currentScene.ToLower().Contains("battle"))
        {
            if (arrowQuadObj != null && arrowQuadObj.activeSelf)
            {
                arrowQuadObj.SetActive(false);
            }
            return;
        }

        Vector3 soldierPos = transform.parent != null ? transform.parent.position : transform.position;

        Vector3 destinationPos = targetPosition;

        UnitController uc = GetComponentInParent<UnitController>();
        if (uc != null && uc.isExpeditionMarching && uc.marchDestinationPosition != Vector3.zero)
        {
            destinationPos = uc.marchDestinationPosition;
            hasTargetPosition = true;
        }
        else if (targetTransform != null && targetTransform.gameObject.activeInHierarchy)
        {
            destinationPos = targetTransform.position;
            hasTargetPosition = true;
        }

        if (!hasTargetPosition)
        {
            if (arrowQuadObj != null && arrowQuadObj.activeSelf)
            {
                arrowQuadObj.SetActive(false);
            }
            return;
        }

        Vector3 dir = destinationPos - soldierPos;
        dir.y = 0f;
        float dist = dir.magnitude;

        if (dist < 0.5f)
        {
            if (arrowQuadObj != null && arrowQuadObj.activeSelf)
            {
                arrowQuadObj.SetActive(false);
            }
            return;
        }

        if (arrowQuadObj != null)
        {
            if (!arrowQuadObj.activeSelf) arrowQuadObj.SetActive(true);

            dir.Normalize();
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);

            arrowQuadObj.transform.position = soldierPos + Vector3.up * arrowGroundOffset;
            arrowQuadObj.transform.rotation = rot;

            float totalLength = Mathf.Max(0.5f, dist * arrowLengthMultiplier + arrowExtraLength);
            arrowQuadObj.transform.localScale = new Vector3(arrowSize, totalLength, 1f);
        }
    }
}
