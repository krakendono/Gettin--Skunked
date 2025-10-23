using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class CraftingRecipe
{
    public string recipeName;
    public string description;
    public List<CraftingIngredient> ingredients;
    public InventoryItem result;
    public int resultQuantity = 1;
    public bool isUnlocked = true;
    
    public CraftingRecipe(string name, string desc, InventoryItem resultItem, int qty = 1)
    {
        recipeName = name;
        description = desc;
        result = resultItem;
        resultQuantity = qty;
        ingredients = new List<CraftingIngredient>();
    }
    
    public void AddIngredient(string itemName, int quantity)
    {
        ingredients.Add(new CraftingIngredient(itemName, quantity));
    }
    
    public bool CanCraft(InventorySystem inventory)
    {
        if (!isUnlocked) return false;
        
        foreach (var ingredient in ingredients)
        {
            if (inventory.GetItemCount(ingredient.itemName) < ingredient.quantity)
            {
                return false;
            }
        }
        
        return true;
    }
}

[System.Serializable]
public class CraftingIngredient
{
    public string itemName;
    public int quantity;
    
    public CraftingIngredient(string name, int qty)
    {
        itemName = name;
        quantity = qty;
    }
}

public class CraftingSystem : MonoBehaviour
{
    [Header("Crafting Settings")]
    public KeyCode craftingMenuKey = KeyCode.C;
    public bool showDebugCrafting = true;
    
    [Header("Audio")]
    public AudioClip craftingSound;
    public AudioClip craftingFailSound;
    
    private List<CraftingRecipe> recipes;
    private InventorySystem playerInventory;
    private bool isCraftingMenuOpen = false;
    private Vector2 craftingScrollPosition = Vector2.zero;
    private AudioSource audioSource;
    
    // Events
    public System.Action<CraftingRecipe> OnItemCrafted;
    public System.Action<CraftingRecipe> OnCraftingFailed;
    
    void Start()
    {
        InitializeCrafting();
        SetupAudio();
        CreateDefaultRecipes();
    }
    
    void Update()
    {
        HandleInput();
    }
    
    void InitializeCrafting()
    {
        recipes = new List<CraftingRecipe>();
        
        // Find inventory system
        playerInventory = FindFirstObjectByType<InventorySystem>();
        if (playerInventory == null)
        {
            Debug.LogWarning("No InventorySystem found! Crafting system requires an InventorySystem.");
        }
        
        Debug.Log("Crafting system initialized");
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
        if (Input.GetKeyDown(craftingMenuKey))
        {
            ToggleCraftingMenu();
        }
        
        // Close crafting menu with ESC key
        if (Input.GetKeyDown(KeyCode.Escape) && isCraftingMenuOpen)
        {
            CloseCraftingMenu();
        }
    }
    
    void ToggleCraftingMenu()
    {
        isCraftingMenuOpen = !isCraftingMenuOpen;
        Debug.Log($"Crafting menu {(isCraftingMenuOpen ? "opened" : "closed")}");
    }
    
    void CloseCraftingMenu()
    {
        if (isCraftingMenuOpen)
        {
            isCraftingMenuOpen = false;
            Debug.Log("Crafting menu closed");
        }
    }
    
    void OpenCraftingMenu()
    {
        if (!isCraftingMenuOpen)
        {
            isCraftingMenuOpen = true;
            Debug.Log("Crafting menu opened");
        }
    }
    
