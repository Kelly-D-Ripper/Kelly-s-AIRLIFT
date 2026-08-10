# Contributing

Kelly's AIRLIFT targets Nuclear Option 0.34 and BepInEx 5. Keep changes
server-authoritative and fail closed when an aircraft, cargo, runway or weapon
contract cannot be proven against the live game catalogue.

Before submitting a change:

1. Set `NUCLEAR_OPTION_DIR` to a legally installed Nuclear Option directory.
2. Optionally set `BEPINEX_DIR` to a BepInEx 5 `core` directory; otherwise the
   project uses `$env:NUCLEAR_OPTION_DIR\BepInEx\core`.
3. Run `dotnet build KellysAIRLIFT.csproj -c Release`.
4. Run `dotnet build KellysAIRLIFTPublicUI/KellysAIRLIFTPublicUI.csproj -c Release`.
5. Run `dotnet run --project KellysAIRLIFT.Tests/KellysAIRLIFT.Tests.csproj -c Release`.
6. Do not commit Nuclear Option assemblies, BepInEx binaries, Chimera content,
   server configuration containing a private Steam ID, logs, `bin`, or `obj`.

Runtime behavior changes require a private 0.34 server test covering both BDF
and PALA, a transport casualty, an empty server and a mission transition.
