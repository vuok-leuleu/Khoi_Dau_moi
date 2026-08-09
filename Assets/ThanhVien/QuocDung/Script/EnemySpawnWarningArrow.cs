using UnityEngine;
using UnityEngine.UI;
using TMPro;

[ExecuteAlways]
public class EnemySpawnWarningArrow : MonoBehaviour
{
    [Header("Target & Tracking")]
    public Transform targetEnemy;

    [Header("Mũi Tên Dưới Chân (Ground Arrow)")]
    [Tooltip("Bật/Tắt hiển thị mũi tên dưới chân Enemy này")]
    public bool showGroundArrow = true;

    [Tooltip("Cờ bật/tắt toàn cục cho tất cả Enemy")]
    public static bool globalShowEnemyGroundArrow = true;

    [Tooltip("Điều chỉnh chiều rộng mũi tên (mét) kéo dài bệt dưới chân Enemy")]
    [Range(0.1f, 5f)]
    public float arrowSize = 1.0f;

    [Tooltip("Hệ số điều chỉnh độ dài mũi tên (1.0 = duỗi tới đúng mục tiêu, >1.0 = dài hơn, <1.0 = ngắn hơn)")]
    [Range(0.1f, 5f)]
    public float arrowLengthMultiplier = 1.0f;

    [Tooltip("Độ dài cộng thêm cố định (mét) của mũi tên nếu muốn kéo dài hơn nữa")]
    public float arrowExtraLength = 0.0f;

    [Tooltip("Độ cao của mũi tên sát mặt đất (tránh bị chìm dưới terrain)")]
    [Range(0.01f, 0.5f)]
    public float arrowGroundOffset = 0.05f;

    [Tooltip("Màu sắc của mũi tên dưới chân")]
    public Color arrowColor = new Color(1f, 0.2f, 0.2f, 0.95f);

    [Header("Cảnh Báo Thời Gian (Timer Text)")]
    [Tooltip("Điều chỉnh tỷ lệ kích thước chữ đếm ngược")]
    [Range(0.1f, 5f)]
    public float timerTextScale = 1.0f;
    [Tooltip("Độ cao chữ đếm ngược trên đầu/thân Enemy")]
    [Range(0.5f, 5f)]
    public float textHeightOffset = 1.8f;
    [Tooltip("Màu chữ đếm ngược")]
    public Color textColor = Color.yellow;

