using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Server-authoritative player inventory for Photon Fusion.
/// Clients send RPC requests; server validates and updates replicated slots.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkInventory : NetworkBehaviour
{
    // TYPES

    public const int MaxSlots = 30;
    private const int MaxStackDefault = 99; // Fallback stack size for stackable items
    private const float DefaultPickupRange = 4f;

    [Serializable]
    public struct InventorySlotNet : INetworkStruct
    {
        public ItemType ItemType;
        public NetworkString<_32> Name;
        public int Quantity;

        // Resource
        public ResourceType ResourceType;

        // Weapon
        public WeaponType WeaponType;
        public float Damage;
        public float Durability;
        public float MaxDurability;

    // Key Item
    public NetworkString<_32> KeyId;
    public NetworkBool IsQuestItem;

    public bool IsEmpty => ItemType == 0 && Quantity <= 0 && Name.Length == 0;

        public void Clear()
        {
            ItemType = 0;
            Name = default;
            Quantity = 0;
            ResourceType = 0;
            WeaponType = 0;
            Damage = 0;
            Durability = 0;
            MaxDurability = 0;
            KeyId = default;
            IsQuestItem = false;
        }
    }

    // STATE

    [Networked, Capacity(MaxSlots)]
    private NetworkArray<InventorySlotNet> Slots { get; }

    [Header("Drop Spawning (Server)")]
    [SerializeField] private NetworkObject resourcePickupPrefab;
    [SerializeField] private NetworkObject weaponPickupPrefab;
    [SerializeField] private float dropHorizontalForce = 3f;
    [SerializeField] private float dropUpwardForce = 5f;
    [SerializeField] private float dropRadius = 1f;

    // RPCs - called by owning client (input authority), executed on state authority

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestPickup(NetworkId pickupObjectId)
    {
        var pickupObj = Runner.FindObject(pickupObjectId);
        if (pickupObj == null)
            return;

        // Validate proximity
        var pickupTransform = pickupObj.transform;
        if (pickupTransform != null)
        {
            float dist = Vector3.Distance(transform.position, pickupTransform.position);
            if (dist > DefaultPickupRange)
            {
                return; // Too far
            }
        }

        // Determine pickup type and add
        bool added = false;

    var resourcePickup = pickupObj.GetComponent<ResourcePickup>();
        if (resourcePickup != null)
        {
            added = TryAddResource(resourcePickup.resourceName, resourcePickup.resourceType, resourcePickup.quantity);
        }
        else
        {
            var weaponPickup = pickupObj.GetComponent<WeaponPickup>();
            if (weaponPickup != null)
            {
                added = TryAddWeapon(weaponPickup.weaponName, weaponPickup.weaponType, weaponPickup.damage, weaponPickup.maxDurability);
            }
        }

        // Despawn on success
        if (added)
        {
            Runner.Despawn(pickupObj);
        }
    }

    // Optional debug RPCs to add items without world pickups (useful for dev menus)

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestAddResource(NetworkString<_32> name, ResourceType type, int quantity)
    {
        TryAddResource(name.ToString(), type, quantity);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestAddWeapon(NetworkString<_32> name, WeaponType type, float damage, float maxDurability)
    {
        TryAddWeapon(name.ToString(), type, damage, maxDurability);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestCraftByName(NetworkString<_32> recipeName)
    {
        var def = CraftingDatabase.GetByName(recipeName.ToString());
        if (def == null)
            return;

        // Validate ingredients
        if (HasIngredients(def.Ingredients) == false)
            return;

        // Remove ingredients
        ConsumeIngredients(def.Ingredients);

        // Add result
        switch (def.ResultType)
        {
            case ItemType.Resource:
                TryAddResource(def.ResultName, def.ResourceType, def.ResultQuantity);
                break;
            case ItemType.Weapon:
                TryAddWeapon(def.ResultName, def.WeaponType, def.Damage, def.MaxDurability);
                break;
            case ItemType.KeyItem:
                TryAddKeyItem(def.ResultName, def.KeyId, def.IsQuestItem);
                break;
            default:
                // Key items or others not implemented yet
                break;
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestDrop(NetworkString<_32> itemName, int quantity)
    {
        if (quantity <= 0) return;

        // Find first slot with this name
        int foundIndex = -1;
        for (int i = 0; i < Slots.Length; i++)
        {
            var s = Slots[i];
            if (s.IsEmpty) continue;
            if (s.Name.Equals(itemName.ToString()))
            {
                foundIndex = i;
                break;
            }
        }

        if (foundIndex < 0) return;

        var slot = Slots[foundIndex];
        switch (slot.ItemType)
        {
            case ItemType.Resource:
            {
                int dropQty = Mathf.Min(quantity, Mathf.Max(1, slot.Quantity));
                slot.Quantity -= dropQty;
                if (slot.Quantity <= 0) slot.Clear();
                Slots.Set(foundIndex, slot);

                SpawnResourcePickup(slot.Name.ToString(), slot.ResourceType, dropQty);
                break;
            }
            case ItemType.Weapon:
            {
                // Weapons are non-stackable, drop one
                slot.Clear();
                Slots.Set(foundIndex, slot);
                SpawnWeaponPickup(slot.Name.ToString(), slot.WeaponType, slot.Damage, slot.Durability);
                break;
            }
            case ItemType.KeyItem:
            {
                // Not implemented: optionally spawn a key item pickup prefab in the future
                break;
            }
        }
    }

    // SERVER: mutate slots

    private bool TryAddResource(string name, ResourceType type, int quantity)
    {
        if (quantity <= 0)
            return false;

        // First try to stack with the same resource name/type
        for (int i = 0; i < Slots.Length && quantity > 0; i++)
        {
            var slot = Slots[i];
            if (slot.IsEmpty)
                continue;

            if (slot.ItemType == ItemType.Resource && slot.ResourceType == type && slot.Name.Equals(name))
            {
                int space = MaxStackDefault - slot.Quantity;
                if (space <= 0)
                    continue;

                int add = Mathf.Min(space, quantity);
                slot.Quantity += add;
                quantity -= add;
                Slots.Set(i, slot);
            }
        }

        // Then fill empty slots
        for (int i = 0; i < Slots.Length && quantity > 0; i++)
        {
            var slot = Slots[i];
            if (slot.IsEmpty)
            {
                int add = Mathf.Min(MaxStackDefault, quantity);
                slot.ItemType = ItemType.Resource;
                slot.Name = name;
                slot.Quantity = add;
                slot.ResourceType = type;
                slot.WeaponType = 0;
                slot.Damage = 0;
                slot.Durability = 0;
                slot.MaxDurability = 0;
                Slots.Set(i, slot);
                quantity -= add;
            }
        }

        return quantity == 0;
    }

    private bool TryAddWeapon(string name, WeaponType type, float damage, float maxDurability)
    {
        // Weapons are not stackable; find first empty slot
        for (int i = 0; i < Slots.Length; i++)
        {
            var slot = Slots[i];
            if (slot.IsEmpty)
            {
                slot.ItemType = ItemType.Weapon;
                slot.Name = name;
                slot.Quantity = 1;
                slot.ResourceType = 0;
                slot.WeaponType = type;
                slot.Damage = damage;
                slot.Durability = maxDurability;
                slot.MaxDurability = maxDurability;
                Slots.Set(i, slot);
                return true;
            }
        }

        return false;
    }

    private bool TryAddKeyItem(string name, string keyId, bool isQuestItem)
    {
        // Key items are not stackable; find first empty slot
        for (int i = 0; i < Slots.Length; i++)
        {
            var slot = Slots[i];
            if (slot.IsEmpty)
            {
                slot.ItemType = ItemType.KeyItem;
                slot.Name = name;
                slot.Quantity = 1;
                slot.ResourceType = 0;
                slot.WeaponType = 0;
                slot.Damage = 0;
                slot.Durability = 0;
                slot.MaxDurability = 0;
                slot.KeyId = keyId;
                slot.IsQuestItem = isQuestItem;
                Slots.Set(i, slot);
                return true;
            }
        }
        return false;
    }

    // INVENTORY QUERIES & MUTATIONS (server-side)

    private bool HasIngredients(List<(string itemName, int qty)> ingredients)
    {
        // Count available amounts by name
        var remaining = new Dictionary<string, int>();
        foreach (var (itemName, qty) in ingredients)
        {
            if (!remaining.ContainsKey(itemName)) remaining[itemName] = 0;
            remaining[itemName] += qty;
        }

        // Sum all matching slots
        foreach (var kvp in new List<string>(remaining.Keys))
        {
            int needed = remaining[kvp];
            int have = GetItemCountByName(kvp);
            if (have < needed) return false;
        }
        return true;
    }

    private void ConsumeIngredients(List<(string itemName, int qty)> ingredients)
    {
        // Build map of needed quantities
        var needed = new Dictionary<string, int>();
        foreach (var (itemName, qty) in ingredients)
        {
            if (!needed.ContainsKey(itemName)) needed[itemName] = 0;
            needed[itemName] += qty;
        }

        // Iterate slots and subtract
        for (int i = 0; i < Slots.Length; i++)
        {
            var slot = Slots[i];
            if (slot.IsEmpty) continue;
            string name = slot.Name.ToString();
            if (!needed.TryGetValue(name, out int req) || req <= 0) continue;

            int take = Mathf.Min(req, slot.Quantity);
            slot.Quantity -= take;
            req -= take;
            if (slot.Quantity <= 0)
            {
                slot.Clear();
            }
            Slots.Set(i, slot);
            needed[name] = req;

            // Early out if all met
            bool allDone = true;
            foreach (var v in needed.Values) { if (v > 0) { allDone = false; break; } }
            if (allDone) return;
        }
    }

    private int GetItemCountByName(string name)
    {
        int total = 0;
        for (int i = 0; i < Slots.Length; i++)
        {
            var slot = Slots[i];
            if (slot.IsEmpty) continue;
            if (slot.Name.Equals(name))
            {
                total += Mathf.Max(0, slot.Quantity);
            }
        }
        return total;
    }

    // Spawning pickups (server-only)
    private void SpawnResourcePickup(string name, ResourceType type, int qty)
    {
        if (resourcePickupPrefab == null) return;
        Vector3 pos = GetDropPosition();
        var obj = Runner.Spawn(resourcePickupPrefab, pos, Quaternion.identity);
        var pickup = obj.GetComponent<ResourcePickup>();
        if (pickup != null)
        {
            pickup.NetResourceName = name;
            pickup.NetResourceType = type;
            pickup.NetQuantity = qty;
            ApplyDropForces(obj);
        }
    }

    private void SpawnWeaponPickup(string name, WeaponType type, float damage, float maxDurability)
    {
        if (weaponPickupPrefab == null) return;
        Vector3 pos = GetDropPosition();
        var obj = Runner.Spawn(weaponPickupPrefab, pos, Quaternion.identity);
        var pickup = obj.GetComponent<WeaponPickup>();
        if (pickup != null)
        {
            pickup.NetWeaponName = name;
            pickup.NetWeaponType = type;
            pickup.NetDamage = damage;
            pickup.NetMaxDurability = maxDurability;
            ApplyDropForces(obj);
        }
    }

    private Vector3 GetDropPosition()
    {
        Vector2 rand = UnityEngine.Random.insideUnitCircle * dropRadius;
        return transform.position + transform.forward * 1.5f + new Vector3(rand.x, 0.5f, rand.y);
    }

    private void ApplyDropForces(NetworkObject obj)
    {
        var rb = obj.GetComponent<Rigidbody>();
        if (rb == null) return;
        Vector3 vertical = Vector3.up * dropUpwardForce;
        rb.AddForce(vertical, ForceMode.Impulse);
        Vector3 randomDir = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f)).normalized;
        rb.AddForce(randomDir * dropHorizontalForce, ForceMode.Impulse);
    }

    // PUBLIC read helpers (client-side UI can read replicated slots)

    public int GetSlotCount() => Slots.Length;

    public InventorySlotNet GetSlot(int index) => Slots[index];
}
