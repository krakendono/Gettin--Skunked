using UnityEngine;

/// <summary>
/// Main manager for the inventory and crafting systems.
/// Handles integration between systems and provides a central point of control.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    [Header("System References")]
    public InventorySystem inventorySystem;
    public CraftingSystem craftingSystem;
    
    [Header("Player Integration")]
    public Transform player;
    public bool autoFindPlayer = true;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    public KeyCode debugInfoToggle = KeyCode.F1;
    
    // Events for UI integration
    public System.Action OnInventoryChanged;
    public System.Action OnCraftingChanged;
    
    // Singleton pattern for easy access
    public static InventoryManager Instance { get; private set; }
    
    void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    void Start()
    {
        InitializeManager();
        SetupEventListeners();
    }
    
    void Update()
    {
        HandleDebugInput();
    }
    
    void InitializeManager()
    {
        // Auto-find player if needed
        if (autoFindPlayer && player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }
        
        // Auto-find systems if not assigned
        if (inventorySystem == null)
        {
            inventorySystem = FindFirstObjectByType<InventorySystem>();
        }
        
        if (craftingSystem == null)
        {
            craftingSystem = FindFirstObjectByType<CraftingSystem>();
        }
        
        // Validate setup
        if (inventorySystem == null)
        {
            Debug.LogWarning("InventoryManager: No InventorySystem found! Creating one...");
            CreateInventorySystem();
        }
        
        if (craftingSystem == null)
        {
            Debug.LogWarning("InventoryManager: No CraftingSystem found! Creating one...");
            CreateCraftingSystem();
        }
        
        Debug.Log("InventoryManager initialized successfully");
    }
    
    void CreateInventorySystem()
    {
        GameObject inventoryObj = new GameObject("InventorySystem");
        inventoryObj.transform.SetParent(transform);
        inventorySystem = inventoryObj.AddComponent<InventorySystem>();
    }
    
    void CreateCraftingSystem()
    {
        GameObject craftingObj = new GameObject("CraftingSystem");
        craftingObj.transform.SetParent(transform);
        craftingSystem = craftingObj.AddComponent<CraftingSystem>();
    }
    
    void SetupEventListeners()
    {
        // Listen to inventory events
        if (inventorySystem != null)
        {
            inventorySystem.OnItemAdded += HandleItemAdded;
            inventorySystem.OnItemRemoved += HandleItemRemoved;
            inventorySystem.OnWeaponEquipped += HandleWeaponEquipped;
            inventorySystem.OnWeaponUnequipped += HandleWeaponUnequipped;
        }
        
        // Listen to crafting events
        if (craftingSystem != null)
        {
            craftingSystem.OnItemCrafted += HandleItemCrafted;
            craftingSystem.OnCraftingFailed += HandleCraftingFailed;
        }
    }
    
    void HandleDebugInput()
    {
        if (Input.GetKeyDown(debugInfoToggle))
        {
            showDebugInfo = !showDebugInfo;
        }
    }
    
    // Event handlers
    void HandleItemAdded(InventoryItem item)
    {
        Debug.Log($"[InventoryManager] Item added: {item.itemName} x{item.quantity}");
        OnInventoryChanged?.Invoke();
    }
    
    void HandleItemRemoved(InventoryItem item)
    {
        Debug.Log($"[InventoryManager] Item removed: {item.itemName}");
        OnInventoryChanged?.Invoke();
    }
    
    void HandleWeaponEquipped(WeaponItem weapon)
    {
        Debug.Log($"[InventoryManager] Weapon equipped: {weapon.itemName}");
        // Here you could instantiate the weapon prefab, enable weapon scripts, etc.
    }
    
    void HandleWeaponUnequipped(WeaponItem weapon)
    {
        Debug.Log($"[InventoryManager] Weapon unequipped: {weapon.itemName}");
        // Here you could hide the weapon, disable weapon scripts, etc.
    }
    
    void HandleItemCrafted(CraftingRecipe recipe)
    {
        Debug.Log($"[InventoryManager] Item crafted: {recipe.recipeName}");
        OnCraftingChanged?.Invoke();
        OnInventoryChanged?.Invoke();
    }
    
    void HandleCraftingFailed(CraftingRecipe recipe)
    {
        Debug.Log($"[InventoryManager] Crafting failed: {recipe.recipeName}");
    }
    
    // Public API methods for external scripts
    public bool AddItemToInventory(InventoryItem item)
    {
        if (inventorySystem != null)
        {
            return inventorySystem.AddItem(item);
        }
        return false;
    }
    
    public bool RemoveItemFromInventory(string itemName, int quantity = 1)
    {
        if (inventorySystem != null)
        {
            return inventorySystem.RemoveItem(itemName, quantity);
        }
        return false;
    }
    
    public int GetItemCount(string itemName)
    {
        if (inventorySystem != null)
        {
            return inventorySystem.GetItemCount(itemName);
        }
        return 0;
    }
    
    public bool HasItem(string itemName, int quantity = 1)
    {
        if (inventorySystem != null)
        {
            return inventorySystem.HasItem(itemName, quantity);
        }
        return false;
    }
    
    public WeaponItem GetEquippedWeapon()
    {
        if (inventorySystem != null)
        {
            return inventorySystem.GetEquippedWeapon();
        }
        return null;
    }
    
    public bool TryCraftItem(string recipeName)
    {
        if (craftingSystem != null)
        {
            var recipe = craftingSystem.GetAvailableRecipes().Find(r => r.recipeName == recipeName);
            if (recipe != null)
            {
                return craftingSystem.TryCraftItem(recipe);
            }
        }
        return false;
    }
    
    // Quick access methods for common items
    public void AddWood(int amount)
    {
        AddItemToInventory(new ResourceItem("Oak Wood", ResourceType.Wood, amount));
    }
    
    public void AddStone(int amount)
    {
        AddItemToInventory(new ResourceItem("Stone", ResourceType.Stone, amount));
    }
    
    public void AddMetal(int amount)
    {
        AddItemToInventory(new ResourceItem("Iron Ore", ResourceType.Metal, amount));
    }
    
    public void GiveBasicAxe()
    {
        AddItemToInventory(new WeaponItem("Basic Axe", WeaponType.Tool, 30f, 75f));
    }
    
    public void GiveBasicGun()
    {
        AddItemToInventory(new WeaponItem("Basic Pistol", WeaponType.Ranged, 25f, 100f));
    }
    
    // Debug GUI
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(Screen.width - 300, 10, 290, 400));
        
        GUILayout.Label("=== INVENTORY MANAGER DEBUG ===", GUI.skin.box);
        
        if (inventorySystem != null)
        {
            GUILayout.Label($"Inventory: {(inventorySystem.IsInventoryOpen() ? "OPEN" : "CLOSED")}");
            GUILayout.Label($"Equipped: {(GetEquippedWeapon()?.itemName ?? "None")}");
        }
        
        if (craftingSystem != null)
        {
            GUILayout.Label($"Crafting: {(craftingSystem.IsCraftingMenuOpen() ? "OPEN" : "CLOSED")}");
            GUILayout.Label($"Recipes: {craftingSystem.GetAvailableRecipeCount()}/{craftingSystem.GetRecipeCount()}");
        }
        
        GUILayout.Space(10);
        
        GUILayout.Label("=== QUICK ACTIONS ===", GUI.skin.box);
        
        if (GUILayout.Button("Add 10 Wood"))
        {
            AddWood(10);
        }
        
        if (GUILayout.Button("Add 5 Stone"))
        {
            AddStone(5);
        }
        
        if (GUILayout.Button("Add 3 Metal"))
        {
            AddMetal(3);
        }
        
        if (GUILayout.Button("Give Basic Axe"))
        {
            GiveBasicAxe();
        }
        
        if (GUILayout.Button("Give Basic Gun"))
        {
            GiveBasicGun();
        }
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Clear Inventory"))
        {
            if (inventorySystem != null)
            {
                inventorySystem.ClearInventory();
            }
        }
        
        GUILayout.Space(10);
        
        GUILayout.Label("=== CONTROLS ===", GUI.skin.box);
        GUILayout.Label($"F1 - Toggle Debug");
        GUILayout.Label($"Tab - Inventory");
        GUILayout.Label($"C - Crafting");
        
        GUILayout.EndArea();
    }
}