    void CreateDefaultRecipes()
    {
        // Wooden Tools
        var woodenAxe = new CraftingRecipe("Wooden Axe", "A basic axe made from wood", 
            new WeaponItem("Wooden Axe", WeaponType.Tool, 25f, 50f));
        woodenAxe.AddIngredient("Oak Wood", 5);
        AddRecipe(woodenAxe);
        
        var woodenSpear = new CraftingRecipe("Wooden Spear", "A simple spear for hunting", 
            new WeaponItem("Wooden Spear", WeaponType.Melee, 30f, 40f));
        woodenSpear.AddIngredient("Pine Wood", 3);
        AddRecipe(woodenSpear);
        
        // Resource Processing
        var refinedWood = new CraftingRecipe("Refined Wood", "Processed wood planks", 
            new ResourceItem("Refined Wood", ResourceType.Wood, 2));
        refinedWood.AddIngredient("Oak Wood", 5);
        AddRecipe(refinedWood);
        
        // Advanced Tools (require multiple resource types)
        var ironAxe = new CraftingRecipe("Iron Axe", "A sturdy metal axe", 
            new WeaponItem("Iron Axe", WeaponType.Tool, 45f, 120f));
        ironAxe.AddIngredient("Iron Ore", 3);
        ironAxe.AddIngredient("Oak Wood", 2);
        AddRecipe(ironAxe);
        
        // Makeshift Weapons
        var improviseKnife = new CraftingRecipe("Makeshift Knife", "A crude but effective blade", 
            new WeaponItem("Makeshift Knife", WeaponType.Melee, 15f, 30f));
        improviseKnife.AddIngredient("Stone", 2);
        improviseKnife.AddIngredient("Pine Wood", 1);
        AddRecipe(improviseKnife);
        
        // Key Items (example)
        var campKey = new CraftingRecipe("Camp Key", "A key to unlock camp facilities", 
            new KeyItem("Camp Key", "camp_access_key"));
        campKey.AddIngredient("Iron Ore", 1);
        campKey.AddIngredient("Stone", 1);
        AddRecipe(campKey);
        
        Debug.Log($"Created {recipes.Count} default crafting recipes");
    }
    
    public void AddRecipe(CraftingRecipe recipe)
    {
        if (recipe != null && !recipes.Contains(recipe))
        {
            recipes.Add(recipe);
        }
    }
    
    public void RemoveRecipe(CraftingRecipe recipe)
    {
        recipes.Remove(recipe);
    }
    
    public void UnlockRecipe(string recipeName)
    {
        var recipe = recipes.FirstOrDefault(r => r.recipeName == recipeName);
        if (recipe != null)
        {
            recipe.isUnlocked = true;
            Debug.Log($"Unlocked recipe: {recipeName}");
        }
    }
    
    public void LockRecipe(string recipeName)
    {
        var recipe = recipes.FirstOrDefault(r => r.recipeName == recipeName);
        if (recipe != null)
        {
            recipe.isUnlocked = false;
            Debug.Log($"Locked recipe: {recipeName}");
        }
    }
    
    public bool TryCraftItem(CraftingRecipe recipe)
    {
        if (playerInventory == null)
        {
            Debug.LogWarning("No inventory system available for crafting!");
            return false;
        }
        
        if (!recipe.CanCraft(playerInventory))
        {
            Debug.Log($"Cannot craft {recipe.recipeName} - missing ingredients");
            PlaySound(craftingFailSound);
            OnCraftingFailed?.Invoke(recipe);
            return false;
        }
        
        // Check if we have space for the result
        if (playerInventory.GetEmptySlots(recipe.result.itemType) <= 0)
        {
            Debug.Log($"Cannot craft {recipe.recipeName} - no space in inventory");
            PlaySound(craftingFailSound);
            OnCraftingFailed?.Invoke(recipe);
            return false;
        }
        
        // Consume ingredients
        foreach (var ingredient in recipe.ingredients)
        {
            playerInventory.RemoveItem(ingredient.itemName, ingredient.quantity);
        }
        
        // Create result item with proper quantity
        InventoryItem resultItem = CloneItem(recipe.result);
        resultItem.quantity = recipe.resultQuantity;
        
        // Add result to inventory
        if (playerInventory.AddItem(resultItem))
        {
            Debug.Log($"Successfully crafted {recipe.recipeName}!");
            PlaySound(craftingSound);
            OnItemCrafted?.Invoke(recipe);
            return true;
        }
        else
        {
            // This shouldn't happen since we checked space above, but just in case
            Debug.LogError($"Failed to add crafted item to inventory: {recipe.recipeName}");
            return false;
        }
    }
    
