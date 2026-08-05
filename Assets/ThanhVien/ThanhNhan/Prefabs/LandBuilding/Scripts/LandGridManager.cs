using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ExpandDirection
{
    North, // +Z
    South, // -Z
    East,  // +X
    West   // -X
}

public class LandGridManager : MonoBehaviour
{
    public static LandGridManager Ins { get; private set; }

    [Header("=== CẤU HÌNH MAP CÓ SẴN ===")]
    [Tooltip("Parent chứa toàn bộ Ground/Map trong Scene")]
    [SerializeField] private Transform existingMapTilesParent;
    [Tooltip("Parent chứa các Công trình có sẵn trên Map (nếu có)")]
    [SerializeField] private Transform existingBuildingsParent;

    [Header("=== CẤU HÌNH VÙNG XÂY DỰNG BAN ĐẦU ===")]
    [Tooltip("Số ô vuông chiều ngang được phép xây ở trung tâm")]
    [SerializeField] private int initialWidth = 4;
    [Tooltip("Số ô vuông chiều dọc được phép xây ở trung tâm")]
    [SerializeField] private int initialHeight = 4;

    [Header("=== CẤU HÌNH GRID ===")]
    [Tooltip("Kích thước dài / rộng của 1 ô Grid")]
    [SerializeField] private float tileSize = 10f;
    [Tooltip("Độ cao Y của ô đất trên bản đồ")]
    [SerializeField] private float plotSpawnY = 0f;

    [Header("=== HIỂN THỊ LƯỚI GRID BẰNG MESH ===")]
    [Tooltip("Material dùng cho đường lưới (Nếu bỏ trống sẽ tự dùng Sprites/Default)")]
    [SerializeField] private Material gridLineMaterial;
    [Tooltip("Màu sắc đường lưới vùng ĐƯỢC XÂY DỰNG")]
    [SerializeField] private Color buildableGridColor = Color.green;
    [Tooltip("Màu sắc đường lưới vùng KHÔNG ĐƯỢC XÂY DỰNG")]
    [SerializeField] private Color nonBuildableGridColor = Color.red;
    [Tooltip("Độ lệch chiều cao Y để lưới hiển thị nổi nhẹ trên mặt đất")]
    [SerializeField] private float gridYOffset = 0.05f;

    [Header("=== CẤU HÌNH TƯỜNG & CỔNG THÀNH ===")]
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private GameObject gatePrefab;

    [Header("=== 4 NÚT MỞ RỘNG (+) ===")]
    [SerializeField] private Transform btnNorth;
    [SerializeField] private Transform btnSouth;
    [SerializeField] private Transform btnEast;
    [SerializeField] private Transform btnWest;

    // Toàn bộ các ô nằm trên mặt Ground (Đã quét từ scene)
    private HashSet<Vector2Int> allGroundTiles = new HashSet<Vector2Int>();
    // Các ô ĐÃ MỞ KHÓA được phép xây dựng (Hiển thị lưới XANH)
    private HashSet<Vector2Int> unlockedTiles = new HashSet<Vector2Int>();
    
    private List<GameObject> activeFences = new List<GameObject>();
    private HashSet<Vector2Int> occupiedTiles = new HashSet<Vector2Int>();

    // Mesh vẽ lưới Grid
    private GameObject generatedGridOverlay;
    private MeshFilter gridMeshFilter;
    private MeshRenderer gridMeshRenderer;

    private int expandCount = 0;
    private GameObject expandConfirmPanel;
    private Button expandConfirmYesButton;
    private Button expandConfirmNoButton;
    private TextMeshProUGUI expandConfirmText;
    private ExpandDirection? pendingExpandDirection = null;

    private readonly int[] expandCostSequence = new int[] { 100, 150, 300, 600, 1500 };

    private void Awake()
    {
        if (Ins == null) Ins = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        InitializeGridSystem();
    }

