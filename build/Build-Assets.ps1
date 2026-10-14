param([string]$Unity=(Join-Path $PSScriptRoot '../artifacts/tools/Unity-6000.0.75f1/Editor/Unity.exe'))
$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../unity'))
$models=Get-ChildItem -LiteralPath (Join-Path $project 'Assets/Model') -Recurse -File -ErrorAction SilentlyContinue | Where-Object Extension -In '.fbx','.obj'
if(@($models).Count -ne 1){throw 'Model input missing: import the approved Sketchfab model as FBX or OBJ, including textures, into unity/Assets/Model. Do not build an install package without it.'}
$log=Join-Path $PSScriptRoot 'unity-build.log'
$process=Start-Process -FilePath $Unity -WindowStyle Hidden -PassThru -Wait -ArgumentList @('-batchmode','-nographics','-quit','-projectPath',('"'+$project+'"'),'-executeMethod','BuildBardBox.Build','-logFile',('"'+$log+'"'))
if($process.ExitCode -ne 0){throw "Unity build failed; see $log"}
if(-not(Test-Path (Join-Path $PSScriptRoot '../assets/bundle/bardbox.assets'))){throw "Unity did not produce the bundle; see $log"}
