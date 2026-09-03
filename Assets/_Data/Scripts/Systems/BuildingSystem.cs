using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/*
 * BuildingSystem.cs
 * Folder: Scripts/Building/
 * Dự án: KHẨN HOANG (PENTA DEV)
 */

public class BuildingSystem : Singleton<BuildingSystem>
{
    [Header("Ghost Prefabs – Dân sự")]
    public GameObject ghostHousePrefab;
    public GameObject ghostWoodCutterPrefab;
    public GameObject ghostStoneMinePrefab;
    public GameObject ghostKitchenPrefab;
    public GameObject ghostFoodStoragePrefab;
    public GameObject ghostStoneStoragePrefab;
    public GameObject ghostWarehousePrefab;

    [Header("Ghost Prefabs – Phòng thủ")]
    public GameObject ghostWatchTowerPrefab;
    public GameObject ghostArcherTowerPrefab;
    public GameObject ghostCannonPrefab;

    [Header("Ghost Prefabs – Quân sự (Nhà lính)")]
    public GameObject ghostBarracksMeleePrefab;
    public GameObject ghostBarracksArcherPrefab;
    public GameObject ghostBarracksSpearPrefab;

    [Header("Ghost Prefabs – Tài nguyên")]
    public GameObject ghostWoodPrefab;
    public GameObject ghostRicePrefab;
    public GameObject ghostStonePrefab;

    [Header("Demacia Rising – Khung chọn ô đất")]
    public GameObject slotHighlightPrefab;
    private GameObject slotHighlightInstance;
    private Vector3 selectedSlotPos;
    private bool hasSelectedSlot = false;

    public bool HasSelectedSlot => hasSelectedSlot;
    public Vector3 SelectedSlotPos => selectedSlotPos;

    private GhostBuilding currentGhost;
    private bool isPlacing = false;

    private UpgradeableBuilding _movingBuilding = null; 
    private bool _isMovingMode = false;

    public bool IsPlacing => isPlacing;
    public bool IsMovingMode => _isMovingMode;

    private void Update()
    {
        if (_isMovingMode)
        {
            HandlePlacementInput();
            return;
        }

        HandleSlotSelectionInput();
    }

    private void HandleSlotSelectionInput()
    {
        // MoveModeController là hệ thống duy nhất xử lý click vùng đích khi
        // đang điều quân. Nếu tiếp tục xử lý ở đây, click vùng đích sẽ mở nhầm
        // Settlement UI của vùng đó.
        if (MoveModeController.IsMoveModeActive) return;

        // Phím chuột phải hoặc ESC để bỏ chọn ô đất và đóng toàn bộ giao diện
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            DeselectSlot();
            if (UIManager.Ins != null) UIManager.Ins.CloseAllActiveWindows();
            return;
        }

