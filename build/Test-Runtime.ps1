param([switch]$PoseBaseline,[switch]$World,[ValidateSet('','host','client')][string]$NetworkRole='', [string]$Coordination='', [string]$NoRangedDll='', [string]$ForestMusicDll='')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$game='C:\Program Files (x86)\Steam\steamapps\common\Valheim'
$bep=Join-Path $env:APPDATA 'com.kesomannen.gale/valheim/profiles/Prod-v1/BepInEx'
& dotnet build (Join-Path $root 'tests/RuntimeSmoke/RuntimeSmoke.csproj') -c Release --nologo -v:q *> (Join-Path $PSScriptRoot 'smoke-build.log')
if($LASTEXITCODE -ne 0){throw 'Smoke build failed; see build/smoke-build.log'}
$stage=Join-Path $root ('artifacts/smoke-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$plugin=Join-Path $stage 'BepInEx/plugins/BardBox'
New-Item -ItemType Directory -Force "$stage/BepInEx/core",$plugin,"$plugin/icons","$plugin/songs","$stage/saves" | Out-Null
Copy-Item -Path "$bep/core/*" -Destination "$stage/BepInEx/core"
Copy-Item -LiteralPath "$bep/plugins/ValheimModding-Jotunn" -Destination "$stage/BepInEx/plugins/Jotunn" -Recurse
if(Test-Path "$bep/plugins/ValheimModding-JsonDotNET"){Copy-Item -LiteralPath "$bep/plugins/ValheimModding-JsonDotNET" -Destination "$stage/BepInEx/plugins/JsonDotNET" -Recurse}
Copy-Item -LiteralPath "$root/src/BardBox/bin/Release/netstandard2.1/BardBox.dll","$root/tests/RuntimeSmoke/bin/Release/netstandard2.1/BardBox.RuntimeSmoke.dll","$root/assets/bundle/bardbox.assets" -Destination $plugin
Copy-Item -Path "$root/assets/icons/*.png" -Destination "$plugin/icons"
Copy-Item -Path "$root/assets/audio/*.wav" -Destination "$plugin/songs"
if($NoRangedDll){
    $noRanged=Join-Path $stage 'BepInEx/plugins/NoRanged'
    New-Item -ItemType Directory -Force $noRanged | Out-Null
    Copy-Item -LiteralPath $NoRangedDll -Destination (Join-Path $noRanged 'NoRanged.dll')
}
$priorForest=$env:BARD_FOREST_MUSIC_DLL
$priorTarget=$env:DOORSTOP_TARGET_ASSEMBLY
$priorOutput=$env:BARD_SMOKE_OUTPUT
$priorBaseline=$env:BARD_POSE_BASELINE
$priorWorld=$env:BARD_WORLD_TEST
$priorNetworkRole=$env:BARD_NETWORK_ROLE
$priorNetworkCoord=$env:BARD_NETWORK_COORD
try {
    $env:BARD_FOREST_MUSIC_DLL=$ForestMusicDll
    $env:BARD_NETWORK_ROLE=$NetworkRole
    $env:BARD_NETWORK_COORD=$Coordination
    $env:BARD_WORLD_TEST=if($World){'1'}else{'0'}
    $env:BARD_POSE_BASELINE=if($PoseBaseline){'1'}else{'0'}
    $env:DOORSTOP_TARGET_ASSEMBLY="$stage/BepInEx/core/BepInEx.Preloader.dll"
    $env:BARD_SMOKE_OUTPUT=$stage
    $process=Start-Process -FilePath "$game/valheim.exe" -WorkingDirectory $stage -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-screen-width','640','-screen-height','480','-savedir',('"'+"$stage/saves"+'"'),'-logFile',('"'+"$stage/unity.log"+'"'),'--doorstop-enabled','true','--doorstop-target-assembly',('"'+$env:DOORSTOP_TARGET_ASSEMBLY+'"'))
    $process.Id | Set-Content "$stage/process-id.txt"
    Write-Output "Isolated smoke process $($process.Id): $stage"
} finally {
    $env:BARD_FOREST_MUSIC_DLL=$priorForest
    $env:DOORSTOP_TARGET_ASSEMBLY=$priorTarget
    $env:BARD_SMOKE_OUTPUT=$priorOutput
    $env:BARD_POSE_BASELINE=$priorBaseline
    $env:BARD_WORLD_TEST=$priorWorld
    $env:BARD_NETWORK_ROLE=$priorNetworkRole
    $env:BARD_NETWORK_COORD=$priorNetworkCoord
}
