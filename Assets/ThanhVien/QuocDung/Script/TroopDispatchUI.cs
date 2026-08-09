using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class TroopDispatchUI : MonoBehaviour
{
    [System.Serializable]
    public class BarracksDispatchData
    {
        public SpawnSoldier spawner;
        public string barracksName;
        public int soldierCount;
        public bool isSelected = true;
        public Toggle toggleUI;
    }

    [Header("Dispatch Data")]
    public Vector3 attackPosition;
    public Transform enemyTarget;
    public string battleSceneName = "SceneBattle";
    public List<BarracksDispatchData> barracksList = new List<BarracksDispatchData>();

    private Camera mainCamera;
    private static TroopDispatchUI activeInstance;

    public static TroopDispatchUI OpenPanel(Vector3 attackPos, Transform enemyTargetTr, string sceneName = "SceneBattle")
    {
        if (activeInstance != null)
        {
            Destroy(activeInstance.gameObject);
        }

        // Check EventSystem
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject eventSys = new GameObject("EventSystem");
            eventSys.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSys.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        GameObject canvasObj = new GameObject("TroopDispatchCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        TroopDispatchUI panel = canvasObj.AddComponent<TroopDispatchUI>();
        panel.attackPosition = attackPos;
        panel.enemyTarget = enemyTargetTr;
        panel.battleSceneName = sceneName;
        panel.BuildUI(canvasObj.transform);

        activeInstance = panel;
        return panel;
    }

    private void BuildUI(Transform parentCanvas)
    {
        mainCamera = Camera.main;

        // Dark Background Overlay
        GameObject bgObj = new GameObject("BackgroundOverlay");
        bgObj.transform.SetParent(parentCanvas, false);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.65f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Main Panel Box
        GameObject panelObj = new GameObject("MainDispatchPanel");
        panelObj.transform.SetParent(bgObj.transform, false);
        Image panelImg = panelObj.AddComponent<Image>();
        panelImg.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(650f, 520f);
        panelRect.anchoredPosition = Vector2.zero;

        // Header Title Background
        GameObject headerObj = new GameObject("HeaderTitle");
        headerObj.transform.SetParent(panelObj.transform, false);
        Image headerImg = headerObj.AddComponent<Image>();
        headerImg.color = new Color(0.75f, 0.15f, 0.15f, 1f); // Red Header Banner
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 65f);
        headerRect.anchoredPosition = Vector2.zero;

        // Header Text
        GameObject headerTextObj = new GameObject("TitleText");
        headerTextObj.transform.SetParent(headerObj.transform, false);
        TextMeshProUGUI titleTMP = headerTextObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "⚔️ BẢNG XUẤT QUÂN TẤN CÔNG (STATIONED UNITS)";
        titleTMP.fontSize = 22;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.color = Color.white;
        RectTransform titleRect = headerTextObj.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.sizeDelta = Vector2.zero;

        // Subtitle Text
        GameObject subTextObj = new GameObject("SubtitleText");
        subTextObj.transform.SetParent(panelObj.transform, false);
        TextMeshProUGUI subTMP = subTextObj.AddComponent<TextMeshProUGUI>();
        subTMP.text = "Chọn các Doanh Trại sẽ cử lính tham gia đợt hành quân tấn công:";
        subTMP.fontSize = 16;
        subTMP.alignment = TextAlignmentOptions.Center;
        subTMP.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        RectTransform subRect = subTextObj.GetComponent<RectTransform>();
        subRect.anchoredPosition = new Vector3(0f, 185f, 0f);
        subRect.sizeDelta = new Vector2(600f, 30f);

        // Scroll Content Area
        GameObject scrollObj = new GameObject("BarracksListContainer");
        scrollObj.transform.SetParent(panelObj.transform, false);
        RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
        scrollRect.anchoredPosition = new Vector3(0f, 20f, 0f);
        scrollRect.sizeDelta = new Vector2(580f, 280f);

        VerticalLayoutGroup layout = scrollObj.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        // Gather all Barracks / SpawnSoldier in scene
        barracksList.Clear();
        SpawnSoldier[] spawners = Object.FindObjectsByType<SpawnSoldier>(FindObjectsSortMode.None);
        HashSet<GameObject> processedBuildings = new HashSet<GameObject>();
        int bIndex = 1;

        foreach (var spawner in spawners)
        {
            if (spawner == null || !spawner.gameObject.activeInHierarchy) continue;

            // Bỏ qua các object ghost / preview đang trong chế độ đặt công trình
            if (spawner.GetComponentInParent<GhostBuilding>() != null || spawner.GetComponent<GhostBuilding>() != null) continue;
            string objNameLower = spawner.gameObject.name.ToLower();
            if (objNameLower.Contains("ghost") || objNameLower.Contains("ghot")) continue;

            // Xác định GameObject gốc đại diện cho công trình này
            UpgradeableBuilding ub = spawner.GetComponent<UpgradeableBuilding>();
            if (ub == null) ub = spawner.GetComponentInParent<UpgradeableBuilding>();

            BuildingCtrl bc = spawner.GetComponent<BuildingCtrl>();
            if (bc == null) bc = spawner.GetComponentInParent<BuildingCtrl>();

            GameObject bObj = ub != null ? ub.gameObject : (bc != null ? bc.gameObject : spawner.transform.root.gameObject);

            // Deduplicate: Mỗi công trình Doanh Trại thật trong scene chỉ hiển thị đúng 1 dòng trên bảng UI
            if (processedBuildings.Contains(bObj)) continue;
            processedBuildings.Add(bObj);

            // Bỏ qua công trình đang bị tàn phá hoặc chưa hoàn thành xây dựng ban đầu
            if (ub != null && (ub.IsRuined || ub.IsInitialBuildNeeded)) continue;

            // Gom toàn bộ lính từ các spawner thuộc công trình này
            List<UnitController> activeSoldiers = new List<UnitController>();
            SpawnSoldier[] childSpawners = bObj.GetComponentsInChildren<SpawnSoldier>(true);
            foreach (var s in childSpawners)
            {
                if (s == null) continue;
                List<UnitController> soldiers = s.GetActiveSoldierControllers();
                if (soldiers != null)
                {
                    foreach (var unit in soldiers)
                    {
                        if (unit != null && unit.gameObject.activeInHierarchy && !activeSoldiers.Contains(unit))
                        {
                            activeSoldiers.Add(unit);
                        }
                    }
                }
            }

            string rawName = ub != null && !string.IsNullOrEmpty(ub.buildingName) ? ub.buildingName : bObj.name;
            rawName = rawName.Replace("(Clone)", "").Trim();

            string bName = $"Doanh Trại {bIndex} ({rawName})";
            bool hasSoldiers = activeSoldiers.Count > 0;

            BarracksDispatchData data = new BarracksDispatchData
            {
                spawner = spawner,
                barracksName = bName,
                soldierCount = activeSoldiers.Count,
                isSelected = true
            };

            // Create UI Item Row
            GameObject rowObj = new GameObject($"BarracksRow_{bIndex}");
            rowObj.transform.SetParent(scrollObj.transform, false);
            Image rowImg = rowObj.AddComponent<Image>();
            rowImg.color = new Color(0.2f, 0.24f, 0.3f, 0.9f);
            RectTransform rowRect = rowObj.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(560f, 55f);

            Toggle toggle = rowObj.AddComponent<Toggle>();
            toggle.isOn = true;
            data.toggleUI = toggle;

            // Toggle Checkmark UI
            GameObject checkBg = new GameObject("CheckmarkBG");
            checkBg.transform.SetParent(rowObj.transform, false);
            Image checkBgImg = checkBg.AddComponent<Image>();
            checkBgImg.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            RectTransform checkBgRect = checkBg.GetComponent<RectTransform>();
            checkBgRect.anchoredPosition = new Vector2(-240f, 0f);
            checkBgRect.sizeDelta = new Vector2(30f, 30f);

            GameObject checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(checkBg.transform, false);
            Image checkImg = checkmark.AddComponent<Image>();
            checkImg.color = new Color(0.2f, 0.8f, 0.2f, 1f); // Green Checkmark
            RectTransform checkRect = checkmark.GetComponent<RectTransform>();
            checkRect.sizeDelta = new Vector2(22f, 22f);

            toggle.graphic = checkImg;
            toggle.targetGraphic = checkBgImg;

            // Row Label Text
            GameObject labelObj = new GameObject("LabelText");
            labelObj.transform.SetParent(rowObj.transform, false);
            TextMeshProUGUI labelTMP = labelObj.AddComponent<TextMeshProUGUI>();
            string countText = hasSoldiers 
                ? $"<color=#FFD700>{activeSoldiers.Count} Lính Xuất Trận</color>" 
                : "<color=#FF6666>0 Lính (Chưa sẵn sàng)</color>";
            labelTMP.text = $"<b>{bName}</b> - {countText}";
            labelTMP.fontSize = 18;
            labelTMP.alignment = TextAlignmentOptions.Left;
            labelTMP.color = Color.white;
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchoredPosition = new Vector2(30f, 0f);
            labelRect.sizeDelta = new Vector2(450f, 40f);

            int capturedIndex = barracksList.Count;
            toggle.onValueChanged.AddListener((val) =>
            {
                if (capturedIndex >= 0 && capturedIndex < barracksList.Count)
                {
                    barracksList[capturedIndex].isSelected = val;
                }
            });

            barracksList.Add(data);
            bIndex++;
        }

        if (barracksList.Count == 0)
        {
            GameObject emptyTextObj = new GameObject("EmptyText");
            emptyTextObj.transform.SetParent(scrollObj.transform, false);
            TextMeshProUGUI emptyTMP = emptyTextObj.AddComponent<TextMeshProUGUI>();
            emptyTMP.text = "⚠️ Không có Doanh trại nào có Lính sẵn sàng xuất quân!";
            emptyTMP.fontSize = 18;
            emptyTMP.alignment = TextAlignmentOptions.Center;
            emptyTMP.color = Color.yellow;
            RectTransform emptyRect = emptyTextObj.GetComponent<RectTransform>();
            emptyRect.sizeDelta = new Vector2(560f, 50f);
        }

        // Bottom Action Buttons Panel
        GameObject btnPanel = new GameObject("ButtonsPanel");
        btnPanel.transform.SetParent(panelObj.transform, false);
        RectTransform btnPanelRect = btnPanel.AddComponent<RectTransform>();
        btnPanelRect.anchoredPosition = new Vector3(0f, -195f, 0f);
        btnPanelRect.sizeDelta = new Vector2(580f, 60f);

        // DISPATCH / MOVE BUTTON
        GameObject dispatchBtnObj = new GameObject("DispatchButton");
        dispatchBtnObj.transform.SetParent(btnPanel.transform, false);
        Image dispatchImg = dispatchBtnObj.AddComponent<Image>();
        dispatchImg.color = new Color(0.15f, 0.65f, 0.25f, 1f); // Green Move Button
        Button dispatchBtn = dispatchBtnObj.AddComponent<Button>();
        RectTransform dispatchRect = dispatchBtnObj.GetComponent<RectTransform>();
        dispatchRect.anchoredPosition = new Vector2(100f, 0f);
        dispatchRect.sizeDelta = new Vector2(240f, 50f);

        GameObject dispatchTextObj = new GameObject("Text");
        dispatchTextObj.transform.SetParent(dispatchBtnObj.transform, false);
        TextMeshProUGUI dispatchTMP = dispatchTextObj.AddComponent<TextMeshProUGUI>();
        dispatchTMP.text = "⚔️ XUẤT QUÂN (MOVE)";
        dispatchTMP.fontSize = 18;
        dispatchTMP.fontStyle = FontStyles.Bold;
        dispatchTMP.alignment = TextAlignmentOptions.Center;
        dispatchTMP.color = Color.white;
        RectTransform dispatchTextRect = dispatchTextObj.GetComponent<RectTransform>();
        dispatchTextRect.anchorMin = Vector2.zero;
        dispatchTextRect.anchorMax = Vector2.one;
        dispatchTextRect.sizeDelta = Vector2.zero;

        dispatchBtn.onClick.AddListener(OnDispatchConfirmed);

        // CANCEL / CLOSE BUTTON
        GameObject cancelBtnObj = new GameObject("CancelButton");
        cancelBtnObj.transform.SetParent(btnPanel.transform, false);
        Image cancelImg = cancelBtnObj.AddComponent<Image>();
        cancelImg.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        Button cancelBtn = cancelBtnObj.AddComponent<Button>();
        RectTransform cancelRect = cancelBtnObj.GetComponent<RectTransform>();
        cancelRect.anchoredPosition = new Vector2(-150f, 0f);
        cancelRect.sizeDelta = new Vector2(160f, 50f);

        GameObject cancelTextObj = new GameObject("Text");
        cancelTextObj.transform.SetParent(cancelBtnObj.transform, false);
        TextMeshProUGUI cancelTMP = cancelTextObj.AddComponent<TextMeshProUGUI>();
        cancelTMP.text = "HỦY BỎ";
        cancelTMP.fontSize = 18;
        cancelTMP.alignment = TextAlignmentOptions.Center;
        cancelTMP.color = Color.white;
        RectTransform cancelTextRect = cancelTextObj.GetComponent<RectTransform>();
        cancelTextRect.anchorMin = Vector2.zero;
        cancelTextRect.anchorMax = Vector2.one;
        cancelTextRect.sizeDelta = Vector2.zero;

        cancelBtn.onClick.AddListener(() => Destroy(gameObject));
    }

    private void OnDispatchConfirmed()
    {
        List<UnitController> selectedSoldiersToDispatch = new List<UnitController>();

        foreach (var data in barracksList)
        {
            if (data != null && data.isSelected && data.spawner != null)
            {
                List<UnitController> soldiers = data.spawner.GetActiveSoldierControllers();
                foreach (var soldier in soldiers)
                {
                    if (soldier != null && soldier.gameObject.activeInHierarchy && !selectedSoldiersToDispatch.Contains(soldier))
                    {
                        selectedSoldiersToDispatch.Add(soldier);
                    }
                }
            }
        }

        if (selectedSoldiersToDispatch.Count == 0)
        {
            if (UIManager.Ins != null) UIManager.Ins.ShowWarning("Vui lòng chọn ít nhất 1 Doanh trại để xuất quân!");
            Debug.LogWarning("[TroopDispatchUI] Không có Doanh Trại nào được chọn để xuất quân!");
            return;
        }

        Debug.Log($"[TroopDispatchUI] ⚔️ Đã chọn {selectedSoldiersToDispatch.Count} lính từ các Doanh Trại được chọn để xuất quân hành quân!");

        Vector3 centerTarget = attackPosition;
        int totalSoldiers = selectedSoldiersToDispatch.Count;

        // Chỉ gửi những lính của Doanh Trại ĐƯỢC CHỌN đi hành quân (với vị trí Formation đồng đều)
        for (int i = 0; i < totalSoldiers; i++)
        {
            var soldier = selectedSoldiersToDispatch[i];
            if (soldier != null)
            {
                float angle = (360f / Mathf.Max(1, totalSoldiers)) * i;
                Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * 2.0f;
                soldier.StartExpeditionMarch(centerTarget + offset, -1);
            }
        }

        // Bắt đầu Coroutine giám sát khi nhóm Lính hành quân này tới nơi thì nổ SceneBattle
        GameObject runner = new GameObject("ExpeditionBattleTriggerRunner");
        ExpeditionBattleTrigger trigger = runner.AddComponent<ExpeditionBattleTrigger>();
        trigger.StartMonitoring(selectedSoldiersToDispatch, enemyTarget, battleSceneName);

        Destroy(gameObject);
    }
}
