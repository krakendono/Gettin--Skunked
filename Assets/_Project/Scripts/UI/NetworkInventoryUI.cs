using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Simple UI binder for NetworkInventory.
/// - Instantiates a grid of InventorySlotUI elements
/// - Updates display from replicated NetworkInventory slots
/// - Supports drag/drop between slots to move/merge/swap via RPC_RequestMoveStack
/// - Supports right-click to consume 1 via RPC_RequestUseSlot
///
/// Usage:
/// - Create a Canvas with a Panel (RectTransform) to hold slots
/// - Assign "slotsParent" to that RectTransform and set a Slot prefab in "slotPrefab"
/// - Ensure there is an EventSystem in the scene
/// </summary>
[AddComponentMenu("UI/Inventory/Network Inventory UI")]
public class NetworkInventoryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform slotsParent;
    [SerializeField] private InventorySlotUI slotPrefab;

    [Header("Layout")] 
    [SerializeField] private int columns = 6;
    [SerializeField] private Vector2 cellSize = new Vector2(100, 30);
    [SerializeField] private Vector2 spacing = new Vector2(8, 8);

    [Header("Input")] 
    [Tooltip("Hold Shift while dropping to move 1 item instead of the whole stack.")]
    [SerializeField] private bool shiftMovesOne = true;

    private NetworkInventory _inventory;
    private readonly List<InventorySlotUI> _slots = new();
    private float _nextRefresh;
    private const float RefreshInterval = 0.1f; // seconds

    void Awake()
    {
        if (slotsParent == null)
        {
            Debug.LogWarning("NetworkInventoryUI: slotsParent not set");
        }
        if (slotPrefab == null)
        {
            Debug.LogWarning("NetworkInventoryUI: slotPrefab not set");
        }
    }

    void Start()
    {
        EnsureEventSystem();
        TryFindInventory();
        BuildSlots();
        RefreshAll();
    }

    void Update()
    {
        if (_inventory == null || (_inventory.Object != null && !_inventory.Object.HasInputAuthority))
        {
            // Try to find again if missing or wrong authority
            TryFindInventory();
        }

        if (Time.unscaledTime >= _nextRefresh)
        {
            RefreshAll();
            _nextRefresh = Time.unscaledTime + RefreshInterval;
        }
    }

    private void TryFindInventory()
    {
        // Prefer local input-authority inventory
        var runner = FindObjectOfType<NetworkRunner>();
        if (runner != null && runner.IsRunning)
        {
            foreach (var p in runner.ActivePlayers)
            {
                if (runner.TryGetPlayerObject(p, out var obj) && obj != null && obj.HasInputAuthority)
                {
                    var inv = obj.GetComponent<NetworkInventory>();
                    if (inv != null)
                    {
                        _inventory = inv;
                        return;
                    }
                }
            }
        }
        else
        {
            // Offline: allow binding to any NetworkInventory in scene
            var any = FindObjectOfType<NetworkInventory>();
            if (any != null)
            {
                _inventory = any;
            }
        }
    }

    private void BuildSlots()
    {
        if (slotsParent == null || slotPrefab == null) return;
        // Clear existing
        foreach (Transform child in slotsParent)
        {
            Destroy(child.gameObject);
        }
        _slots.Clear();

        int count = _inventory != null ? _inventory.GetSlotCount() : NetworkInventory.MaxSlots;
        // Basic grid layout without requiring a GridLayoutGroup component
        int col = 0, row = 0;
        for (int i = 0; i < count; i++)
        {
            var slot = Instantiate(slotPrefab, slotsParent);
            slot.Initialize(this, i);
            var rt = slot.GetComponent<RectTransform>();
            if (rt != null)
            {
                Vector2 pos = new Vector2(col * (cellSize.x + spacing.x), -row * (cellSize.y + spacing.y));
                rt.anchoredPosition = pos;
                rt.sizeDelta = cellSize;
            }
            _slots.Add(slot);

            col++;
            if (col >= columns)
            {
                col = 0; row++;
            }
        }
        // Resize parent to fit
        var prt = slotsParent as RectTransform;
        if (prt != null)
        {
            int rows = Mathf.CeilToInt(count / (float)columns);
            float width = columns * cellSize.x + (columns - 1) * spacing.x;
            float height = rows * cellSize.y + (rows - 1) * spacing.y;
            prt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            prt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
    }

    private void RefreshAll()
    {
        if (_slots.Count == 0) return;
        for (int i = 0; i < _slots.Count; i++)
        {
            var slotUI = _slots[i];
            if (_inventory == null)
            {
                slotUI.SetEmpty();
                continue;
            }
            var data = _inventory.GetSlot(i);
            if (data.IsEmpty)
            {
                slotUI.SetEmpty();
            }
            else
            {
                slotUI.SetItem(data.Name.ToString(), data.Quantity);
            }
        }
    }

    public void OnSlotRightClick(int index)
    {
        if (_inventory == null) return;
        _inventory.RPC_RequestUseSlot((byte)index, 1, 0);
    }

    // Drag/drop API used by slot components
    public void OnBeginDrag(int index)
    {
        // no-op, but available for visuals
    }

    public void OnDropOnto(int fromIndex, int toIndex)
    {
        if (_inventory == null) return;
        if (fromIndex == toIndex) return;
        ushort amount = 0; // 0 = whole stack (we clamp on server). Use 1 if Shift is held.
        if (shiftMovesOne && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
        {
            amount = 1;
        }
        _inventory.RPC_RequestMoveStack((byte)fromIndex, (byte)toIndex, amount, 0);
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }
    }
}
