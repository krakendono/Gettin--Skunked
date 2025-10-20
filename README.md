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