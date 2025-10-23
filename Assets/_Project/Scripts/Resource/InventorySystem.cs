using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public enum ItemType
{
    Resource,
    Weapon,
    KeyItem
}

[System.Serializable]
public enum ResourceType
{
    Wood,
    Stone,
    Metal,
    Food,
    Honey,
    HoneyComb,
    Bones,
    Other
}

[System.Serializable]
public enum WeaponType
{
    Melee,
    Ranged,
    Tool
}

// Base class for all inventory items
[System.Serializable]
public abstract class InventoryItem
{
    public string itemName;
    public string description;
    public Sprite icon;
    public int quantity;
    public int maxStackSize;
    public ItemType itemType;
    
    public InventoryItem(string name, string desc, int qty = 1, int maxStack = 99)
    {
        itemName = name;
        description = desc;
        quantity = qty;
        maxStackSize = maxStack;
    }
    
    public abstract bool CanStackWith(InventoryItem other);
    public abstract void Use();
}

// Resource items (wood, stone, etc.)
[System.Serializable]
public class ResourceItem : InventoryItem
{
    public ResourceType resourceType;
    
    public ResourceItem(string name, ResourceType type, int qty = 1, int maxStack = 99) 
        : base(name, $"A basic {type.ToString().ToLower()} resource", qty, maxStack)
    {
        itemType = ItemType.Resource;
        resourceType = type;
    }
    
    public override bool CanStackWith(InventoryItem other)
    {
        if (other is ResourceItem otherResource)
        {
            return resourceType == otherResource.resourceType && itemName == otherResource.itemName;
        }
        return false;
    }
    
    public override void Use()
    {
        Debug.Log($"Used {itemName} resource");
    }
}

// Weapon items
[System.Serializable]
public class WeaponItem : InventoryItem
{
    public WeaponType weaponType;
    public float damage;
    public float durability;
    public float maxDurability;
    public GameObject weaponPrefab;
    
    public WeaponItem(string name, WeaponType type, float dmg, float maxDur, GameObject prefab = null) 
        : base(name, $"A {type.ToString().ToLower()} weapon", 1, 1)
    {
        itemType = ItemType.Weapon;
        weaponType = type;
        damage = dmg;
        durability = maxDur;
        maxDurability = maxDur;
        weaponPrefab = prefab;
    }
    
    public override bool CanStackWith(InventoryItem other)
    {
        return false; // Weapons don't stack
    }
    
    public override void Use()
    {
        Debug.Log($"Equipped {itemName}");
        // This would trigger weapon equipping logic
    }
    
    public float GetDurabilityPercentage()
    {
        return maxDurability > 0 ? durability / maxDurability : 0f;
    }
}

// Key items (quest items, keys, etc.)
[System.Serializable]
public class KeyItem : InventoryItem
{
    public string keyId;
    public bool isQuestItem;
    
    public KeyItem(string name, string id, bool quest = false) 
        : base(name, quest ? "An important quest item" : "A special key item", 1, 1)
    {
        itemType = ItemType.KeyItem;
        keyId = id;
        isQuestItem = quest;
    }
    
    public override bool CanStackWith(InventoryItem other)
    {
        return false; // Key items don't stack
    }
    
    public override void Use()
    {
        Debug.Log($"Used key item: {itemName}");
        // This would trigger key item usage logic
    }
}

// Inventory slot structure
[System.Serializable]
public class InventorySlot
{
    public InventoryItem item;
    public bool isEmpty => item == null || item.quantity <= 0;
    
    public bool AddItem(InventoryItem newItem)
    {
        if (isEmpty)
        {
            item = newItem;
            return true;
        }
        
        if (item.CanStackWith(newItem))
        {
            int spaceAvailable = item.maxStackSize - item.quantity;
            int amountToAdd = Mathf.Min(spaceAvailable, newItem.quantity);
            
            item.quantity += amountToAdd;
            newItem.quantity -= amountToAdd;
            
            return newItem.quantity <= 0;
        }
        
        return false;
    }
    
