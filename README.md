# InfoLoomBridge

InfoLoomBridge is a small Cities: Skylines II mod that makes selected InfoLoom data available outside the game.

When the game is running with InfoLoom installed, the bridge reads supported InfoLoom panels and writes a local JSON snapshot that other tools can consume.

## Output

The latest export is written to:

```text
%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\ModsData\InfoLoomBridge\latest.json
```

The payload includes bridge metadata, detected InfoLoom version/build information, and the currently supported panel data.

## Compatibility

This build targets Cities: Skylines II 1.5.7f1 and supports local or subscribed InfoLoom/InfoLoom Two assemblies with versions from 1.0.0 up to, but not including, 2.0.0.

## Building

Build a release copy with:

```powershell
$env:DOTNET_ROLL_FORWARD = "Major"
dotnet build InfoLoomBridge.csproj -c Release -p:LangVersion=latest
```

The Cities: Skylines II mod toolchain copies the built mod into the local game mods folder during the build.

## Testing

Run the test project with:

```powershell
$env:DOTNET_ROLL_FORWARD = "Major"
dotnet test Tests\InfoLoomBridge.Tests.csproj -c Release -p:LangVersion=latest
```