        // Click chuột trái vào bản đồ 3D
        if (Input.GetMouseButtonUp(0))
        {
            if (RTSCameraController.IsMouseDragging || RTSCameraController.WasMouseDragThisPress) return;

            // Bỏ qua nếu bấm vào các phần tử UI Canvas
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (currentGhost != null) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                SettlementZone zone = hit.collider.GetComponentInParent<SettlementZone>();
                UpgradeableBuilding building = hit.collider.GetComponentInParent<UpgradeableBuilding>();

                if (building != null || zone != null)
                {
                    // Click vào công trình hoặc vùng đất 3D: Chọn vùng đất đó & Mở Bảng Thủ Đô (SettlementSidePanelUI)
                    SettlementZone targetZone = (zone != null) ? zone : building.GetComponentInParent<SettlementZone>();

                    if (targetZone != null && !targetZone.IsConquered)
                    {
                        // Không cho click vùng chưa chinh phục mở nhầm panel của
                        // settlement trước đó hoặc hiện nút Điều quân.
                        SettlementSidePanelUI.Ins?.SetMoveButtonVisible(false);
                        UIManager.Ins?.CloseSettlementPanel();
                        return;
                    }

                    // Khi đã mở bảng của thành này, click lại tòa thành chỉ giữ
                    // lựa chọn hiện tại. Không gọi SelectSettlement/OpenSettlementPanel
                    // lần nữa để camera và VFX không bị reset/chớp.
                    bool isCurrentSettlement = SettlementManager.Ins != null &&
                                               SettlementManager.Ins.CurrentSettlement == targetZone;
                    bool isSettlementPanelOpen = BuildTrainingUIManager.Ins != null &&
                                                 BuildTrainingUIManager.Ins.IsSettlementPanelVisible;
                    bool clickedTownObject = building != null ||
                                             hit.collider.GetComponentInParent<SettlementZoneClickHandler>() != null;
                    if (isCurrentSettlement && isSettlementPanelOpen && clickedTownObject)
                    {
                        return;
                    }

                    if (targetZone != null && SettlementManager.Ins != null)
                    {
                        SettlementManager.Ins.SelectSettlement(targetZone);
                    }
                    SelectSlot(hit.point);
                    if (UIManager.Ins != null) UIManager.Ins.OpenSettlementPanel();
                }
                else
                {
                    // Click ra KHOẢNG KHÔNG / ĐẤT TRỐNG OUTSIDE: Bỏ chọn slot & Đóng TOÀN BỘ giao diện (bao gồm SettlementSidePanel)!
                    DeselectSlot();
                    if (UIManager.Ins != null) UIManager.Ins.CloseAllActiveWindows();
                }
            }
        }
    }

    public void SelectSlot(Vector3 worldPos)
    {
        selectedSlotPos = worldPos;
        hasSelectedSlot = true;

        if (slotHighlightPrefab != null)
        {
            if (slotHighlightInstance == null)
            {
                slotHighlightInstance = Instantiate(slotHighlightPrefab);
            }
            slotHighlightInstance.transform.position = worldPos;
            slotHighlightInstance.SetActive(true);
        }
    }

    public void DeselectSlot()
    {
        hasSelectedSlot = false;
        if (slotHighlightInstance != null)
        {
            slotHighlightInstance.SetActive(false);
        }
    }

    public void StartPlacing(BuildingType type)
    {
        if (type == BuildingType.None) return;
        if (TroopTrainingManager.IsCentralBarracksType(type))
        {
            UIManager.Ins?.ShowWarning("Trại Lính chỉ có sẵn tại thành đầu tiên và không thể xây thêm!");
            return;
        }

        if (!SettlementZone.IsBuildingTypeUnlockedGlobally(type))
        {
            UIManager.Ins?.ShowWarning("Công trình này chưa được mở khóa. Hãy chinh phục vùng đất tương ứng trước!");
            return;
        }

        // 🔥 DEMACIA RISING STYLE: NẾU ĐÃ CHỌN Ô ĐẤT, XÂY TRỰC TIẾP TẠI Ô ĐẤT ĐÓ
        if (hasSelectedSlot)
        {
            ConstructionManager.Ins.PlaceBuilding(type, selectedSlotPos, Quaternion.identity);
            DeselectSlot();
            if (UIManager.Ins != null) UIManager.Ins.CloseBuildMenu();
            return;
        }

        // FALLBACK: NẾU CHƯA CHỌN Ô ĐẤT, KÍCH HOẠT CHẾ ĐỘ RÊ CHUỘT GHOST CŨ
        StartPlacingGhost(type);
    }

    public void StartPlacingGhost(BuildingType type)
    {
        if (TroopTrainingManager.IsCentralBarracksType(type))
        {
            UIManager.Ins?.ShowWarning("Trại Lính chỉ có sẵn tại thành đầu tiên và không thể xây thêm!");
            return;
        }

        if (!SettlementZone.IsBuildingTypeUnlockedGlobally(type))
        {
            UIManager.Ins?.ShowWarning("Công trình này chưa được mở khóa. Hãy chinh phục vùng đất tương ứng trước!");
            return;
        }

        if (_isMovingMode) CancelMoving();
        else CancelPlacing();

        GameObject prefab = GetGhostPrefab(type);
        if (prefab == null) return;

        GameObject obj = Instantiate(prefab);
        currentGhost = obj.GetComponent<GhostBuilding>();

        if (currentGhost == null)
        {
            Destroy(obj);
            return;
        }

        currentGhost.buildingType = type;
        currentGhost.InstantSnapToMouse();

        // 🔥 CẬP NHẬT TUTORIAL: Báo cho Tutorial Manager người chơi đã bắt đầu chế độ đặt nhà
        if (CampaignTutorialManager.Ins != null)
        {
            CampaignTutorialManager.Ins.OnStartPlacement();
        }

        if (UIManager.Ins != null)
        {
            UIManager.Ins.EnterPlacementMode();
        }
    }

    public void CancelPlacing()
    {
        if (currentGhost != null)
        {
            Destroy(currentGhost.gameObject);
            currentGhost = null;
        }

        // 🔥 CẬP NHẬT TUTORIAL: Báo cho Tutorial Manager khi hủy đặt nhà
        if (CampaignTutorialManager.Ins != null)
        {
            CampaignTutorialManager.Ins.OnCancelPlacement();
        }

        if (UIManager.Ins != null)
        {
            UIManager.Ins.ExitPlacementMode(true);
        }
    }

    public void OnPlacingCompleted(bool shouldReopenMenu)
    {
        currentGhost = null;

        if (UIManager.Ins != null)
        {
            UIManager.Ins.ExitPlacementMode(shouldReopenMenu);
        }
    }

    public void StartMoving(UpgradeableBuilding building)
    {
        if (building == null) return;

        if (isPlacing) CancelPlacing();
        if (_isMovingMode) CancelMoving();

        _movingBuilding = building;
        _isMovingMode = true;

        _movingBuilding.PauseBuildingProcess();
        _movingBuilding.gameObject.SetActive(false);

        BuildingType currentType = building.buildingType; 

        GameObject prefab = GetGhostPrefab(currentType);
        if (prefab != null)
        {
            GameObject obj = Instantiate(prefab, building.transform.position, building.transform.rotation);
            currentGhost = obj.GetComponent<GhostBuilding>();
            if (currentGhost != null)
            {
                currentGhost.buildingType = currentType;
                currentGhost.SetGhostLevel(building.CurrentLevel);
                currentGhost.InstantSnapToMouse();
            }
        }

        if (UIManager.Ins != null)
        {
            UIManager.Ins.EnterPlacementMode();
        }
    }

    private void HandlePlacementInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (currentGhost == null || _movingBuilding == null) return;

            bool isValidPosition = currentGhost != null && currentGhost.isValid; 
            if (isValidPosition)
            {
                Vector3 newPosition = currentGhost.transform.position;
                Quaternion newRotation = currentGhost.transform.rotation;

                _movingBuilding.transform.position = newPosition;
                _movingBuilding.transform.rotation = newRotation;
                _movingBuilding.gameObject.SetActive(true);

                _movingBuilding.ResumeBuildingProcess();

                if (currentGhost != null)
                {
                    Destroy(currentGhost.gameObject);
                    currentGhost = null;
                }

                SaveBuildingsToSlot(1);
                EndMovingMode();
            }
            else
            {
                if (UIManager.Ins != null) 
                    UIManager.Ins.ShowWarning("Vị trí mới bị cản trở bởi vật thể khác, không thể đặt nhà!");
            }
        }

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelMoving();
        }
    }

    private void EndMovingMode()
    {
        _isMovingMode = false;
        _movingBuilding = null;
        currentGhost = null;

        if (UIManager.Ins != null)
        {
            UIManager.Ins.ExitPlacementMode(false);
        }
    }

    private void CancelMoving()
    {
        if (currentGhost != null)
        {
            Destroy(currentGhost.gameObject);
            currentGhost = null;
        }

        if (_movingBuilding != null)
        {
            _movingBuilding.gameObject.SetActive(true);
            _movingBuilding.ResumeBuildingProcess();
        }

        EndMovingMode();
    }

    public void SaveBuildings() => SaveBuildingsToSlot(1);
    public void LoadBuildings() => LoadBuildingsFromSlot(1);

    public void SaveBuildingsToSlot(int slotIndex)
    {
        if (BuildingManager.Ins == null) return;
        var states = BuildingManager.Ins.GetAllStates();

        DayNightManager dayNightManager = UnityEngine.Object.FindFirstObjectByType<DayNightManager>();

        var saveData = new JsonDataManager.GameSaveData
        {
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            savedAtUnix = System.DateTimeOffset.Now.ToUnixTimeSeconds(),
            buildings = states,
            resources = new System.Collections.Generic.List<JsonDataManager.ResourceData>(),
            currentWave = dayNightManager != null ? dayNightManager.CurrentWave : 0,
            waveState = dayNightManager != null ? (int)dayNightManager.CurrentWaveState : (int)DayNightManager.WaveState.Preparation,
            isWaveActive = dayNightManager != null && dayNightManager.IsWaveActive,
            waveTimer = dayNightManager != null ? dayNightManager.CurrentTimer : 0f
        };

        JsonDataManager.Ins.SaveGame(slotIndex, saveData);
    }

    /// <summary>
    /// Nạp lại công trình từ save slot. Trả về false khi các manager hoặc file
    /// save chưa sẵn sàng, để luồng chuyển SceneBattle không ghi đè save bằng
    /// scene mặc định trước khi dữ liệu được khôi phục xong.
    /// </summary>
    public bool TryLoadBuildingsFromSlot(int slotIndex)
    {
        JsonDataManager dataManager = JsonDataManager.Ins;
        BuildingManager buildingManager = BuildingManager.Ins;
        if (dataManager == null || buildingManager == null)
        {
            Debug.LogWarning("[BuildingSystem] Chưa đủ manager để nạp công trình.");
            return false;
        }

        var saveData = dataManager.LoadGame(slotIndex);

        if (saveData == null) return false;

        DayNightManager dayNightManager = UnityEngine.Object.FindFirstObjectByType<DayNightManager>();
        if (dayNightManager != null)
        {
            DayNightManager.WaveState waveState = saveData.waveState == (int)DayNightManager.WaveState.Combat
                ? DayNightManager.WaveState.Combat
                : DayNightManager.WaveState.Preparation;

            dayNightManager.RestoreWaveState(
                Mathf.Max(0, saveData.currentWave),
                waveState,
                saveData.isWaveActive,
                Mathf.Max(0f, saveData.waveTimer));
        }

        if (saveData.buildings == null || saveData.buildings.Count == 0)
        {
            Debug.Log($"[BuildingSystem] Save slot {slotIndex} không có công trình cần khôi phục.");
            return true;
        }

        buildingManager.LoadStates(saveData.buildings);
        Debug.Log($"[BuildingSystem] Đã khôi phục {saveData.buildings.Count} công trình từ save slot {slotIndex}.");
        return true;
    }

    // Giữ API void cũ cho các Button/Event trong Inspector và các script hiện có.
    public void LoadBuildingsFromSlot(int slotIndex) => TryLoadBuildingsFromSlot(slotIndex);

    private GameObject GetGhostPrefab(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.House: return ghostHousePrefab;
            case BuildingType.WoodCutter: return ghostWoodCutterPrefab;
            case BuildingType.StoneMine: return ghostStoneMinePrefab;
            case BuildingType.Kitchen: return ghostKitchenPrefab;
            case BuildingType.FoodStorage: return ghostFoodStoragePrefab;
            case BuildingType.StoneStorage: return ghostStoneStoragePrefab;
            case BuildingType.Warehouse: return ghostWarehousePrefab;
            case BuildingType.WatchTower: return ghostWatchTowerPrefab;
            case BuildingType.ArcherTower: return ghostArcherTowerPrefab;
            case BuildingType.Cannon: return ghostCannonPrefab;
            case BuildingType.BarracksMelee: return ghostBarracksMeleePrefab;
            case BuildingType.BarracksArcher: return ghostBarracksArcherPrefab;
            case BuildingType.BarracksSpear: return ghostBarracksSpearPrefab;
            case BuildingType.Wood: return ghostWoodPrefab;
            case BuildingType.Rice: return ghostRicePrefab;
            case BuildingType.Stone: return ghostStonePrefab;
            default: return null;
        }
    }
}