    [Header("References (Internal)")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private GameObject arrowQuadObj;
    [SerializeField] private MeshRenderer arrowMeshRenderer;
    [SerializeField] private TextMeshProUGUI timerText;

    private Camera mainCamera;
    private static Material sharedArrowMaterial;
    private static Texture2D sharedArrowTexture;

    public static EnemySpawnWarningArrow Create(Transform leadEnemy)
    {
        if (leadEnemy == null) return null;

        EnemySpawnWarningArrow existing = leadEnemy.GetComponentInChildren<EnemySpawnWarningArrow>();
        if (existing != null) return existing;

        GameObject warningObj = new GameObject("EnemySpawnWarning_WorldSpace");
        warningObj.transform.SetParent(leadEnemy, false);
        warningObj.transform.localPosition = Vector3.zero;
        warningObj.transform.localRotation = Quaternion.identity;

        EnemySpawnWarningArrow arrowComp = warningObj.AddComponent<EnemySpawnWarningArrow>();
        arrowComp.targetEnemy = leadEnemy;
        arrowComp.BuildWorldUI();

        return arrowComp;
    }

    private void Awake()
    {
        if (targetEnemy == null && transform.parent != null)
        {
            targetEnemy = transform.parent;
        }
        EnsureComponents();
    }

    private void Start()
    {
        mainCamera = Camera.main;
        EnsureComponents();
        UpdateVisuals();
    }

    private void OnValidate()
    {
        // Để trống để tránh cảnh báo Unity OnRectTransformDimensionsChange trong Inspector
    }

    public void BuildWorldUI()
    {
        EnsureComponents();
        UpdateVisuals();
    }

    private static Texture2D CreateStretchedArrowTexture()
    {
        if (sharedArrowTexture != null) return sharedArrowTexture;

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
        sharedArrowTexture = tex;
        return sharedArrowTexture;
    }

    private static Material GetArrowMaterial()
    {
        if (sharedArrowMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("UI/Default");
            sharedArrowMaterial = new Material(shader);
            sharedArrowMaterial.mainTexture = CreateStretchedArrowTexture();
        }
        return sharedArrowMaterial;
    }

    private void EnsureComponents()
    {
        // Remove old UI GroundArrow if present
        Transform oldUIArrow = transform.Find("GroundArrow");
        if (oldUIArrow != null && oldUIArrow.GetComponent<RectTransform>() != null && oldUIArrow.GetComponent<MeshRenderer>() == null)
        {
            if (Application.isPlaying) Destroy(oldUIArrow.gameObject);
            else DestroyImmediate(oldUIArrow.gameObject);
        }

        if (worldCanvas == null)
        {
            worldCanvas = GetComponent<Canvas>();
            if (worldCanvas == null)
            {
                worldCanvas = gameObject.AddComponent<Canvas>();
            }
            worldCanvas.renderMode = RenderMode.WorldSpace;
            
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100;
        }

        // 1. Mũi tên 3D Quad bệt mặt đất (3D Ground Arrow Quad)
        Transform arrowTr = transform.Find("GroundArrow3D");
        if (arrowTr == null)
        {
            GameObject arrowObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            arrowObj.name = "GroundArrow3D";
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

        // 2. Chữ đếm ngược thời gian (Timer Text)
        if (timerText == null)
        {
            Transform textTr = transform.Find("TimerText");
            if (textTr != null)
            {
                timerText = textTr.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                GameObject textObj = new GameObject("TimerText");
                textObj.transform.SetParent(transform, false);
                timerText = textObj.AddComponent<TextMeshProUGUI>();
            }
        }

        if (timerText != null)
        {
            timerText.enableWordWrapping = false;
            timerText.overflowMode = TextOverflowModes.Overflow;
            timerText.raycastTarget = false;
        }
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
            timerText.overflowMode = TextOverflowModes.Overflow;
            timerText.color = textColor;
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.rectTransform.sizeDelta = new Vector2(250f, 60f);
            timerText.rectTransform.localScale = Vector3.one * (baseScale * timerTextScale);
            timerText.rectTransform.localPosition = new Vector3(0f, textHeightOffset, 0f);
        }
    }

    private void Update()
    {
        if (targetEnemy == null && transform.parent != null)
        {
            targetEnemy = transform.parent;
        }

        if (Application.isPlaying)
        {
            if (targetEnemy == null || !targetEnemy.gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }

            EnemyAI enemyAI = targetEnemy.GetComponent<EnemyAI>();
            if (enemyAI != null && enemyAI.isCombatActive)
            {
                Destroy(gameObject);
                return;
            }

            UpdateTimerText();
        }
        else
        {
            if (timerText != null)
            {
                timerText.text = "00:45";
            }
        }

        UpdateStretchedArrowGeometry();

        if (timerText != null)
        {
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

        UpdateVisuals();
    }

    private Transform GetActualEnemyTarget()
    {
        // 1. Ưu tiên tuyệt đối attackTarget từ EnemySpawn (chính là Nhachinhs trong Inspector)
        EnemySpawn[] spawners = Object.FindObjectsByType<EnemySpawn>(FindObjectsSortMode.None);
        foreach (var spawner in spawners)
        {
            if (spawner != null && spawner.attackTarget != null && spawner.attackTarget.gameObject.activeInHierarchy)
            {
                return spawner.attackTarget;
            }
        }

        // 2. Nếu không có, tìm theo villageCenter của EnemyAI
        if (targetEnemy != null)
        {
            EnemyAI enemyAI = targetEnemy.GetComponent<EnemyAI>();
            if (enemyAI != null)
            {
                if (enemyAI.villageCenter != null) return enemyAI.villageCenter;
                Transform currentTarget = enemyAI.GetCurrentTarget();
                if (currentTarget != null) return currentTarget;
            }
        }

        // 3. Tìm theo tên GameObject Nhachinhs / Nhachinh
        GameObject nhaChinhObj = GameObject.Find("Nhachinhs");
        if (nhaChinhObj == null) nhaChinhObj = GameObject.Find("Nhachinh");
        if (nhaChinhObj != null) return nhaChinhObj.transform;

        // 4. Tìm theo Tag Main
        GameObject mainObj = GameObject.FindGameObjectWithTag("Main");
        if (mainObj != null) return mainObj.transform;

        // 5. Tìm theo UpgradeableBuilding
        UpgradeableBuilding[] buildings = Object.FindObjectsByType<UpgradeableBuilding>(FindObjectsSortMode.None);
        foreach (var b in buildings)
        {
            if (b != null && b.gameObject.activeInHierarchy && !b.IsRuined)
            {
                return b.transform;
            }
        }

        return null;
    }

    private Vector3 GetTargetFeetPosition(Transform target)
    {
        if (target == null) return targetEnemy.position + targetEnemy.forward * 10f;

        Vector3 pos = target.position;

        Collider col = target.GetComponentInChildren<Collider>();
        if (col != null)
        {
            pos = col.bounds.center;
        }
        else
        {
            Renderer ren = target.GetComponentInChildren<Renderer>();
            if (ren != null)
            {
                pos = ren.bounds.center;
            }
        }

        pos.y = targetEnemy.position.y;
        return pos;
    }

    private void UpdateStretchedArrowGeometry()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!showGroundArrow || !globalShowEnemyGroundArrow || currentScene.ToLower().Contains("battle"))
        {
            if (arrowQuadObj != null && arrowQuadObj.activeSelf)
            {
                arrowQuadObj.SetActive(false);
            }
            return;
        }

        Transform target = GetActualEnemyTarget();
        if (target == null || targetEnemy == null)
        {
            if (arrowQuadObj != null && arrowQuadObj.activeSelf)
            {
                arrowQuadObj.SetActive(false);
            }
            return;
        }

        Vector3 enemyPos = targetEnemy.position;
        Vector3 targetFeetPos = GetTargetFeetPosition(target);

        Vector3 dir = targetFeetPos - enemyPos;
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

            arrowQuadObj.transform.position = enemyPos + Vector3.up * arrowGroundOffset;
            arrowQuadObj.transform.rotation = rot;

            float totalLength = Mathf.Max(0.5f, dist * arrowLengthMultiplier + arrowExtraLength);
            arrowQuadObj.transform.localScale = new Vector3(arrowSize, totalLength, 1f);
        }
    }

    private void UpdateTimerText()
    {
        if (timerText == null) return;

        int remainingWaves = 3;
        bool hasReachedWall = false;

        if (targetEnemy != null)
        {
            EnemyAI enemyAI = targetEnemy.GetComponent<EnemyAI>();
            if (enemyAI != null)
            {
                int currentWave = (DayNightManager.HasInstance && DayNightManager.Ins != null) ? DayNightManager.Ins.CurrentWave : 1;
                remainingWaves = Mathf.Max(0, enemyAI.targetWave - currentWave);

                Vector3 castleWallPos = EnemyAI.GetCastleWallDestination(enemyAI.transform.position, enemyAI.villageCenter);
                float distToWall = Vector3.Distance(enemyAI.transform.position, castleWallPos);
                hasReachedWall = (remainingWaves <= 0) && (distToWall <= 2.5f);
            }
        }

        if (hasReachedWall)
        {
            timerText.text = "Đã đến thành!";
        }
        else
        {
            int showWaves = Mathf.Max(1, remainingWaves);
            timerText.text = $"Còn {showWaves} Wave";
        }
    }
}