    public void RemoveItem(int amount = 1)
    {
        if (!isEmpty)
        {
            item.quantity -= amount;
            if (item.quantity <= 0)
            {
                item = null;
            }
        }
    }
    
    public void ClearSlot()
    {
        item = null;
    }
}

public class InventorySystem : MonoBehaviour
{
    [Header("Inventory Settings")]
    public int inventorySize = 30;
    public int resourceSlotsCount = 10;
    public int weaponSlotsCount = 8;
    public int keyItemSlotsCount = 12;
    
    [Header("UI Settings")]
    public bool showDebugInventory = true;
    public KeyCode inventoryToggleKey = KeyCode.Tab;
    
    [Header("Audio")]
    public AudioClip pickupSound;
    public AudioClip useSound;
    public AudioClip equipSound;
    
    // Inventory storage
    private List<InventorySlot> resourceSlots;
    private List<InventorySlot> weaponSlots;
    private List<InventorySlot> keyItemSlots;
    
    // Currently equipped items
    private WeaponItem equippedWeapon;
    
    // UI state
    private bool isInventoryOpen = false;
    private Vector2 scrollPosition = Vector2.zero;
    
    // Events
    public System.Action<InventoryItem> OnItemAdded;
    public System.Action<InventoryItem> OnItemRemoved;
    public System.Action<WeaponItem> OnWeaponEquipped;
    public System.Action<WeaponItem> OnWeaponUnequipped;
    
    private AudioSource audioSource;
    
    void Start()
    {
        InitializeInventory();
        SetupAudio();
        
        // Add some example items for testing
        AddExampleItems();
    }
    
    void Update()
    {
        HandleInput();
    }
    
    void InitializeInventory()
    {
        resourceSlots = new List<InventorySlot>();
        weaponSlots = new List<InventorySlot>();
        keyItemSlots = new List<InventorySlot>();
        
        // Initialize resource slots
        for (int i = 0; i < resourceSlotsCount; i++)
        {
            resourceSlots.Add(new InventorySlot());
        }
        
        // Initialize weapon slots
        for (int i = 0; i < weaponSlotsCount; i++)
        {
            weaponSlots.Add(new InventorySlot());
        }
        
        // Initialize key item slots
        for (int i = 0; i < keyItemSlotsCount; i++)
        {
            keyItemSlots.Add(new InventorySlot());
        }
        
        Debug.Log($"Inventory initialized with {resourceSlotsCount} resource slots, {weaponSlotsCount} weapon slots, and {keyItemSlotsCount} key item slots");
    }
    
