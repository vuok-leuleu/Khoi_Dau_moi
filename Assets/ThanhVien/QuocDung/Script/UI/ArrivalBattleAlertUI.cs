using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ArrivalBattleAlertUI : MonoBehaviour
{
    private const int AlertSortingOrder = 1000;
    private const string SpotlightShaderName = "UI/Arrival Battle Spotlight";

    [Header("Presentation Assets")]
    [SerializeField] private Sprite meleeIcon;
    [SerializeField] private Sprite rangedIcon;
    [SerializeField] private Sprite dragonIcon;

    [Header("Canvas References")]
    [SerializeField] private Image dimOverlay;
    [SerializeField] private GameObject enemyEntry;
    [SerializeField] private Image enemyIcon;
    [SerializeField] private TextMeshProUGUI enemyCountText;
    [SerializeField] private Button fightButton;

    [Header("Spotlight")]
    [SerializeField, Range(0.05f, 0.5f)] private float spotlightRadius = 0.18f;
    [SerializeField, Range(0.005f, 0.2f)] private float spotlightSoftness = 0.06f;
    [SerializeField, Range(0f, 1f)] private float backgroundDarkness = 0.72f;

    private EnemyAI activeLeader;
    private bool isTransitioning = false;
    private readonly Dictionary<Canvas, bool> hiddenCanvases = new();
    private Canvas alertCanvas;
    private Transform focusTarget;
    private Material spotlightMaterial;
    private RTSCameraController lockedCameraController;
    private bool cameraControllerWasEnabled;
    private readonly List<EnemyDisplayEntry> enemyEntries = new();

    private sealed class EnemyDisplayEntry
    {
        public readonly GameObject root;
        public readonly Image icon;
        public readonly TextMeshProUGUI countText;

        public EnemyDisplayEntry(GameObject root, Image icon, TextMeshProUGUI countText)
        {
            this.root = root;
            this.icon = icon;
            this.countText = countText;
        }
    }

    private sealed class EnemyComposition
    {
        public readonly GameObject prefab;
        public readonly EnemyAI.EnemyAttackType attackType;
        public readonly bool isDragon;
        public int count;

        public EnemyComposition(GameObject prefab, EnemyAI.EnemyAttackType attackType, bool isDragon = false)
        {
            this.prefab = prefab;
            this.attackType = attackType;
            this.isDragon = isDragon;
            count = 1;
        }
    }

    public static void ShowFor(EnemyAI leader, Transform focusTarget)
    {
        ArrivalBattleAlertUI alert =
            FindFirstObjectByType<ArrivalBattleAlertUI>(FindObjectsInactive.Include);

        if (alert == null)
        {
            Debug.LogWarning("[ArrivalBattleAlertUI] ArrivalBattleAlertUI canvas was not found in this scene.");
            return;
        }

        alert.Show(leader, focusTarget);
    }

    private void Awake()
    {
        alertCanvas = GetComponent<Canvas>();
        if (alertCanvas != null)
        {
            alertCanvas.overrideSorting = true;
            alertCanvas.sortingOrder = AlertSortingOrder;
        }

        if (dimOverlay == null)
        {
            Transform overlayTransform = transform.Find("DimOverlay");
            if (overlayTransform != null) dimOverlay = overlayTransform.GetComponent<Image>();
        }

        CacheEnemyEntries();
        CreateSpotlightMaterial();

        if (fightButton != null)
        {
            fightButton.onClick.RemoveListener(StartBattle);
            fightButton.onClick.AddListener(StartBattle);
        }
    }

    private void OnEnable()
    {
        isTransitioning = false;
        if (fightButton != null) fightButton.interactable = true;
    }

    private void LateUpdate()
    {
        UpdateSpotlightPosition();
    }

    private void OnDisable()
    {
        RestoreOtherCanvases();
        RestoreCameraControls();
    }

    private void OnDestroy()
    {
        RestoreOtherCanvases();
        RestoreCameraControls();

        if (spotlightMaterial != null)
        {
            Destroy(spotlightMaterial);
            spotlightMaterial = null;
        }
    }

    public void Show(EnemyAI leader, Transform target)
    {
        activeLeader = leader;
        focusTarget = target;
        isTransitioning = false;
        if (fightButton != null) fightButton.interactable = true;
        gameObject.SetActive(true);

        LockCameraControls();
        HideOtherCanvases();
        UpdateSpotlightPosition();
        PopulateEnemyInfo(leader);
    }

    public void Hide()
    {
        RestoreOtherCanvases();
        RestoreCameraControls();
        gameObject.SetActive(false);
    }

    private void PopulateEnemyInfo(EnemyAI leader)
    {
        CacheEnemyEntries();

        List<EnemyAI> activeEnemies = GetActiveSquad(leader);
        bool raidIncludesDragon = EnemyInvasionManager.Ins != null &&
                                   EnemyInvasionManager.Ins.CurrentRaidSpawnsDragon;
        List<EnemyComposition> composition = BuildComposition(activeEnemies, raidIncludesDragon);
        EnsureEntryCapacity(composition.Count);

        for (int i = 0; i < enemyEntries.Count; i++)
        {
            EnemyDisplayEntry entry = enemyEntries[i];
            bool shouldShow = i < composition.Count;
            entry.root.SetActive(shouldShow);
            if (!shouldShow) continue;

            EnemyComposition enemyType = composition[i];
            entry.countText.text = enemyType.count.ToString();
            entry.icon.sprite = GetIcon(enemyType);
            entry.icon.preserveAspect = true;
            entry.root.transform.SetSiblingIndex(i);
        }
    }

    private List<EnemyAI> GetActiveSquad(EnemyAI leader)
    {
        List<EnemyAI> enemies = new();
        if (leader == null) return enemies;

        if (leader.squadEnemies != null)
        {
            foreach (EnemyAI enemy in leader.squadEnemies)
            {
                if (enemy != null && enemy.gameObject.activeInHierarchy && !enemies.Contains(enemy))
                {
                    enemies.Add(enemy);
                }
            }
        }

        if (leader.gameObject.activeInHierarchy && !enemies.Contains(leader))
        {
            enemies.Add(leader);
        }

        return enemies;
    }

    private static List<EnemyComposition> BuildComposition(IEnumerable<EnemyAI> enemies, bool includeDragon)
    {
        List<EnemyComposition> composition = new();
        Dictionary<GameObject, EnemyComposition> byPrefab = new();
        Dictionary<EnemyAI.EnemyAttackType, EnemyComposition> unknownByAttackType = new();

        // Rồng là Boss riêng của raid lớn, không có EnemyAI và không nằm trong
        // squadEnemies. Cờ này là cùng cờ BattleManager dùng để spawn Rồng.
        if (includeDragon)
        {
            composition.Add(new EnemyComposition(null, EnemyAI.EnemyAttackType.Tank, true));
        }

        foreach (EnemyAI enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            if (EnemySpawn.TryGetSpawnedEnemyPrefab(enemy, out GameObject sourcePrefab))
            {
                if (byPrefab.TryGetValue(sourcePrefab, out EnemyComposition existing))
                {
                    existing.count++;
                }
                else
                {
                    EnemyComposition added = new(sourcePrefab, enemy.attackType);
                    byPrefab.Add(sourcePrefab, added);
                    composition.Add(added);
                }

                continue;
            }

            // Fallback cho Enemy được tạo bởi hệ thống cũ không lưu prefab nguồn.
            // Chúng vẫn hiện đúng số lượng, chỉ không thể phân biệt các prefab cùng attack type.
            if (unknownByAttackType.TryGetValue(enemy.attackType, out EnemyComposition fallback))
            {
                fallback.count++;
            }
            else
            {
                EnemyComposition added = new(null, enemy.attackType);
                unknownByAttackType.Add(enemy.attackType, added);
                composition.Add(added);
            }
        }

        return composition;
    }

    private void CacheEnemyEntries()
    {
        if (enemyEntries.Count > 0 || enemyEntry == null) return;

        Transform entryParent = enemyEntry.transform.parent;
        if (entryParent != null)
        {
            for (int i = 0; i < entryParent.childCount; i++)
            {
                Transform child = entryParent.GetChild(i);
                Image icon = child.GetComponentInChildren<Image>(true);
                TextMeshProUGUI countText = child.GetComponentInChildren<TextMeshProUGUI>(true);
                if (icon != null && countText != null)
                {
                    enemyEntries.Add(new EnemyDisplayEntry(child.gameObject, icon, countText));
                }
            }
        }

        // Tương thích prefab cũ chỉ có một EnemyEntry được gán trong Inspector.
        if (enemyEntries.Count == 0 && enemyIcon != null && enemyCountText != null)
        {
            enemyEntries.Add(new EnemyDisplayEntry(enemyEntry, enemyIcon, enemyCountText));
        }
    }

    private void EnsureEntryCapacity(int requiredCount)
    {
        while (enemyEntries.Count < requiredCount && enemyEntry != null && enemyEntry.transform.parent != null)
        {
            GameObject duplicate = Instantiate(enemyEntry, enemyEntry.transform.parent);
            duplicate.name = "EnemyEntry (Runtime)";

            Image icon = duplicate.GetComponentInChildren<Image>(true);
            TextMeshProUGUI countText = duplicate.GetComponentInChildren<TextMeshProUGUI>(true);
            if (icon == null || countText == null)
            {
                Destroy(duplicate);
                break;
            }

            enemyEntries.Add(new EnemyDisplayEntry(duplicate, icon, countText));
        }
    }

    private Sprite GetIcon(EnemyComposition enemyType)
    {
        if (dragonIcon != null && (enemyType.isDragon ||
                                   (enemyType.prefab != null && enemyType.prefab.name.ToLowerInvariant().Contains("dragon"))))
        {
            return dragonIcon;
        }

        EnemyAI.EnemyAttackType attackType = enemyType.attackType;
        if (enemyType.prefab != null)
        {
            EnemyAI prefabEnemy = enemyType.prefab.GetComponent<EnemyAI>();
            if (prefabEnemy != null) attackType = prefabEnemy.attackType;
        }

        return attackType == EnemyAI.EnemyAttackType.Ranged ? rangedIcon : meleeIcon;
    }

    private void StartBattle()
    {
        if (isTransitioning) return;
        isTransitioning = true;

        if (fightButton != null) fightButton.interactable = false;

        ExecuteStartBattle();
    }

    private void ExecuteStartBattle()
    {
        if (activeLeader != null)
        {
            activeLeader.OnAttackButtonClicked();
        }
        else
        {
            CloudSceneTransition.LoadSceneWithCloud("SceneBattle");
        }
    }

    /// <summary>
    /// Hides every other UI canvas while the arrival alert is active. Their original enabled
    /// state is remembered so this alert never turns on UI that was intentionally hidden.
    /// </summary>
    private void HideOtherCanvases()
    {
        RestoreOtherCanvases();

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || canvas == alertCanvas) continue;

            hiddenCanvases[canvas] = canvas.enabled;
            canvas.enabled = false;
        }
    }

    private void RestoreOtherCanvases()
    {
        foreach (KeyValuePair<Canvas, bool> entry in hiddenCanvases)
        {
            if (entry.Key != null) entry.Key.enabled = entry.Value;
        }

        hiddenCanvases.Clear();
    }

    private void LockCameraControls()
    {
        RestoreCameraControls();

        Camera sceneCamera = Camera.main;
        if (sceneCamera == null) sceneCamera = FindFirstObjectByType<Camera>();
        if (sceneCamera == null) return;

        lockedCameraController = sceneCamera.GetComponent<RTSCameraController>();
        if (lockedCameraController == null) return;

        cameraControllerWasEnabled = lockedCameraController.enabled;
        lockedCameraController.enabled = false;
    }

    private void RestoreCameraControls()
    {
        if (lockedCameraController != null)
        {
            lockedCameraController.enabled = cameraControllerWasEnabled;
        }

        lockedCameraController = null;
    }

    private void CreateSpotlightMaterial()
    {
        if (dimOverlay == null || spotlightMaterial != null) return;

        Shader shader = Shader.Find(SpotlightShaderName);
        if (shader == null)
        {
            Debug.LogWarning($"[ArrivalBattleAlertUI] Could not find shader '{SpotlightShaderName}'. Falling back to the normal dark overlay.");
            return;
        }

        spotlightMaterial = new Material(shader)
        {
            name = "Arrival Battle Spotlight (Runtime)"
        };
        dimOverlay.material = spotlightMaterial;
    }

    private void UpdateSpotlightPosition()
    {
        if (dimOverlay == null || focusTarget == null) return;

        if (spotlightMaterial == null) CreateSpotlightMaterial();
        if (spotlightMaterial == null) return;

        Camera sceneCamera = Camera.main;
        if (sceneCamera == null) sceneCamera = FindFirstObjectByType<Camera>();
        if (sceneCamera == null) return;

        Vector3 viewportPosition = sceneCamera.WorldToViewportPoint(focusTarget.position);
        if (viewportPosition.z <= 0f) return;

        spotlightMaterial.SetVector("_Focus", new Vector4(
            Mathf.Clamp01(viewportPosition.x),
            Mathf.Clamp01(viewportPosition.y),
            0f,
            0f));
        spotlightMaterial.SetFloat("_Radius", spotlightRadius);
        spotlightMaterial.SetFloat("_Softness", spotlightSoftness);
        spotlightMaterial.SetFloat("_DimAlpha", backgroundDarkness);
    }
}
