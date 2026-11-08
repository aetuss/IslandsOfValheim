# Builds IslandsOfValheim.dll with the .NET Framework C# compiler against the game's own assemblies,
# checks the translation files, then packs it with package/ into dist/IslandsOfValheim-<version>.zip for Thunderstore.
# The old compiler can't handle long paths, so it compiles from a short temp folder.
param(
    [string]$Managed = "C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed",
    [string]$BepInExCore = "C:\Valheim Mod\valheim\profiles\mod 1\BepInEx\core",
    [string]$Jotunn = "C:\Valheim Mod\valheim\profiles\mod 1\BepInEx\plugins\ValheimModding-Jotunn\Jotunn.dll",
    # A development build: with the design work's test locations and their iov_goto commands (IOV_DEV).
    # Never upload one; its zip is named -dev.
    [switch]$Dev
)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# ---- translations: every language has the same keys as English, nothing empty, the same placeholders,
# tags and line breaks, UTF-8 without BOM; every key the code uses is in the English file.
$langRoot = Join-Path $root "package\Translations"
function Read-Translation([string]$file) {
    $bytes = [IO.File]::ReadAllBytes($file)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { throw "$file has a BOM" }
    $json = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json
    $map = @{}
    foreach ($p in $json.PSObject.Properties) { $map[$p.Name] = [string]$p.Value }
    return $map
}
function Marks([string]$text) {
    $found = [regex]::Matches($text, '\{\d+\}|<[^>]+>|\$[A-Za-z_][A-Za-z0-9_]*|\n') | ForEach-Object { $_.Value }
    return (($found | Sort-Object) -join ' ')
}
$english = Read-Translation (Join-Path $langRoot "English\IslandsOfValheim.json")
$problems = @()
foreach ($dir in Get-ChildItem $langRoot -Directory) {
    if ($dir.Name -eq "English") { continue }
    $other = Read-Translation (Join-Path $dir.FullName "IslandsOfValheim.json")
    foreach ($key in $english.Keys) {
        if (-not $other.ContainsKey($key)) { $problems += "$($dir.Name): missing $key"; continue }
        if ([string]::IsNullOrWhiteSpace($other[$key])) { $problems += "$($dir.Name): empty $key" }
        if ((Marks $english[$key]) -ne (Marks $other[$key])) { $problems += "$($dir.Name): placeholders differ in $key" }
    }
    foreach ($key in $other.Keys) { if (-not $english.ContainsKey($key)) { $problems += "$($dir.Name): $key is not in English" } }
}
foreach ($key in $english.Keys) { if ([string]::IsNullOrWhiteSpace($english[$key])) { $problems += "English: empty $key" } }
$used = @()
foreach ($file in Get-ChildItem (Join-Path $root "src") -Filter *.cs) {
    $code = [IO.File]::ReadAllText($file.FullName)
    $used += [regex]::Matches($code, '"\$?(iov_[a-z0-9_]+)"') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -notin @("iov_info", "iov_goto", "iov_light", "iov_placeislets") -and -not $_.EndsWith("_") }
    # Items and effects add _desc / _tooltip to their token; the trader's lines are numbered.
    $used += [regex]::Matches($code, '\(\w+, new\[\] \{[^}]*\}, "(iov_[a-z0-9_]+)"') | ForEach-Object { $_.Groups[1].Value + "_desc" }
    $used += [regex]::Matches($code, 'Effect\("[^"]+", "(iov_[a-z0-9_]+)"') | ForEach-Object { $_.Groups[1].Value + "_tooltip" }
    foreach ($m in [regex]::Matches($code, 'Lines\("([a-z]+)", (\d+)\)')) {
        for ($i = 1; $i -le [int]$m.Groups[2].Value; $i++) { $used += "iov_trader_" + $m.Groups[1].Value + $i }
    }
}
foreach ($key in ($used | Sort-Object -Unique)) { if (-not $english.ContainsKey($key)) { $problems += "English: the code uses $key, which isn't translated" } }
if ($problems.Count -gt 0) { $problems | ForEach-Object { Write-Output $_ }; throw "Translation check failed" }
Write-Output "Translations OK: $($english.Count) keys"

# ---- build ----
$tmp = Join-Path $env:TEMP "vhm\build_iov"
New-Item -ItemType Directory -Force $tmp | Out-Null
Remove-Item "$tmp\*" -Recurse -Force
Copy-Item (Join-Path $root "src\*.cs") $tmp -Force

$refs = @(
    "$Managed\mscorlib.dll", "$Managed\netstandard.dll", "$Managed\System.dll", "$Managed\System.Core.dll",
    "$Managed\UnityEngine.dll", "$Managed\UnityEngine.CoreModule.dll", "$Managed\assembly_valheim.dll", "$Managed\assembly_utils.dll",
    "$BepInExCore\BepInEx.dll", "$BepInExCore\0Harmony.dll", $Jotunn,
    "$Managed\UnityEngine.PhysicsModule.dll", "$Managed\UnityEngine.ParticleSystemModule.dll", "$Managed\SoftReferenceableAssets.dll",
    "$Managed\assembly_guiutils.dll", "$Managed\UnityEngine.UI.dll", "$Managed\UnityEngine.AnimationModule.dll", "$Managed\UnityEngine.AudioModule.dll"
) | ForEach-Object { "/r:$_" }

# The translations go into the dll too (Texts.LoadOwn), so the texts are there however the mod is installed.
$resources = Get-ChildItem $langRoot -Directory | ForEach-Object { "/resource:" + (Join-Path $_.FullName "IslandsOfValheim.json") + ",IoV.Translations." + $_.Name + ".json" }
# The large islets' drafts (what stands on each design, laid out in the game with the dev tool).
$resources += Get-ChildItem (Join-Path $root "drafts") -Filter *.json -ErrorAction SilentlyContinue | ForEach-Object { "/resource:" + $_.FullName + ",IoV.Drafts." + $_.Name }

$defines = @()
if ($Dev) { $defines += "/define:IOV_DEV" }
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /nostdlib /noconfig /target:library /optimize+ `
    /codepage:65001 $refs $resources $defines /out:"$tmp\IslandsOfValheim.dll" "$tmp\*.cs"
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$out = Join-Path $root "bin"
New-Item -ItemType Directory -Force $out | Out-Null
Copy-Item "$tmp\IslandsOfValheim.dll" $out -Force
Write-Output "Built: $out\IslandsOfValheim.dll"

# ---- package ----
# Zipped by hand: Compress-Archive in Windows PowerShell writes backslashes into the paths of files in
# folders (Translations\English\...), which other tools don't read as folders.
$version = (Get-Content (Join-Path $root "package\manifest.json") -Raw | ConvertFrom-Json).version_number
$stage = Join-Path $tmp "package"
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item (Join-Path $root "package\*") $stage -Recurse -Force
Copy-Item "$out\IslandsOfValheim.dll" $stage -Force
$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Force $dist | Out-Null
$zip = Join-Path $dist ("IslandsOfValheim-$version" + $(if ($Dev) { "-dev" } else { "" }) + ".zip")
if (Test-Path $zip) { Remove-Item $zip -Force }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem $stage -Recurse -File) {
        $entry = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry) | Out-Null
    }
} finally { $archive.Dispose() }
Write-Output "Packaged: $zip"
