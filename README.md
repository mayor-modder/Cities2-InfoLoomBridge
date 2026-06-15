# InfoLoomBridge

InfoLoomBridge is a small Cities: Skylines II mod that makes selected InfoLoom data available outside the game.

When the game is running with InfoLoom installed, the bridge reads supported InfoLoom panels and writes a local JSON snapshot that other tools can consume.

## Output

The latest export is written to:

```text
%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\ModsData\InfoLoomBridge\latest.json
```

The payload includes bridge metadata, detected InfoLoom version/build information, and the currently supported panel data.

Top-level payload groups include:

- bridge/export metadata
- detected InfoLoom version and build fingerprint
- compatibility `status` and `message`
- supported InfoLoom panel slices for demographics, workforce, and workplaces
- bridge extension data such as commute destinations

## Compatibility

This build targets Cities: Skylines II 1.5.7f1 and supports local or subscribed InfoLoom/InfoLoom Two assemblies with versions from 1.0.0 up to, but not including, 2.0.0.

## Installation

If you are installing a prebuilt copy, place the built mod files in:

```text
%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\InfoLoomBridge
```

Then launch or reload the game. InfoLoom or InfoLoom Two must also be installed.

After launching or loading a city, check for:

```text
%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\ModsData\InfoLoomBridge\latest.json
```

If that file exists and has a recent timestamp, the bridge is exporting.

## Building From Source

These commands are for PowerShell. If you downloaded the GitHub source zip, Windows may extract it as `Downloads\Cities2-InfoLoomBridge-main\Cities2-InfoLoomBridge-main`; the commands below find the project file automatically.

Close Cities: Skylines II before replacing a local mod build.

```powershell
$sourceRoot = Join-Path $HOME 'Downloads\Cities2-InfoLoomBridge-main'

if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot 'InfoLoomBridge.csproj'))) {
    $projectFile = Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter InfoLoomBridge.csproj |
        Select-Object -First 1

    if ($null -eq $projectFile) {
        throw "Could not find InfoLoomBridge.csproj under $sourceRoot. Check where the zip was extracted."
    }

    $sourceRoot = $projectFile.DirectoryName
}

Set-Location -LiteralPath $sourceRoot

$env:DOTNET_ROLL_FORWARD = 'Major'
Remove-Item -LiteralPath .\obj -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath .\bin -Recurse -Force -ErrorAction SilentlyContinue
dotnet build .\InfoLoomBridge.csproj -c Release -p:LangVersion=latest
```

The Cities: Skylines II mod toolchain copies the built mod into the local game mods folder during the build.

More detailed Windows install notes are in [INSTALL.md](INSTALL.md).

## Testing

Run the test project with:

```powershell
$env:DOTNET_ROLL_FORWARD = 'Major'
dotnet test .\Tests\InfoLoomBridge.Tests.csproj -c Release -p:LangVersion=latest
```
