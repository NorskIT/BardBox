param([Parameter(Mandatory=$true)][string]$Archive)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Drawing
$zip=[IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Archive))
try {
    $entries=@{}
    foreach($entry in $zip.Entries){$entries[$entry.FullName.Replace('\','/')]=$entry}
    foreach($name in @('ServerSync-LICENSE.txt','manifest.json','icon.png','README.md','SONGS.md','CREDITS.md','CHANGELOG.md','SHA256SUMS.txt','BepInEx/plugins/BardBox/BardBox.dll','BepInEx/plugins/BardBox/bardbox.assets')){
        if(-not $entries.ContainsKey($name)){throw "Missing $name"}
    }
    $reader=New-Object IO.StreamReader($entries['manifest.json'].Open())
    try {$manifest=$reader.ReadToEnd() | ConvertFrom-Json} finally {$reader.Dispose()}
    $projectVersion=([xml](Get-Content (Join-Path $PSScriptRoot '../src/BardBox/BardBox.csproj') -Raw)).Project.PropertyGroup.Version
    if($manifest.version_number -ne $projectVersion){throw 'Manifest version mismatch'}
    $stream=$entries['icon.png'].Open()
    try {
        $icon=[Drawing.Image]::FromStream($stream)
        try {if($icon.Width -ne 256 -or $icon.Height -ne 256){throw 'Icon must be 256 x 256'}} finally {$icon.Dispose()}
    } finally {$stream.Dispose()}
    if(@($entries.Keys | Where-Object {$_ -match '\.dll$'}).Count -ne 1){throw 'Unexpected DLLs'}
    if(@($entries.Keys | Where-Object {$_ -like 'BepInEx/plugins/BardBox/songs/*.wav'}).Count -ne 11){throw 'Expected 11 songs'}
    if(@($entries.Keys | Where-Object {$_ -like 'BepInEx/plugins/BardBox/icons/*.png'}).Count -ne 13){throw 'Expected 13 item icons'}
    $reader=New-Object IO.StreamReader($entries['SHA256SUMS.txt'].Open())
    try {$lines=$reader.ReadToEnd() -split '\r?\n'} finally {$reader.Dispose()}
    $count=0
    foreach($line in $lines){
        if(-not $line.Trim()){continue}
        $parts=$line -split '  ',2
        $entry=$entries[$parts[1].Replace('\','/')]
        if(-not $entry){throw "Missing hashed file: $($parts[1])"}
        $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
        try {$actual=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')} finally {$stream.Dispose();$sha.Dispose()}
        if($actual -ne $parts[0]){throw "Hash mismatch: $($parts[1])"}
        $count++
    }
    if($count -ne @($zip.Entries | Where-Object {$_.Name -and $_.Name -ne 'SHA256SUMS.txt'}).Count){throw 'Incomplete hash inventory'}
    Write-Output "Package verified: version $projectVersion, 256px icon, 11 songs, 13 item icons, $count file hashes."
} finally {$zip.Dispose()}
