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

    public void OnAttackButtonClicked()
    {
        Time.timeScale = 1f;
        if (targetLeadEnemy == null) return;

        Vector3 attackPos = targetLeadEnemy.position;
        Debug.Log($"[UIEnemyWaveButton] Player clicked Leader Attack Button! Recording scene state and switching to battle...");

        EnemyAI leadAI = targetLeadEnemy.GetComponent<EnemyAI>();
        List<EnemyAI> attackedSquad = (leadAI != null && leadAI.squadEnemies != null && leadAI.squadEnemies.Count > 0)
            ? new List<EnemyAI>(leadAI.squadEnemies)
            : new List<EnemyAI>();

        if (attackedSquad.Count == 0 && leadAI != null)
        {
            attackedSquad.Add(leadAI);
        }

        int waveCount = attackedSquad.Count > 0 ? attackedSquad.Count : 1;

        // 🔥 Lưu danh sách các quái thuộc đợt KHÁC (chưa tham chiến) để phục hồi sau trận đấu
        BattleData.SaveRemainingEnemiesState(attackedSquad);

        // Lưu trạng thái trước khi giao tranh / chuyển cảnh
        BattleData.RecordCurrentSceneState(waveCount);

        if (!string.IsNullOrEmpty(battleSceneName))
        {
            Debug.Log($"[UIEnemyWaveButton] Đang chuyển sang Scene giao tranh: {battleSceneName}");
            UnityEngine.SceneManagement.SceneManager.LoadScene(battleSceneName);
            return;
        }

        EnemyAI[] allEnemiesInScene = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (var enemy in allEnemiesInScene)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                enemy.EnableCombat();
            }
        }

        List<UnitController> soldierList = new List<UnitController>();
        UnitController[] foundUnits = Object.FindObjectsByType<UnitController>(FindObjectsSortMode.None);
        foreach (var unit in foundUnits)
        {
            if (unit != null && unit.gameObject.activeInHierarchy && !soldierList.Contains(unit))
            {
                soldierList.Add(unit);
            }
        }

        int count = 0;
        foreach (UnitController soldier in soldierList)
        {
            if (soldier != null)
            {
                soldier.RespondToWarning(attackPos);
                count++;
            }
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// Helper method to dynamically generate a World Space UI Attack Button over the leader enemy.
    /// </summary>
    public static UIEnemyWaveButton CreateButton(Transform leadEnemy, float heightOffset = 3.0f)
    {
        if (leadEnemy == null) return null;

        // Ensure only ONE button is created per leader enemy
        UIEnemyWaveButton existing = leadEnemy.GetComponentInChildren<UIEnemyWaveButton>();
        if (existing != null) return existing;

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
        btnImage.color = new Color(0.9f, 0.2f, 0.2f, 0.95f); // Red UI Button

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
        tmpText.text = "⚔️ TẤN CÔNG!";
        tmpText.fontSize = 20;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = Color.white;
        tmpText.fontStyle = FontStyles.Bold;

        UIEnemyWaveButton waveBtn = canvasObj.AddComponent<UIEnemyWaveButton>();
        waveBtn.attackButton = btn;
        waveBtn.buttonText = tmpText;
        waveBtn.Initialize(leadEnemy, heightOffset);

        return waveBtn;
    }
}
