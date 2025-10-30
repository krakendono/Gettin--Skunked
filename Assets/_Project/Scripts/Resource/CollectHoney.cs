using Fusion;
using UnityEngine;

/// <summary>
/// Networked beehive/honey source using Photon Fusion.
/// Clients with input authority can request honey while in range; server validates, reduces stock,
/// and spawns a ResourcePickup for Honey at the player's position so it integrates with existing pickup flow.
/// Falls back to local InventorySystem when not in a network session.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class CollectHoney : NetworkBehaviour
{
    [Header("Honey Source")]
    [SerializeField] private int maxHoney = 10;
    [SerializeField] private int collectPerUse = 1;
    [SerializeField] private float collectCooldownSeconds = 1.0f;
    [SerializeField] private float interactRange = 3.0f;

    [Networked] private int NetCurrentHoney { get; set; }
    [Networked] private TickTimer NetCollectCooldown { get; set; }

    [Header("Pickup Spawning (Server)")]
    [Tooltip("Networked ResourcePickup prefab configured in Fusion's Prefab Table")]
    [SerializeField] private NetworkObject resourcePickupPrefab;
    [SerializeField] private string honeyItemName = "Honey";

    [Header("Regen (Server)")]
    [SerializeField] private bool regenEnabled = true;
    [SerializeField] private float regenPerSecond = 0.25f; // 1 honey every 4 seconds by default

    [Header("Client UX")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private bool showPrompt = true;

    [Header("Bee Aggression Hook (Optional)")]
    [Tooltip("If set, nearby BeeAggression components will be notified when honey is harvested.")]
    [SerializeField] private bool notifyBeeAggression = true;
    [SerializeField] private float notifyRadius = 12f;

    // Server-only accumulator for fractional regen
    private float _regenAccumulator;

    // Offline fallback (non-networked) current honey
    private int _localHoney;

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            if (NetCurrentHoney <= 0)
                NetCurrentHoney = Mathf.Max(0, maxHoney);
        }

        // Initialize local fallback value if not networked
        if (Runner == null || Runner.IsRunning == false)
        {
            _localHoney = Mathf.Max(0, maxHoney);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority && regenEnabled)
        {
            if (NetCurrentHoney < maxHoney)
            {
                _regenAccumulator += regenPerSecond * Runner.DeltaTime;
                if (_regenAccumulator >= 1f)
                {
                    int add = Mathf.FloorToInt(_regenAccumulator);
                    _regenAccumulator -= add;
                    NetCurrentHoney = Mathf.Min(maxHoney, NetCurrentHoney + add);
                }
            }
            else
            {
                _regenAccumulator = 0f;
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // Networked interaction: only the local input-authority player can request
        if (Runner != null && Runner.IsRunning)
        {
            var nob = other.GetComponentInParent<NetworkObject>();
            if (nob != null && nob.HasInputAuthority)
            {
                // Validate proximity on client (server will re-validate)
                if (Input.GetKeyDown(interactKey))
                {
                    // Only send if we have something to give and not on cooldown (client-side quick check)
                    if (NetCurrentHoney > 0 && (NetCollectCooldown.ExpiredOrNotRunning(Runner)))
                    {
                        int desired = Mathf.Max(1, collectPerUse);
                        RPC_RequestCollect(nob.InputAuthority, desired);
                    }
                }
            }
            return;
        }

        // Offline fallback: use local inventory and local honey state
        if (Input.GetKeyDown(interactKey))
        {
            TryCollectOffline(other);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestCollect(PlayerRef player, int desired)
    {
        if (desired <= 0) return;

        // Cooldown check
        if (NetCollectCooldown.ExpiredOrNotRunning(Runner) == false)
            return;

        // Validate player object and range
        if (Runner.TryGetPlayerObject(player, out var playerObj) == false)
            return;

        float dist = Vector3.Distance(transform.position, playerObj.transform.position);
        if (dist > interactRange + 0.5f) // small tolerance
            return;

        // Determine amount to grant
        int amount = Mathf.Min(desired, collectPerUse, Mathf.Max(0, NetCurrentHoney));
        if (amount <= 0)
            return;

        // Reduce hive stock and start cooldown
        NetCurrentHoney = Mathf.Max(0, NetCurrentHoney - amount);
        NetCollectCooldown = TickTimer.CreateFromSeconds(Runner, collectCooldownSeconds);

        // Spawn a networked Honey pickup at player's position to integrate with existing pickup flow
        if (resourcePickupPrefab != null)
        {
            Vector3 pos = playerObj.transform.position + Vector3.up * 0.5f;
            var pickupObj = Runner.Spawn(resourcePickupPrefab, pos, Quaternion.identity);
            var pickup = pickupObj.GetComponent<ResourcePickup>();
            if (pickup != null)
            {
                pickup.NetResourceName = honeyItemName;
                pickup.NetResourceType = ResourceType.Honey;
                pickup.NetQuantity = amount;
            }
        }

        // Notify nearby bee aggression controllers (server-side hint)
        if (notifyBeeAggression)
        {
            var center = transform.position;
            var hits = Physics.OverlapSphere(center, notifyRadius);
            foreach (var h in hits)
            {
                var bee = h.GetComponentInParent<BeeAggression>();
                if (bee != null)
                {
                    bee.NotifyHoneyStolen(Runner.TryGetPlayerObject(player, out var pobj) ? pobj.transform : null, amount);
                }
            }
        }
    }

    private void TryCollectOffline(Collider other)
    {
        if (_localHoney <= 0)
            return;

        // Ensure the collider is the player
        var playerGo = other.GetComponentInParent<Transform>();
        if (playerGo == null)
            return;

        // Range check (defensive)
        if (Vector3.Distance(transform.position, playerGo.position) > interactRange + 0.5f)
            return;

        int amount = Mathf.Min(collectPerUse, _localHoney);
        _localHoney -= amount;

        // Give directly to local InventorySystem if available
        var inv = playerGo.GetComponentInParent<InventorySystem>();
        if (inv == null)
            inv = FindFirstObjectByType<InventorySystem>();

        if (inv != null)
        {
            inv.AddItem(new ResourceItem(honeyItemName, ResourceType.Honey, amount));
        }
        else
        {
            // Fallback: spawn a non-networked pickup
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = playerGo.position + Vector3.up * 0.5f;
            var rp = go.AddComponent<ResourcePickup>();
            rp.resourceName = honeyItemName;
            rp.resourceType = ResourceType.Honey;
            rp.quantity = amount;
            rp.autoPickup = true;
        }
    }

    private void OnGUI()
    {
        if (!showPrompt) return;

        // Only show prompt when a local player is within range
        Transform localPlayer = FindLocalPlayerTransform();
        if (localPlayer == null) return;

        float dist = Vector3.Distance(transform.position, localPlayer.position);
        if (dist > interactRange) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 2f);
        if (screenPos.z > 0)
        {
            string text = $"Press {interactKey} to collect Honey";
            if (Runner != null && Runner.IsRunning)
            {
                text += NetCurrentHoney > 0 ? $" ({NetCurrentHoney} left)" : " (Depleted)";
            }
            else
            {
                text += _localHoney > 0 ? $" ({_localHoney} left)" : " (Depleted)";
            }

            var rect = new Rect(screenPos.x - 90, Screen.height - screenPos.y - 20, 180, 20);
            GUI.Box(rect, text);
        }
    }

    private Transform FindLocalPlayerTransform()
    {
        if (Runner != null && Runner.IsRunning)
        {
            foreach (var p in Runner.ActivePlayers)
            {
                if (Runner.TryGetPlayerObject(p, out var obj) && obj != null && obj.HasInputAuthority)
                    return obj.transform;
            }
            return null;
        }
        else
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            return playerObj != null ? playerObj.transform : null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