    /// <summary>
    /// Khởi tạo hệ thống Grid: Quét toàn bộ Ground, xác định vùng trung tâm mở khóa & vẽ Grid
    /// </summary>
    public void InitializeGridSystem()
    {
        allGroundTiles.Clear();
        unlockedTiles.Clear();

        // 1. Quét lấy Bounds thực tế của toàn bộ Ground trong Scene
        if (existingMapTilesParent != null && existingMapTilesParent.childCount > 0)
        {
            Bounds totalBounds = new Bounds();
            bool hasBounds = false;

            foreach (Transform tile in existingMapTilesParent)
            {
                Collider col = tile.GetComponent<Collider>();
                Renderer rend = tile.GetComponent<Renderer>();
                Bounds b = (col != null) ? col.bounds : ((rend != null) ? rend.bounds : new Bounds(tile.position, Vector3.one * tileSize));

                if (!hasBounds)
                {
                    totalBounds = b;
                    hasBounds = true;
                }
                else
                {
                    totalBounds.Encapsulate(b);
                }
            }

            // Quy đổi diện tích Ground ra tọa độ ô Grid từ từng tile riêng lẻ
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minZ = int.MaxValue;
            int maxZ = int.MinValue;

            foreach (Transform tile in existingMapTilesParent)
            {
                Collider col = tile.GetComponent<Collider>();
                Renderer rend = tile.GetComponent<Renderer>();
                Bounds b = (col != null) ? col.bounds : ((rend != null) ? rend.bounds : new Bounds(tile.position, Vector3.one * tileSize));

                int tileMinX = Mathf.FloorToInt(b.min.x / tileSize);
                int tileMaxX = Mathf.CeilToInt(b.max.x / tileSize);
                int tileMinZ = Mathf.FloorToInt(b.min.z / tileSize);
                int tileMaxZ = Mathf.CeilToInt(b.max.z / tileSize);

                for (int x = tileMinX; x < tileMaxX; x++)
                {
                    for (int z = tileMinZ; z < tileMaxZ; z++)
                    {
                        allGroundTiles.Add(new Vector2Int(x, z));
                    }
                }

                if (tileMinX < minX) minX = tileMinX;
                if (tileMaxX > maxX) maxX = tileMaxX;
                if (tileMinZ < minZ) minZ = tileMinZ;
                if (tileMaxZ > maxZ) maxZ = tileMaxZ;
            }

            // 2. Tìm tâm của Ground và mở khóa VÙNG XÂY DỰNG BAN ĐẦU ở trung tâm
            int centerX = Mathf.RoundToInt((minX + maxX) * 0.5f);
            int centerZ = Mathf.RoundToInt((minZ + maxZ) * 0.5f);

            int startX = centerX - (initialWidth / 2);
            int startZ = centerZ - (initialHeight / 2);

            for (int x = startX; x < startX + initialWidth; x++)
            {
                for (int z = startZ; z < startZ + initialHeight; z++)
                {
                    Vector2Int coord = new Vector2Int(x, z);
                    if (allGroundTiles.Contains(coord))
                    {
                        unlockedTiles.Add(coord);
                    }
                }
            }
        }
        else // Fallback nếu chưa gán existingMapTilesParent
        {
            for (int x = 0; x < initialWidth; x++)
            {
                for (int z = 0; z < initialHeight; z++)
                {
                    Vector2Int coord = new Vector2Int(x, z);
                    allGroundTiles.Add(coord);
                    unlockedTiles.Add(coord);
                }
            }
        }

        // 3. Quét công trình sẵn có
        ScanExistingBuildings();

        // 4. Dựng tường + Nút mở rộng + Vẽ lưới Xanh/Đỏ
        RebuildFences();
        UpdateExpandButtonsPosition();
        CreateOrUpdateGridMesh();
    }

