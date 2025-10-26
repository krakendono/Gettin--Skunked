using System.Collections.Generic;

/// <summary>
/// Simple static database providing recipes for server-side validation in NetworkInventory.
/// Mirrors the default recipes created in CraftingSystem.
/// </summary>
public static class CraftingDatabase
{
    public class RecipeDef
    {
        public string Name;
        public string Description;
        public List<(string itemName, int qty)> Ingredients = new();
        public ItemType ResultType;
        public string ResultName;
        public int ResultQuantity = 1;

        // Weapon result
        public WeaponType WeaponType;
        public float Damage;
        public float MaxDurability;

        // Resource result
        public ResourceType ResourceType;

        // KeyItem result
        public string KeyId;
        public bool IsQuestItem;
    }

    private static readonly Dictionary<string, RecipeDef> _recipesByName = new();
    private static bool _initialized;

    public static RecipeDef GetByName(string recipeName)
    {
        EnsureInit();
        _recipesByName.TryGetValue(recipeName, out var def);
        return def;
    }

    private static void EnsureInit()
    {
        if (_initialized) return;
        _initialized = true;

        // Wooden Axe
        Add(new RecipeDef
        {
            Name = "Wooden Axe",
            Description = "A basic axe made from wood",
            Ingredients = new() { ("Oak Wood", 5) },
            ResultType = ItemType.Weapon,
            ResultName = "Wooden Axe",
            WeaponType = WeaponType.Tool,
            Damage = 25f,
            MaxDurability = 50f,
        });

        // Wooden Spear
        Add(new RecipeDef
        {
            Name = "Wooden Spear",
            Description = "A simple spear for hunting",
            Ingredients = new() { ("Pine Wood", 3) },
            ResultType = ItemType.Weapon,
            ResultName = "Wooden Spear",
            WeaponType = WeaponType.Melee,
            Damage = 30f,
            MaxDurability = 40f,
        });

        // Refined Wood
        Add(new RecipeDef
        {
            Name = "Refined Wood",
            Description = "Processed wood planks",
            Ingredients = new() { ("Oak Wood", 5) },
            ResultType = ItemType.Resource,
            ResultName = "Refined Wood",
            ResourceType = ResourceType.Wood,
            ResultQuantity = 2,
        });

        // Iron Axe
        Add(new RecipeDef
        {
            Name = "Iron Axe",
            Description = "A sturdy metal axe",
            Ingredients = new() { ("Iron Ore", 3), ("Oak Wood", 2) },
            ResultType = ItemType.Weapon,
            ResultName = "Iron Axe",
            WeaponType = WeaponType.Tool,
            Damage = 45f,
            MaxDurability = 120f,
        });

        // Makeshift Knife
        Add(new RecipeDef
        {
            Name = "Makeshift Knife",
            Description = "A crude but effective blade",
            Ingredients = new() { ("Stone", 2), ("Pine Wood", 1) },
            ResultType = ItemType.Weapon,
            ResultName = "Makeshift Knife",
            WeaponType = WeaponType.Melee,
            Damage = 15f,
            MaxDurability = 30f,
        });

        // Camp Key (now supported with KeyItem in NetworkInventory)
        Add(new RecipeDef
        {
            Name = "Camp Key",
            Description = "A key to unlock camp facilities",
            Ingredients = new() { ("Iron Ore", 1), ("Stone", 1) },
            ResultType = ItemType.KeyItem,
            ResultName = "Camp Key",
            KeyId = "camp_access_key",
            IsQuestItem = false,
        });
    }

    private static void Add(RecipeDef def)
    {
        _recipesByName[def.Name] = def;
    }
}
