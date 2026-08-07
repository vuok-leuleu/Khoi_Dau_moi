using System.Collections.Generic;
using UnityEngine;

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
    public float TileSize => tileSize;
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

    private void Awake()
    {
        if (Ins == null) Ins = this;
        else Destroy(gameObject);

        InitializeGridSystem();
    }

    private void Start()
    {
        if (unlockedTiles == null || unlockedTiles.Count == 0)
        {
            InitializeGridSystem();
        }
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
    public void ExpandGrid(ExpandDirection direction)
    {
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

        // Cập nhật lại giao diện sau khi mở rộng
        RebuildFences();
        UpdateExpandButtonsPosition();
        CreateOrUpdateGridMesh();

        // Thông báo cho Tutorial Manager
        if (CampaignTutorialManager.Ins != null)
        {
            CampaignTutorialManager.Ins.OnLandExpanded();
        }
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
        Vector3 localPos = worldPos - transform.position;
        return new Vector2Int(Mathf.FloorToInt(localPos.x / tileSize), Mathf.FloorToInt(localPos.z / tileSize));
    }

    public Vector3 GetWorldPosition(int x, int z, float yOffset = 0f)
    {
        return transform.position + new Vector3(x * tileSize, yOffset, z * tileSize);
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

    /// <summary>
    /// Giới hạn một vị trí thế giới (Vector3) luôn nằm bên trong VÙNG ĐẤT ĐÃ MỞ KHÓA (LƯỚI XANH)
    /// </summary>
    public Vector3 ClampToUnlockedArea(Vector3 worldPos, float padding = 1.0f)
    {
        if (unlockedTiles == null || unlockedTiles.Count == 0)
        {
            InitializeGridSystem();
        }

        if (unlockedTiles == null || unlockedTiles.Count == 0) return worldPos;

        GetGridBounds(out int minX, out int maxX, out int minZ, out int maxZ);

        Vector3 origin = transform.position;
        float minXWorld = origin.x + minX * tileSize + padding;
        float maxXWorld = origin.x + (maxX + 1) * tileSize - padding;
        float minZWorld = origin.z + minZ * tileSize + padding;
        float maxZWorld = origin.z + (maxZ + 1) * tileSize - padding;

        float clampedX = Mathf.Clamp(worldPos.x, minXWorld, maxXWorld);
        float clampedZ = Mathf.Clamp(worldPos.z, minZWorld, maxZWorld);

        return new Vector3(clampedX, worldPos.y, clampedZ);
    }

    /// <summary>
    /// Kiểm tra xem một vị trí thế giới bất kỳ có nằm trong vùng ĐÃ MỞ KHÓA hay không
    /// </summary>
    public bool IsWorldPositionUnlocked(Vector3 worldPos)
    {
        if (unlockedTiles == null || unlockedTiles.Count == 0)
        {
            InitializeGridSystem();
        }
        Vector2Int gridCoord = WorldToGridCoord(worldPos);
        return unlockedTiles != null && unlockedTiles.Contains(gridCoord);
    }
}