    /// <summary>
    /// Mở khóa 1 ô logic (chỉ thêm vào HashSet unlockedTiles, KHÔNG spawn gameobject)
    /// </summary>
    public void UnlockTile(int x, int z)
    {
        Vector2Int gridCoord = new Vector2Int(x, z);
        if (unlockedTiles.Contains(gridCoord)) return;

        unlockedTiles.Add(gridCoord);
        allGroundTiles.Add(gridCoord); // Đảm bảo ô luôn thuộc bản đồ
    }

    /// <summary>
    /// 🔥 MỞ RỘNG KHU VỰC XÂY DỰNG KHI BẤM NÚT (+)
    /// </summary>
    private float pendingExpandConfirmTime = 0f;
    private const float ExpandConfirmTimeout = 4f;
    [Header("Expand Cost")]
    [SerializeField] private int baseExpandGoldCost = 100;
    [SerializeField] private int expandGoldCostPerRow = 50;

    public void RequestExpandGrid(ExpandDirection direction)
    {
        pendingExpandDirection = direction;
        int cost = GetExpandGoldCost(direction);
        ShowExpandConfirmationUI(direction, cost);
    }

    public int GetExpandGoldCost(ExpandDirection direction)
    {
        int index = Mathf.Clamp(expandCount, 0, expandCostSequence.Length - 1);
        if (expandCount < expandCostSequence.Length)
            return expandCostSequence[expandCount];

        int extra = expandCostSequence[expandCostSequence.Length - 1] * (1 << (expandCount - expandCostSequence.Length + 1));
        return extra;
    }

    private void PerformExpandGrid(ExpandDirection direction)
    {
        int cost = GetExpandGoldCost(direction);
        if (JsonDataManager.Ins != null && !JsonDataManager.Ins.TrySpendGold(cost))
        {
            UIManager.Ins?.ShowWarning("Không đủ vàng để mở rộng đất.");
            pendingExpandDirection = null;
            pendingExpandConfirmTime = 0f;
            return;
        }

        GetGridBounds(out int minX, out int maxX, out int minZ, out int maxZ);

        switch (direction)
        {
            case ExpandDirection.North:
                for (int x = minX; x <= maxX; x++) UnlockTile(x, maxZ + 1);
                break;
            case ExpandDirection.South:
                for (int x = minX; x <= maxX; x++) UnlockTile(x, minZ - 1);
                break;
            case ExpandDirection.East:
                for (int z = minZ; z <= maxZ; z++) UnlockTile(maxX + 1, z);
                break;
            case ExpandDirection.West:
                for (int z = minZ; z <= maxZ; z++) UnlockTile(minX - 1, z);
                break;
        }

        expandCount++;
        RebuildFences();
        UpdateExpandButtonsPosition();
        CreateOrUpdateGridMesh();

        HideExpandConfirmationUI();
        UIManager.Ins?.ShowWarning($"Mở rộng đất thành công. Đã trừ {cost} vàng.");

        if (CampaignTutorialManager.Ins != null)
        {
            CampaignTutorialManager.Ins.OnLandExpanded();
        }
    }

    private void Update()
    {
    }

    private void ShowExpandConfirmationUI(ExpandDirection direction, int cost)
    {
        if (expandConfirmPanel == null)
            CreateExpandConfirmationUI();

        if (expandConfirmText != null)
        {
            expandConfirmText.text = $"Xác nhận mở rộng đất {direction} với chi phí {cost} vàng?";
        }

        if (expandConfirmPanel != null)
            expandConfirmPanel.SetActive(true);

        if (expandConfirmYesButton != null)
        {
            expandConfirmYesButton.onClick.RemoveAllListeners();
            expandConfirmYesButton.onClick.AddListener(() => {
                if (pendingExpandDirection.HasValue)
                    PerformExpandGrid(pendingExpandDirection.Value);
            });
        }

        if (expandConfirmNoButton != null)
        {
            expandConfirmNoButton.onClick.RemoveAllListeners();
            expandConfirmNoButton.onClick.AddListener(HideExpandConfirmationUI);
        }
    }

    private void HideExpandConfirmationUI()
    {
        if (expandConfirmPanel != null)
            expandConfirmPanel.SetActive(false);
        pendingExpandDirection = null;
    }

