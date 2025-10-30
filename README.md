# Gettin' Skunked

A Unity multiplayer game project using Photon Fusion for networking.

## Prerequisites

### Required Software
- **Unity Editor**: Version `6000.2.7f2` (Unity 6)
  - Download from [Unity Hub](https://unity.com/download)
  - Make sure to install the exact version for compatibility

### Required Assets/Packages

#### Photon Fusion
This project requires Photon Fusion for multiplayer networking functionality.

1. **Get Photon Fusion**:
   - Visit [Photon Engine](https://www.photonengine.com/)
   - Create a free account if you don't have one
   - Download Photon Fusion from the Asset Store or Photon Dashboard
   - Import the Photon Fusion package into your project

2. **Setup Photon Account**:
   - Create a new Fusion app in your Photon Dashboard
   - Copy your App ID from the dashboard
   - In Unity, go to `Photon → Fusion → Configure` and enter your App ID

#### TextMesh Pro
- TextMesh Pro should be automatically imported when opening the project
- If prompted, click "Import TMP Essentials"

## Project Setup

1. **Clone the Repository**:
   ```bash
   git clone [your-repository-url]
   cd "Gettin' Skunked"
   ```

2. **Open in Unity**:
   - Open Unity Hub
   - Click "Open" and select the project folder
   - Unity will automatically import and set up the project

3. **Import Photon Fusion**:
   - Follow the Photon Fusion setup steps above
   - The project expects Photon assets to be in `Assets/Photon/`

4. **Configure Photon Settings**:
   - Open `Window → Photon Fusion → Fusion Hub`
   - Follow the setup wizard to configure your connection settings

## Project Structure

```
Assets/
├── _Project/              # Custom project files (tracked in git)
│   ├── Scripts/          # Custom C# scripts
│   └── Scenes/           # Game scenes
├── Photon/               # Photon Fusion assets (not tracked)
├── TextMesh Pro/         # TextMesh Pro assets (not tracked)
└── Resources/            # Unity resources
```

## Development Notes

- Only files in `Assets/_Project/` are tracked in version control
- All Unity-generated files, Photon assets, and third-party packages are excluded from git
- Make sure to keep your custom scripts and scenes in the `_Project` folder

## Gameplay Systems

### Honey Collection (Networked)
- Script: `Assets/_Project/Scripts/Resource/CollectHoney.cs`
- Server-authoritative beehive that regenerates honey over time. Players within range press E to harvest.
- On successful harvest, the server spawns a `ResourcePickup` for Honey which flows into the inventory when picked up.
- Optional: CollectHoney can notify nearby `BeeAggression` controllers to react to theft.

Setup steps:
- Place a `CollectHoney` on a beehive GameObject with a `NetworkObject`.
- Assign the `resourcePickupPrefab` to a networked `ResourcePickup` prefab in the Fusion Prefab Table.
- Adjust `maxHoney`, `collectPerUse`, `collectCooldownSeconds`, and `regenPerSecond` as desired.

### Server-Authoritative Inventory
- Script: `Assets/_Project/Scripts/Resource/NetworkInventory.cs`
- Each player owns a networked inventory replicated as slots (server is the source of truth).
- Clients request actions via RPC; server validates, mutates, and replicates the updated slots.

Key RPCs (client → server):
- `RPC_RequestPickup(NetworkId)`
- `RPC_RequestAddResource(name, type, quantity)` / `RPC_RequestAddWeapon(...)`
- `RPC_RequestDrop(name, quantity)`
- `RPC_RequestMoveStack(from, to, amount, seq)` to move/merge/swap stacks
- `RPC_RequestUseSlot(index, amount, seq)` to consume resources

Notes:
- Lightweight per-inventory spam guard protects against key-repeat flooding.
- Optional `seq` parameter provides idempotency for client retries (values <= last processed are ignored).

### Inventory UI (drag, drop, consume)
- Scripts:
   - `Assets/_Project/Scripts/UI/NetworkInventoryUI.cs`
   - `Assets/_Project/Scripts/UI/InventorySlotUI.cs`
- Features:
   - Auto-finds the local player’s `NetworkInventory` (input authority) and displays a grid of slots
   - Drag and drop between slots calls `RPC_RequestMoveStack`
   - Right-click a slot to consume 1 via `RPC_RequestUseSlot`

Setup:
1. Create a Canvas and a Panel (empty RectTransform) for slots
2. Add `NetworkInventoryUI` to a GameObject and assign the Panel to `slotsParent`
3. Create a simple UI prefab for a slot (e.g., an empty GameObject with an Image + Text) and assign it to `slotPrefab`
4. Make sure there’s an `EventSystem` in the scene (the script will auto-create one if missing)
5. Optionally hold Shift while dropping to move a single item instead of the whole stack

## Build Instructions

1. Ensure Photon Fusion is properly configured
2. Open the main scene from `Assets/_Project/Scenes/`
3. Go to `File → Build Settings`
4. Add your scenes and configure platform settings
5. Build the project

## Multiplayer Testing

To test multiplayer functionality:
1. Build the project for your target platform
2. Run one instance from the built executable
3. Run another instance from the Unity Editor (Play mode)
4. Both instances should be able to connect through Photon Fusion

## Troubleshooting

### Common Issues
- **"Photon not found"**: Make sure Photon Fusion is imported and properly configured
- **Connection issues**: Verify your Photon App ID is correctly set
- **Build errors**: Ensure all required packages are imported and up to date

### Getting Help
- Check the [Photon Fusion Documentation](https://doc.photonengine.com/fusion)
- Visit the [Unity Documentation](https://docs.unity3d.com/)

## License

[Add your license information here]