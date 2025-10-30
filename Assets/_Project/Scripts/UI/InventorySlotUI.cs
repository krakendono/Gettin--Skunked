using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Visual for a single inventory slot with basic interactions.
/// - Left click: select (reserved)
/// - Right click: consume 1 (asks parent UI)
/// - Drag from one slot and drop on another to move/merge/swap
///
/// This component expects a Text (or TMP via Text subtype) to display the label.
/// </summary>
[AddComponentMenu("UI/Inventory/Inventory Slot UI")]
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField] private Text label; // Can be replaced by TMP_Text; assign accordingly

    private NetworkInventoryUI _owner;
    private int _index;

    private static int s_dragSourceIndex = -1;

    public void Initialize(NetworkInventoryUI owner, int index)
    {
        _owner = owner;
        _index = index;
        if (label == null)
        {
            label = GetComponentInChildren<Text>();
        }
        SetEmpty();
    }

    public void SetEmpty()
    {
        if (label != null) label.text = "-";
    }

    public void SetItem(string name, int qty)
    {
        if (label != null) label.text = qty > 1 ? $"{name} x{qty}" : name;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            _owner?.OnSlotRightClick(_index);
        }
        // Left click selection reserved for future
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        s_dragSourceIndex = _index;
        _owner?.OnBeginDrag(_index);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // If ended drag without dropping on a slot, clear drag state
        s_dragSourceIndex = -1;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (s_dragSourceIndex >= 0)
        {
            int from = s_dragSourceIndex;
            int to = _index;
            s_dragSourceIndex = -1;
            _owner?.OnDropOnto(from, to);
        }
    }
}
