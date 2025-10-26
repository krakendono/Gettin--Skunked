using UnityEngine;
using Fusion;
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
    
    [Header("Debug")]
    public bool showDebugInventory = true;
    public KeyCode inventoryToggleKey = KeyCode.Tab;
    public bool addExampleItemsOnStart = true; // Toggle to disable example items
    
    [Header("Item Dropping")]
    public Transform itemDropPoint; // Where to spawn dropped items
    public float dropHorizontalForce = 3f; // Horizontal scatter force (outward spread)
    public float dropUpwardForce = 5f; // Upward force (positive = up, negative = down)
    public float dropRadius = 1f; // Random spread radius for dropped items
    public bool enableItemDropping = true;
    
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
        
        // Add some example items for testing (only if enabled)
        if (addExampleItemsOnStart)
        {
            AddExampleItems();
        }
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
        
        // Close inventory with ESC key
        if (Input.GetKeyDown(KeyCode.Escape) && isInventoryOpen)
        {
            CloseInventory();
        }
    }
    
    void ToggleInventory()
    {
        isInventoryOpen = !isInventoryOpen;
        Debug.Log($"Inventory {(isInventoryOpen ? "opened" : "closed")}");
    }
    
    void CloseInventory()
    {
        if (isInventoryOpen)
        {
            isInventoryOpen = false;
            Debug.Log("Inventory closed");
        }
    }
    
    void OpenInventory()
    {
        if (!isInventoryOpen)
        {
            isInventoryOpen = true;
            Debug.Log("Inventory opened");
        }
    }
    
    // Add item to inventory
    public bool AddItem(InventoryItem item, bool playSound = true)
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
                    if (playSound) PlaySound(pickupSound);
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
                    if (playSound) PlaySound(pickupSound);
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
    
    // Drop item from inventory
    public bool DropItem(InventoryItem item, int quantity = 1)
    {
        if (item == null || !enableItemDropping) return false;

        // Networked path: request server drop; do not mutate locally
        var runner = FindFirstObjectByType<NetworkRunner>();
        if (runner != null && runner.IsRunning)
        {
            foreach (var p in runner.ActivePlayers)
            {
                if (runner.TryGetPlayerObject(p, out var obj))
                {
                    var netInv = obj.GetComponent<NetworkInventory>();
                    if (netInv != null && netInv.Object != null && netInv.Object.HasInputAuthority)
                    {
                        netInv.RPC_RequestDrop(item.itemName, quantity);
                        return true;
                    }
                }
            }
            return false;
        }
        
        // Find the item in inventory and remove it
        foreach (var slotList in new[] { resourceSlots, weaponSlots, keyItemSlots })
        {
            foreach (var slot in slotList)
            {
                if (!slot.isEmpty && slot.item == item)
                {
                    int amountToDrop = Mathf.Min(quantity, slot.item.quantity);
                    
                    // Create dropped item
                    SpawnDroppedItem(item, amountToDrop);
                    
                    // Remove from inventory
                    slot.RemoveItem(amountToDrop);
                    OnItemRemoved?.Invoke(item);
                    
                    Debug.Log($"Dropped {amountToDrop}x {item.itemName}");
                    return true;
                }
            }
        }
        
        return false;
    }
    
    // Drop item by name
    public bool DropItemByName(string itemName, int quantity = 1)
    {
        // Networked path: request server drop; do not mutate locally
        var runner = FindFirstObjectByType<NetworkRunner>();
        if (runner != null && runner.IsRunning)
        {
            foreach (var p in runner.ActivePlayers)
            {
                if (runner.TryGetPlayerObject(p, out var obj))
                {
                    var netInv = obj.GetComponent<NetworkInventory>();
                    if (netInv != null && netInv.Object != null && netInv.Object.HasInputAuthority)
                    {
                        netInv.RPC_RequestDrop(itemName, quantity);
                        return true;
                    }
                }
            }
            return false;
        }

        foreach (var slotList in new[] { resourceSlots, weaponSlots, keyItemSlots })
        {
            foreach (var slot in slotList)
            {
                if (!slot.isEmpty && slot.item.itemName == itemName)
                {
                    return DropItem(slot.item, quantity);
                }
            }
        }
        
        return false;
    }
    
    // Spawn the actual dropped item in the world
    void SpawnDroppedItem(InventoryItem originalItem, int quantity)
    {
        // Determine spawn position
        Vector3 spawnPosition = GetDropPosition();
        
        // Create appropriate pickup based on item type
        GameObject droppedObject = null;
        
        if (originalItem is ResourceItem resource)
        {
            droppedObject = CreateResourcePickup(resource, quantity, spawnPosition);
        }
        else if (originalItem is WeaponItem weapon)
        {
            droppedObject = CreateWeaponPickup(weapon, spawnPosition);
        }
        else if (originalItem is KeyItem keyItem)
        {
            droppedObject = CreateKeyItemPickup(keyItem, spawnPosition);
        }
        
        // Apply physics if rigidbody exists
        if (droppedObject != null)
        {
            Rigidbody rb = droppedObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Apply upward/downward force (positive = up, negative = down)
                Vector3 verticalForce = Vector3.up * dropUpwardForce;
                rb.AddForce(verticalForce, ForceMode.Impulse);
                
                // Then add horizontal spread force
                Vector3 randomDirection = new Vector3(
                    Random.Range(-1f, 1f),
                    0f, // No Y component here, we control Y with dropUpwardForce
                    Random.Range(-1f, 1f)
                ).normalized;
                
                rb.AddForce(randomDirection * dropHorizontalForce, ForceMode.Impulse);
            }
        }
    }
    
    Vector3 GetDropPosition()
    {
        Vector3 basePosition;
        
        // Use drop point if assigned, otherwise use player position
        if (itemDropPoint != null)
        {
            basePosition = itemDropPoint.position;
        }
        else
        {
            // Try to find player
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                basePosition = player.transform.position + Vector3.forward * 2f; // Drop in front of player
            }
            else
            {
                basePosition = Vector3.zero; // Fallback
            }
        }
        
        // Add random spread
        Vector2 randomOffset = Random.insideUnitCircle * dropRadius;
        return basePosition + new Vector3(randomOffset.x, 0.5f, randomOffset.y);
    }
    
    GameObject CreateResourcePickup(ResourceItem resource, int quantity, Vector3 position)
    {
        // Create a simple cube for resource
        GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pickup.transform.position = position;
        pickup.transform.localScale = Vector3.one * 0.5f;
        pickup.name = $"Dropped_{resource.itemName}";
        
        // Set material color based on resource type
        Renderer renderer = pickup.GetComponent<Renderer>();
        renderer.material.color = GetResourceColor(resource.resourceType);
        
        // Add ResourcePickup component
        ResourcePickup pickupScript = pickup.AddComponent<ResourcePickup>();
        pickupScript.resourceName = resource.itemName;
        pickupScript.resourceType = resource.resourceType;
        pickupScript.quantity = quantity;
        pickupScript.autoPickup = false; // Manual pickup for dropped items
        pickupScript.bobHeight = 0f; // No bobbing for dropped items
        pickupScript.bobSpeed = 0f; // No bobbing animation
        
        return pickup;
    }
    
    GameObject CreateWeaponPickup(WeaponItem weapon, Vector3 position)
    {
        // Create a capsule for weapon
        GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        pickup.transform.position = position;
        pickup.transform.localScale = new Vector3(0.3f, 0.8f, 0.3f);
        pickup.name = $"Dropped_{weapon.itemName}";
        
        // Set material color based on weapon type
        Renderer renderer = pickup.GetComponent<Renderer>();
        renderer.material.color = GetWeaponColor(weapon.weaponType);
        
        // Add WeaponPickup component
        WeaponPickup pickupScript = pickup.AddComponent<WeaponPickup>();
        pickupScript.weaponName = weapon.itemName;
        pickupScript.weaponType = weapon.weaponType;
        pickupScript.damage = weapon.damage;
        pickupScript.maxDurability = weapon.durability; // Use current durability
        pickupScript.weaponPrefab = weapon.weaponPrefab;
        pickupScript.autoPickup = false; // Manual pickup for dropped items
        pickupScript.bobHeight = 0f; // No bobbing for dropped items
        pickupScript.bobSpeed = 0f; // No bobbing animation
        pickupScript.rotationSpeed = 0f; // No rotation for dropped items
        
        return pickup;
    }
    
    GameObject CreateKeyItemPickup(KeyItem keyItem, Vector3 position)
    {
        // Create a sphere for key items
        GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pickup.transform.position = position;
        pickup.transform.localScale = Vector3.one * 0.4f;
        pickup.name = $"Dropped_{keyItem.itemName}";
        
        // Set material color (golden for key items)
        Renderer renderer = pickup.GetComponent<Renderer>();
        renderer.material.color = Color.yellow;
        
        // Add a simple pickup script (you might want to create a KeyItemPickup script)
        ResourcePickup pickupScript = pickup.AddComponent<ResourcePickup>();
        pickupScript.resourceName = keyItem.itemName;
        pickupScript.resourceType = ResourceType.Other;
        pickupScript.quantity = 1;
        pickupScript.autoPickup = false; // Manual pickup for dropped items
        pickupScript.bobHeight = 0f; // No bobbing for dropped items
        pickupScript.bobSpeed = 0f; // No bobbing animation
        
        return pickup;
    }
    
    Color GetResourceColor(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Wood => new Color(0.6f, 0.3f, 0.1f), // Brown
            ResourceType.Stone => Color.gray,
            ResourceType.Metal => new Color(0.8f, 0.8f, 0.9f), // Silver
            ResourceType.Food => Color.green,
            ResourceType.Honey => Color.yellow,
            ResourceType.HoneyComb => new Color(1f, 0.8f, 0.2f), // Orange-yellow
            ResourceType.Bones => new Color(0.9f, 0.9f, 0.8f), // Off-white
            _ => Color.white
        };
    }
    
    Color GetWeaponColor(WeaponType weaponType)
    {
        return weaponType switch
        {
            WeaponType.Melee => Color.red,
            WeaponType.Ranged => Color.blue,
            WeaponType.Tool => new Color(0.5f, 0.3f, 0.1f), // Dark brown
            _ => Color.magenta
        };
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
        // Add some wood resources (silent - no pickup sound)
        AddItem(new ResourceItem("Oak Wood", ResourceType.Wood, 25), false);
        AddItem(new ResourceItem("Pine Wood", ResourceType.Wood, 15), false);
        AddItem(new ResourceItem("Iron Ore", ResourceType.Metal, 10), false);
        AddItem(new ResourceItem("Stone", ResourceType.Stone, 30), false);
        
        // Add some weapons (silent - no pickup sound)
        AddItem(new WeaponItem("Battle Axe", WeaponType.Melee, 50f, 100f), false);
        AddItem(new WeaponItem("Pistol", WeaponType.Ranged, 25f, 80f), false);
        AddItem(new WeaponItem("Hunting Knife", WeaponType.Melee, 20f, 60f), false);
        
        // Add some key items (silent - no pickup sound)
        AddItem(new KeyItem("Cabin Key", "cabin_key_01"), false);
        AddItem(new KeyItem("Map Fragment", "map_fragment_01", true), false);
    }
    
    // Debug GUI
    void OnGUI()
    {
        if (!showDebugInventory || !isInventoryOpen) return;
        
        GUI.Window(0, new Rect(50, 50, 650, 540), DrawInventoryWindow, "Inventory System");
    }
    
    void DrawInventoryWindow(int windowID)
    {
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        
        bool networked = false;
        NetworkInventory netInv = null;
        var runner = FindFirstObjectByType<NetworkRunner>();
        if (runner != null && runner.IsRunning)
        {
            // Find local player's network inventory
            foreach (var p in runner.ActivePlayers)
            {
                if (runner.TryGetPlayerObject(p, out var obj))
                {
                    var inv = obj.GetComponent<NetworkInventory>();
                    if (inv != null && inv.Object != null && inv.Object.HasInputAuthority)
                    {
                        netInv = inv;
                        networked = true;
                        break;
                    }
                }
            }
        }

        if (networked && netInv != null)
        {
            GUILayout.Label("=== NETWORK INVENTORY (READ-ONLY VIEW) ===", GUI.skin.box);
            DrawNetworkInventory(netInv);
            GUILayout.Space(10);
            GUILayout.Label("Note: In networked sessions this UI is read-only in this pass.");
        }
        else
        {
            // Local inventory view & controls
            GUILayout.Label("=== RESOURCES ===", GUI.skin.box);
            DrawItemSlots(resourceSlots, "Resource");
            
            GUILayout.Space(10);
            
            GUILayout.Label("=== WEAPONS ===", GUI.skin.box);
            DrawItemSlots(weaponSlots, "Weapon");
            
            GUILayout.Space(10);
            
            GUILayout.Label("=== KEY ITEMS ===", GUI.skin.box);
            DrawItemSlots(keyItemSlots, "Key Item");
        }
        
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
        GUILayout.Label("Click 'Use' to use/equip items (offline only in this pass)");
        
        GUI.enabled = !networked;
        if (GUILayout.Button("Clear All"))
        {
            ClearInventory();
        }
        GUI.enabled = true;
        
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
                
                // Drop button
                if (GUILayout.Button("Drop", GUILayout.Width(50)))
                {
                    DropItem(slot.item, 1);
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

    void DrawNetworkInventory(NetworkInventory netInv)
    {
        int count = netInv.GetSlotCount();
        for (int i = 0; i < count; i++)
        {
            var slot = netInv.GetSlot(i);
            if (slot.IsEmpty) continue;

            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label($"[{i:00}] {slot.Name} x{slot.Quantity}");

            if (slot.ItemType == ItemType.Resource)
            {
                GUILayout.Label($"Type: {slot.ResourceType}");
            }
            else if (slot.ItemType == ItemType.Weapon)
            {
                GUILayout.Label($"{slot.WeaponType} Dmg: {slot.Damage:F0} Dur: {(slot.MaxDurability > 0 ? (slot.Durability/slot.MaxDurability):0f):P0}");
            }
            else if (slot.ItemType == ItemType.KeyItem)
            {
                GUILayout.Label($"Key Item");
            }

            GUILayout.FlexibleSpace();
            GUI.enabled = false; // read-only for now in network mode
            GUILayout.Button("Use", GUILayout.Width(50));
            GUILayout.Button("Drop", GUILayout.Width(50));
            GUILayout.Button("X", GUILayout.Width(25));
            GUI.enabled = true;
            GUILayout.EndHorizontal();
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
