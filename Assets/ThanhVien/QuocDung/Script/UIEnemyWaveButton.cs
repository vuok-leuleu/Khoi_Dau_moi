using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UIEnemyWaveButton : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform targetLeadEnemy;
    public float heightOffset = 3.0f;

    [Header("UI Components")]
    public Button attackButton;
    public TMP_Text buttonText;

    private Camera mainCamera;

    public void Initialize(Transform leadEnemy, float offset = 3.0f)
    {
        targetLeadEnemy = leadEnemy;
        heightOffset = offset;
        mainCamera = Camera.main;

        EnsureEventSystem();
        EnsureButtonListener();
    }

    private void Start()
    {
        mainCamera = Camera.main;
        EnsureEventSystem();
        EnsureButtonListener();
    }

    private void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject eventSys = new GameObject("EventSystem");
            eventSys.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSys.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Debug.Log("[UIEnemyWaveButton] Auto-created missing EventSystem in scene.");
        }
    }

    private void EnsureButtonListener()
    {
        if (attackButton == null)
        {
            attackButton = GetComponentInChildren<Button>();
        }

        if (attackButton != null)
        {
            attackButton.onClick.RemoveAllListeners();
            attackButton.onClick.AddListener(OnAttackButtonClicked);
        }
    }

    private void Update()
    {
        // Fallback 3D Raycast click detection if UI Raycast misses
        if (Input.GetMouseButtonDown(0) && targetLeadEnemy != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    OnAttackButtonClicked();
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (!gameObject.scene.isLoaded) return;

        // If lead enemy is killed or destroyed, remove the warning button
        if (targetLeadEnemy == null || !targetLeadEnemy.gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }

        // Follow the lead enemy position
        transform.position = targetLeadEnemy.position + Vector3.up * heightOffset;

        // Billboard effect: Face towards the main camera
        if (mainCamera != null)
        {
            transform.rotation = mainCamera.transform.rotation;
        }
        else
        {
            mainCamera = Camera.main;
        }
    }

    [Header("Scene Settings")]
    public string battleSceneName = "SceneBattle";

    [HideInInspector]
    public bool isTroopsArrivedAtTarget = false;

    public void OnAttackButtonClicked()
    {
        Time.timeScale = 1f;
        if (targetLeadEnemy == null) return;

        // 🔥 Nếu lính đã hành quân tới căn cứ địch thành công, người chơi bấm Tấn Công sẽ vào thẳng SceneBattle
        if (isTroopsArrivedAtTarget)
        {
            int enemyCount = 5;
            EnemySpawn spawner = targetLeadEnemy.GetComponentInParent<EnemySpawn>();
            if (spawner == null) spawner = Object.FindFirstObjectByType<EnemySpawn>();
            if (spawner != null && spawner.enemyCountInBase > 0)
            {
                enemyCount = spawner.enemyCountInBase;
            }
            else
            {
                SettlementZone zone = targetLeadEnemy.GetComponentInParent<SettlementZone>();
                if (zone != null && zone.enemyCountInBase > 0)
                {
                    enemyCount = zone.enemyCountInBase;
                }
            }

            Debug.Log($"[UIEnemyWaveButton] ⚔️ Người chơi bấm TẤN CÔNG khi lính đã tập kết đầy đủ! Chuyển sang {battleSceneName}...");
            
            // Record targeted settlement zone
            if (targetLeadEnemy != null)
            {
                SettlementZone zone = targetLeadEnemy.GetComponentInParent<SettlementZone>();
                if (zone == null) zone = targetLeadEnemy.GetComponentInChildren<SettlementZone>();
                if (zone != null)
                {
                    BattleData.TargetedSettlementZoneName = zone.settlementName;
                }
            }

            BattleData.RecordCurrentSceneState(enemyCount);
            if (!string.IsNullOrEmpty(battleSceneName))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(battleSceneName);
            }
            Destroy(gameObject);
            return;
        }

        Vector3 attackPos = targetLeadEnemy.position;
        Debug.Log($"[UIEnemyWaveButton] Player clicked Attack Button! Opening TroopDispatchUI panel...");

        // Mở Bảng Chọn Quân Đội Xuất Trận (TroopDispatchUI)
        TroopDispatchUI.OpenPanel(attackPos, targetLeadEnemy, battleSceneName);

        Destroy(gameObject);
    }

    /// <summary>
    /// Helper method to dynamically generate a World Space UI Attack Button over the leader enemy.
    /// </summary>
    public static UIEnemyWaveButton CreateButton(Transform leadEnemy, float heightOffset = 3.0f, bool isArrived = false)
    {
        if (leadEnemy == null) return null;

        // Ensure only ONE button is created per leader enemy
        UIEnemyWaveButton existing = leadEnemy.GetComponentInChildren<UIEnemyWaveButton>();
        if (existing != null)
        {
            if (isArrived)
            {
                existing.isTroopsArrivedAtTarget = true;
                if (existing.buttonText != null) existing.buttonText.text = "⚔️ TẤN CÔNG (START)";
            }
            return existing;
        }

        // Check EventSystem
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject eventSys = new GameObject("EventSystem");
            eventSys.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSys.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        GameObject canvasObj = new GameObject("EnemyWaveWarningCanvas");
        canvasObj.transform.SetParent(leadEnemy, false);
        canvasObj.transform.localPosition = Vector3.up * heightOffset;

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;

        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Add BoxCollider for 3D Raycast fallback
        BoxCollider boxCol = canvasObj.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(180f, 50f, 2f);

        RectTransform rect = canvasObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(180f, 50f);
        rect.localScale = new Vector3(0.015f, 0.015f, 0.015f);

        // Button Background
        GameObject btnObj = new GameObject("AttackButton");
        btnObj.transform.SetParent(canvasObj.transform, false);

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = Vector2.zero;
        btnRect.anchorMax = Vector2.one;
        btnRect.sizeDelta = Vector2.zero;

        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = isArrived ? new Color(0.15f, 0.75f, 0.25f, 0.95f) : new Color(0.9f, 0.2f, 0.2f, 0.95f);

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(1f, 0.4f, 0.4f, 1f);
        colors.pressedColor = new Color(0.7f, 0.1f, 0.1f, 1f);
        btn.colors = colors;

        // Button Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.text = isArrived ? "⚔️ TẤN CÔNG (START)" : "⚔️ TẤN CÔNG!";
        tmpText.fontSize = 20;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = Color.white;
        tmpText.fontStyle = FontStyles.Bold;

        UIEnemyWaveButton waveBtn = canvasObj.AddComponent<UIEnemyWaveButton>();
        waveBtn.attackButton = btn;
        waveBtn.buttonText = tmpText;
        waveBtn.isTroopsArrivedAtTarget = isArrived;
        waveBtn.Initialize(leadEnemy, heightOffset);

        return waveBtn;
    }
}

