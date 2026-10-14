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
$stage=Join-Path $project ('artifacts/BardBox-'+$version+'-upload-'+$stamp)
$plugin=Join-Path $stage 'BepInEx/plugins/BardBox'
New-Item -ItemType Directory -Force $plugin,(Join-Path $plugin 'songs'),(Join-Path $plugin 'icons') | Out-Null
Copy-Item -LiteralPath (Join-Path $project 'src/BardBox/bin/Release/netstandard2.1/BardBox.dll') -Destination $plugin
Copy-Item -LiteralPath $bundle -Destination $plugin
Copy-Item -Path (Join-Path $project 'assets/audio/*.wav') -Destination (Join-Path $plugin 'songs')
Copy-Item -Path (Join-Path $project 'assets/icons/*.png') -Destination (Join-Path $plugin 'icons')
foreach($file in @('README.md','DEVELOPMENT.md','SONGS.md','TESTING.md','CREDITS.md')){Copy-Item -LiteralPath (Join-Path $project $file) -Destination $stage}
# Listing metadata uses the same layout as the other mod packages in this workspace.
$manifest=[ordered]@{
    name='BardBox'
    version_number=[string]$version
    website_url='https://github.com/NorskIT/BardBox'
    description='Craft a boombox and reusable tapes to share music and buffs with nearby Vikings.'
    dependencies=@('denikson-BepInExPack_Valheim-5.4.2333','ValheimModding-Jotunn-2.30.2')
}
$manifest | ConvertTo-Json | Set-Content (Join-Path $stage 'manifest.json') -Encoding UTF8
Add-Type -AssemblyName System.Drawing
$source=[Drawing.Image]::FromFile((Join-Path $project 'docs/images/bardbox.png'))
$icon=New-Object Drawing.Bitmap 256,256
$graphics=[Drawing.Graphics]::FromImage($icon)
try {
    $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawImage($source,0,0,256,256)
    $icon.Save((Join-Path $stage 'icon.png'),[Drawing.Imaging.ImageFormat]::Png)
} finally { $graphics.Dispose();$icon.Dispose();$source.Dispose() }
Copy-Item -LiteralPath (Join-Path $project 'CHANGELOG.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $project 'src/BardBox/ThirdParty/ServerSync/LICENSE.txt') -Destination (Join-Path $stage 'ServerSync-LICENSE.txt')
$hashes=Get-ChildItem -LiteralPath $stage -Recurse -File | Get-FileHash -Algorithm SHA256
$hashes | ForEach-Object { $_.Hash+'  '+$_.Path.Substring($stage.Length+1) } | Set-Content (Join-Path $stage 'SHA256SUMS.txt')
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath ($stage+'.zip')
& (Join-Path $PSScriptRoot 'Validate-Package.ps1') -Archive ($stage+'.zip')
Write-Output ($stage+'.zip')
