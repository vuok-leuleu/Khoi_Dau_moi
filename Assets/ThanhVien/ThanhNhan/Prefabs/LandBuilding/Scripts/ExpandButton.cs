using UnityEngine;
using UnityEngine.EventSystems;

public class ExpandButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private ExpandDirection direction;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (LandGridManager.Ins != null)
        {
            LandGridManager.Ins.RequestExpandGrid(direction);
        }
    }
}