# Building Kelly's AIRLIFT Public UI

The project targets .NET Framework 4.7.2 and requires a legal local installation
of Nuclear Option 0.34 plus BepInEx 5. Game and BepInEx assemblies are referenced
for compilation only and must not be committed or redistributed.

Set `NUCLEAR_OPTION_DIR` to the game installation and build:

```powershell
$env:NUCLEAR_OPTION_DIR = 'C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option'
dotnet build .\KellysAIRLIFTPublicUI.csproj -c Release
```

The output is `bin/Release/net472/KellysAIRLIFTPublicUI.dll`. Install it under:

```text
BepInEx/plugins/KellysAIRLIFTPublicUI/KellysAIRLIFTPublicUI.dll
```

The UI is not server-authoritative. It sends an authenticated purchase request;
the AIRLIFT server plugin independently validates the player, funds, price,
target, terrain/runway, route and manifest before charging or spawning anything.

