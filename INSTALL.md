# Install (Windows)

These instructions assume you downloaded the GitHub source zip and extracted it under your Downloads folder.

PowerShell uses different environment variable syntax than Command Prompt:

- PowerShell: `$HOME` or `$env:USERPROFILE`
- Command Prompt: `%USERPROFILE%`

## 1. Build the mod with PowerShell

Close Cities: Skylines II before replacing a local mod build.

Open PowerShell, then run:

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

If you extracted the zip somewhere else, change the first line to that folder.

The Cities: Skylines II mod toolchain copies the built mod into the local game mods folder during the build.

## 2. Launch or reload the game

InfoLoom or InfoLoom Two must also be installed. InfoLoomBridge writes an error snapshot if it cannot find a supported InfoLoom assembly.

## 3. Verify JSON export output

Expected output file:

```text
%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\ModsData\InfoLoomBridge\latest.json
```

Optional quick checks from PowerShell:

```powershell
$latest = Join-Path $env:USERPROFILE 'AppData\LocalLow\Colossal Order\Cities Skylines II\ModsData\InfoLoomBridge\latest.json'
$snapshot = Get-Content -Raw -LiteralPath $latest | ConvertFrom-Json
"export_version=$($snapshot.export_version)"
"status=$($snapshot.status)"
"infoloom_version=$($snapshot.infoloom_version)"
```

For a working bridge, `status` should be `ok`. If `status` is `error`, read the `message` field in `latest.json`.
