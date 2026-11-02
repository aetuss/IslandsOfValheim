# Builds IslandsOfValheim.dll with the .NET Framework C# compiler against the game's own assemblies,
# then packs it with package/ into dist/IslandsOfValheim-<version>.zip for Thunderstore.
# The old compiler can't handle long paths, so it compiles from a short temp folder.
param(
    [string]$Managed = "C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed",
    [string]$BepInExCore = "C:\Valheim Mod\valheim\profiles\mod 1\BepInEx\core",
    [string]$Jotunn = "C:\Valheim Mod\valheim\profiles\mod 1\BepInEx\plugins\ValheimModding-Jotunn\Jotunn.dll"
)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$tmp = Join-Path $env:TEMP "vhm\build_iov"
New-Item -ItemType Directory -Force $tmp | Out-Null
Remove-Item "$tmp\*" -Recurse -Force
Copy-Item (Join-Path $root "src\*.cs") $tmp -Force

$refs = @(
    "$Managed\mscorlib.dll", "$Managed\netstandard.dll", "$Managed\System.dll", "$Managed\System.Core.dll",
    "$Managed\UnityEngine.dll", "$Managed\UnityEngine.CoreModule.dll", "$Managed\assembly_valheim.dll", "$Managed\assembly_utils.dll",
    "$BepInExCore\BepInEx.dll", "$BepInExCore\0Harmony.dll", $Jotunn,
    "$Managed\UnityEngine.PhysicsModule.dll", "$Managed\UnityEngine.ParticleSystemModule.dll", "$Managed\SoftReferenceableAssets.dll"
) | ForEach-Object { "/r:$_" }

& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /nostdlib /noconfig /target:library /optimize+ `
    /codepage:65001 $refs /out:"$tmp\IslandsOfValheim.dll" "$tmp\*.cs"
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$out = Join-Path $root "bin"
New-Item -ItemType Directory -Force $out | Out-Null
Copy-Item "$tmp\IslandsOfValheim.dll" $out -Force
Write-Output "Built: $out\IslandsOfValheim.dll"

$version = (Get-Content (Join-Path $root "package\manifest.json") -Raw | ConvertFrom-Json).version_number
$stage = Join-Path $tmp "package"
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item (Join-Path $root "package\*") $stage -Force
Copy-Item "$out\IslandsOfValheim.dll" $stage -Force
$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Force $dist | Out-Null
$zip = Join-Path $dist "IslandsOfValheim-$version.zip"
Compress-Archive -Path "$stage\*" -DestinationPath $zip -Force
Write-Output "Packaged: $zip"