    [Header("UI References")]
    [SerializeField] private Canvas uiCanvas;

    private void CreateExpandConfirmationUI()
    {
        Canvas canvas = uiCanvas != null ? uiCanvas : GetComponentInChildren<Canvas>();
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        expandConfirmPanel = new GameObject("ExpandConfirmPanel", typeof(RectTransform), typeof(Image));
        expandConfirmPanel.transform.SetParent(canvas.transform, false);
        var panelImage = expandConfirmPanel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.8f);

        RectTransform panelRect = expandConfirmPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(360f, 180f);
        panelRect.anchoredPosition = Vector2.zero;

        GameObject textGO = new GameObject("ExpandConfirmText", typeof(RectTransform));
        textGO.transform.SetParent(expandConfirmPanel.transform, false);
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.1f, 0.6f);
        textRect.anchorMax = new Vector2(0.9f, 0.9f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        expandConfirmText = textGO.AddComponent<TextMeshProUGUI>();
        expandConfirmText.font = TMP_Settings.defaultFontAsset;
        expandConfirmText.alignment = TextAlignmentOptions.Center;
        expandConfirmText.fontSize = 20;
        expandConfirmText.color = Color.white;
        expandConfirmText.enableWordWrapping = true;

        GameObject yesButtonGO = CreateSimpleButton("YesButton", "Đồng ý", new Vector2(0.25f, 0.2f));
        yesButtonGO.transform.SetParent(expandConfirmPanel.transform, false);
        expandConfirmYesButton = yesButtonGO.GetComponent<Button>();

        GameObject noButtonGO = CreateSimpleButton("NoButton", "Hủy", new Vector2(0.75f, 0.2f));
        noButtonGO.transform.SetParent(expandConfirmPanel.transform, false);
        expandConfirmNoButton = noButtonGO.GetComponent<Button>();

        expandConfirmPanel.SetActive(false);
    }

    private GameObject CreateSimpleButton(string name, string textLabel, Vector2 anchorPos)
    {
        GameObject buttonGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonGO.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(anchorPos.x - 0.2f, anchorPos.y - 0.1f);
        buttonRect.anchorMax = new Vector2(anchorPos.x + 0.2f, anchorPos.y + 0.1f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        buttonRect.sizeDelta = Vector2.zero;

        var image = buttonGO.GetComponent<Image>();
        image.color = new Color(0.2f, 0.5f, 0.85f, 1f);

        var button = buttonGO.GetComponent<Button>();
        button.targetGraphic = image;

        GameObject labelGO = new GameObject("Text", typeof(RectTransform));
        labelGO.transform.SetParent(buttonGO.transform, false);
        RectTransform labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var labelText = labelGO.AddComponent<TextMeshProUGUI>();
        labelText.font = TMP_Settings.defaultFontAsset;
        labelText.text = textLabel;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.fontSize = 18;
        labelText.color = Color.white;

        return buttonGO;
    }

    /// <summary>
    /// Kiểm tra xem vị trí đặt công trình có nằm hoàn toàn trong VÙNG ĐÃ MỞ KHÓA (LƯỚI XANH) không
    /// </summary>
    public bool IsAreaUnlocked(Vector3 centerWorldPos, int sizeX = 1, int sizeZ = 1)
    {
        Vector3 snappedPos = GetSnappedGridPosition(centerWorldPos, sizeX, sizeZ);
        Vector2Int baseCoord = WorldToGridCoord(snappedPos);
        int startX = baseCoord.x - (sizeX / 2);
        int startZ = baseCoord.y - (sizeZ / 2);

        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                Vector2Int checkCoord = new Vector2Int(startX + x, startZ + z);
                if (!unlockedTiles.Contains(checkCoord)) return false;
            }
        }
        return true;
    }
    /// <summary>
    /// Kiểm tra xem vị trí có bị công trình khác chiếm chỗ chưa (Grid thuần)
    /// </summary>
    public bool IsAreaOccupied(Vector3 centerWorldPos, int sizeX = 1, int sizeZ = 1)
    {
        Vector3 snappedPos = GetSnappedGridPosition(centerWorldPos, sizeX, sizeZ);
        Vector2Int baseCoord = WorldToGridCoord(snappedPos);
        int startX = baseCoord.x - (sizeX / 2);
        int startZ = baseCoord.y - (sizeZ / 2);

        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                Vector2Int checkCoord = new Vector2Int(startX + x, startZ + z);
                if (occupiedTiles.Contains(checkCoord)) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Đánh dấu mảng ô đất đã bị công trình chiếm chỗ
    /// </summary>
    public void MarkAreaAsOccupied(Vector3 centerWorldPos, int sizeX = 1, int sizeZ = 1)
    {
        Vector3 snappedPos = GetSnappedGridPosition(centerWorldPos, sizeX, sizeZ);
        Vector2Int baseCoord = WorldToGridCoord(snappedPos);
        int startX = baseCoord.x - (sizeX / 2);
        int startZ = baseCoord.y - (sizeZ / 2);

        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                occupiedTiles.Add(new Vector2Int(startX + x, startZ + z));
            }
        }
    }

    /// <summary>
    /// Xóa đánh dấu chiếm chỗ (dùng khi di chuyển hoặc phá hủy công trình)
    /// </summary>
    public void UnmarkAreaAsOccupied(Vector3 centerWorldPos, int sizeX = 1, int sizeZ = 1)
    {
        Vector3 snappedPos = GetSnappedGridPosition(centerWorldPos, sizeX, sizeZ);
        Vector2Int baseCoord = WorldToGridCoord(snappedPos);
        int startX = baseCoord.x - (sizeX / 2);
        int startZ = baseCoord.y - (sizeZ / 2);

        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                occupiedTiles.Remove(new Vector2Int(startX + x, startZ + z));
            }
        }
    }

    /// <summary>
    /// Cập nhật Mesh lưới: Ô trong unlockedTiles = XANH, ô còn lại = ĐỎ
    /// </summary>
    public void CreateOrUpdateGridMesh()
    {
        if (generatedGridOverlay == null)
        {
            generatedGridOverlay = new GameObject("GeneratedGridOverlay");
            generatedGridOverlay.transform.SetParent(transform);
            generatedGridOverlay.transform.localPosition = Vector3.zero;

            gridMeshFilter = generatedGridOverlay.AddComponent<MeshFilter>();
            gridMeshRenderer = generatedGridOverlay.AddComponent<MeshRenderer>();

            if (gridLineMaterial != null)
            {
                gridMeshRenderer.material = gridLineMaterial;
            }
            else
            {
                Material defaultMat = new Material(Shader.Find("Sprites/Default"));
                gridMeshRenderer.material = defaultMat;
            }
            generatedGridOverlay.SetActive(true);
        }

        List<Vector3> vertices = new List<Vector3>();
        List<Color> colors = new List<Color>();
        List<int> indices = new List<int>();
        int vIndex = 0;
        float yPos = plotSpawnY + gridYOffset;

        foreach (var tile in allGroundTiles)
        {
            Color currentCellColor = unlockedTiles.Contains(tile) ? buildableGridColor : nonBuildableGridColor;

            float minXPos = tile.x * tileSize;
            float maxXPos = (tile.x + 1) * tileSize;
            float minZPos = tile.y * tileSize;
            float maxZPos = (tile.y + 1) * tileSize;

            Vector3 p1 = new Vector3(minXPos, yPos, minZPos);
            Vector3 p2 = new Vector3(maxXPos, yPos, minZPos);
            Vector3 p3 = new Vector3(maxXPos, yPos, maxZPos);
            Vector3 p4 = new Vector3(minXPos, yPos, maxZPos);

            // 4 Cạnh ô vuông
            vertices.Add(p1); colors.Add(currentCellColor);
            vertices.Add(p2); colors.Add(currentCellColor);
            indices.Add(vIndex++); indices.Add(vIndex++);

            vertices.Add(p2); colors.Add(currentCellColor);
            vertices.Add(p3); colors.Add(currentCellColor);
            indices.Add(vIndex++); indices.Add(vIndex++);

            vertices.Add(p3); colors.Add(currentCellColor);
            vertices.Add(p4); colors.Add(currentCellColor);
            indices.Add(vIndex++); indices.Add(vIndex++);

            vertices.Add(p4); colors.Add(currentCellColor);
            vertices.Add(p1); colors.Add(currentCellColor);
            indices.Add(vIndex++); indices.Add(vIndex++);
        }

        Mesh gridMesh = new Mesh();
        gridMesh.name = "DynamicGridMesh";
        gridMesh.SetVertices(vertices);
        gridMesh.SetColors(colors);
        gridMesh.SetIndices(indices, MeshTopology.Lines, 0);

        gridMeshFilter.mesh = gridMesh;
    }

    private void RebuildFences()
    {
        foreach (var fence in activeFences) if (fence != null) Destroy(fence);
        activeFences.Clear();

        if (wallPrefab == null) return;

        GetGridBounds(out int minX, out int maxX, out int minZ, out int maxZ);
        float halfTile = tileSize / 2f;
        int centerNorthX = (minX + maxX) / 2;

        for (int x = minX; x <= maxX; x++)
        {
            Vector3 northPos = GetWorldPosition(x, maxZ) + new Vector3(halfTile, 0, tileSize);
            GameObject northPrefab = (x == centerNorthX && gatePrefab != null) ? gatePrefab : wallPrefab;
            SpawnWallSegment(northPrefab, northPos, 0f);

            Vector3 southPos = GetWorldPosition(x, minZ) + new Vector3(halfTile, 0, 0);
            SpawnWallSegment(wallPrefab, southPos, 180f);
        }

        for (int z = minZ; z <= maxZ; z++)
        {
            Vector3 eastPos = GetWorldPosition(maxX, z) + new Vector3(tileSize, 0, halfTile);
            SpawnWallSegment(wallPrefab, eastPos, 90f);

            Vector3 westPos = GetWorldPosition(minX, z) + new Vector3(0, 0, halfTile);
            SpawnWallSegment(wallPrefab, westPos, -90f);
        }
    }

    private void SpawnWallSegment(GameObject prefab, Vector3 pos, float rotationY)
    {
        GameObject wall = Instantiate(prefab, pos, Quaternion.Euler(0, rotationY, 0), transform);
        activeFences.Add(wall);
    }

    private void UpdateExpandButtonsPosition()
    {
        GetGridBounds(out int minX, out int maxX, out int minZ, out int maxZ);

        float centerX = (minX + maxX + 1) * tileSize / 2f;
        float centerZ = (minZ + maxZ + 1) * tileSize / 2f;
        float offset = 1.5f;

        if (btnNorth != null) btnNorth.position = new Vector3(centerX, 0.5f, (maxZ + 1) * tileSize + offset);
        if (btnSouth != null) btnSouth.position = new Vector3(centerX, 0.5f, minZ * tileSize - offset);
        if (btnEast != null)  btnEast.position  = new Vector3((maxX + 1) * tileSize + offset, 0.5f, centerZ);
        if (btnWest != null)  btnWest.position  = new Vector3(minX * tileSize - offset, 0.5f, centerZ);
    }

    public void ScanExistingBuildings()
    {
        if (existingBuildingsParent == null) return;
        occupiedTiles.Clear();

        // 1. Tính toán tọa độ TÂM của khu vực đã mở khóa
        GetGridBounds(out int minX, out int maxX, out int minZ, out int maxZ);
        Vector2Int centerCoord = new Vector2Int((minX + maxX) / 2, (minZ + maxZ) / 2);

        foreach (Transform child in existingBuildingsParent)
        {
            if (!child.CompareTag("Tower")) continue;

            int sizeX = 1, sizeZ = 1;
            Vector3 targetPos = GetSnappedGridPosition(child.position, sizeX, sizeZ);
            Vector2Int gridCoord = WorldToGridCoord(targetPos);

            bool isValidPos = unlockedTiles.Contains(gridCoord) && !occupiedTiles.Contains(gridCoord);

            if (!isValidPos)
            {
                Vector2Int bestCoord = centerCoord;
                float minDistance = float.MaxValue;
                bool foundAvailableTile = false;

                // 2. Tìm ô trống GẦN TÂM NHẤT thay vì gần vị trí cũ của công trình
                foreach (Vector2Int unlockedCoord in unlockedTiles)
                {
                    if (occupiedTiles.Contains(unlockedCoord)) continue;

                    // 🔥 THAY ĐỔI Ở ĐÂY: So sánh khoảng cách tới centerCoord
                    float distFromCenter = Vector2Int.Distance(centerCoord, unlockedCoord);
                    if (distFromCenter < minDistance)
                    {
                        minDistance = distFromCenter;
                        bestCoord = unlockedCoord;
                        foundAvailableTile = true;
                    }
                }

                if (foundAvailableTile)
                {
                    gridCoord = bestCoord;
                    Vector3 cellCenterPos = GetWorldPosition(gridCoord.x, gridCoord.y, plotSpawnY) + new Vector3(tileSize * 0.5f, 0, tileSize * 0.5f);
                    targetPos = GetSnappedGridPosition(cellCenterPos, sizeX, sizeZ);
                }
            }

            child.position = targetPos;
            MarkAreaAsOccupied(gridCoord, sizeX, sizeZ);
        }
    }

    private void MarkAreaAsOccupied(Vector2Int baseCoord, int sizeX, int sizeZ)
    {
        int startX = baseCoord.x - (sizeX / 2);
        int startZ = baseCoord.y - (sizeZ / 2);

        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                occupiedTiles.Add(new Vector2Int(startX + x, startZ + z));
            }
        }
    }

    public Vector3 GetSnappedGridPosition(Vector3 rawWorldPos, int sizeX = 1, int sizeZ = 1)
    {
        float snapX = (sizeX % 2 == 1) 
            ? Mathf.Floor(rawWorldPos.x / tileSize) * tileSize + (tileSize * 0.5f)
            : Mathf.Round(rawWorldPos.x / tileSize) * tileSize;

        float snapZ = (sizeZ % 2 == 1) 
            ? Mathf.Floor(rawWorldPos.z / tileSize) * tileSize + (tileSize * 0.5f)
            : Mathf.Round(rawWorldPos.z / tileSize) * tileSize;

        return new Vector3(snapX, plotSpawnY, snapZ);
    }

    public Vector2Int WorldToGridCoord(Vector3 worldPos)
    {
        return new Vector2Int(Mathf.FloorToInt(worldPos.x / tileSize), Mathf.FloorToInt(worldPos.z / tileSize));
    }

    public Vector3 GetWorldPosition(int x, int z, float yOffset = 0f)
    {
        return new Vector3(x * tileSize, yOffset, z * tileSize);
    }

    public void GetGridBounds(out int minX, out int maxX, out int minZ, out int maxZ)
    {
        minX = int.MaxValue; maxX = int.MinValue;
        minZ = int.MaxValue; maxZ = int.MinValue;

        foreach (var tile in unlockedTiles)
        {
            if (tile.x < minX) minX = tile.x;
            if (tile.x > maxX) maxX = tile.x;
            if (tile.y < minZ) minZ = tile.y;
            if (tile.y > maxZ) maxZ = tile.y;
        }

        if (unlockedTiles.Count == 0)
        {
            minX = maxX = minZ = maxZ = 0;
        }
    }

    public void SetGridVisualActive(bool active)
    {
        if (generatedGridOverlay != null) generatedGridOverlay.SetActive(active);
    }
}