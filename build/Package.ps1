$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$bundle=Join-Path $project 'assets/bundle/bardbox.assets'
if(-not(Test-Path -LiteralPath $bundle)){throw 'The approved boombox model bundle is missing. Finish model import and Build-Assets.ps1 before packaging.'}
& dotnet build (Join-Path $project 'src/BardBox/BardBox.csproj') -c Release --nologo
if($LASTEXITCODE -ne 0){throw 'Build failed'}
& dotnet run --project (Join-Path $project 'tests/BardBox.Tests.csproj') -c Release --nologo
if($LASTEXITCODE -ne 0){throw 'Tests failed'}
$stamp=Get-Date -Format 'yyyyMMdd-HHmmss'
$version=([xml](Get-Content (Join-Path $project 'src/BardBox/BardBox.csproj') -Raw)).Project.PropertyGroup.Version
$stage=Join-Path $project ('artifacts/BardBox-'+$version+'-test-'+$stamp)
$plugin=Join-Path $stage 'BepInEx/plugins/BardBox'
New-Item -ItemType Directory -Force $plugin,(Join-Path $plugin 'songs'),(Join-Path $plugin 'icons') | Out-Null
Copy-Item -LiteralPath (Join-Path $project 'src/BardBox/bin/Release/netstandard2.1/BardBox.dll') -Destination $plugin
Copy-Item -LiteralPath $bundle -Destination $plugin
Copy-Item -Path (Join-Path $project 'assets/audio/*.wav') -Destination (Join-Path $plugin 'songs')
Copy-Item -Path (Join-Path $project 'assets/icons/*.png') -Destination (Join-Path $plugin 'icons')
foreach($file in @('README.md','DEVELOPMENT.md','SONGS.md','TESTING.md','CREDITS.md')){Copy-Item -LiteralPath (Join-Path $project $file) -Destination $stage}
$hashes=Get-ChildItem -LiteralPath $stage -Recurse -File | Get-FileHash -Algorithm SHA256
$hashes | ForEach-Object { $_.Hash+'  '+$_.Path.Substring($stage.Length+1) } | Set-Content (Join-Path $stage 'SHA256SUMS.txt')
Compress-Archive -LiteralPath (Join-Path $stage 'BepInEx'),(Join-Path $stage 'README.md'),(Join-Path $stage 'DEVELOPMENT.md'),(Join-Path $stage 'SONGS.md'),(Join-Path $stage 'TESTING.md'),(Join-Path $stage 'CREDITS.md'),(Join-Path $stage 'SHA256SUMS.txt') -DestinationPath ($stage+'.zip')
Write-Output ($stage+'.zip')