    void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }
    
    void HandleInput()
    {
        if (Input.GetKeyDown(inventoryToggleKey))
        {
            ToggleInventory();
        }
    }
    
    void ToggleInventory()
    {
        isInventoryOpen = !isInventoryOpen;
        Debug.Log($"Inventory {(isInventoryOpen ? "opened" : "closed")}");
    }
    
    // Add item to inventory
    public bool AddItem(InventoryItem item)
    {
        if (item == null) return false;
        
        List<InventorySlot> targetSlots = GetSlotsForItemType(item.itemType);
        
        // Try to stack with existing items first
        foreach (var slot in targetSlots)
        {
            if (!slot.isEmpty && slot.item.CanStackWith(item))
            {
                if (slot.AddItem(item))
                {
                    OnItemAdded?.Invoke(item);
                    PlaySound(pickupSound);
                    return true;
                }
            }
        }
        
        // Find empty slot
        foreach (var slot in targetSlots)
        {
            if (slot.isEmpty)
            {
                if (slot.AddItem(item))
                {
                    OnItemAdded?.Invoke(item);
                    PlaySound(pickupSound);
                    return true;
                }
            }
        }
        
        Debug.Log($"Could not add {item.itemName} to inventory - no space available");
        return false;
    }
    
    // Remove item from inventory
    public bool RemoveItem(string itemName, int quantity = 1)
    {
        foreach (var slotList in new[] { resourceSlots, weaponSlots, keyItemSlots })
        {
            foreach (var slot in slotList)
            {
                if (!slot.isEmpty && slot.item.itemName == itemName)
                {
                    int amountToRemove = Mathf.Min(quantity, slot.item.quantity);
                    slot.RemoveItem(amountToRemove);
                    
                    OnItemRemoved?.Invoke(slot.item);
                    return true;
                }
            }
        }
        
        return false;
    }
    
    // Get item count
    public int GetItemCount(string itemName)
    {
        int totalCount = 0;
        
        foreach (var slotList in new[] { resourceSlots, weaponSlots, keyItemSlots })
        {
            foreach (var slot in slotList)
            {
                if (!slot.isEmpty && slot.item.itemName == itemName)
                {
                    totalCount += slot.item.quantity;
                }
            }
        }
        
        return totalCount;
    }
    
    // Check if inventory has item
    public bool HasItem(string itemName, int quantity = 1)
    {
        return GetItemCount(itemName) >= quantity;
    }
    
    // Equip weapon
    public bool EquipWeapon(WeaponItem weapon)
    {
        if (weapon == null) return false;
        
        // Unequip current weapon first
        if (equippedWeapon != null)
        {
            UnequipWeapon();
        }
        
        equippedWeapon = weapon;
        OnWeaponEquipped?.Invoke(weapon);
        PlaySound(equipSound);
        
        Debug.Log($"Equipped weapon: {weapon.itemName}");
        return true;
    }
    
    // Unequip weapon
    public void UnequipWeapon()
    {
        if (equippedWeapon != null)
        {
            OnWeaponUnequipped?.Invoke(equippedWeapon);
            Debug.Log($"Unequipped weapon: {equippedWeapon.itemName}");
            equippedWeapon = null;
        }
    }
    
    // Use item
    public void UseItem(InventoryItem item)
    {
        if (item == null) return;
        
        item.Use();
        PlaySound(useSound);
        
        // Handle specific item types
        if (item is WeaponItem weapon)
        {
            EquipWeapon(weapon);
        }
    }
    
    // Helper methods
    private List<InventorySlot> GetSlotsForItemType(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.Resource => resourceSlots,
            ItemType.Weapon => weaponSlots,
            ItemType.KeyItem => keyItemSlots,
            _ => resourceSlots
        };
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
    
    // Get all items of a specific type
    public List<InventoryItem> GetItemsByType(ItemType itemType)
    {
        var slots = GetSlotsForItemType(itemType);
        return slots.Where(slot => !slot.isEmpty).Select(slot => slot.item).ToList();
    }
    
    // Get all resources of a specific type
    public List<ResourceItem> GetResourcesByType(ResourceType resourceType)
    {
        return resourceSlots
            .Where(slot => !slot.isEmpty && slot.item is ResourceItem resource && resource.resourceType == resourceType)
            .Select(slot => slot.item as ResourceItem)
            .ToList();
    }
    
    // Get all weapons of a specific type
    public List<WeaponItem> GetWeaponsByType(WeaponType weaponType)
    {
        return weaponSlots
            .Where(slot => !slot.isEmpty && slot.item is WeaponItem weapon && weapon.weaponType == weaponType)
            .Select(slot => slot.item as WeaponItem)
            .ToList();
    }
    
    // Clear inventory
    public void ClearInventory()
    {
        foreach (var slot in resourceSlots) slot.ClearSlot();
        foreach (var slot in weaponSlots) slot.ClearSlot();
        foreach (var slot in keyItemSlots) slot.ClearSlot();
        
        equippedWeapon = null;
        Debug.Log("Inventory cleared");
    }
    
    // Add example items for testing
    private void AddExampleItems()
    {
        // Add some wood resources
        AddItem(new ResourceItem("Oak Wood", ResourceType.Wood, 25));
        AddItem(new ResourceItem("Pine Wood", ResourceType.Wood, 15));
        AddItem(new ResourceItem("Iron Ore", ResourceType.Metal, 10));
        AddItem(new ResourceItem("Stone", ResourceType.Stone, 30));
        
        // Add some weapons
        AddItem(new WeaponItem("Battle Axe", WeaponType.Melee, 50f, 100f));
        AddItem(new WeaponItem("Pistol", WeaponType.Ranged, 25f, 80f));
        AddItem(new WeaponItem("Hunting Knife", WeaponType.Melee, 20f, 60f));
        
        // Add some key items
        AddItem(new KeyItem("Cabin Key", "cabin_key_01"));
        AddItem(new KeyItem("Map Fragment", "map_fragment_01", true));
    }
    
    // Debug GUI
    void OnGUI()
    {
        if (!showDebugInventory || !isInventoryOpen) return;
        
        GUI.Window(0, new Rect(50, 50, 600, 500), DrawInventoryWindow, "Inventory System");
    }
    
    void DrawInventoryWindow(int windowID)
    {
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        
        // Resources section
        GUILayout.Label("=== RESOURCES ===", GUI.skin.box);
        DrawItemSlots(resourceSlots, "Resource");
        
        GUILayout.Space(10);
        
        // Weapons section
        GUILayout.Label("=== WEAPONS ===", GUI.skin.box);
        DrawItemSlots(weaponSlots, "Weapon");
        
        GUILayout.Space(10);
        
        // Key items section
        GUILayout.Label("=== KEY ITEMS ===", GUI.skin.box);
        DrawItemSlots(keyItemSlots, "Key Item");
        
        GUILayout.Space(10);
        
        // Currently equipped
        GUILayout.Label("=== EQUIPPED ===", GUI.skin.box);
        if (equippedWeapon != null)
        {
            GUILayout.Label($"Weapon: {equippedWeapon.itemName} (Durability: {equippedWeapon.GetDurabilityPercentage():P0})");
            if (GUILayout.Button("Unequip"))
            {
                UnequipWeapon();
            }
        }
        else
        {
            GUILayout.Label("No weapon equipped");
        }
        
        GUILayout.Space(10);
        
        // Controls
        GUILayout.Label("=== CONTROLS ===", GUI.skin.box);
        GUILayout.Label($"Press {inventoryToggleKey} to toggle inventory");
        GUILayout.Label("Click 'Use' to use/equip items");
        
        if (GUILayout.Button("Clear All"))
        {
            ClearInventory();
        }
        
        GUILayout.EndScrollView();
        
        GUI.DragWindow();
    }
    
    void DrawItemSlots(List<InventorySlot> slots, string sectionName)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            
            if (!slot.isEmpty)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                
                // Item info
                GUILayout.Label($"{slot.item.itemName} x{slot.item.quantity}");
                
                // Additional info based on item type
                if (slot.item is WeaponItem weapon)
                {
                    GUILayout.Label($"Dmg: {weapon.damage:F0}, Dur: {weapon.GetDurabilityPercentage():P0}");
                }
                else if (slot.item is ResourceItem resource)
                {
                    GUILayout.Label($"Type: {resource.resourceType}");
                }
                
                GUILayout.FlexibleSpace();
                
                // Use button
                if (GUILayout.Button("Use", GUILayout.Width(50)))
                {
                    UseItem(slot.item);
                }
                
                // Remove button
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    slot.RemoveItem(1);
                }
                
                GUILayout.EndHorizontal();
            }
        }
    }
    
    // Public getters for external access
    public WeaponItem GetEquippedWeapon() => equippedWeapon;
    public bool IsInventoryOpen() => isInventoryOpen;
    public int GetEmptySlots(ItemType itemType)
    {
        var slots = GetSlotsForItemType(itemType);
        return slots.Count(slot => slot.isEmpty);
    }
}
