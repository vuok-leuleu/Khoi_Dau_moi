using System.Collections.Generic;
using UnityEngine;

public class TabGroupManager : MonoBehaviour
{
    [SerializeField] private List<TabButton> tabButtons;
    [SerializeField] private TabButton defaultTab;
    [SerializeField] private QuestUIController questUIController;

    private TabButton selectedTab;

    private void Start()
    {
        if (questUIController == null)
        {
            questUIController = GetComponentInParent<QuestUIController>();
        }

        // Mặc định chọn tab đầu tiên khi mở bảng
        if (defaultTab != null)
        {
            OnTabSelected(defaultTab);
        }
        else if (tabButtons != null && tabButtons.Count > 0)
        {
            OnTabSelected(tabButtons[0]);
        }
    }

    public void OnTabSelected(TabButton button)
    {
        selectedTab = button;

        foreach (TabButton tab in tabButtons)
        {
            if (tab == selectedTab)
            {
                tab.SetSelected(true);
            }
            else
            {
                tab.SetSelected(false);
            }
        }

        // Gọi logic chuyển trang nội dung tương ứng ở QuestUIController
        if (questUIController != null && selectedTab != null)
        {
            questUIController.SwitchTab(selectedTab.QuestType);
        }
    }
}