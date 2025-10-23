# Inventory System Setup Guide

## Overview
This inventory system provides a comprehensive solution for managing resources, weapons, and key items in your Unity game. It includes crafting functionality and integrates with existing game systems.

## Core Components

### 1. InventorySystem.cs
- Main inventory management
- Handles item storage in categorized slots (Resources, Weapons, Key Items)
- Equipment system for weapons
- Debug GUI for testing

### 2. CraftingSystem.cs
- Recipe-based crafting system
- Automatic ingredient checking
- Integration with inventory for consuming/adding items
- Expandable recipe system

### 3. InventoryManager.cs
- Central coordinator for all inventory/crafting systems
- Singleton pattern for easy access
- Event system for UI integration
- Quick access methods for common operations

### 4. Pickup Scripts
- **ResourcePickup.cs**: For collectible resources (wood, stone, etc.)
- **WeaponPickup.cs**: For weapon items with stats display

## Item Types

### Resources
- Wood, Stone, Metal, Food, Other
- Stackable up to defined limits
- Used in crafting recipes

### Weapons
- Melee, Ranged, Tool types
- Individual durability tracking
- Non-stackable unique items
- Equippable with stats

### Key Items
- Quest items and special keys
- Non-stackable unique items
- Quest progression tracking

## Setup Instructions

### Basic Setup
1. Create an empty GameObject and add the `InventoryManager` component
2. The manager will automatically create InventorySystem and CraftingSystem if not found
3. Tag your player GameObject as "Player" for automatic detection

### Advanced Setup
1. Manually add `InventorySystem` to your player or a manager object
2. Add `CraftingSystem` to the same or different object
3. Assign these components to the `InventoryManager` references
4. Configure slot counts and settings in the inspector

### Integration with Existing Systems

#### Wood Resource Integration
The `Wood.cs` script has been modified to:
- Automatically add resources to player inventory when chopped
- Create resource pickups if inventory is full
- Use the new inventory system seamlessly

#### Adding Resource Pickups
1. Create a GameObject for your resource
2. Add the `ResourcePickup` component
3. Configure the resource type, quantity, and pickup settings
4. The pickup will automatically integrate with the inventory

#### Adding Weapon Pickups
1. Create a GameObject for your weapon
2. Add the `WeaponPickup` component
3. Configure weapon stats and pickup settings
4. Assign weapon prefab if you have one

## Controls (Default)
- **Tab**: Toggle inventory menu
- **C**: Toggle crafting menu
- **E**: Pick up items (when in range)
- **F1**: Toggle debug info
- **L**: Toggle aiming line (for guns)

## Default Recipes
The system comes with several pre-configured recipes:
- Wooden Axe (5 Oak Wood)
- Wooden Spear (3 Pine Wood)
- Iron Axe (3 Iron Ore + 2 Oak Wood)
- Makeshift Knife (2 Stone + 1 Pine Wood)
- Refined Wood (5 Oak Wood → 2 Refined Wood)

## Extending the System

### Adding New Item Types
1. Extend the base `InventoryItem` class
2. Implement required abstract methods
3. Add to the ItemType enum if needed
4. Update inventory slot allocation

### Adding New Recipes
```csharp
var newRecipe = new CraftingRecipe("Item Name", "Description", resultItem);
newRecipe.AddIngredient("Required Item", quantity);
craftingSystem.AddRecipe(newRecipe);
```

### Custom Pickup Types
Create new pickup scripts inheriting from MonoBehaviour:
1. Find the InventorySystem
2. Create appropriate InventoryItem
3. Call `inventorySystem.AddItem(item)`

## Event System
The system provides events for UI integration:
- `OnItemAdded` / `OnItemRemoved`
- `OnWeaponEquipped` / `OnWeaponUnequipped`
- `OnItemCrafted` / `OnCraftingFailed`

## Debug Features
- Real-time inventory display
- Crafting recipe availability
- Quick item addition buttons
- Clear inventory function
- Detailed logging system

## Performance Notes
- Uses Unity's FindFirstObjectByType (Unity 2023.1+)
- Efficient slot-based storage
- Event-driven updates
- Minimal Update() calls

## Troubleshooting

### Items not being picked up
- Check if InventorySystem is present
- Verify item types match slot availability
- Check pickup range and layer settings

### Crafting not working
- Ensure CraftingSystem is initialized
- Check recipe ingredient names match exactly
- Verify inventory has required items

### Integration issues
- Make sure player is tagged as "Player"
- Check component references in InventoryManager
- Verify event subscriptions are working

## Future Enhancements
- Save/Load system integration
- UI system integration
- Sound effect improvements
- Animation system integration
- Multiplayer support considerations