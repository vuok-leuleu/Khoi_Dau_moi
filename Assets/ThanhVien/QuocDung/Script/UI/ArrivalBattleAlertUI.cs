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
        if (enemyEntry == null || enemyCountText == null) return;

        int meleeCount = 0;
        int rangedCount = 0;

        IEnumerable<EnemyAI> enemies =
            leader != null && leader.squadEnemies != null && leader.squadEnemies.Count > 0
                ? leader.squadEnemies
                : new[] { leader };

        foreach (EnemyAI enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            if (enemy.attackType == EnemyAI.EnemyAttackType.Ranged)
                rangedCount++;
            else
                meleeCount++;
        }

        int totalCount = meleeCount + rangedCount;
        enemyEntry.SetActive(totalCount > 0);
        enemyCountText.text = totalCount.ToString();

        if (enemyIcon != null)
        {
            enemyIcon.sprite = rangedCount > meleeCount ? rangedIcon : meleeIcon;
            enemyIcon.preserveAspect = true;
        }
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