public class ExpeditionBattleTrigger : MonoBehaviour
{
    private List<UnitController> marchingSoldiers;
    private Transform enemyTarget;
    private string sceneToLoad;

    public void StartMonitoring(List<UnitController> soldiers, Transform target, string sceneName)
    {
        marchingSoldiers = soldiers;
        enemyTarget = target;
        sceneToLoad = sceneName;
    }

    private void Update()
    {
        if (enemyTarget == null)
        {
            Destroy(gameObject);
            return;
        }

        if (marchingSoldiers == null || marchingSoldiers.Count == 0)
        {
            Destroy(gameObject);
            return;
        }

        List<UnitController> activeMarchers = new List<UnitController>();
        foreach (var s in marchingSoldiers)
        {
            if (s != null && s.gameObject.activeInHierarchy && s.isExpeditionMarching)
            {
                activeMarchers.Add(s);
            }
        }

        if (activeMarchers.Count == 0)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPos = enemyTarget.position;
        int currentWave = (DayNightManager.HasInstance && DayNightManager.Ins != null) ? DayNightManager.Ins.CurrentWave : 1;
        bool allReached = true;

        foreach (var s in activeMarchers)
        {
            int remainingWaves = Mathf.Max(0, s.marchTargetWave - currentWave);
            float distToDest = Vector3.Distance(s.transform.position, s.marchDestinationPosition);
            float distToCenter = Vector3.Distance(s.transform.position, targetPos);

            // 🔥 Đảm bảo TẤT CẢ lính trong đoàn xuất trận đều đã hoàn thành số wave hành quân và tập kết áp sát mục tiêu
            if (remainingWaves > 0 || (distToDest > 2.0f && distToCenter > 4.5f))
            {
                allReached = false;
                break;
            }
        }

        if (allReached)
        {
            Debug.Log("[ExpeditionBattleTrigger] ⚔️ TẤT CẢ lính đã tập kết đầy đủ tại Căn cứ Địch! Dừng hành quân và hiển thị Nút Tấn Công...");

            // Dừng trạng thái di chuyển hành quân để lính tập kết đứng chờ
            foreach (var s in activeMarchers)
            {
                if (s != null)
                {
                    s.isExpeditionMarching = false;
                }
            }

            // 🔥 Hiển thị Nút Tấn Công (màu xanh lá) trên đầu Căn cứ Địch để người chơi bấm kích hoạt trận đấu
            if (enemyTarget != null)
            {
                UIEnemyWaveButton attackBtn = UIEnemyWaveButton.CreateButton(enemyTarget, 3.5f, true);
                if (attackBtn != null)
                {
                    attackBtn.battleSceneName = sceneToLoad;
                }
            }

            Destroy(gameObject);
        }
    }
}