    private InventoryItem CloneItem(InventoryItem original)
    {
        // Create a new instance based on the type
        if (original is ResourceItem resource)
        {
            return new ResourceItem(resource.itemName, resource.resourceType, resource.quantity, resource.maxStackSize);
        }
        else if (original is WeaponItem weapon)
        {
            return new WeaponItem(weapon.itemName, weapon.weaponType, weapon.damage, weapon.maxDurability, weapon.weaponPrefab);
        }
        else if (original is KeyItem keyItem)
        {
            return new KeyItem(keyItem.itemName, keyItem.keyId, keyItem.isQuestItem);
        }
        
        // Fallback (shouldn't reach here with proper item types)
        return original;
    }
    
    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
    
    public List<CraftingRecipe> GetAvailableRecipes()
    {
        return recipes.Where(r => r.isUnlocked).ToList();
    }
    
    public List<CraftingRecipe> GetCraftableRecipes()
    {
        if (playerInventory == null) return new List<CraftingRecipe>();
        
        return recipes.Where(r => r.isUnlocked && r.CanCraft(playerInventory)).ToList();
    }
    
    // Debug GUI
    void OnGUI()
    {
        if (!showDebugCrafting || !isCraftingMenuOpen) return;
        
        GUI.Window(1, new Rect(100, 100, 700, 600), DrawCraftingWindow, "Crafting System");
    }
    
    void DrawCraftingWindow(int windowID)
    {
        craftingScrollPosition = GUILayout.BeginScrollView(craftingScrollPosition);
        
        GUILayout.Label("=== CRAFTING RECIPES ===", GUI.skin.box);
        
        if (playerInventory == null)
        {
            GUILayout.Label("No inventory system found!");
            GUILayout.EndScrollView();
            GUI.DragWindow();
            return;
        }
        
        var availableRecipes = GetAvailableRecipes();
        var craftableRecipes = GetCraftableRecipes();
        
        GUILayout.Label($"Available Recipes: {availableRecipes.Count}");
        GUILayout.Label($"Craftable Now: {craftableRecipes.Count}");
        
        GUILayout.Space(10);
        
        foreach (var recipe in availableRecipes)
        {
            bool canCraft = recipe.CanCraft(playerInventory);
            
            // Recipe header
            GUILayout.BeginVertical(GUI.skin.box);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{recipe.recipeName}</b>", GUILayout.Width(200));
            GUILayout.FlexibleSpace();
            
            // Craft button
            GUI.enabled = canCraft;
            if (GUILayout.Button("Craft", GUILayout.Width(80)))
            {
                TryCraftItem(recipe);
            }
            GUI.enabled = true;
            
            GUILayout.EndHorizontal();
            
            // Description
            GUILayout.Label($"<i>{recipe.description}</i>");
            
            // Result
            GUILayout.Label($"Result: {recipe.result.itemName} x{recipe.resultQuantity}");
            
            // Ingredients
            GUILayout.Label("Ingredients:");
            foreach (var ingredient in recipe.ingredients)
            {
                int playerHas = playerInventory.GetItemCount(ingredient.itemName);
                bool hasEnough = playerHas >= ingredient.quantity;
                
                Color originalColor = GUI.color;
                GUI.color = hasEnough ? Color.green : Color.red;
                
                GUILayout.Label($"  • {ingredient.itemName}: {playerHas}/{ingredient.quantity}");
                
                GUI.color = originalColor;
            }
            
            GUILayout.EndVertical();
            GUILayout.Space(5);
        }
        
        GUILayout.Space(10);
        
        // Controls
        GUILayout.Label("=== CONTROLS ===", GUI.skin.box);
        GUILayout.Label($"Press {craftingMenuKey} to toggle crafting menu");
        GUILayout.Label("Green ingredients = you have enough");
        GUILayout.Label("Red ingredients = you need more");
        
        GUILayout.EndScrollView();
        
        GUI.DragWindow();
    }
    
    // Public getters
    public bool IsCraftingMenuOpen() => isCraftingMenuOpen;
    public int GetRecipeCount() => recipes.Count;
    public int GetAvailableRecipeCount() => recipes.Count(r => r.isUnlocked);
}