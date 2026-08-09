using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class UpgradeableBuilding : MonoBehaviour
{
    [System.Serializable]
    public struct UpgradeCost
    {
        public int woodCost;
        public int stoneCost;
        public int foodCost;
        public int upgradeDuration;
    }

    [Header("Penta Dev - Khởi Tạo Xây Dựng Ban Đầu")]
    [Tooltip("TÍCH VÀO: Công trình chưa xây. Khi vừa chạy game sẽ ép chạy thời gian, VFX, SFX như nâng cấp.\nTẮT TÍCH: Công trình đã xây xong từ trước, vào game sẽ không lặp lại.")]
    [SerializeField] private bool isInitialBuildNeeded = true;

    [Tooltip("TÍCH VÀO: Khi vừa mở Game, nhà sẽ tự động nằm ở trạng thái PHÁ HỦY/TÀN TÍCH và cần được SỬA CHỮA.")]
    [SerializeField] private bool startAsRuined = false;

    public bool StartAsRuined => startAsRuined;

    [Tooltip("Thời gian để hoàn thành việc xây dựng công trình này lần đầu tiên (tính bằng Wave/Ngày)")]
    [SerializeField] private int initialBuildDuration = 2;

    public bool IsInitialBuildNeeded => isInitialBuildNeeded;

    [Header("Loại công trình")]
    public BuildingType buildingType;

    [Header("Tên công trình")]
    public string buildingName = "Công trình";

    [Header("Mảng chứa các Model Cấp 1, 2, 3...")]
    [SerializeField] private GameObject[] visualModels;

    [Header("Cấu hình chi phí nâng cấp (Phần tử 0 là từ Lv1 -> Lv2)")]
    [SerializeField] private UpgradeCost[] upgradeCosts;

    public int CurrentLevel { get; private set; } = 0;
    public int MaxLevel => visualModels != null ? visualModels.Length : 0;

    private GameObject[] instantiatedModels;
    public GameObject[] VisualModels => (instantiatedModels != null && instantiatedModels.Length > 0) ? instantiatedModels : visualModels;

    public bool IsUpgrading { get; private set; } = false;

    public event System.Action OnUpgradeStart;
    public event System.Action OnUpgradeComplete;
    public event System.Action OnLevelChanged;

    [Header("Quản lý Code AI của từng Cấp độ (Kéo các Script tương ứng vào đây)")]
    [SerializeField] private AttackTowerAI[] towerLevelScripts;
    public AttackTowerAI[] TowerLevelScripts => towerLevelScripts;

    [Header("Penta Dev - Quản lý Cấp độ Công trình Dân sự")]
    [SerializeField] private WoodStorage[] woodStorageLevels;
    [SerializeField] private StoneStorage[] stoneStorageLevels;
    [SerializeField] private RiceStorage[] riceStorageLevels;
    [SerializeField] private Kitchen[] kitchenLevels;
    [SerializeField] private House[] houseLevels;

    public WoodStorage[] WoodStorageLevels => woodStorageLevels;
    public StoneStorage[] StoneStorageLevels => stoneStorageLevels;
    public RiceStorage[] RiceStorageLevels => riceStorageLevels;
    public Kitchen[] KitchenLevels => kitchenLevels;
    public House[] HouseLevels => houseLevels;

    private System.Collections.Generic.List<GameObject> originalChildren = new System.Collections.Generic.List<GameObject>();
    private MeshRenderer rootRendererComponent;
    private SkinnedMeshRenderer rootSkinnedRendererComponent;
    private int selfRefIndex = -1;

    [Header("Mảng chứa các Icon hiển thị trên UI tương ứng từng Cấp")]
    [SerializeField] private Sprite[] buildingIcons;
    public Sprite[] BuildingIcons => buildingIcons;

    [Header("Penta Dev - Giao Diện Tàn Tích")]
    [Tooltip("Kéo Model nhà nát (Xác nhà đổ nát) vào đây")]
    [SerializeField] private GameObject ruinedVisualModel;

    [Header("Penta Dev - Chi Phí Sửa Chữa")]
    [SerializeField] private int repairWoodCost = 30;
    [SerializeField] private int repairStoneCost = 30;
    [SerializeField] private float repairDuration = 2f;

    public bool IsRuined { get; private set; } = false;

    private float currentProcessTimer = 0f;
    private float currentProcessDuration = 0f;
    private Coroutine currentProcessCoroutine = null;

    private enum ProcessType { None, BuildOrUpgrade, Repair }
    private ProcessType activeProcessType = ProcessType.None;

    private void Awake()
    {
        if (transform.parent != null && transform.parent.GetComponentInParent<UpgradeableBuilding>() != null)
        {
            var attackAI = GetComponent<AttackTowerAI>();
            if (attackAI != null) attackAI.enabled = false;

            var defenceAI = GetComponent<DefenceTowerAI>();
            if (defenceAI != null) defenceAI.enabled = false;

            var spawnSoldier = GetComponent<SpawnSoldier>();
            if (spawnSoldier != null) spawnSoldier.enabled = false;

            enabled = false;
        }
    }

    private static UpgradeableBuilding selectedInstance = null;

    public void SelectThisBuilding()
    {
        selectedInstance = this;
        Debug.Log($"[UpgradeableBuilding] Selected {buildingName} for debug upgrade");
    }

    private void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        BuildingUpgradeUI localUI = GetComponentInChildren<BuildingUpgradeUI>(true);
        if (localUI != null && localUI.IsOpen) return;

        SelectThisBuilding();
        if (localUI != null) localUI.OpenUI();
    }

    private void SaveOriginalVisuals()
    {
        if (originalChildren.Count > 0 || rootRendererComponent != null || rootSkinnedRendererComponent != null) return;

        rootRendererComponent = GetComponent<MeshRenderer>();
        rootSkinnedRendererComponent = GetComponent<SkinnedMeshRenderer>();

        foreach (Transform child in transform)
        {
            if (ruinedVisualModel != null && child.gameObject == ruinedVisualModel) continue;
            bool isOtherVisualModel = false;
            if (visualModels != null)
            {
                for (int j = 0; j < visualModels.Length; j++)
                {
                    if (visualModels[j] != gameObject && visualModels[j] == child.gameObject)
                    {
                        isOtherVisualModel = true;
                        break;
                    }
                }
            }
            if (!isOtherVisualModel) originalChildren.Add(child.gameObject);
        }
    }

    private void EnsureRenderersEnabled(GameObject model)
    {
        if (model == null) return;
        foreach (var mr in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr != null) mr.enabled = true;
        }
        foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr != null) smr.enabled = true;
        }
    }

    private void SetOriginalLevelActive(bool active)
    {
        if (rootRendererComponent != null) rootRendererComponent.enabled = active;
        if (rootSkinnedRendererComponent != null) rootSkinnedRendererComponent.enabled = active;

        for (int i = 0; i < originalChildren.Count; i++)
        {
            if (originalChildren[i] != null)
            {
                originalChildren[i].SetActive(active);
                if (active) EnsureRenderersEnabled(originalChildren[i]);
            }
        }
    }

    private void UpdateFirePointForLevel()
    {
        var attackAI = GetComponent<AttackTowerAI>();
        if (attackAI == null) return;

        Transform fp = null;
        if (CurrentLevel == selfRefIndex)
        {
            for (int i = 0; i < originalChildren.Count; i++)
            {
                if (originalChildren[i] != null)
                {
                    fp = FindFirePointRecursive(originalChildren[i].transform);
                    if (fp != null) break;
                }
            }
        }
        else
        {
            if (instantiatedModels != null && CurrentLevel >= 0 && CurrentLevel < instantiatedModels.Length)
            {
                GameObject activeModel = instantiatedModels[CurrentLevel];
                if (activeModel != null) fp = FindFirePointRecursive(activeModel.transform);
            }
        }

        if (fp != null)
        {
            attackAI.firePoint = fp;
            Debug.Log($"[UpgradeableBuilding] Updated attackAI.firePoint to: {fp.name} on Level {CurrentLevel + 1}");
        }
    }

    private Transform FindFirePointRecursive(Transform parent)
    {
        string nameLower = parent.name.ToLower();
        if (nameLower.Contains("firepoint") || nameLower.Contains("muzzle") || nameLower.Contains("spawn") || nameLower.Contains("shoot"))
            return parent;

        foreach (Transform child in parent)
        {
            Transform found = FindFirePointRecursive(child);
            if (found != null) return found;
        }

        return null;
    }

    public void InitializeModels()
    {
        if (instantiatedModels != null) return;
        if (visualModels == null) return;

        instantiatedModels = new GameObject[visualModels.Length];
        selfRefIndex = -1;

        for (int i = 0; i < visualModels.Length; i++)
        {
            if (visualModels[i] == gameObject)
            {
                selfRefIndex = i;
                break;
            }
        }

        if (selfRefIndex != -1)
        {
            SaveOriginalVisuals();
            instantiatedModels[selfRefIndex] = gameObject;
        }

        for (int i = 0; i < visualModels.Length; i++)
        {
            if (i == selfRefIndex) continue;

            GameObject modelSource = visualModels[i];
            if (modelSource == null) continue;

            if (!modelSource.scene.IsValid() || string.IsNullOrEmpty(modelSource.scene.name))
            {
                GameObject newInstance = Instantiate(modelSource, transform.position, transform.rotation, transform);
                newInstance.name = modelSource.name;
                instantiatedModels[i] = newInstance;
                newInstance.SetActive(i == CurrentLevel);
                if (i == CurrentLevel) EnsureRenderersEnabled(newInstance);
            }
            else
            {
                instantiatedModels[i] = modelSource;
                modelSource.SetActive(i == CurrentLevel);
                if (i == CurrentLevel) EnsureRenderersEnabled(modelSource);
            }
        }

        if (selfRefIndex == -1)
        {
            MeshRenderer rootRenderer = GetComponent<MeshRenderer>();
            if (rootRenderer != null) rootRenderer.enabled = false;
            SkinnedMeshRenderer rootSkinnedRenderer = GetComponent<SkinnedMeshRenderer>();
            if (rootSkinnedRenderer != null) rootSkinnedRenderer.enabled = false;
        }
    }

    private void Start()
    {
        InitializeModels();
        UpdateVisualModel();

        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            if (col.gameObject.GetComponent<ClickHelper>() == null)
            {
                ClickHelper helper = col.gameObject.AddComponent<ClickHelper>();
                helper.parentBuilding = this;
            }
        }

        if (startAsRuined)
        {
            isInitialBuildNeeded = false;
            TriggerDestructionSequence();
        }
        else if (isInitialBuildNeeded)
        {
            ToggleBuildingLogic(false);
            IsUpgrading = true;
            currentProcessCoroutine = StartCoroutine(UpgradeRoutine(initialBuildDuration));
        }
        else
        {
            // 🔥 FIX: Công trình đặt sẵn lành lặn -> Kích hoạt AI bắn VÀ Reset HP đầy đủ để Enemy nhận diện làm mục tiêu!
            ToggleBuildingLogic(true);

            HPTower hpComponent = GetComponent<HPTower>();
            if (hpComponent != null)
            {
                hpComponent.ResetHealth();
            }
        }

        UpdateCivilianBuildingData();
    }

    public UpgradeCost GetNextUpgradeCost()
    {
        if (upgradeCosts != null && CurrentLevel < upgradeCosts.Length)
            return upgradeCosts[CurrentLevel];
        return new UpgradeCost { woodCost = 0, stoneCost = 0, foodCost = 0, upgradeDuration = 1 };
    }

    public void StartUpgradeProcess()
    {
        if (IsUpgrading || CurrentLevel >= MaxLevel - 1) return;

        UpgradeCost nextCost = GetNextUpgradeCost();
        int duration = nextCost.upgradeDuration;

        if (buildingType == BuildingType.BarracksMelee || 
            buildingType == BuildingType.BarracksArcher || 
            buildingType == BuildingType.BarracksSpear || 
            duration <= 0)
        {
            duration = 1;
        }

        currentProcessCoroutine = StartCoroutine(UpgradeRoutine(duration));
    }

    private IEnumerator UpgradeRoutine(int durationWaves, int startWavesPassed = 0)
    {
        IsUpgrading = true;
        activeProcessType = ProcessType.BuildOrUpgrade;
        currentProcessDuration = durationWaves;
        currentProcessTimer = startWavesPassed;

        OnUpgradeStart?.Invoke();
        if (CampaignTutorialManager.Ins != null)
        {
            CampaignTutorialManager.Ins.OnBuildingUpgradeStarted(this);
        }

        if (DayNightManager.Ins != null)
        {
            int startWave = DayNightManager.Ins.CurrentWave - startWavesPassed;
            
            while (currentProcessTimer < durationWaves)
            {
                if (DayNightManager.Ins == null) break;

                currentProcessTimer = DayNightManager.Ins.CurrentWave - startWave;
                if (currentProcessTimer < 0) currentProcessTimer = 0;

                var targetProgressUI = BuildingProgressBridge.GetUI(this);
                if (targetProgressUI != null) targetProgressUI.UpdateProgress(currentProcessTimer, durationWaves, true);
                
                if (currentProcessTimer >= durationWaves)
                    break;
                    
                yield return null;
            }
        }
        else
        {
            // Fallback nếu không có DayNightManager
            while (currentProcessTimer < durationWaves)
            {
                currentProcessTimer += Time.deltaTime / 10f; // 10s tượng trưng 1 wave
                var targetProgressUI = BuildingProgressBridge.GetUI(this);
                if (targetProgressUI != null) targetProgressUI.UpdateProgress(currentProcessTimer, durationWaves, true);
                yield return null;
            }
        }

        activeProcessType = ProcessType.None;
        currentProcessTimer = 0f;
        currentProcessDuration = 0f;
        currentProcessCoroutine = null;

        if (isInitialBuildNeeded)
        {
            isInitialBuildNeeded = false;
            IsUpgrading = false;
            
            ToggleBuildingLogic(true);

            HPTower hpComponent = GetComponent<HPTower>();
            if (hpComponent != null) hpComponent.ResetHealth();

            OnUpgradeComplete?.Invoke();
            OnLevelChanged?.Invoke();

            // 👈 DÁN 4 DÒNG CODE VÀO ĐÚNG VỊ TRÍ NÀY
            if (CampaignTutorialManager.Ins != null)
            {
                CampaignTutorialManager.Ins.OnBuildingConstructionFinished(buildingType);
            }

            var targetUI = BuildingProgressBridge.GetUI(this);
            if (targetUI != null) targetUI.HandleCompleteSequence();

            var buildingCtrl = GetComponent<BuildingCtrl>();
            if (buildingCtrl != null) buildingCtrl.AddProgress(1f);
        }
        else
        {
            IsUpgrading = false;
            OnUpgradeComplete?.Invoke();
            ExecuteLevelUp();

            var targetUI = BuildingProgressBridge.GetUI(this);
            if (targetUI != null) targetUI.HandleCompleteSequence();
        }

        if (UIManager.Ins != null) UIManager.Ins.RefreshUpgradePanel(this);
    }

    public void HideAllVisualModels()
    {
        InitializeModels();
        if (instantiatedModels != null)
        {
            for (int i = 0; i < instantiatedModels.Length; i++)
            {
                if (instantiatedModels[i] != null && instantiatedModels[i] != gameObject)
                    instantiatedModels[i].SetActive(false);
            }
        }
        SetOriginalLevelActive(false);
    }

    public void TriggerDestructionSequence()
    {
        IsRuined = true;
        HideAllVisualModels();

        if (ruinedVisualModel != null) ruinedVisualModel.SetActive(true);
        ToggleBuildingLogic(false);

        HPTower hpComponent = GetComponent<HPTower>();
        if (hpComponent != null) hpComponent.SetRuinedHealth();
    }

    public void StartRepair()
    {
        if (!IsRuined || IsUpgrading) return;
        if (JsonDataManager.Ins == null) return;

        bool spent = JsonDataManager.Ins.TrySpendCombined(woodCost: repairWoodCost, stoneCost: repairStoneCost);
        if (!spent) return;

        StartCoroutine(RepairRoutine());
    }

    private IEnumerator RepairRoutine(float startTimer = 0f)
    {
        IsUpgrading = true;
        activeProcessType = ProcessType.Repair;
        currentProcessDuration = repairDuration;
        currentProcessTimer = startTimer;

        var targetProgressUI = BuildingProgressBridge.GetUI(this);

        while (currentProcessTimer < repairDuration)
        {
            currentProcessTimer += Time.deltaTime;
            if (targetProgressUI != null) targetProgressUI.UpdateProgress(currentProcessTimer, repairDuration, false);
            yield return null;
        }

        activeProcessType = ProcessType.None;
        currentProcessTimer = 0f;
        currentProcessDuration = 0f;
        currentProcessCoroutine = null;

        IsUpgrading = false;
        IsRuined = false;
        
        // 🔥 FIX: Bỏ luôn dấu tích startAsRuined khi sửa xong để đồng bộ với Inspector & Enemy AI!
        startAsRuined = false;

        if (CampaignTutorialManager.Ins != null) CampaignTutorialManager.Ins.OnBuildingRepaired(this);

        HPTower hpComponent = GetComponent<HPTower>();
        if (hpComponent != null) hpComponent.ResetHealth();

        if (ruinedVisualModel != null) ruinedVisualModel.SetActive(false);

        UpdateVisualModel();
        ToggleBuildingLogic(true);

        if (targetProgressUI != null) targetProgressUI.HandleCompleteSequence();
        if (UIManager.Ins != null) UIManager.Ins.RefreshUpgradePanel(this);
    }

    public void ToggleBuildingLogic(bool active)
    {
        if (towerLevelScripts != null)
        {
            foreach (var towerScript in towerLevelScripts)
            {
                if (towerScript != null) towerScript.enabled = active;
            }
        }
    }

    [ContextMenu("⚡ Nâng cấp Tháp này")]
    public void ExecuteLevelUp()
    {
        if (CurrentLevel < MaxLevel - 1)
        {
            SetActiveModel(CurrentLevel, false);
            CurrentLevel++;
            UpdateCivilianBuildingData();

            if (UIManager.Ins != null) UIManager.Ins.RefreshUpgradePanel(this);

            SetActiveModel(CurrentLevel, true);
            OnLevelChanged?.Invoke();
        }
        if (CampaignTutorialManager.Ins != null) CampaignTutorialManager.Ins.OnBuildingUpgraded(this);
    }

    [ContextMenu("🔄 Reset level về 1")]
    public void ResetLevel()
    {
        SetActiveModel(CurrentLevel, false);
        CurrentLevel = 0;
        SetActiveModel(CurrentLevel, true);
        UpdateCivilianBuildingData();
        OnLevelChanged?.Invoke();
    }

    private void SetActiveModel(int index, bool active)
    {
        InitializeModels();
        if (instantiatedModels == null || index < 0 || index >= instantiatedModels.Length) return;

        if (index == selfRefIndex) SetOriginalLevelActive(active);
        else if (instantiatedModels[index] != null) instantiatedModels[index].SetActive(active);

        if (active) UpdateFirePointForLevel();
    }

    public void UpdateVisualModel()
    {
        InitializeModels();
        if (instantiatedModels == null) return;
        for (int i = 0; i < instantiatedModels.Length; i++)
        {
            bool isActive = (i == CurrentLevel);
            if (i == selfRefIndex)
            {
                SetOriginalLevelActive(isActive);
            }
            else if (instantiatedModels[i] != null)
            {
                instantiatedModels[i].SetActive(isActive);
                if (isActive) EnsureRenderersEnabled(instantiatedModels[i]);
            }
        }
        UpdateFirePointForLevel();
    }

    private void UpdateCivilianBuildingData()
    {
        switch (buildingType)
        {
            case BuildingType.WoodCutter:
                WoodStorage ws = GetComponentInChildren<WoodStorage>();
                if (ws != null) ws.SetupLevel(CurrentLevel);
                break;
            case BuildingType.StoneStorage:
                StoneStorage ss = GetComponentInChildren<StoneStorage>();
                if (ss != null) ss.SetupLevel(CurrentLevel);
                break;
            case BuildingType.FoodStorage:
                RiceStorage rs = GetComponentInChildren<RiceStorage>();
                if (rs != null) rs.SetupLevel(CurrentLevel);
                break;
            case BuildingType.Kitchen:
                Kitchen kc = GetComponentInChildren<Kitchen>();
                if (kc != null) kc.SetupLevel(CurrentLevel);
                break;
        }
    }

    public void LoadBuildingData(int level, bool isRuinedState = false, bool isInitialBuildNeededState = false)
    {
        StopAllCoroutines();
        IsUpgrading = false;
        currentProcessCoroutine = null;

        HideAllVisualModels();
        if (ruinedVisualModel != null)
        {
            ruinedVisualModel.SetActive(false);
        }

        CurrentLevel = Mathf.Clamp(level, 0, MaxLevel - 1);
        IsRuined = isRuinedState;
        
        // 🔥 FIX: Đồng bộ cả biến startAsRuined và isInitialBuildNeeded khi Load Data từ Save
        startAsRuined = isRuinedState;
        isInitialBuildNeeded = isInitialBuildNeededState;

        BuildingProgressBarUI progressUI = GetComponentInChildren<BuildingProgressBarUI>(true);

        if (IsRuined)
        {
            if (ruinedVisualModel != null) ruinedVisualModel.SetActive(true);
            ToggleBuildingLogic(false);

            HPTower hpComponent = GetComponent<HPTower>();
            if (hpComponent != null) hpComponent.SetRuinedHealth();

            if (progressUI != null)
            {
                progressUI.HideProgress();
                progressUI.DeactivateAllVFX();
            }
        }
        else if (isInitialBuildNeeded)
        {
            ToggleBuildingLogic(false);
            IsUpgrading = true;
            currentProcessCoroutine = StartCoroutine(UpgradeRoutine(initialBuildDuration));
        }
        else
        {
            UpdateVisualModel();
            ToggleBuildingLogic(true);

            HPTower hpComponent = GetComponent<HPTower>();
            if (hpComponent != null) hpComponent.ResetHealth();

            if (progressUI != null)
            {
                progressUI.HideProgress();
                progressUI.DeactivateAllVFX();
            }
        }

        UpdateCivilianBuildingData();
        OnLevelChanged?.Invoke();
    }

    public void LoadLevel(int level)
    {
        LoadBuildingData(level, false, false);
    }

    public void PauseBuildingProcess()
    {
        if (currentProcessCoroutine != null)
        {
            StopCoroutine(currentProcessCoroutine);
            currentProcessCoroutine = null;
        }
    }

    public void ResumeBuildingProcess()
    {
        if (activeProcessType == ProcessType.BuildOrUpgrade && currentProcessDuration > 0f)
            currentProcessCoroutine = StartCoroutine(UpgradeRoutine((int)currentProcessDuration, (int)currentProcessTimer));
        else if (activeProcessType == ProcessType.Repair && currentProcessDuration > 0f)
            currentProcessCoroutine = StartCoroutine(RepairRoutine(currentProcessTimer));
    }
}

public class ClickHelper : MonoBehaviour
{
    public UpgradeableBuilding parentBuilding;

    private void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (parentBuilding == null) return;

        BuildingUpgradeUI ui = parentBuilding.GetComponentInChildren<BuildingUpgradeUI>(true);
        if (ui != null && ui.IsOpen) return;

        parentBuilding.SelectThisBuilding();
        if (ui != null) ui.OpenUI();

        if (CampaignTutorialManager.Ins != null) CampaignTutorialManager.Ins.OnClickTownHall(); 
    }
}